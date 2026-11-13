using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FmAtlas.Services;

public sealed record LiveClubRecord(uint Uid, string Name, int ArrayIndex, ulong Address, bool WitnessVerified);
public sealed record LivePlayerRecord(
    uint Uid, string Name, string? TeamObjectAddress, uint? ClubUid, int ArrayIndex, ulong Address,
    bool NameVerified, bool RelationshipVerified, DateTime? DateOfBirth, string RegenStatus, string RegenEvidence,
    int? CurrentAbility = null, int? PotentialAbility = null,
    IReadOnlyDictionary<string, double>? Attributes = null,
    IReadOnlyDictionary<string, double>? Personality = null,
    IReadOnlyDictionary<string, double>? PositionRatings = null);
// ModuleSha256 is retained as the database/API field name for schema compatibility. The profile-matched
// importer stores the PE build fingerprint (SizeOfImage + TimeDateStamp) here, not a file SHA-256.
public sealed record LiveImportPayload(string GameVersion, string ModuleSha256, List<LiveClubRecord> Clubs, List<LivePlayerRecord> Players, int PlayerSlots, int UnnamedPlayers, int UnlinkedPlayers, DateTime? GameDate = null);
public sealed record LiveImportResult(int RunId, int PlayersSeen, int PlayersImported, int ClubsSeen, int ClubsImported, int UnnamedPlayers, int UnlinkedPlayers, int AttributeRowsImported = 0);

/// <summary>Read-only FM27 extractor using exact per-storefront module fingerprints. Field-level evidence is retained as provisional until UI-witnessed.</summary>
public static class LiveDataImporter
{
    private const uint QueryInformation = 0x0400, VmRead = 0x0010;
    private static readonly string[] PositionKeys = { "GK", "SW", "DL", "DC", "DR", "DM", "ML", "MC", "MR", "AML", "AMC", "AMR", "ST", "WBL", "WBR" };
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ReadProcessMemory(nint process, nint address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);

