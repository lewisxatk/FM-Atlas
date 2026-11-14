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
    IReadOnlyDictionary<string, double>? PositionRatings = null,
    string? NationName = null,
    uint? NationUid = null,
    int? HeightCm = null, int? Condition = null, int? MatchSharpness = null,
    int? Jadedness = null, int? Morale = null,
    int? HomeReputation = null, int? CurrentReputation = null, int? WorldReputation = null,
    int? AskingPrice = null,
    int? InternationalCaps = null, int? InternationalGoals = null,
    int? U21Caps = null, int? U21Goals = null,
    int? LeftFoot = null, int? RightFoot = null,
    int? TransferValue = null,
    IReadOnlyDictionary<string, double>? ContractFacts = null,
    IReadOnlyDictionary<string, double>? TraitFlags = null);
public sealed record LiveStaffRecord(
    uint Uid, string Name, int ArrayIndex, ulong Address, bool IsHumanManager,
    DateTime? DateOfBirth, int? Age, string? NationName, uint? NationUid,
    string? ClubName, uint? ClubUid, string? JobTitle, int? JobCode,
    int? CurrentAbility, int? PotentialAbility,
    StaffJobPreferenceResult? JobPreferences = null,
    IReadOnlyDictionary<string, double>? Attributes = null,
    IReadOnlyDictionary<string, double>? Personality = null,
    int? WagePerWeek = null, DateTime? ContractStart = null,
    DateTime? ContractExpiry = null, DateTime? JoinedClub = null);
// ModuleSha256 is retained as the database/API field name for schema compatibility. The profile-matched
// importer stores the PE build fingerprint (SizeOfImage + TimeDateStamp) here, not a cryptographic file hash.
public sealed record LiveImportPayload(
    string GameVersion, string ModuleSha256, List<LiveClubRecord> Clubs, List<LivePlayerRecord> Players,
    int PlayerSlots, int UnnamedPlayers, int UnlinkedPlayers, DateTime? GameDate = null,
    ulong SaveLoadCounter = 0, int StaffSlots = 0, List<LiveStaffRecord>? Staff = null);
public sealed record LiveImportResult(
    int RunId, int PlayersSeen, int PlayersImported, int ClubsSeen, int ClubsImported,
    int UnnamedPlayers, int UnlinkedPlayers, int AttributeRowsImported = 0,
    int StaffSeen = 0, int StaffImported = 0);

/// <summary>Read-only FM27 extractor using exact per-storefront module fingerprints. Field-level evidence is retained as provisional until UI-witnessed.</summary>
public static class LiveDataImporter
{
    private const uint QueryInformation = 0x0400, VmRead = 0x0010;
    private static readonly string[] PositionKeys = { "GK", "SW", "DL", "DC", "DR", "DM", "ML", "MC", "MR", "AML", "AMC", "AMR", "ST", "WBL", "WBR" };
    private static readonly string[] PersonalityKeys = { "adaptability", "ambition", "loyalty", "pressure", "professionalism", "sportsmanship", "temperament", "controversy" };
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ReadProcessMemory(nint process, nint address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);

