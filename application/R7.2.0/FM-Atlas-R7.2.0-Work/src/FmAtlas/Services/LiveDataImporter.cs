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
    // Legacy fixed-layout surveys are intentionally disabled: their former SHA-256
    // and array RVAs were not validated for the current multi-storefront profiles.
    // Keeping explicit sentinels makes those obsolete entry points fail closed.
    private const string ExpectedModuleSha256 = "LEGACY_SURVEY_DISABLED_PROFILE_MATCH_REQUIRED";
    private const ulong PlayerDataRva = 0x57037D0;
    private const ulong PlayerEndRva = 0x57037D8;
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
    [Obsolete("Legacy fixed-build survey. Use the profile-matched read-only importer and diagnostics instead.")]
    public static string CreatePlayerFieldSurvey()
    {
        var processes = Process.GetProcessesByName("fm");
        if (processes.Length == 0) throw new InvalidOperationException("Football Manager 27 is not running.");
        try
        {
            using var process = processes[0];
            var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => string.Equals(m.ModuleName, "game_plugin.dll", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("game_plugin.dll was not found in the FM process.");
            var moduleBase = (ulong)module.BaseAddress.ToInt64();
            var handle = OpenProcess(QueryInformation | VmRead, false, process.Id);
            if (handle == 0) throw new InvalidOperationException($"Read-only access was denied (Win32 {Marshal.GetLastWin32Error()}).");
            try
            {
                var pe = Read(handle, (nint)moduleBase, 0x40);
                if (pe[0] != 'M' || pe[1] != 'Z') throw new InvalidOperationException("Invalid in-memory PE header.");
                var peOffset = BitConverter.ToInt32(pe, 0x3C);
                var nt = Read(handle, (nint)(moduleBase + (ulong)peOffset), 24);
                var optSize = BitConverter.ToUInt16(nt, 20);
                var optional = Read(handle, (nint)(moduleBase + (ulong)peOffset + 24), optSize);
                var imageSize = BitConverter.ToUInt32(optional, 56);
                using (var stream = File.OpenRead(module.FileName))
                {
                    var hash = Convert.ToHexString(SHA256.HashData(stream));
                    if (!string.Equals(hash, ExpectedModuleSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("This FM27 module fingerprint is not the validated build. Survey cancelled safely.");
                }
                var bounds = ReadBounds(handle, moduleBase, imageSize, PlayerDataRva, PlayerEndRva, 20_000, 200_000, "player");
                var targets = new Dictionary<string, (ulong Address, uint Uid)>();
                for (var i = 0; i < bounds.Count && targets.Count < 2; i++)
                {
                    var address = U64(handle, (nint)(bounds.Data + (ulong)i * 8));
                    if (!Pointer(address)) continue;
                    try
                    {
                        var common = ReadNameSub(handle, address + 0x2E8);
                        var first = ReadNameSub(handle, address + 0x2D8);
                        var last = ReadNameSub(handle, address + 0x2E0);
                        var name = !string.IsNullOrWhiteSpace(common) ? common : Join(first, last);
                        if (name is not ("Bradley Barcola" or "Cole Palmer")) continue;
                        var uid = BitConverter.ToUInt32(Read(handle, (nint)(address + 0x294), 4), 0);
                        targets[name] = (address, uid);
                    }
                    catch { }
                }
                if (!targets.ContainsKey("Bradley Barcola") || !targets.ContainsKey("Cole Palmer"))
                    throw new InvalidOperationException("Both validated player witnesses must be present before a field survey can run.");

                var output = new StringBuilder();
                output.AppendLine("FM ATLAS — PLAYER FIELD OFFSET SURVEY");
                output.AppendLine("READ ONLY. No game memory was modified. Raw pointer values are intentionally omitted.");
                output.AppendLine($"Game module SHA-256: {ExpectedModuleSha256}");
                output.AppendLine($"Player structure survey length: 0x3E0 bytes; player slots: {bounds.Count}");
                output.AppendLine("Witness,UID,Offset,Byte0,Byte1,Byte2,Byte3,UInt32LE,Int32LE,Float32");
                foreach (var target in targets.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    var bytes = Read(handle, (nint)target.Value.Address, 0x3E0);
                    for (var offset = 0; offset <= bytes.Length - 4; offset += 4)
                    {
                        var u = BitConverter.ToUInt32(bytes, offset);
                        var f = BitConverter.ToSingle(bytes, offset);
                        output.Append(target.Key).Append(',').Append(target.Value.Uid).Append(",0x").Append(offset.ToString("X3"));
                        output.Append(',').Append(bytes[offset].ToString("X2")).Append(',').Append(bytes[offset + 1].ToString("X2"));
                        output.Append(',').Append(bytes[offset + 2].ToString("X2")).Append(',').Append(bytes[offset + 3].ToString("X2"));
                        output.Append(',').Append(u).Append(',').Append(BitConverter.ToInt32(bytes, offset));
                        output.Append(',').Append(float.IsFinite(f) ? f.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "NaN").AppendLine();
                    }
                }
                output.AppendLine("Known validated identity offsets: UID +0x294; name subrecords +0x2D8/+0x2E0/+0x2E8; team pointer +0x130.");
                output.AppendLine("All other offsets are UNMAPPED candidates. Do not interpret them as attributes until independently matched to known in-game values.");
                return output.ToString();
            }
            finally { CloseHandle(handle); }
        }
        finally { foreach (var p in processes.Skip(1)) p.Dispose(); }
    }

    /// <summary>
    /// Read-only pointer graph survey for explicitly selected player witnesses.
    /// Pointer values are never exported. Only bounded blocks reached through pointer-shaped
    /// fields are recorded, with source offsets and byte windows for later correlation.
    /// </summary>
    [Obsolete("Legacy fixed-build survey. Use profile-matched diagnostics instead.")]
    public static string CreateLinkedMemorySurvey() =>
        CreateLinkedMemorySurvey(new[] { "Bradley Barcola", "Cole Palmer", "Alisson Becker", "Leon Goretzka" });

    [Obsolete("Legacy fixed-build survey. Use profile-matched diagnostics instead.")]
    public static string CreateLinkedMemorySurvey(IReadOnlyList<string> selectedNames)
    {
        var processes = Process.GetProcessesByName("fm");
        if (processes.Length == 0) throw new InvalidOperationException("Football Manager 27 is not running.");
        try
        {
            using var process = processes[0];
            var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => string.Equals(m.ModuleName, "game_plugin.dll", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("game_plugin.dll was not found in the FM process.");
            var moduleBase = (ulong)module.BaseAddress.ToInt64();
            var handle = OpenProcess(QueryInformation | VmRead, false, process.Id);
            if (handle == 0) throw new InvalidOperationException($"Read-only access was denied (Win32 {Marshal.GetLastWin32Error()}).");
            try
            {
                using (var stream = File.OpenRead(module.FileName))
                {
                    var hash = Convert.ToHexString(SHA256.HashData(stream));
                    if (!string.Equals(hash, ExpectedModuleSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("This FM27 module fingerprint is not the validated build. Linked survey cancelled safely.");
                }
                var pe = Read(handle, (nint)moduleBase, 0x40);
                if (pe[0] != 'M' || pe[1] != 'Z') throw new InvalidOperationException("Invalid in-memory PE header.");
                var peOffset = BitConverter.ToInt32(pe, 0x3C);
                if (peOffset < 0x40 || peOffset > 0x1000) throw new InvalidOperationException("Invalid PE header offset.");
                var nt = Read(handle, (nint)(moduleBase + (ulong)peOffset), 24);
                var optSize = BitConverter.ToUInt16(nt, 20);
                var optional = Read(handle, (nint)(moduleBase + (ulong)peOffset + 24), optSize);
                var imageSize = BitConverter.ToUInt32(optional, 56);
                var bounds = ReadBounds(handle, moduleBase, imageSize, PlayerDataRva, PlayerEndRva, 20_000, 200_000, "player");
                var requested = selectedNames.Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray();
                if (requested.Length == 0) throw new InvalidOperationException("Select at least one player by exact name.");
                var requestedSet = requested.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var targets = new Dictionary<string, (ulong Address, uint Uid)>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < bounds.Count && targets.Count < requested.Length; i++)
                {
                    var address = U64(handle, (nint)(bounds.Data + (ulong)i * 8));
                    if (!Pointer(address)) continue;
                    try
                    {
                        var common = ReadNameSub(handle, address + 0x2E8);
                        var first = ReadNameSub(handle, address + 0x2D8);
                        var last = ReadNameSub(handle, address + 0x2E0);
                        var full = ReadLengthString(handle, U64(handle, (nint)(address + 0x2C8)), 96);
                        var candidates = new[] { common, Join(first, last), full }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                        var name = requested.FirstOrDefault(r => candidates.Any(c => NormalizeName(c!) == NormalizeName(r)) ||
                            (NormalizeName(r).Contains("alisson") && candidates.Any(c => NormalizeName(c!).Contains("alisson") && NormalizeName(c!).Contains("becker"))));
                        if (name is null) continue;
                        var uid = BitConverter.ToUInt32(Read(handle, (nint)(address + 0x294), 4), 0);
                        targets[name] = (address, uid);
                    }
                    catch { }
                }
                var missing = requested.Where(n => !targets.Keys.Contains(n, StringComparer.OrdinalIgnoreCase)).ToArray();
                if (targets.Count == 0)
                    throw new InvalidOperationException("None of the selected players could be matched by exact name. Check spelling and make sure the career is loaded.");

                var output = new StringBuilder();
                output.AppendLine("FM ATLAS — LINKED PLAYER MEMORY DISCOVERY v2");
                output.AppendLine("READ ONLY. Pointer values and process addresses are deliberately omitted.");
                output.AppendLine($"Module SHA-256: {ExpectedModuleSha256}");
                output.AppendLine($"Requested players: {string.Join("; ", requested)}");
                output.AppendLine($"Matched players: {string.Join("; ", targets.Keys)}");
                output.AppendLine($"Unmatched players: {string.Join("; ", missing)}");
                output.AppendLine($"Player slots: {bounds.Count}");
                output.AppendLine("Witness,UID,Depth,SourceOffset,BlockOffset,BlockHex,UInt32LE_0,UInt32LE_4,UInt32LE_8,UInt32LE_C,AsciiPreview");
                var blocksWritten = 0;
                foreach (var witness in targets.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    var root = witness.Value.Address;
                    var rootBytes = Read(handle, (nint)root, 0x3E0);
                    // Root snapshot is retained in 16-byte windows to allow byte/unaligned analysis.
                    for (var o = 0; o < rootBytes.Length; o += 16)
                        AppendBlockRow(output, witness.Key, witness.Value.Uid, 0, o, o, RedactPointers(rootBytes.AsSpan(o, Math.Min(16, rootBytes.Length - o)).ToArray()));

                    var seen = new HashSet<ulong> { root };
                    var frontier = new List<(ulong Address, int SourceOffset, int Depth)>();
                    for (var o = 0; o <= rootBytes.Length - 8; o += 8)
                    {
                        var ptr = BitConverter.ToUInt64(rootBytes, o);
                        if (Pointer(ptr) && seen.Add(ptr)) frontier.Add((ptr, o, 1));
                    }
                    // Bounded graph walk: at most 48 unique nodes per witness, depth two,
                    // 0x180 bytes per node. Every candidate read is guarded and failures are skipped.
                    var nodeCount = 0;
                    while (frontier.Count > 0 && nodeCount < 48)
                    {
                        var node = frontier[0]; frontier.RemoveAt(0);
                        byte[] block;
                        try { block = Read(handle, (nint)node.Address, 0x180); }
                        catch { continue; }
                        nodeCount++; blocksWritten++;
                        for (var o = 0; o < block.Length; o += 16)
                            AppendBlockRow(output, witness.Key, witness.Value.Uid, node.Depth, node.SourceOffset, o, RedactPointers(block.AsSpan(o, Math.Min(16, block.Length - o)).ToArray()));
                        if (node.Depth >= 2) continue;
                        for (var o = 0; o <= block.Length - 8; o += 8)
                        {
                            var ptr = BitConverter.ToUInt64(block, o);
                            if (Pointer(ptr) && seen.Add(ptr) && frontier.Count + nodeCount < 48)
                                frontier.Add((ptr, node.SourceOffset, node.Depth + 1));
                        }
                    }
                    output.AppendLine($"# {witness.Key}: {nodeCount} linked blocks captured");
                }
                output.AppendLine($"# Total linked blocks captured: {blocksWritten}");
                output.AppendLine("# Depth 0 = inline player record. Depth 1 = direct pointer target. Depth 2 = pointer target reached from a depth-1 block.");
                output.AppendLine($"# Requested player count: {requested.Length}; matched: {targets.Count}; unmatched: {missing.Length}");
                output.AppendLine("# This is a discovery dataset, not an attribute map. Pointer-shaped values can be false positives; validate candidates against additional players and in-game values.");
                return output.ToString();
            }
            finally { CloseHandle(handle); }
        }
        finally { foreach (var p in processes.Skip(1)) p.Dispose(); }
    }


    /// <summary>Read-only FM27 attribute snapshot using documented Steam field offsets.
    /// Values are emitted only when raw bytes are in the documented 0..98 range.
    /// The result is a diagnostic CSV, not a write/edit operation.</summary>
    [Obsolete("Legacy fixed-build survey. Use profile-matched diagnostics instead.")]
    public static string CreateAttributeSnapshot(IReadOnlyList<string> selectedNames)
    {
        var processes = Process.GetProcessesByName("fm");
        if (processes.Length == 0) throw new InvalidOperationException("Football Manager 27 is not running.");
        try
        {
            using var process = processes[0];
            var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => string.Equals(m.ModuleName, "game_plugin.dll", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("game_plugin.dll was not found in the FM process.");
            var moduleBase = (ulong)module.BaseAddress.ToInt64();
            var handle = OpenProcess(QueryInformation | VmRead, false, process.Id);
            if (handle == 0) throw new InvalidOperationException($"Read-only access was denied (Win32 {Marshal.GetLastWin32Error()}).");
            try
            {
                using (var stream = File.OpenRead(module.FileName))
                {
                    var hash = Convert.ToHexString(SHA256.HashData(stream));
                    if (!string.Equals(hash, ExpectedModuleSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"FM27 build fingerprint mismatch. Expected {ExpectedModuleSha256}; found {hash}. No scan performed.");
                }
                var pe = Read(handle, (nint)moduleBase, 0x40);
                var peOffset = BitConverter.ToInt32(pe, 0x3C);
                var nt = Read(handle, (nint)(moduleBase + (ulong)peOffset), 24);
                var optSize = BitConverter.ToUInt16(nt, 20);
                var optional = Read(handle, (nint)(moduleBase + (ulong)peOffset + 24), optSize);
                var imageSize = BitConverter.ToUInt32(optional, 56);
                var bounds = ReadBounds(handle, moduleBase, imageSize, PlayerDataRva, PlayerEndRva, 20_000, 200_000, "player");
                var requested = selectedNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray();
                if (requested.Length == 0) throw new InvalidOperationException("Select at least one player.");
                var targets = new Dictionary<string, (ulong Address, uint Uid, string Display)>(StringComparer.OrdinalIgnoreCase);
                var requestedNorm = requested.ToDictionary(n => n, n => NormalizeName(n), StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < bounds.Count && targets.Count < requested.Length; i++)
                {
                    try
                    {
                        var address = U64(handle, (nint)(bounds.Data + (ulong)i * 8));
                        if (!Pointer(address)) continue;
                        var common = ReadNameSub(handle, address + 0x2E8);
                        var first = ReadNameSub(handle, address + 0x2D8);
                        var last = ReadNameSub(handle, address + 0x2E0);
                        var fullPtr = U64(handle, (nint)(address + 0x2C8));
                        var full = ReadLengthString(handle, fullPtr, 96);
                        var display = !string.IsNullOrWhiteSpace(common) ? common : Join(first, last) ?? full;
                        var variants = new[] { common, Join(first,last), full, Join(first, last) is string joined ? joined + " " : null }
                            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => NormalizeName(x!)).ToHashSet();
                        var matched = requested.FirstOrDefault(n => variants.Contains(requestedNorm[n]) ||
                            (requestedNorm[n].Contains("alisson") && variants.Any(v => v.Contains("alisson") && v.Contains("becker"))));
                        if (matched is null) continue;
                        var uid = BitConverter.ToUInt32(Read(handle, (nint)(address + 0x294), 4), 0);
                        targets[matched] = (address, uid, display ?? matched);
                    }
                    catch { }
                }
                var output = new StringBuilder();
                output.AppendLine("FM Atlas R3.2.1 — READ-ONLY PLAYER ATTRIBUTE & STATE DISCOVERY");
                output.AppendLine($"Module SHA-256: {ExpectedModuleSha256}");
                output.AppendLine("Source: FM27 Steam field registry cross-reference. Attribute bytes decoded as (raw + 2) / 5; positional suitability is a direct 1–20 value. Condition/medical fields are not attribute bytes.");
                output.AppendLine("Player,UID,FieldKey,Attribute,Category,Offset,RawByte,DisplayedValue,ProfileApplicability,MappingStatus");
                foreach (var target in targets.OrderBy(x=>x.Key,StringComparer.OrdinalIgnoreCase))
                {
                    var bytes = Read(handle, (nint)target.Value.Address, 0x3E0);
                    var gkSuitability = bytes[0x150];
                    var maxOutfieldSuitability = bytes.Skip(0x151).Take(14).Max();
                    var isGoalkeeper = gkSuitability >= 10 && gkSuitability >= maxOutfieldSuitability;
                    var profile = isGoalkeeper ? "Goalkeeper" : "Outfield";
                    foreach (var field in PlayerAttributeSchema.Fields)
                    {
                        var raw = bytes[field.Offset];
                        var value = (raw + 2) / 5;
                        var valid = raw <= 98 && value >= 1 && value <= 20;
                        var applicability = field.Category == "Goalkeeping" ? (isGoalkeeper ? "Primary" : "Secondary/NotPrimary") :
                            field.Category == "Hidden" ? "HiddenAttribute" : "Shared";
                        var status = valid ? "Provisional-FM27CrossReference" : (raw == 100 ? "SentinelOrWrongMapping-Investigate" : "OutOfRange");
                        output.Append(Csv(target.Value.Display)).Append(',').Append(target.Value.Uid).Append(',')
                            .Append(field.Key).Append(',').Append(Csv(field.Label)).Append(',').Append(Csv(field.Category))
                            .Append(",0x").Append(field.Offset.ToString("X3")).Append(',').Append(raw).Append(',')
                            .Append(valid ? value.ToString() : "").Append(',').Append(applicability).Append(',').Append(status).AppendLine();
                    }
                    output.Append(Csv(target.Value.Display)).Append(',').Append(target.Value.Uid)
                        .Append(",profile_type,Profile Type,Classification,0x150,").Append(gkSuitability).Append(',')
                        .Append(profile).Append(',').Append(profile).AppendLine(",PositionDerived");
                    output.Append(Csv(target.Value.Display)).Append(',').Append(target.Value.Uid)
                        .Append(",medical_record,Medical Record Pointer,Medical,0x0F8,").Append(BitConverter.ToUInt64(bytes, 0x0F8) != 0 ? "Present" : "Null")
                        .Append(",,NotAnAttribute,PointerPresenceOnly-DetailsNotDecoded").AppendLine();
                    output.Append(Csv(target.Value.Display)).Append(',').Append(target.Value.Uid)
                        .Append(",fitness,Match Fitness,Condition,UNMAPPED,,,,Unmapped-DoNotDecodeAsAttribute").AppendLine();
                    output.Append(Csv(target.Value.Display)).Append(',').Append(target.Value.Uid)
                        .Append(",sharpness,Match Sharpness,Condition,UNMAPPED,,,,Unmapped-DoNotDecodeAsAttribute").AppendLine();
                    output.Append(Csv(target.Value.Display)).Append(',').Append(target.Value.Uid)
                        .Append(",fatigue,Fatigue/Jadedness,Condition,UNMAPPED,,,,Unmapped-DoNotDecodeAsAttribute").AppendLine();
                }
                foreach (var name in requested.Where(n => !targets.ContainsKey(n)))
                    output.Append(Csv(name)).Append(",,,,,,,,Unmatched").AppendLine();
                output.AppendLine($"# Requested={requested.Length}; Matched={targets.Count}; Unmatched={requested.Length-targets.Count}");
                output.AppendLine("# Important: most attribute offsets are inherited FM26 leads and remain provisional on FM27. GK/outfield profile is inferred from positional suitability bytes (GK +0x150 versus outfield +0x151..+0x15E). Medical pointer presence only is reported; injury records, fitness and sharpness are not decoded. No writes are performed.");
                return output.ToString();
            }
            finally { CloseHandle(handle); }
        }
        finally { foreach (var p in processes.Skip(1)) p.Dispose(); }
    }

    /// <summary>Broad R3.3 read-only discovery: attributes, position suitability, ability, reputation, contract and medical raw probes.</summary>
    [Obsolete("Legacy fixed-build survey. Use profile-matched diagnostics instead.")]
    public static string CreateFullPlayerIntelligenceSurvey(IReadOnlyList<string> selectedNames)
    {
        var processes = Process.GetProcessesByName("fm");
        if (processes.Length == 0) throw new InvalidOperationException("Football Manager 27 is not running.");
        try
        {
            using var process = processes[0];
            var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => string.Equals(m.ModuleName, "game_plugin.dll", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("game_plugin.dll was not found in the FM process.");
            var moduleBase = (ulong)module.BaseAddress.ToInt64();
            var handle = OpenProcess(QueryInformation | VmRead, false, process.Id);
            if (handle == 0) throw new InvalidOperationException($"Read-only access was denied (Win32 {Marshal.GetLastWin32Error()}).");
            try
            {
                using (var stream = File.OpenRead(module.FileName))
                {
                    var hash = Convert.ToHexString(SHA256.HashData(stream));
                    if (!string.Equals(hash, ExpectedModuleSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"FM27 build fingerprint mismatch. Expected {ExpectedModuleSha256}; found {hash}. No scan performed.");
                }
                var pe = Read(handle, (nint)moduleBase, 0x40);
                if (pe[0] != 'M' || pe[1] != 'Z') throw new InvalidOperationException("Invalid module PE header.");
                var peOffset = BitConverter.ToInt32(pe, 0x3C);
                if (peOffset < 0x40 || peOffset > 0x1000) throw new InvalidOperationException("Invalid PE header offset.");
                var nt = Read(handle, (nint)(moduleBase + (ulong)peOffset), 24);
                if (nt[0] != 'P' || nt[1] != 'E' || BitConverter.ToUInt16(nt, 4) != 0x8664) throw new InvalidOperationException("Unexpected module architecture.");
                var optSize = BitConverter.ToUInt16(nt, 20);
                var optional = Read(handle, (nint)(moduleBase + (ulong)peOffset + 24), optSize);
                var bounds = ReadBounds(handle, moduleBase, BitConverter.ToUInt32(optional, 56), PlayerDataRva, PlayerEndRva, 20_000, 200_000, "player");
                var requested = selectedNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray();
                if (requested.Length == 0) throw new InvalidOperationException("Select at least one player.");
                var wanted = requested.ToDictionary(n => n, NormalizeName, StringComparer.OrdinalIgnoreCase);
                var targets = new Dictionary<string, (ulong Address,uint Uid,string Display)>(StringComparer.OrdinalIgnoreCase);
                for (var i=0; i<bounds.Count && targets.Count<requested.Length; i++)
                {
                    try
                    {
                        var a=U64(handle,(nint)(bounds.Data+(ulong)i*8)); if(!Pointer(a)) continue;
                        var common=ReadNameSub(handle,a+0x2E8); var first=ReadNameSub(handle,a+0x2D8); var last=ReadNameSub(handle,a+0x2E0);
                        var fp=U64(handle,(nint)(a+0x2C8)); var full=ReadLengthString(handle,fp,96);
                        var display=!string.IsNullOrWhiteSpace(common)?common:Join(first,last)??full;
                        var variants=new[]{common,Join(first,last),full}.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>NormalizeName(x!)).ToHashSet();
                        var match=requested.FirstOrDefault(n=>variants.Contains(wanted[n]) || (wanted[n].Contains("alisson") && variants.Any(v=>v.Contains("alisson")&&v.Contains("becker"))));
                        if(match is null) continue;
                        var uid=BitConverter.ToUInt32(Read(handle,(nint)(a+0x294),4),0); if(uid!=0) targets[match]=(a,uid,display??match);
                    }
                    catch { }
                }
                var sb=new StringBuilder();
                sb.AppendLine("FM Atlas R3.4 — FULL PLAYER INTELLIGENCE DISCOVERY (READ-ONLY)");
                sb.AppendLine($"Module SHA-256: {ExpectedModuleSha256}");
                sb.AppendLine("Player,UID,Section,Field,Offset,Raw,Decoded,Applicability,Confidence,Notes");
                void Row(string n,uint uid,string section,string field,string offset,string raw,string decoded,string applies,string confidence,string notes)
                    => sb.Append(Csv(n)).Append(',').Append(uid).Append(',').Append(Csv(section)).Append(',').Append(Csv(field)).Append(',').Append(offset).Append(',').Append(Csv(raw)).Append(',').Append(Csv(decoded)).Append(',').Append(Csv(applies)).Append(',').Append(Csv(confidence)).Append(',').Append(Csv(notes)).AppendLine();
                static string Hex(byte[] b)=>Convert.ToHexString(b);
                static int U16(byte[] b,int o)=>BitConverter.ToUInt16(b,o);
                foreach(var t in targets.OrderBy(x=>x.Key,StringComparer.OrdinalIgnoreCase))
                {
                    var n=t.Value.Display; var uid=t.Value.Uid; var b=Read(handle,(nint)t.Value.Address,0x3E0);
                    var gk=b[0x150]; var outMax=b.Skip(0x151).Take(14).Max(); var keeper=gk>=10&&gk>=outMax; var profile=keeper?"Goalkeeper":"Outfield";
                    Row(n,uid,"Classification","Profile Type","0x150-0x15E",$"GK={gk};OutfieldMax={outMax}",profile,profile,"Position-derived","Both GK and outfield attributes are retained; this only selects the primary profile.");
                    string[] pos={"GK","SW","DL","DC","DR","DM","ML","MC","MR","AML","AMC","AMR","ST","WBL","WBR"};
                    for(int j=0;j<pos.Length;j++) Row(n,uid,"Positions",pos[j],$"0x{0x150+j:X3}",b[0x150+j].ToString(),b[0x150+j].ToString(),pos[j]=="GK"?"Goalkeeper":"Outfield","Direct 1-20 cross-reference","Position suitability; not a role rating.");
                    foreach(var f in PlayerAttributeSchema.Fields)
                    {
                        var raw=b[f.Offset]; var val=(raw+2)/5; var valid=raw<=98&&val>=1&&val<=20;
                        var app=f.Category=="Goalkeeping"?(keeper?"Primary":"Secondary"):f.Category=="Hidden"?"Hidden":"Shared";
                        Row(n,uid,f.Category,f.Label,$"0x{f.Offset:X3}",raw.ToString(),valid?val.ToString():"",app,valid?"Provisional-FM27-cross-reference":raw==100?"Anomalous-sentinel-or-offset":"Out-of-range",f.Key=="injury_proneness"?"Hidden attribute: Injury Proneness (FMRTE-style hidden stat); FM27 offset is still a cross-reference pending independent witness.":f.Key=="penalty_taking"&&raw==100?"Raw 100 outside expected encoded range; do not display as rating.":"Candidate decode (raw+2)/5; most inherited mappings still require independent witnesses.");
                    }
                    foreach(var f in new[]{("Current Ability",0x264),("Potential Ability",0x266),("Home Reputation",0x25E),("Current Reputation",0x260),("World Reputation",0x262)})
                        Row(n,uid,f.Item1.Contains("Reputation")?"Reputation":"Ability",f.Item1,$"0x{f.Item2:X3}",Hex(b.Skip(f.Item2).Take(2).ToArray()),U16(b,f.Item2).ToString(),"All players","u16 cross-reference","Raw integer; not the 1-20 attribute encoding.");
                    Row(n,uid,"Identity","Height","0x22E",Hex(b.Skip(0x22E).Take(2).ToArray()),U16(b,0x22E).ToString(),"All players","Cross-reference","Raw u16; units require witness confirmation.");
                    Row(n,uid,"Identity","Date of Birth raw","0x310",Hex(b.Skip(0x310).Take(4).ToArray()),"","All players","Raw evidence","Packed date not decoded; flags preserved.");
                    Row(n,uid,"Personality","Personality bytes","0x2F8-0x2FF",Hex(b.Skip(0x2F8).Take(8).ToArray()),"","All players","Offset cross-reference","Eight raw bytes; individual labels/ranges remain provisional.");
                    var cp=U64(handle,(nint)(t.Value.Address+0x330));
                    if(Pointer(cp))
                    {
                        try { var c=Read(handle,(nint)cp,0xC4);
                            Row(n,uid,"Contract","Record","Player+0x330","Present","","All players","Pointer cross-reference","Pointer address intentionally omitted.");
                            Row(n,uid,"Contract","Weekly Wage GBP","Contract+0x20",Hex(c.Skip(0x20).Take(4).ToArray()),BitConverter.ToUInt32(c,0x20).ToString(),"All players","FM27 witnessed","u32 pounds per week.");
                            foreach(var f in new[]{("Signed date raw",0x44),("Expiry date raw",0x48)}) Row(n,uid,"Contract",f.Item1,$"Contract+0x{f.Item2:X2}",Hex(c.Skip(f.Item2).Take(4).ToArray()),"","All players","Raw evidence","Packed date; not decoded.");
                            foreach(var f in new[]{("Squad Status",0x54),("Listing Flags",0x57),("Squad Number",0x5D)}) Row(n,uid,"Contract",f.Item1,$"Contract+0x{f.Item2:X2}",c[f.Item2].ToString(),c[f.Item2].ToString(),"All players","FM27 cross-reference","Numeric value retained; unknown enum bits preserved.");
                        } catch { Row(n,uid,"Contract","Record","Player+0x330","Present-but-unreadable","","All players","Read failed","No address exported."); }
                    } else Row(n,uid,"Contract","Record","Player+0x330","Null","","All players","Not present","No contract pointer.");
                    var mp=U64(handle,(nint)(t.Value.Address+0xF8));
                    if(Pointer(mp))
                    {
                        try { var m=Read(handle,(nint)mp,0x60);
                            Row(n,uid,"Medical","Record","Player+0x0F8","Present","","All players","FM27 witnessed pointer","Pointer presence only; contents are raw discovery evidence.");
                            Row(n,uid,"Medical discovery","Medical block 0x00-0x5F","Medical+0x00",Hex(m),"","All players","Raw capture","No pointer addresses exported; no injury conclusions drawn.");
                            Row(n,uid,"Medical discovery","Injuries vector header","Medical+0x00",Hex(m.Take(0x18).ToArray()),"","All players","Layout lead","Header bytes only; count and stride not interpreted.");
                            Row(n,uid,"Medical discovery","Absences vector header","Medical+0x18",Hex(m.Skip(0x18).Take(0x18).ToArray()),"","All players","Layout lead","Header bytes only; ban/holiday labels not inferred.");
                        } catch { Row(n,uid,"Medical","Record","Player+0x0F8","Present-but-unreadable","","All players","Read failed","Bounded read failed."); }
                    } else Row(n,uid,"Medical","Record","Player+0x0F8","Null","","All players","Not present","Null does not prove no injury; state may be stored elsewhere.");
                    foreach(var f in new[]{"Match Fitness","Match Sharpness","Fatigue / Jadedness"}) Row(n,uid,"Condition",f,"UNMAPPED","","","All players","Not mapped","Do not decode as normal 1-20 attributes.");
                }
                foreach(var name in requested.Where(n=>!targets.ContainsKey(n))) Row(name,0,"Discovery","Player match","","","","","Unmatched","Requested name not found.");
                sb.AppendLine($"# Requested={requested.Length}; Matched={targets.Count}; Unmatched={requested.Length-targets.Count}");
                sb.AppendLine("# Read-only. No memory writes, save writes or raw process addresses. Medical vectors are raw probes, not decoded injury records.");
                return sb.ToString();
            }
            finally { CloseHandle(handle); }
        }
        finally { foreach(var p in processes.Skip(1)) p.Dispose(); }
    }

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