    public static LiveImportPayload Extract()
    {
        // Reuse the exact profile detector used by the live editor. Never take the first
        // process named "fm": a second launch-preview instance or unrelated process can
        // otherwise make Atlas import the wrong address space.
        using var process = Process.GetProcessById(LiveMemoryEditService.FindSupportedProcessId());
            ProcessModule module;
            try { module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => string.Equals(m.ModuleName, "game_plugin.dll", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("game_plugin.dll was not found in the FM process."); }
            catch (Exception ex) when (ex is not InvalidOperationException) { throw new InvalidOperationException("Could not enumerate FM modules. Run FM Atlas with the same Windows permission level as Football Manager.", ex); }
            var moduleBase = (ulong)module.BaseAddress.ToInt64();
            var handle = OpenProcess(QueryInformation | VmRead, false, process.Id);
            if (handle == 0) throw new InvalidOperationException($"Read-only access to FM was denied (Win32 {Marshal.GetLastWin32Error()}). Try running FM Atlas at the same privilege level as Football Manager.");
            try
            {
                var pe = Read(handle, (nint)moduleBase, 0x40);
                if (pe[0] != 'M' || pe[1] != 'Z') throw new InvalidOperationException("The in-memory module did not have a valid PE header.");
                var peOffset = BitConverter.ToInt32(pe, 0x3C);
                if (peOffset < 0x40 || peOffset > 0x1000) throw new InvalidOperationException("The in-memory PE header offset was invalid.");
                var nt = Read(handle, (nint)(moduleBase + (ulong)peOffset), 24);
                if (nt[0] != 'P' || nt[1] != 'E' || BitConverter.ToUInt16(nt, 4) != 0x8664) throw new InvalidOperationException("The live module is not the expected x64 PE image.");
                var timestamp = BitConverter.ToUInt32(nt, 8);
                var optSize = BitConverter.ToUInt16(nt, 20);
                var optional = Read(handle, (nint)(moduleBase + (ulong)peOffset + 24), optSize);
                var imageSize = BitConverter.ToUInt32(optional, 56);
                var profile = Fm27BuildProfiles.Match(imageSize, timestamp);
                if (profile is null)
                {
                    var known = string.Join("; ", Fm27BuildProfiles.All.Select(x => x.Id));
                    throw new InvalidOperationException($"This Football Manager 27 build is not in the independently fingerprinted profile registry. SizeOfImage=0x{imageSize:X8}; TimeDateStamp=0x{timestamp:X8}. No memory layout was used and no import was performed. Known profiles: {known}. Update Atlas after a game patch or export diagnostics for profile research.");
                }
                var gameDate = profile.CurrentDateRva == 0 ? null : TryReadGameDate(handle, moduleBase, imageSize, profile.CurrentDateRva);

                var playerBounds = ReadBounds(handle, moduleBase, imageSize, profile.PlayersArrayRva, profile.PlayersArrayRva + 8, 20_000, 200_000, "player");
                var clubBounds = ReadBounds(handle, moduleBase, imageSize, profile.ClubsArrayRva, profile.ClubsArrayRva + 8, 1_000, 100_000, "club");
                var clubs = new List<LiveClubRecord>();
                var clubByAddress = new Dictionary<ulong, LiveClubRecord>();
                var seenClubUids = new HashSet<uint>();
                for (var i = 0; i < clubBounds.Count; i++)
                {
                    try
                    {
                        var address = U64(handle, (nint)(clubBounds.Data + (ulong)i * 8));
                        if (!Pointer(address)) continue;
                        var uid = BitConverter.ToUInt32(Read(handle, (nint)(address + 0x0C), 4), 0);
                        var namePtr = U64(handle, (nint)(address + 0xC0));
                        var name = ReadLengthString(handle, namePtr, 96);
                        if (uid == 0 || string.IsNullOrWhiteSpace(name)) continue;
                        var record = new LiveClubRecord(uid, name, i, address, false);
                        clubByAddress[address] = record;
                        if (seenClubUids.Add(uid)) clubs.Add(record);
                    }
                    catch { /* Keep scanning; one invalid entity must not abort a full import. */ }
                }
                if (clubs.Count < 500) throw new InvalidOperationException($"Safety check failed: only {clubs.Count:N0} club names were decoded from {clubBounds.Count:N0} slots. No import was committed.");

                var players = new List<LivePlayerRecord>(playerBounds.Count);
                var seenPlayerUids = new HashSet<uint>();
                var unnamed = 0; var unlinked = 0;
                for (var i = 0; i < playerBounds.Count; i++)
                {
                    try
                    {
                        var address = U64(handle, (nint)(playerBounds.Data + (ulong)i * 8));
                        if (!Pointer(address)) { unnamed++; continue; }
                        var common = ReadNameSub(handle, address + 0x2E8);
                        var first = ReadNameSub(handle, address + 0x2D8);
                        var last = ReadNameSub(handle, address + 0x2E0);
                        var name = !string.IsNullOrWhiteSpace(common) ? common : Join(first, last);
                        if (string.IsNullOrWhiteSpace(name)) { unnamed++; continue; }
                        var uid = BitConverter.ToUInt32(Read(handle, (nint)(address + 0x294), 4), 0);
                        if (uid == 0 || !seenPlayerUids.Add(uid)) { unnamed++; continue; }
                        var dob = TryReadPlayerDob(handle, moduleBase, address);
                        string? teamAddress = null; uint? clubUid = null; var relationshipVerified = false;
                        try
                        {
                            var team = U64(handle, (nint)(address + 0x130));
                            if (Pointer(team))
                            {
                                teamAddress = $"0x{team:X}";
                                var clubAddress = U64(handle, (nint)(team + 0x30));
                                if (clubByAddress.TryGetValue(clubAddress, out var club)) { clubUid = club.Uid; relationshipVerified = true; }
                            }
                        }
                        catch { /* Preserve a valid player identity even if its relationship is unreadable. */ }
                        if (!relationshipVerified) unlinked++;

                        // The 10-November Steam and Game Pass profiles share the documented object layout.
                        // Keep only range-valid decoded values and label the layout evidence in diagnostics.
                        var inline = Read(handle, (nint)address, 0x410);
                        var caRaw = BitConverter.ToUInt16(inline, 0x264);
                        var paRaw = BitConverter.ToInt16(inline, 0x266);
                        int? ca = caRaw is >= 1 and <= 200 ? caRaw : null;
                        int? pa = paRaw is >= 1 and <= 200 ? paRaw : null;
                        var attributes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        foreach (var field in PlayerAttributeSchema.Fields)
                        {
                            if (field.Offset < 0 || field.Offset >= inline.Length) continue;
                            var raw = inline[field.Offset];
                            var shown = (raw + 2) / 5;
                            if (raw <= 98 && shown is >= 1 and <= 20) attributes[field.Key] = shown;
                        }
                        // Personality fields sit in the person block, which is shifted for player-coach objects.
                        // Until the current build's player-coach class discriminator is validated, don't guess.
                        var personality = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        var positions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        for (var pos = 0; pos < PositionKeys.Length; pos++)
                        {
                            var shown = inline[0x150 + pos];
                            if (shown is >= 1 and <= 20) positions[PositionKeys[pos]] = shown;
                        }
                        players.Add(new LivePlayerRecord(uid, name, teamAddress, clubUid, i, address, true, relationshipVerified, dob, "Unknown", "No independently verified FM27 generated-player flag is currently mapped.", ca, pa, attributes, personality, positions));
                    }
                    catch { unnamed++; }
                }
                if (players.Count < 30_000) throw new InvalidOperationException($"Safety check failed: only {players.Count:N0} named player records were extracted from {playerBounds.Count:N0} slots. Expected a full FM27 player array; no import was performed.");
                var attributeCoverage = players.Count(p => p.Attributes?.Count >= 35);
                if (attributeCoverage < Math.Min(10_000, players.Count / 3))
                    throw new InvalidOperationException($"The player attribute cross-check did not pass: only {attributeCoverage:N0} of {players.Count:N0} players had 35+ range-valid attributes. No live attribute import was committed.");

                string version = profile.DisplayName;
                try { var executablePath = process.MainModule?.FileName; if (!string.IsNullOrWhiteSpace(executablePath)) version += " · exe " + (FileVersionInfo.GetVersionInfo(executablePath).FileVersion ?? "unknown"); } catch { }
                AtlasLog.Info($"Matched FM27 live read profile: {profile.Id}; fingerprint={profile.Fingerprint}; gameDate={gameDate?.ToString("yyyy-MM-dd") ?? "unavailable"}; players={players.Count}; attributeCoverage={attributeCoverage}; clubs={clubs.Count}.");
                return new LiveImportPayload(version, profile.Fingerprint, clubs, players, playerBounds.Count, unnamed, unlinked, gameDate);
            }
            finally { CloseHandle(handle); }
    }

    /// <summary>Legacy read-only survey retained for historical comparison only. It targets one old single-build witness layout and must not be used for current FM27 import or editing.</summary>
    [Obsolete("Legacy fixed-build survey retired. Use the profile-matched importer and diagnostics instead.")]
    public static string CreatePlayerFieldSurvey()
        => throw new NotSupportedException("The legacy fixed-build player survey was retired because its SHA/RVA witness profile is no longer maintained. Use the current profile-matched importer.");

    /// <summary>
    /// Read-only pointer graph survey for explicitly selected player witnesses.
    /// Pointer values are never exported. Only bounded blocks reached through pointer-shaped
    /// fields are recorded, with source offsets and byte windows for later correlation.
    /// </summary>
    [Obsolete("Legacy fixed-build survey. Use profile-matched diagnostics instead.")]
    public static string CreateLinkedMemorySurvey() =>
        CreateLinkedMemorySurvey(new[] { "Bradley Barcola", "Cole Palmer", "Alisson Becker", "Leon Goretzka" });

    [Obsolete("Legacy fixed-build survey retired. Use profile-matched diagnostics instead.")]
    public static string CreateLinkedMemorySurvey(IReadOnlyList<string> selectedNames)
        => throw new NotSupportedException("The legacy linked-memory survey was retired because its fixed-build RVA profile is no longer maintained. Use the current profile-matched importer.");


    /// <summary>Read-only FM27 attribute snapshot using documented Steam field offsets.
    /// Values are emitted only when raw bytes are in the documented 0..98 range.
    /// The result is a diagnostic CSV, not a write/edit operation.</summary>
    [Obsolete("Legacy fixed-build survey retired. Use profile-matched diagnostics instead.")]
    public static string CreateAttributeSnapshot(IReadOnlyList<string> selectedNames)
        => throw new NotSupportedException("The legacy fixed-offset attribute snapshot was retired because its fixed-build RVA profile is no longer maintained. Use profile-matched data only.");

    /// <summary>Broad R3.3 read-only discovery: attributes, position suitability, ability, reputation, contract and medical raw probes.</summary>
    [Obsolete("Legacy fixed-build survey retired. Use profile-matched diagnostics instead.")]
    public static string CreateFullPlayerIntelligenceSurvey(IReadOnlyList<string> selectedNames)
        => throw new NotSupportedException("The legacy full-player survey was retired because its fixed-build RVA profile is no longer maintained. Use profile-matched diagnostics only.");

    private static string NormalizeName(string value) =>
        string.Concat(value.Normalize(NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark))
            .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).Aggregate(new StringBuilder(), (b,c)=>b.Append(c)).ToString();