    public static LiveImportPayload Extract(
        LiveCareerProbe expectedSession,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null,
        TimeSpan? maximumDuration = null)
    {
        ArgumentNullException.ThrowIfNull(expectedSession);
        cancellationToken.ThrowIfCancellationRequested();
        if (!expectedSession.IsCareerLoaded || expectedSession.ProcessId <= 0 || expectedSession.SaveLoadCounter == 0)
            throw new InvalidOperationException("FM27 has not finished loading a supported career. No live data was imported.");

        // Hard time bound plus a user cancellation token. Cancellation is checked at
        // entity boundaries so a large save cannot keep reading indefinitely.
        using var scanDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        scanDeadline.CancelAfter(maximumDuration ?? TimeSpan.FromMinutes(2));
        var scanToken = scanDeadline.Token;
        progress?.Report("Checking FM27 build and save generation…");
        // Attach only to the exact process and career generation previously observed by the read-only monitor.
        using var process = Process.GetProcessById(expectedSession.ProcessId);
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
                if (!string.Equals(profile.Id, expectedSession.ProfileId, StringComparison.Ordinal)
                    || profile.GameUiRva == 0
                    || ReadSaveLoadCounter(handle, moduleBase, profile) != expectedSession.SaveLoadCounter)
                    throw new InvalidOperationException("Football Manager changed build or loaded another career before Atlas connected. No data was imported.");
                var saveLoadCounter = expectedSession.SaveLoadCounter;
                var gameDate = profile.CurrentDateRva == 0 ? null : TryReadGameDate(handle, moduleBase, imageSize, profile.CurrentDateRva);

                var playerBounds = ReadBounds(handle, moduleBase, imageSize, profile.PlayersArrayRva, profile.PlayersArrayRva + 8, 20_000, 200_000, "player");
                var staffBounds = ReadBounds(handle, moduleBase, imageSize, profile.StaffArrayRva, profile.StaffArrayRva + 8, 1_000, 100_000, "staff");
                var clubBounds = ReadBounds(handle, moduleBase, imageSize, profile.ClubsArrayRva, profile.ClubsArrayRva + 8, 500, 100_000, "club");
                var clubs = new List<LiveClubRecord>();
                var clubByAddress = new Dictionary<ulong, LiveClubRecord>();
                var seenClubUids = new HashSet<uint>();
                progress?.Report($"Reading club identities · 0/{clubBounds.Count:N0}");
                for (var i = 0; i < clubBounds.Count; i++)
                {
                    if ((i & 255) == 0)
                    {
                        scanToken.ThrowIfCancellationRequested();
                        progress?.Report($"Reading club identities · {i:N0}/{clubBounds.Count:N0}");
                    }
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
                    catch (OperationCanceledException) { throw; }
                    catch { /* Keep scanning; one invalid entity must not abort a full import. */ }
                }
                scanToken.ThrowIfCancellationRequested();
                progress?.Report($"Club identities read · {clubs.Count:N0}");
                if (clubs.Count < 500) throw new InvalidOperationException($"Safety check failed: only {clubs.Count:N0} club names were decoded from {clubBounds.Count:N0} slots. No import was committed.");

                // Staff, including the controlled human manager, is read from FM's staff pointer array.
                // The 0x340 identity-block shift follows the independently documented FM27 staff layout.
                progress?.Report($"Reading staff and human-manager identities · 0/{staffBounds.Count:N0}");
                var staff = ReadStaff(handle, staffBounds, clubByAddress, gameDate, scanToken,
                    (done, total) => progress?.Report($"Reading staff and human-manager identities · {done:N0}/{total:N0}"));
                scanToken.ThrowIfCancellationRequested();
                if (staff.Count < 500) throw new InvalidOperationException($"Safety check failed: only {staff.Count:N0} staff identities were decoded from {staffBounds.Count:N0} slots. No live import was committed.");

                var players = new List<LivePlayerRecord>(playerBounds.Count);
                var seenPlayerUids = new HashSet<uint>();
                // Cache the player identity-block shift by class/vtable, as FMLibreEditor does:
                // plain players use 0; player-coach objects use the documented +0xF8 shift.
                var playerIdentityShifts = new Dictionary<ulong, int>();
                var unnamed = 0; var unlinked = 0;
                progress?.Report($"Reading player identities, CA/PA and attributes · 0/{playerBounds.Count:N0}");
                for (var i = 0; i < playerBounds.Count; i++)
                {
                    if ((i & 255) == 0)
                    {
                        scanToken.ThrowIfCancellationRequested();
                        progress?.Report($"Reading player identities, CA/PA and attributes · {i:N0}/{playerBounds.Count:N0}");
                    }
                    try
                    {
                        var address = U64(handle, (nint)(playerBounds.Data + (ulong)i * 8));
                        if (!Pointer(address)) { unnamed++; continue; }
                        var vtable = U64(handle, (nint)address);
                        if (!playerIdentityShifts.TryGetValue(vtable, out var playerShift))
                        {
                            playerShift = DetectPlayerIdentityShift(handle, address);
                            playerIdentityShifts[vtable] = playerShift;
                        }
                        var common = ReadNameSub(handle, address + (ulong)(0x2E8 + playerShift));
                        var first = ReadNameSub(handle, address + (ulong)(0x2D8 + playerShift));
                        var last = ReadNameSub(handle, address + (ulong)(0x2E0 + playerShift));
                        var fullName = TryReadFmName(handle, address + (ulong)(0x2C8 + playerShift));
                        var name = !string.IsNullOrWhiteSpace(common) ? common : Join(first, last);
                        if (string.IsNullOrWhiteSpace(name)) name = fullName;
                        if (string.IsNullOrWhiteSpace(name)) { unnamed++; continue; }
                        var uid = BitConverter.ToUInt32(Read(handle, (nint)(address + (ulong)(0x294 + playerShift)), 4), 0);
                        if (uid == 0 || !seenPlayerUids.Add(uid)) { unnamed++; continue; }
                        var dob = TryReadPlayerDob(handle, address, playerShift);
                        string? nationName = null;
                        uint? nationUid = null;
                        try
                        {
                            var nationAddress = U64(handle, (nint)(address + (ulong)(0x2F0 + playerShift)));
                            if (Pointer(nationAddress))
                            {
                                nationUid = BitConverter.ToUInt32(Read(handle, (nint)(nationAddress + 0x0C), 4), 0);
                                nationName = ReadLengthString(handle, U64(handle, (nint)(nationAddress + 0x18)), 96);
                                if (nationUid == 0 || nationUid == uint.MaxValue) nationUid = null;
                            }
                        }
                        catch { /* Preserve a valid player identity when nationality is unavailable. */ }
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
                        var inline = Read(handle, (nint)address, 0x450);
                        var caRaw = BitConverter.ToUInt16(inline, 0x264);
                        var paRaw = BitConverter.ToInt16(inline, 0x266);
                        int? ca = caRaw is >= 1 and <= 200 ? caRaw : null;
                        // FM stores negative PA values for random-potential ranges as well as fixed 1–200 values.
                        int? pa = paRaw is >= -10 and <= 200 ? paRaw : null;
                        var attributes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        foreach (var field in PlayerAttributeSchema.Fields)
                        {
                            if (field.Offset < 0 || field.Offset >= inline.Length) continue;
                            var raw = inline[field.Offset];
                            var shown = (raw + 2) / 5;
                            if (raw <= 100 && shown is >= 1 and <= 20) attributes[field.Key] = shown;
                        }
                        // Eight personality ratings sit in the person block. Resolve the same
                        // per-class +0xF8 shift used for names, DOB and nation; keep only 1–20 values.
                        var personality = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        for (var keyIndex = 0; keyIndex < PersonalityKeys.Length; keyIndex++)
                        {
                            var value = inline[0x2F8 + playerShift + keyIndex];
                            if (value is >= 1 and <= 20) personality[PersonalityKeys[keyIndex]] = value;
                        }
                        var positions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        for (var pos = 0; pos < PositionKeys.Length; pos++)
                        {
                            var shown = inline[0x150 + pos];
                            if (shown is >= 1 and <= 20) positions[PositionKeys[pos]] = shown;
                        }

                        // Additional scalar fields use the FM27 player layout documented by
                        // FMLibreEditor. They remain provisional until compared with FM's UI.
                        var heightRaw = BitConverter.ToUInt16(inline, 0x22E);
                        int? heightCm = heightRaw is >= 140 and <= 220 ? heightRaw : null;
                        var conditionRaw = BitConverter.ToUInt16(inline, 0x25C);
                        int? condition = conditionRaw <= 10_000 ? conditionRaw : null;
                        var sharpnessRaw = BitConverter.ToUInt16(inline, 0x258);
                        int? sharpness = sharpnessRaw <= 10_000 ? sharpnessRaw : null;
                        var jadednessRaw = BitConverter.ToInt16(inline, 0x25A);
                        int? jadedness = jadednessRaw is >= -500 and <= 10_000 ? jadednessRaw : null;
                        var moraleRaw = inline[0x26C];
                        int? morale = moraleRaw is >= 1 and <= 20 ? moraleRaw : null;
                        var homeRepRaw = BitConverter.ToUInt16(inline, 0x25E);
                        int? homeRep = homeRepRaw <= 10_000 ? homeRepRaw : null;
                        var currentRepRaw = BitConverter.ToUInt16(inline, 0x260);
                        int? currentRep = currentRepRaw <= 10_000 ? currentRepRaw : null;
                        var worldRepRaw = BitConverter.ToUInt16(inline, 0x262);
                        int? worldRep = worldRepRaw <= 10_000 ? worldRepRaw : null;
                        var transferValueRaw = BitConverter.ToInt32(inline, 0x234);
                        int? transferValue = transferValueRaw is >= 0 and <= 2_000_000_000 ? transferValueRaw : null;
                        var askingPriceRaw = BitConverter.ToInt32(inline, 0x238);
                        int? askingPrice = askingPriceRaw is >= -1 and <= 2_000_000_000 ? askingPriceRaw : null;
                        var capsRaw = BitConverter.ToUInt16(inline, 0x3BC + playerShift);
                        int? internationalCaps = capsRaw <= 1_000 ? capsRaw : null;
                        var goalsRaw = BitConverter.ToUInt16(inline, 0x3BE + playerShift);
                        int? internationalGoals = goalsRaw <= 1_000 ? goalsRaw : null;
                        var u21Caps = inline[0x3C0 + playerShift];
                        var u21Goals = inline[0x3C1 + playerShift];
                        int? leftFoot = inline[0x177] is >= 1 and <= 20 ? inline[0x177] : null;
                        int? rightFoot = inline[0x178] is >= 1 and <= 20 ? inline[0x178] : null;

                        var traitFlags = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        for (var trait = 0; trait < 64; trait++)
                            traitFlags["trait." + trait] = (inline[0x348 + playerShift + trait / 8] & (1 << (trait % 8))) != 0 ? 1 : 0;

                        var contractFacts = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        try
                        {
                            var contractPointer = BitConverter.ToUInt64(inline, 0x330 + playerShift);
                            if (Pointer(contractPointer))
                            {
                                var contractOwner = U64(handle, (nint)(contractPointer + 0x08));
                                if (contractOwner == address + (ulong)(0x288 + playerShift))
                                {
                                    var contractBytes = Read(handle, (nint)contractPointer, 0xC8);
                                    var wage = BitConverter.ToInt32(contractBytes, 0x20);
                                    var loyalty = BitConverter.ToInt32(contractBytes, 0x38);
                                    if (wage is >= 0 and <= 10_000_000) contractFacts["contract.wage"] = wage;
                                    if (loyalty is >= 0 and <= 10_000_000) contractFacts["contract.loyalty_bonus"] = loyalty;
                                    var happiness = unchecked((sbyte)contractBytes[0x5A]);
                                    var playingTime = unchecked((sbyte)contractBytes[0x5C]);
                                    if (happiness is >= -100 and <= 100) contractFacts["contract.happiness"] = happiness;
                                    if (playingTime is >= -100 and <= 100) contractFacts["contract.playing_time_happiness"] = playingTime;
                                    var squadNumber = contractBytes[0x5D];
                                    if (squadNumber is >= 1 and <= 99 or 255) contractFacts["contract.squad_number"] = squadNumber;
                                    var squadStatus = contractBytes[0x54];
                                    if (squadStatus is >= 1 and <= 40) contractFacts["contract.squad_status"] = squadStatus;
                                    contractFacts["contract.transfer_listed"] = (contractBytes[0x57] & 0x01) != 0 ? 1 : 0;
                                    contractFacts["contract.loan_listed"] = (contractBytes[0x57] & 0x02) != 0 ? 1 : 0;
                                    foreach (var (key, offset) in new[] { ("contract.signed", 0x44), ("contract.expires", 0x48), ("contract.joined", 0x4C) })
                                    {
                                        var date = TryReadDate(contractBytes, offset);
                                        if (date.HasValue) contractFacts[key] = date.Value.Year * 10000 + date.Value.Month * 100 + date.Value.Day;
                                        else if (contractBytes.Skip(offset).Take(4).All(x => x == 0)) contractFacts[key] = 0;
                                    }
                                }
                            }
                        }
                        catch { /* Contract details are optional; a malformed contract never invalidates the player identity. */ }

                        players.Add(new LivePlayerRecord(uid, name, teamAddress, clubUid, i, address, true, relationshipVerified,
                            dob, "Unknown", "No independently verified FM27 generated-player flag is currently mapped.",
                            ca, pa, attributes, personality, positions, nationName, nationUid,
                            heightCm, condition, sharpness, jadedness, morale, homeRep, currentRep, worldRep,
                            askingPrice, internationalCaps, internationalGoals, u21Caps, u21Goals, leftFoot, rightFoot,
                            transferValue, contractFacts, traitFlags));
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { unnamed++; }
                }
                scanToken.ThrowIfCancellationRequested();
                progress?.Report($"Player scan complete · {players.Count:N0} named players");
                if (players.Count < 30_000) throw new InvalidOperationException($"Safety check failed: only {players.Count:N0} named player records were extracted from {playerBounds.Count:N0} slots. Expected a full FM27 player array; no import was performed.");
                var attributeCoverage = players.Count(p => p.Attributes?.Count >= 35);
                if (attributeCoverage < Math.Min(10_000, players.Count / 3))
                    throw new InvalidOperationException($"The player attribute cross-check did not pass: only {attributeCoverage:N0} of {players.Count:N0} players had 35+ range-valid attributes. No live attribute import was committed.");

                string version = profile.DisplayName;
                try { var executablePath = process.MainModule?.FileName; if (!string.IsNullOrWhiteSpace(executablePath)) version += " · exe " + (FileVersionInfo.GetVersionInfo(executablePath).FileVersion ?? "unknown"); } catch { }
                var humanManagers = staff.Where(x => x.IsHumanManager).ToArray();
                if (humanManagers.Length > 1)
                    AtlasLog.Warning($"The live staff array contains {humanManagers.Length} human-manager records; Atlas will not silently choose between them.");
                scanToken.ThrowIfCancellationRequested();
                if (ReadSaveLoadCounter(handle, moduleBase, profile) != saveLoadCounter)
                    throw new InvalidOperationException("Football Manager loaded or unloaded a career while Atlas was reading it. The partial scan was discarded; Atlas will reconnect to the current career.");
                progress?.Report("FM27 career scan validated · committing the read-only snapshot…");
                AtlasLog.Info($"Matched FM27 live read profile: {profile.Id}; fingerprint={profile.Fingerprint}; gameDate={gameDate?.ToString("yyyy-MM-dd") ?? "unavailable"}; players={players.Count}; staff={staff.Count}; humanManagers={humanManagers.Length}; attributeCoverage={attributeCoverage}; clubs={clubs.Count}; saveLoadCounter={saveLoadCounter}.");
                return new LiveImportPayload(version, profile.Fingerprint, clubs, players, playerBounds.Count, unnamed, unlinked, gameDate,
                    saveLoadCounter, staffBounds.Count, staff);
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

    private static int ScoreStaffName(nint handle, ulong address, int shift)
    {
        var score = 0;
        try { if (IsNameLike(ReadLengthString(handle, U64(handle, (nint)(address + (ulong)(0x140 + shift))), 128))) score++; } catch { }
        try { if (IsNameLike(ReadNameSub(handle, address + (ulong)(0x150 + shift)))) score++; } catch { }
        try { if (IsNameLike(ReadNameSub(handle, address + (ulong)(0x158 + shift)))) score++; } catch { }
        try { if (IsNameLike(ReadNameSub(handle, address + (ulong)(0x160 + shift)))) score++; } catch { }
        return score;
    }

    private static bool IsNameLike(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        return trimmed.Length >= 2
            && trimmed.Length <= 96
            && trimmed.Any(char.IsLetter)
            && trimmed.All(ch => char.IsLetter(ch) || char.IsWhiteSpace(ch) || ch is '-' or '\'' or '.');
    }

    private static List<LiveStaffRecord> ReadStaff(
        nint handle,
        (ulong Data, int Count) bounds,
        IReadOnlyDictionary<ulong, LiveClubRecord> clubs,
        DateTime? gameDate,
        CancellationToken cancellationToken,
        Action<int, int>? reportProgress = null)
    {
        var staff = new List<LiveStaffRecord>(bounds.Count);
        var seen = new HashSet<uint>();
        // The C++ staff class determines the location of its person block. Like the
        // reference reader, cache the winning shift by vtable/class instead of rescoring
        // every one of tens of thousands of objects.
        var shiftByClass = new Dictionary<ulong, int>();
        for (var i = 0; i < bounds.Count; i++)
        {
            if ((i & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                reportProgress?.Invoke(i, bounds.Count);
            }
            try
            {
                var address = U64(handle, (nint)(bounds.Data + (ulong)i * 8));
                if (!Pointer(address)) continue;
                // FM27's human-manager object keeps the identity fields 0x340 bytes after
                // the normal staff identity offsets. Other staff use shift 0.
                var buffer = Read(handle, (nint)address, 0x540);
                LiveStaffRecord? decoded = null;
                var classId = BitConverter.ToUInt64(buffer, 0);
                if (!shiftByClass.TryGetValue(classId, out var shift))
                {
                    var selectedShift = new[] { 0, 0x340 }
                        .Select(candidate => (Shift: candidate, Score: ScoreStaffName(handle, address, candidate)))
                        .OrderByDescending(x => x.Score)
                        .ThenBy(x => x.Shift)
                        .First();
                    if (selectedShift.Score < 2) continue;
                    shift = selectedShift.Shift;
                    shiftByClass[classId] = shift;
                }
                {
                    var uidOffset = 0x10C + shift;
                    var uid = BitConverter.ToUInt32(buffer, uidOffset);
                    if (uid == 0 || uid == uint.MaxValue) continue;
                    var common = ReadNameSub(handle, address + (ulong)(0x160 + shift));
                    var first = ReadNameSub(handle, address + (ulong)(0x150 + shift));
                    var last = ReadNameSub(handle, address + (ulong)(0x158 + shift));
                    var full = ReadLengthString(handle, U64(handle, (nint)(address + (ulong)(0x140 + shift))), 128);
                    var name = !string.IsNullOrWhiteSpace(common) ? common : Join(first, last);
                    if (string.IsNullOrWhiteSpace(name)) name = full;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var dob = TryReadDate(buffer, 0x188 + shift);
                    if (shift != 0 && !dob.HasValue) continue;
                    // FMLibreEditor/FM27's staff layout places CA/PA and coaching attributes
                    // outside the shifted human-manager identity block. Keep these offsets unshifted.
                    var caRaw = BitConverter.ToUInt16(buffer, 0xDA);
                    var paRaw = BitConverter.ToInt16(buffer, 0xDC);
                    int? ca = caRaw is >= 1 and <= 200 ? caRaw : null;
                    int? pa = paRaw is >= -10 and <= 200 ? paRaw : null;
                    var staffAttributes = StaffAttributeDecoder.Decode(buffer);
                    // Personality belongs to the staff identity/person block, so the human
                    // manager uses the documented +0x340 shift; coaching ratings above do not.
                    var staffPersonality = StaffAttributeDecoder.DecodePersonality(buffer, shift);

                    string? nationName = null;
                    uint? nationUid = null;
                    var nationAddress = U64(handle, (nint)(address + (ulong)(0x168 + shift)));
                    if (Pointer(nationAddress))
                    {
                        try
                        {
                            nationUid = BitConverter.ToUInt32(Read(handle, (nint)(nationAddress + 0x0C), 4), 0);
                            nationName = ReadLengthString(handle, U64(handle, (nint)(nationAddress + 0x18)), 96);
                        }
                        catch { /* A missing nation label does not invalidate a staff identity. */ }
                    }

                    string? clubName = null;
                    uint? clubUid = null;
                    string? jobTitle = null;
                    int? jobCode = null;
                    int? wagePerWeek = null;
                    DateTime? contractStart = null, contractExpiry = null, joinedClub = null;
                    var contractAddress = U64(handle, (nint)(address + (ulong)(0x1A8 + shift)));
                    if (Pointer(contractAddress))
                    {
                        try
                        {
                            // FM27 FULL_CONTRACT layout: weekly wage in GBP, signed/expiry/joined dates,
                            // team relationship and current contract job code.
                            var contract = Read(handle, (nint)contractAddress, 0xC8);
                            jobCode = contract[0x26];
                            var wage = BitConverter.ToInt32(contract, 0x20);
                            if (wage is >= 0 and <= 10_000_000) wagePerWeek = wage;
                            contractStart = TryReadDate(contract, 0x44);
                            contractExpiry = TryReadDate(contract, 0x48);
                            joinedClub = TryReadDate(contract, 0x4C);
                            var teamAddress = BitConverter.ToUInt64(contract, 0x10);
                            if (Pointer(teamAddress))
                            {
                                var clubAddress = U64(handle, (nint)(teamAddress + 0x30));
                                if (clubs.TryGetValue(clubAddress, out var club))
                                {
                                    clubName = club.Name;
                                    clubUid = club.Uid;
                                }
                                else if (Pointer(clubAddress))
                                {
                                    clubUid = BitConverter.ToUInt32(Read(handle, (nint)(clubAddress + 0x0C), 4), 0);
                                    clubName = ReadLengthString(handle, U64(handle, (nint)(clubAddress + 0xC0)), 96);
                                }
                            }
                        }
                        catch { /* Contract facts are optional; keep a valid staff identity if they are unreadable. */ }
                    }
                    jobTitle = JobName(jobCode);
                    var isHuman = shift != 0;
                    var age = gameDate.HasValue && dob.HasValue ? CalculateAge(dob.Value, gameDate.Value) : (int?)null;
                    var jobPreferences = ReadStaffJobPreferences(handle, address, shift);
                    decoded = new LiveStaffRecord(uid, name, i, address, isHuman, dob, age, nationName, nationUid,
                        clubName, clubUid, jobTitle, jobCode, ca, pa, jobPreferences, staffAttributes,
                        staffPersonality, wagePerWeek, contractStart, contractExpiry, joinedClub);
                }
                if (decoded is not null && seen.Add(decoded.Uid))
                    staff.Add(decoded);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* One corrupt or unloading pointer must not abort the remainder of the read-only scan. */ }
        }
        cancellationToken.ThrowIfCancellationRequested();
        reportProgress?.Invoke(bounds.Count, bounds.Count);
        return staff;
    }

    private static StaffJobPreferenceResult? ReadStaffJobPreferences(nint handle, ulong staffAddress, int identityShift)
    {
        // Independent FM27 observations place a pointer to 15 one-byte (1..20) preference
        // scores at person+0xD0. The same class-scoped identity shift used for name decoding
        // is applied here; an unreadable pointer or out-of-range byte leaves this field absent.
        try
        {
            var pointerAddress = checked(staffAddress + (ulong)(0xD0 + identityShift));
            var ratingsAddress = U64(handle, (nint)pointerAddress);
            if (!Pointer(ratingsAddress)) return null;
            var bytes = Read(handle, (nint)ratingsAddress, StaffJobPreferenceDecoder.JobGroupKeys.Count);
            return StaffJobPreferenceDecoder.Decode(bytes);
        }
        catch
        {
            // Preferred-job data is optional. It must never invalidate an otherwise sound staff identity.
            return null;
        }
    }

    private static DateTime? TryReadDate(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length) return null;
        var day = (ushort)(BitConverter.ToUInt16(bytes, offset) & 0x01FF);
        var year = BitConverter.ToUInt16(bytes, offset + 2);
        if (year < 1901 || year > 2100) return null;
        var maximum = DateTime.IsLeapYear(year) ? 366 : 365;
        return day >= 1 && day <= maximum ? new DateTime(year, 1, 1).AddDays(day - 1) : null;
    }

    private static int CalculateAge(DateTime birth, DateTime gameDate)
    {
        var age = gameDate.Year - birth.Year;
        if (birth.Date > gameDate.Date.AddYears(-age)) age--;
        return Math.Max(0, age);
    }

    private static string? JobName(int? code)
    {
        if (!code.HasValue) return null;
        var baseCode = code.Value > 1 && code.Value % 2 == 1 ? code.Value - 1 : code.Value;
        return baseCode switch
        {
            1 => "Player",
            2 => "Coach",
            4 => "Chairperson",
            6 => "Director",
            10 => "Director of Football",
            12 => "Physio",
            14 => "Scout",
            16 => "Manager",
            20 => "Assistant Coach",
            22 => "Set Piece Coach",
            26 => "Fitness Coach",
            34 => "Goalkeeping Coach",
            38 => "Chief Doctor",
            40 => "Head of Sports Science",
            44 => "Chief Scout",
            46 => "Doctor",
            48 => "Sports Scientist",
            50 => "Head Physio",
            58 => "Recruitment Analyst",
            60 => "Performance Analyst",
            62 => "Head Performance Analyst",
            64 => "Head of Youth Development",
            66 => "Owner",
            70 => "President",
            86 => "Loan Manager",
            88 => "Technical Director",
            _ => null
        };
    }

    private static ulong ReadSaveLoadCounter(nint handle, ulong moduleBase, Fm27BuildProfile profile)
    {
        if (profile.GameUiRva == 0 || profile.GameUiRva >= profile.SizeOfImage)
            throw new InvalidOperationException("This FM27 profile has no verified save-load counter.");
        var gameUi = U64(handle, (nint)(moduleBase + profile.GameUiRva));
        if (!Pointer(gameUi)) throw new InvalidOperationException("The FM27 game-session object is not readable.");
        return U64(handle, (nint)(gameUi + Fm27BuildProfile.SaveLoadCounterOffset));
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

    private static int DetectPlayerIdentityShift(nint handle, ulong playerAddress)
    {
        static bool NameLike(string? value)
            => !string.IsNullOrWhiteSpace(value)
               && value.Count(char.IsLetter) >= 2
               && value.Length <= 96
               && !value.Any(char.IsControl);

        int Score(int shift)
        {
            var score = 0;
            foreach (var offset in new[] { 0x2D8, 0x2E0, 0x2E8 })
            {
                var value = ReadNameSub(handle, playerAddress + (ulong)(offset + shift));
                if (NameLike(value)) score++;
            }
            var full = TryReadFmName(handle, playerAddress + (ulong)(0x2C8 + shift));
            if (NameLike(full)) score++;
            return score;
        }

        // The plain identity block wins ties. A player-coach is shifted only when at least
        // two name witnesses decode better from the documented +0xF8 block.
        var plain = Score(0);
        var shifted = Score(0xF8);
        return shifted >= 2 && shifted > plain ? 0xF8 : 0;
    }

    private static string? TryReadFmName(nint handle, ulong pointerAddress)
    {
        try
        {
            var value = U64(handle, (nint)pointerAddress);
            return ReadLengthString(handle, value, 128);
        }
        catch { return null; }
    }

    private static DateTime? TryReadPlayerDob(nint handle, ulong playerAddress, int playerShift)
    {
        // The person-block location was resolved from the object's decoded name fields,
        // not from a stale absolute vtable RVA. FM27 player-coaches shift by +0xF8.
        try
        {
            var raw = Read(handle, (nint)(playerAddress + (ulong)(0x310 + playerShift)), 4);
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