    private static byte[] RedactPointers(byte[] bytes)
    {
        // Redact aligned pointer-shaped 64-bit values from exported windows. The scanner
        // retains them internally only long enough to traverse the bounded graph.
        for (var i = 0; i <= bytes.Length - 8; i += 8)
            if (Pointer(BitConverter.ToUInt64(bytes, i))) Array.Clear(bytes, i, 8);
        return bytes;
    }

    private static void AppendBlockRow(StringBuilder output, string witness, uint uid, int depth, int sourceOffset, int blockOffset, byte[] bytes)
    {
        var padded = new byte[16]; Array.Copy(bytes, padded, bytes.Length);
        var words = Enumerable.Range(0, 4).Select(i => BitConverter.ToUInt32(padded, i * 4).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var ascii = new string(bytes.Select(b => b >= 32 && b <= 126 ? (char)b : '.').ToArray());
        output.Append(Csv(witness)).Append(',').Append(uid).Append(',').Append(depth).Append(",0x").Append(sourceOffset.ToString("X3"))
            .Append(",0x").Append(blockOffset.ToString("X3")).Append(',').Append(Csv(Convert.ToHexString(bytes)));
        foreach (var word in words) output.Append(',').Append(word);
        output.Append(',').Append(Csv(ascii)).AppendLine();
    }


    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static DateTime? TryReadGameDate(nint handle, ulong moduleBase, uint imageSize, ulong dateRva)
    {
        if (dateRva == 0 || dateRva + 4 > imageSize) return null;
        try
        {
            var raw = Read(handle, (nint)(moduleBase + dateRva), 4);
            var day = (ushort)(BitConverter.ToUInt16(raw, 0) & 0x01FF);
            var year = BitConverter.ToUInt16(raw, 2);
            if (year < 1901 || year > 2100) return null;
            var maxDay = DateTime.IsLeapYear(year) ? 366 : 365;
            return day >= 1 && day <= maxDay ? new DateTime(year, 1, 1).AddDays(day - 1) : null;
        }
        catch { return null; }
    }

    private static DateTime? TryReadPlayerDob(nint handle, ulong moduleBase, ulong playerAddress)
    {
        // Public FM27 layout research identifies the player-coach class vtable separately.
        // Only use the shifted DOB field when that exact class is present; never guess from bytes.
        ulong offset = 0x310;
        try
        {
            var vtable = U64(handle, (nint)playerAddress);
            if (vtable == moduleBase + 0x4F75A08) offset = 0x408;
        }
        catch { return null; }
        try
        {
            var raw = Read(handle, (nint)(playerAddress + offset), 4);
            var day = (ushort)(BitConverter.ToUInt16(raw, 0) & 0x01FF); // high bits are date flags
            var year = BitConverter.ToUInt16(raw, 2);
            if (year < 1901 || year > 2100) return null; // 1900 is FM's empty-date sentinel
            var maxDay = DateTime.IsLeapYear(year) ? 366 : 365;
            if (day < 1 || day > maxDay) return null;
            return new DateTime(year, 1, 1).AddDays(day - 1);
        }
        catch { return null; }
    }

    private static (ulong Data, int Count) ReadBounds(nint h, ulong b, uint image, ulong dataRva, ulong endRva, int min, int max, string label)
    {
        if (dataRva + 8 > image || endRva + 8 > image) throw new InvalidOperationException($"{label} array RVA is outside the module image.");
        var data = U64(h, (nint)(b + dataRva)); var end = U64(h, (nint)(b + endRva));
        if (!Pointer(data) || !Pointer(end) || end <= data || (end - data) % 8 != 0) throw new InvalidOperationException($"{label} array bounds failed validation.");
        var count = (end - data) / 8;
        if (count < (ulong)min || count > (ulong)max) throw new InvalidOperationException($"{label} array count {count} was outside the safety range.");
        return (data, (int)count);
    }
    private static bool Pointer(ulong p) => p >= 0x10000 && p <= 0x7FFF_FFFF_FFFF;
    private static ulong U64(nint h, nint a) => BitConverter.ToUInt64(Read(h, a, 8), 0);
    private static byte[] Read(nint h, nint a, int n) { var b = new byte[n]; if (!ReadProcessMemory(h, a, b, (nuint)n, out var got) || got != (nuint)n) throw new InvalidOperationException("A read-only memory read was incomplete."); return b; }
    private static string? ReadLengthString(nint h, ulong p, int max) { if (!Pointer(p)) return null; var n = BitConverter.ToUInt32(Read(h, (nint)p, 4), 0); if (n == 0 || n > max) return null; var s = Encoding.UTF8.GetString(Read(h, (nint)(p + 4), (int)n)); return s.Any(char.IsControl) ? null : s; }
    private static string? ReadNameSub(nint h, ulong p) { try { var a = U64(h, (nint)p); if (!Pointer(a)) return null; var b = U64(h, (nint)a); return ReadLengthString(h, b, 64); } catch { return null; } }
    private static string? Join(string? a, string? b) => string.IsNullOrWhiteSpace(a) ? b : string.IsNullOrWhiteSpace(b) ? a : a + " " + b;
}
