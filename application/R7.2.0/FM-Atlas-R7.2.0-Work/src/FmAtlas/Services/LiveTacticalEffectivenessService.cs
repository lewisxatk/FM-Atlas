using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FmAtlas.Services;

public static partial class LiveEntityMemoryEditService
{
    public sealed record TacticalAreaValue(string Key, string Label, int MatchExposure, string TrainingContribution);
    public sealed record TacticalSnapshot(
        uint ClubUid, string ClubName, string ProfileId, string TeamName,
        int PlayersInTeam, int PlayersWithFamiliarity,
        IReadOnlyList<TacticalAreaValue> Areas,
        string TrainingNote);
    public sealed record TacticalTargetValue(string AreaKey, int Value);
    public sealed record TacticalListChange(
        uint PlayerUid, ulong PlayerAddress, int PlayerShift, ulong RecordAddress,
        ulong HeaderAddress, byte[] OriginalHeader, byte[] OriginalBytes,
        byte[] TargetBytes, int ElementSize, bool RequiresGrowth);
    public sealed record TacticalBatchPlan(
        int ProcessId, uint ClubUid, string ClubName, ulong ClubAddress, ulong SeniorTeamAddress,
        uint TeamUid, ulong TacticAddress, byte[] TacticInstructions,
        string ProfileId, string ProfileFingerprint, ulong SaveLoadCounter, DateTime CreatedUtc,
        int PlayersVisited, IReadOnlyList<TacticalListChange> Changes);
    public sealed record AppliedTacticalListChange(TacticalListChange Change, byte[] TargetHeader, ulong AllocatedBuffer);
    public sealed record TacticalBatchReceipt(TacticalBatchPlan Plan, DateTime AppliedUtc, IReadOnlyList<AppliedTacticalListChange> Changes);

    private sealed record TacticalListState(ulong HeaderAddress, byte[] Header, ulong Begin, ulong End, ulong Capacity, byte[] Bytes);
    private sealed record TacticalPlayer(uint Uid, ulong Address, int Shift, ulong RecordAddress, uint PersonId,
        List<TacticalListState> Areas, TacticalListState? Shapes, TacticalListState? History);
    private sealed record TacticalContext(
        ResolvedEntity Club, ulong SeniorTeam, uint TeamUid, ulong TacticAddress,
        byte[] Instructions, int[] Options, List<TacticalPlayer> Players);

    private static readonly string[] TacticalKeys =
    {
        "line_of_engagement", "pressing_intensity", "passing_style",
        "chance_creation", "build_up", "pressing_trap"
    };
    private static readonly string[] TacticalLabels =
    {
        "Line of Engagement", "Pressing Intensity", "Passing Style",
        "Chance Creation", "Build Up", "Pressing Trap"
    };

    /// <summary>Reads the live team's Match Exposure average and the game-managed Training Contribution status.</summary>
    public static TacticalSnapshot ReadTacticalSnapshot(int processId, uint clubUid, string clubName)
    {
        using var process = Process.GetProcessById(processId);
        var module = FindModule(process);
        using var handle = new Handle(OpenProcess(QueryInformation | VmRead, false, processId));
        if (handle.Value == 0) throw Win32("read tactical effectiveness");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("The running FM27 build is not in Atlas's exact profile registry.");
        var counter = ReadSaveCounter(handle.Value, moduleBase, profile);
        var context = ReadTacticalContext(handle.Value, moduleBase, profile, clubUid, clubName);
        var today = ReadCurrentDayOfYear(handle.Value, moduleBase, profile);
        var playerTraining = context.Players
            .Where(x => x.History is not null)
            .Select(x => HistoryAreaValues(x.History!.Bytes, context.Options, today))
            .ToList();
        string TrainingAverage(int area)
        {
            if (playerTraining.Count == 0) return "No training history";
            var values = playerTraining.Select(x => x[area]).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
            return values.Length == 0 ? "No matching training history"
                : Math.Round(values.Average(), MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture) + "%";
        }
        var rows = new List<TacticalAreaValue>();
        for (var area = 0; area < 6; area++)
        {
            var scores = context.Players
                .Select(x => RecordAreaValue(x.Areas[area].Bytes, context.Options[area]))
                .Where(x => x.HasValue).Select(x => x!.Value).ToArray();
            var average = scores.Length == 0 ? 0 : (int)Math.Round(scores.Average(), MidpointRounding.AwayFromZero);
            rows.Add(new TacticalAreaValue(TacticalKeys[area], TacticalLabels[area], average,
                profile.TrainingManagerRva == 0 ? "Unavailable for this build" : TrainingAverage(area)));
        }
        var shapeScores = context.Players
            .Where(x => x.Shapes is not null)
            .Select(x => ShapeAreaValue(x.Shapes!.Bytes))
            .Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        var shapeAverage = shapeScores.Length == 0 ? 0 : (int)Math.Round(shapeScores.Average(), MidpointRounding.AwayFromZero);
        rows.Add(new TacticalAreaValue("shapes", "Position / Role Shapes", shapeAverage,
            profile.TrainingManagerRva == 0 ? "Unavailable for this build" : TrainingAverage(6)));

        if (ReadSaveCounter(handle.Value, moduleBase, profile) != counter)
            throw new InvalidOperationException("FM loaded or unloaded a career while tactical effectiveness was being read. Refresh and try again.");
        return new TacticalSnapshot(clubUid, clubName, profile.Id, "Senior Team",
            context.Players.Count, context.Players.Count, rows,
            "Training Contribution is calculated from FM's saved player training histories. The Max Training Contribution action rewrites those histories for the currently selected tactic; Atlas does not write a synthetic score field.");
    }

    /// <summary>Stages team-wide Match Exposure changes. No writes are made during staging.</summary>
    public static TacticalBatchPlan StageTacticalExposureBatch(
        int processId, uint clubUid, string clubName, IReadOnlyList<TacticalTargetValue> targets)
    {
        if (targets.Count == 0 || targets.Count > 8)
            throw new ArgumentOutOfRangeException(nameof(targets), "Choose one to eight tactical areas.");
        var maxTraining = targets.Any(x => x.AreaKey.Equals("training_contribution", StringComparison.OrdinalIgnoreCase));
        if (targets.Any(x => x.AreaKey.Equals("training_contribution", StringComparison.OrdinalIgnoreCase) && x.Value != 100))
            throw new ArgumentOutOfRangeException(nameof(targets), "Max Training Contribution is a specific training-history operation and must be 100.");
        var duplicate = targets.GroupBy(x => x.AreaKey, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) throw new ArgumentException("Tactical area " + duplicate.Key + " was selected more than once.");
        foreach (var target in targets)
        {
            if (target.Value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(targets), target.AreaKey + " must be 0–100.");
            if (!TacticalKeys.Contains(target.AreaKey, StringComparer.OrdinalIgnoreCase)
                && !target.AreaKey.Equals("shapes", StringComparison.OrdinalIgnoreCase)
                && !target.AreaKey.Equals("training_contribution", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unknown Tactical Effectiveness area " + target.AreaKey + ".");
        }

        using var process = Process.GetProcessById(processId);
        var module = FindModule(process);
        using var handle = new Handle(OpenProcess(QueryInformation | VmRead, false, processId));
        if (handle.Value == 0) throw Win32("read tactical effectiveness");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("The running FM27 build is not in Atlas's exact profile registry.");
        if (profile.TacticsManagerRva == 0) throw new InvalidOperationException("This exact FM27 build has no independently mapped Tactics Manager. Tactical edits are blocked.");
        var counter = ReadSaveCounter(handle.Value, moduleBase, profile);
        var context = ReadTacticalContext(handle.Value, moduleBase, profile, clubUid, clubName);
        var today = ReadCurrentDayOfYear(handle.Value, moduleBase, profile);
        var planned = new List<TacticalListChange>();

        foreach (var player in context.Players)
        {
            for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                var target = targets[targetIndex];
                if (target.AreaKey.Equals("shapes", StringComparison.OrdinalIgnoreCase))
                {
                    if (player.Shapes is null) continue;
                    var changed = SetShapeValues(player.Shapes.Bytes, target.Value);
                    if (!changed.SequenceEqual(player.Shapes.Bytes))
                        planned.Add(CreateTacticalListChange(player, player.Shapes, changed, 16));
                }
                else
                {
                    var area = Array.FindIndex(TacticalKeys, x => x.Equals(target.AreaKey, StringComparison.OrdinalIgnoreCase));
                    if (area < 0) continue;
                    var changed = SetAreaOption(player.Areas[area].Bytes, context.Options[area], target.Value, player.Areas[area].Bytes.Length == 0 ? (byte)0 : player.Areas[area].Bytes[^1]);
                    if (!changed.SequenceEqual(player.Areas[area].Bytes))
                        planned.Add(CreateTacticalListChange(player, player.Areas[area], changed, 4));
                }
            }
            if (maxTraining && player.History is not null)
            {
                var generated = BuildTodayTrainingHistory(player.History.Bytes, context.Options, today);
                if (!generated.SequenceEqual(player.History.Bytes))
                    planned.Add(CreateTacticalListChange(player, player.History, generated, 12));
            }
        }
        if (planned.Count == 0)
            throw new InvalidOperationException(maxTraining
                ? "No writable player training-history records were found for this senior team. FM creates histories only for players it is training."
                : "No players had a writable tactical familiarity record for the selected team. Nothing was staged.");
        var requiresGrowth = planned.FirstOrDefault(x => x.RequiresGrowth);
        if (requiresGrowth is not null)
            throw new InvalidOperationException(
                "The familiarity list for player UID " + requiresGrowth.PlayerUid
                + " needs a new or expanded buffer. Atlas currently supports in-place updates and appends only where the existing FM allocation has capacity. "
                + "This batch was blocked rather than attaching an incompatible buffer to FM memory.");
        if (ReadSaveCounter(handle.Value, moduleBase, profile) != counter)
            throw new InvalidOperationException("FM switched career while tactical changes were being staged. No edits were prepared.");
        return new TacticalBatchPlan(processId, clubUid, clubName, context.Club.Address, context.SeniorTeam,
            context.TeamUid, context.TacticAddress, context.Instructions, profile.Id, profile.Fingerprint,
            counter, DateTime.UtcNow, context.Players.Count, planned);
    }

    /// <summary>Applies all staged tactical Match Exposure list changes with preflight, save-generation checks and read-back.</summary>
    public static TacticalBatchReceipt ApplyTacticalExposureBatch(TacticalBatchPlan plan)
    {
        if (DateTime.UtcNow - plan.CreatedUtc > TimeSpan.FromMinutes(2) || plan.CreatedUtc > DateTime.UtcNow.AddSeconds(10))
            throw new InvalidOperationException("The tactical preview expired. Read the team again and stage the changes again.");
        using var process = Process.GetProcessById(plan.ProcessId);
        var module = FindModule(process);
        using var handle = new Handle(OpenProcess(QueryInformation | VmRead | VmWrite | VmOperation, false, plan.ProcessId));
        if (handle.Value == 0) throw Win32("write tactical effectiveness");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("FM27 build profile mismatch. Tactical changes were not applied.");
        if (profile.Id != plan.ProfileId || profile.Fingerprint != plan.ProfileFingerprint
            || ReadSaveCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
            throw new InvalidOperationException("The executable or career session changed after tactical staging. Refresh the team and restage.");

        var context = ReadTacticalContext(handle.Value, moduleBase, profile, plan.ClubUid, plan.ClubName);
        if (context.Club.Address != plan.ClubAddress || context.SeniorTeam != plan.SeniorTeamAddress
            || context.TeamUid != plan.TeamUid || context.TacticAddress != plan.TacticAddress
            || !context.Instructions.SequenceEqual(plan.TacticInstructions))
            throw new InvalidOperationException("The club, senior team or active tactic changed after staging. No tactical writes were made.");
        var players = context.Players.ToDictionary(x => x.Uid);
        foreach (var change in plan.Changes)
        {
            if (change.RequiresGrowth)
                throw new InvalidOperationException("Tactical list expansion is not supported by this live writer. No values were changed.");
            if (!players.TryGetValue(change.PlayerUid, out var player)
                || player.Address != change.PlayerAddress || player.Shift != change.PlayerShift
                || player.RecordAddress != change.RecordAddress)
                throw new InvalidOperationException("A player or familiarity record changed after tactical staging. No values were written.");
            var header = Read(handle.Value, (nint)change.HeaderAddress, 24);
            if (!header.SequenceEqual(change.OriginalHeader))
                throw new InvalidOperationException("A tactical list changed after staging. Refresh the team and try again.");
            var begin = BitConverter.ToUInt64(header, 0);
            if (change.OriginalBytes.Length > 0)
            {
                if (!Pointer(begin) || !Read(handle.Value, (nint)begin, change.OriginalBytes.Length).SequenceEqual(change.OriginalBytes))
                    throw new InvalidOperationException("A player's tactical exposure list changed after staging. No values were written.");
            }
        }

        var applied = new List<AppliedTacticalListChange>();
        try
        {
            foreach (var change in plan.Changes)
            {
                if (ReadSaveCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
                    throw new InvalidOperationException("FM switched career during the tactical batch. Atlas stopped.");
                var newHeader = change.OriginalHeader.ToArray();
                ulong newBegin;
                ulong allocated = 0;
                if (change.RequiresGrowth)
                    throw new InvalidOperationException("Tactical list expansion is not supported by this live writer.");
                newBegin = BitConverter.ToUInt64(change.OriginalHeader, 0);
                if (change.TargetBytes.Length > 0)
                {
                    if (!Pointer(newBegin) || !Write(handle.Value, (nint)newBegin, change.TargetBytes)
                        || !Read(handle.Value, (nint)newBegin, change.TargetBytes.Length).SequenceEqual(change.TargetBytes))
                        throw new InvalidOperationException("Atlas could not update an in-place tactical list.");
                }
                BitConverter.GetBytes(newBegin + (ulong)change.TargetBytes.Length).CopyTo(newHeader, 8);
                // Record the rollback entry before committing the pointer/header. If a
                // partial write occurs, the original bytes and header remain recoverable.
                applied.Add(new AppliedTacticalListChange(change, newHeader, allocated));
                if (!newHeader.SequenceEqual(change.OriginalHeader))
                {
                    if (!Write(handle.Value, (nint)change.HeaderAddress, newHeader)
                        || !Read(handle.Value, (nint)change.HeaderAddress, 24).SequenceEqual(newHeader))
                        throw new InvalidOperationException("FM did not accept the new tactical list bounds.");
                }
            }
        }
        catch (Exception failure)
        {
            var errors = RollBackTactical(handle.Value, moduleBase, profile, plan.SaveLoadCounter, applied);
            throw new InvalidOperationException(failure.Message + (errors.Count == 0
                ? " Any tactical list changes already made were rolled back."
                : " TACTICAL ROLLBACK NEEDS INSPECTION: " + string.Join("; ", errors)), failure);
        }
        return new TacticalBatchReceipt(plan, DateTime.UtcNow, applied);
    }

    /// <summary>Restores the original Match Exposure lists only while the same team/tactic/career still holds the applied values.</summary>
    public static void UndoTacticalExposureBatch(TacticalBatchReceipt receipt)
    {
        var plan = receipt.Plan;
        using var process = Process.GetProcessById(plan.ProcessId);
        var module = FindModule(process);
        using var handle = new Handle(OpenProcess(QueryInformation | VmRead | VmWrite | VmOperation, false, plan.ProcessId));
        if (handle.Value == 0) throw Win32("undo tactical effectiveness");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("FM27 build profile mismatch. Tactical Undo is blocked.");
        if (profile.Id != plan.ProfileId || profile.Fingerprint != plan.ProfileFingerprint
            || ReadSaveCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
            throw new InvalidOperationException("The FM build or career changed after tactical Apply. Tactical Undo is blocked.");
        var context = ReadTacticalContext(handle.Value, moduleBase, profile, plan.ClubUid, plan.ClubName);
        if (context.SeniorTeam != plan.SeniorTeamAddress || context.TacticAddress != plan.TacticAddress
            || !context.Instructions.SequenceEqual(plan.TacticInstructions))
            throw new InvalidOperationException("The active tactic or senior team changed. Tactical Undo is blocked.");
        var currentPlayers = context.Players.ToDictionary(x => x.Uid);
        foreach (var applied in receipt.Changes)
        {
            var change = applied.Change;
            if (!currentPlayers.TryGetValue(change.PlayerUid, out var currentPlayer)
                || currentPlayer.Address != change.PlayerAddress
                || currentPlayer.Shift != change.PlayerShift
                || currentPlayer.RecordAddress != change.RecordAddress)
                throw new InvalidOperationException("A player identity or familiarity record changed after tactical Apply. Undo is blocked.");
            var header = Read(handle.Value, (nint)change.HeaderAddress, 24);
            if (!header.SequenceEqual(applied.TargetHeader))
                throw new InvalidOperationException("A tactical list has changed since Apply. Undo is blocked to protect the live career.");
            var begin = BitConverter.ToUInt64(header, 0);
            if (change.TargetBytes.Length > 0 && (!Pointer(begin)
                || !Read(handle.Value, (nint)begin, change.TargetBytes.Length).SequenceEqual(change.TargetBytes)))
                throw new InvalidOperationException("A tactical list's contents have changed since Apply. Undo is blocked.");
        }

        var undone = new List<AppliedTacticalListChange>();
        try
        {
            foreach (var applied in receipt.Changes.Reverse())
            {
                if (ReadSaveCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
                    throw new InvalidOperationException("FM switched career during tactical Undo. Atlas stopped.");
                var change = applied.Change;
                if (!change.RequiresGrowth && change.OriginalBytes.Length > 0)
                {
                    var oldBegin = BitConverter.ToUInt64(change.OriginalHeader, 0);
                    if (!Pointer(oldBegin) || !Write(handle.Value, (nint)oldBegin, change.OriginalBytes)
                        || !Read(handle.Value, (nint)oldBegin, change.OriginalBytes.Length).SequenceEqual(change.OriginalBytes))
                        throw new InvalidOperationException("Tactical Undo could not restore an original list.");
                }
                if (!Write(handle.Value, (nint)change.HeaderAddress, change.OriginalHeader)
                    || !Read(handle.Value, (nint)change.HeaderAddress, 24).SequenceEqual(change.OriginalHeader))
                    throw new InvalidOperationException("Tactical Undo could not restore the original list header.");
                undone.Add(applied);
            }
        }
        catch (Exception failure)
        {
            var errors = new List<string>();
            foreach (var applied in undone.AsEnumerable().Reverse())
            {
                try
                {
                    var change = applied.Change;
                    if (ReadSaveCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
                    { errors.Add("Skipped roll-forward after career change"); continue; }
                    if (!change.RequiresGrowth && change.TargetBytes.Length > 0)
                    {
                        var oldBegin = BitConverter.ToUInt64(change.OriginalHeader, 0);
                        if (!Write(handle.Value, (nint)oldBegin, change.TargetBytes)
                            || !Read(handle.Value, (nint)oldBegin, change.TargetBytes.Length).SequenceEqual(change.TargetBytes))
                            errors.Add("Could not reapply an in-place list");
                    }
                    if (!Write(handle.Value, (nint)change.HeaderAddress, applied.TargetHeader)
                        || !Read(handle.Value, (nint)change.HeaderAddress, 24).SequenceEqual(applied.TargetHeader))
                        errors.Add("Could not restore an applied list header");
                }
                catch (Exception ex) { errors.Add(ex.Message); }
            }
            throw new InvalidOperationException(failure.Message + (errors.Count == 0
                ? " Earlier tactical Undo writes were rolled forward."
                : " TACTICAL UNDO ROLLBACK NEEDS INSPECTION: " + string.Join("; ", errors)), failure);
        }
    }

    private static List<string> RollBackTactical(nint handle, ulong moduleBase, Fm27BuildProfile profile,
        ulong saveCounter, List<AppliedTacticalListChange> applied)
    {
        var errors = new List<string>();
        foreach (var item in applied.AsEnumerable().Reverse())
        {
            try
            {
                if (ReadSaveCounter(handle, moduleBase, profile) != saveCounter)
                { errors.Add("Skipped rollback after career change"); continue; }
                var change = item.Change;
                if (!change.RequiresGrowth && change.OriginalBytes.Length > 0)
                {
                    var oldBegin = BitConverter.ToUInt64(change.OriginalHeader, 0);
                    if (!Write(handle, (nint)oldBegin, change.OriginalBytes)
                        || !Read(handle, (nint)oldBegin, change.OriginalBytes.Length).SequenceEqual(change.OriginalBytes))
                        errors.Add("Could not restore in-place tactical list");
                }
                if (!Write(handle, (nint)change.HeaderAddress, change.OriginalHeader)
                    || !Read(handle, (nint)change.HeaderAddress, 24).SequenceEqual(change.OriginalHeader))
                    errors.Add("Could not restore tactical list header");
            }
            catch (Exception ex) { errors.Add(ex.Message); }
        }
        return errors;
    }

    private static TacticalContext ReadTacticalContext(nint handle, ulong moduleBase, Fm27BuildProfile profile, uint clubUid, string clubName)
    {
        if (profile.TacticsManagerRva == 0)
            throw new InvalidOperationException("This exact FM27 build has no verified Tactics Manager pointer. Tactical editing is not available.");
        var club = ResolveEntity(handle, moduleBase, profile, EntityKind.Club, clubUid, clubName);
        var teamList = U64(handle, club.Address + 0x18);
        if (!Pointer(teamList)) throw new InvalidOperationException("The selected club has no senior-team pointer.");
        var senior = U64(handle, teamList);
        if (!Pointer(senior) || U64(handle, senior + 0x30) != club.Address)
            throw new InvalidOperationException("The selected club's senior-team pointer failed its back-reference check.");
        var teamUid = BitConverter.ToUInt32(Read(handle, (nint)(senior + 0x08), 4), 0);
        var (tacticAddress, instructions, options) = ReadActiveTactic(handle, moduleBase, profile, senior, teamUid);
        var trainingKeeper = FindTrainingKeeper(handle, moduleBase, profile, club.Address);
        var playerBounds = ReadBounds(handle, moduleBase, profile.PlayersArrayRva, "Player");
        var players = new List<TacticalPlayer>();
        for (var i = 0; i < playerBounds.Count; i++)
        {
            var address = U64(handle, playerBounds.Data + (ulong)i * 8);
            if (!Pointer(address)) continue;
            try
            {
                if (U64(handle, address + 0x130) != senior) continue;
                var shift = DetectPlayerShift(handle, address);
                var uid = BitConverter.ToUInt32(Read(handle, (nint)(address + 0x294 + (ulong)shift), 4), 0);
                if (uid == 0 || uid == uint.MaxValue) continue;
                var record = U64(handle, address + 0x100 + (ulong)shift);
                if (!Pointer(record)) continue;
                var person = BitConverter.ToUInt32(Read(handle, (nint)(address + 0x290 + (ulong)shift), 4), 0);
                var areaLists = new List<TacticalListState>(6);
                var valid = true;
                for (var area = 0; area < 6; area++)
                {
                    var header = record + (ulong)(area * 0x20);
                    if (Read(handle, (nint)(header + 0x18), 1)[0] != area) { valid = false; break; }
                    var list = ReadTacticalList(handle, header, 4, 64);
                    if (!ValidAreaEntries(list.Bytes)) { valid = false; break; }
                    areaLists.Add(list);
                }
                if (!valid) continue;
                var shapeHeader = record + 0xC0;
                var shapeWords = Read(handle, (nint)shapeHeader, 24);
                var shapeList = shapeWords.All(x => x == 0) ? null : ReadTacticalList(handle, shapeHeader, 16, 64);
                if (shapeList is not null && !ValidShapeEntries(shapeList.Bytes)) continue;
                var history = trainingKeeper == 0 ? null : ReadPlayerTrainingHistory(handle, trainingKeeper, person);
                players.Add(new TacticalPlayer(uid, address, shift, record, person, areaLists, shapeList, history));
            }
            catch { }
        }
        if (players.Count == 0)
            throw new InvalidOperationException("No players in the selected senior team had a valid FM27 Tactical Effectiveness record.");
        return new TacticalContext(club, senior, teamUid, tacticAddress, instructions, options, players);
    }



    private static ushort ReadCurrentDayOfYear(nint handle, ulong moduleBase, Fm27BuildProfile profile)
    {
        if (profile.CurrentDateRva == 0)
            throw new InvalidOperationException("The exact FM27 build has no current-date pointer for training-history writes.");
        var raw = Read(handle, (nint)(moduleBase + profile.CurrentDateRva), 4);
        var day = (ushort)(BitConverter.ToUInt16(raw, 0) & 0x01FF);
        var year = BitConverter.ToUInt16(raw, 2);
        if (year < 1901 || year > 2100 || day == 0 || day > (DateTime.IsLeapYear(year) ? 366 : 365))
            throw new InvalidOperationException("FM's current in-career date could not be validated for a training-history update.");
        return checked((ushort)(day - 1));
    }

    private static ushort?[] LatestTrainingShapes(byte[] history)
    {
        var result = new ushort?[2];
        var latest = new ushort[] { 0, 0 };
        for (var at = 0; at < history.Length; at += 12)
        {
            var area = history[at + 4];
            if (history[at + 8] != 1 || area is < 6 or > 7) continue;
            var day = BitConverter.ToUInt16(history, at);
            var value = BitConverter.ToUInt16(history, at + 2);
            var index = area - 6;
            if (!result[index].HasValue || day >= latest[index])
            {
                result[index] = value;
                latest[index] = day;
            }
        }
        return result;
    }

    private static double FirstAgeWeight(int age, bool alternate)
    {
        var rows = alternate
            ? new (int Day, double Weight)[] { (6,12), (13,9), (20,7), (41,5), (83,4), (193,3), (251,2) }
            : new (int Day, double Weight)[] { (6,12), (13,9), (20,7), (41,6), (83,4), (251,2) };
        foreach (var row in rows) if (row.Day >= age) return row.Weight;
        return 0;
    }

    private static double SessionFactor(int value)
    {
        var rows = new (int Value, double Factor)[] { (8,.75), (10,.8), (12,.85), (14,.9), (16,.95), (18,1), (20,1) };
        foreach (var row in rows) if (row.Value >= value) return row.Factor;
        return 1;
    }

    private static double PlayerFactor(int value)
    {
        var rows = new (int Value, double Factor)[] { (8,.7), (10,.75), (12,.8), (13,.85), (14,.87), (15,.9), (16,.95), (17,.97), (18,1), (20,1) };
        foreach (var row in rows) if (row.Value >= value) return row.Factor;
        return 1;
    }

    private static double WeeksFactor(int weeks)
    {
        var rows = new (int Value, double Factor)[] { (0,.6), (4,.65), (8,.7), (26,.75), (52,.8), (78,.85), (104,.9), (130,.95), (156,1) };
        var factor = .75;
        foreach (var row in rows) if (row.Value <= weeks) factor = row.Factor; else break;
        return factor;
    }

    private static double? TrainingEntryWeight(byte[] history, int at, int[] options, ushort?[] shapes, ushort today)
    {
        var area = history[at + 4];
        var kind = history[at + 8];
        double closeness;
        if (kind == 0 && area < 6)
        {
            var option = unchecked((sbyte)history[at + 6]);
            closeness = Math.Max(0, 1.0 - .1 * Math.Abs(option - options[area]));
        }
        else if (kind == 1 && area is 6 or 7)
        {
            var shape = BitConverter.ToUInt16(history, at + 2);
            if (shapes[area - 6] != shape) return null;
            closeness = 1.0;
        }
        else closeness = 0.0;
        if (closeness <= 0) return 0.0;

        var recordedDay = BitConverter.ToUInt16(history, at);
        var age = ((int)today - recordedDay) % 365;
        if (age < 0) age += 365;
        var aged = FirstAgeWeight(age, history[at + 7] != 0);
        var kindFactor = history[at + 5] switch
        {
            0 => 1.0,
            1 or 3 => .8,
            2 => .2,
            4 => .1,
            _ => 0.0
        };
        var session = SessionFactor(history[at + 9]);
        var player = PlayerFactor(unchecked((sbyte)history[at + 10]));
        var weeks = WeeksFactor(history[at + 11]);
        return closeness * aged * kindFactor * session * player * weeks;
    }

    private static int?[] HistoryAreaValues(byte[] history, int[] options, ushort today)
    {
        if (history.Length % 12 != 0) throw new InvalidOperationException("FM training-history bytes are not aligned to 12-byte entries.");
        var shapes = LatestTrainingShapes(history);
        var sums = new double[8];
        var invalid = new bool[8];
        for (var at = 0; at < history.Length; at += 12)
        {
            var area = Math.Min(7, history[at + 4]);
            var weight = TrainingEntryWeight(history, at, options, shapes, today);
            if (!weight.HasValue) invalid[area] = true;
            else sums[area] += weight.Value;
        }
        var result = new int?[8];
        for (var area = 0; area < 8; area++)
        {
            if (invalid[area]) { result[area] = null; continue; }
            result[area] = (int)Math.Round(Math.Clamp(sums[area], 0, 100), MidpointRounding.AwayFromZero);
        }
        return result;
    }

    private static byte[] BuildTodayTrainingHistory(byte[] oldHistory, int[] options, ushort today)
    {
        if (oldHistory.Length % 12 != 0)
            throw new InvalidOperationException("FM training history has an invalid entry length.");
        var shapes = LatestTrainingShapes(oldHistory);
        var areas = Enumerable.Range(0, 8).Where(x => x < 6 || shapes[x - 6].HasValue).ToArray();
        if (areas.Length == 0) throw new InvalidOperationException("No valid tactical areas were found for this player's history.");
        var count = Math.Max(oldHistory.Length / 12, 18 * areas.Length);
        if (count > 4_000) throw new InvalidOperationException("The FM27 training-history list exceeds the validated 4,000-entry limit.");
        var output = new byte[count * 12];
        for (var i = 0; i < count; i++)
        {
            var area = areas[i % areas.Length];
            var at = i * 12;
            BitConverter.GetBytes(today).CopyTo(output, at);
            if (area < 6)
            {
                BitConverter.GetBytes((ushort)0x0C8C).CopyTo(output, at + 2);
                output[6 + at] = checked((byte)options[area]);
            }
            else
            {
                BitConverter.GetBytes(shapes[area - 6]!.Value).CopyTo(output, at + 2);
                output[6 + at] = 0xFF;
                output[8 + at] = 1;
            }
            output[4 + at] = checked((byte)area);
            output[9 + at] = 20;
            output[10 + at] = 20;
            output[11 + at] = 156;
        }
        return output;
    }

    private static ulong FindTrainingKeeper(nint handle, ulong moduleBase, Fm27BuildProfile profile, ulong clubAddress)
    {
        if (profile.TrainingManagerRva == 0) return 0;
        var manager = U64(handle, moduleBase + profile.TrainingManagerRva);
        if (!Pointer(manager)) return 0;
        var list = Read(handle, (nint)(manager + 0x40), 16);
        var begin = BitConverter.ToUInt64(list, 0);
        var end = BitConverter.ToUInt64(list, 8);
        if (begin == end) return 0;
        if (!Pointer(begin) || end < begin || (end - begin) % 8 != 0 || (end - begin) > 8 * 64)
            throw new InvalidOperationException("The Training Manager keeper list failed validation.");
        for (var at = begin; at < end; at += 8)
        {
            var keeper = U64(handle, at);
            if (!Pointer(keeper)) continue;
            if (U64(handle, keeper + 0x78) == clubAddress) return keeper;
        }
        return 0;
    }

    private static TacticalListState? ReadPlayerTrainingHistory(nint handle, ulong keeper, uint personId)
    {
        var head = U64(handle, keeper + 0x60);
        if (!Pointer(head)) return null;
        var root = U64(handle, head + 8);
        var stack = new Stack<ulong>();
        stack.Push(root);
        var visited = 0;
        while (stack.Count > 0 && visited++ < 10_000)
        {
            var node = stack.Pop();
            if (!Pointer(node) || node == head) continue;
            if (Read(handle, (nint)(node + 0x19), 1)[0] != 0) continue;
            var candidate = BitConverter.ToUInt32(Read(handle, (nint)(node + 0x20), 4), 0);
            if (candidate == personId)
            {
                var state = ReadTacticalList(handle, node + 0x28, 12, 4_000);
                if (!ValidTrainingHistory(state.Bytes))
                    throw new InvalidOperationException("A player's training history contains entries that do not match the FM27 layout.");
                return state;
            }
            stack.Push(U64(handle, node));
            stack.Push(U64(handle, node + 0x10));
        }
        return null;
    }

    private static bool ValidTrainingHistory(byte[] bytes)
    {
        if (bytes.Length % 12 != 0 || bytes.Length / 12 > 4_000) return false;
        for (var i = 0; i < bytes.Length; i += 12)
        {
            var day = BitConverter.ToUInt16(bytes, i);
            var tag = BitConverter.ToUInt16(bytes, i + 2);
            var area = bytes[i + 4];
            var shape = bytes[i + 8];
            if (day >= 366) return false;
            if (area <= 5 && (shape != 0 || tag != 0x0C8C)) return false;
            if ((area == 6 || area == 7) && shape != 1) return false;
            if (area > 7) return false;
        }
        return true;
    }

    private static (ulong Address, byte[] Instructions, int[] Options) ReadActiveTactic(
        nint handle, ulong moduleBase, Fm27BuildProfile profile, ulong seniorTeam, uint teamUid)
    {
        var manager = U64(handle, moduleBase + profile.TacticsManagerRva);
        if (!Pointer(manager)) throw new InvalidOperationException("The FM27 Tactics Manager could not be resolved.");
        // FM27 stores this manager's tactic-map pointers as a pair (begin, end), not a capacity triple.
        var list = Read(handle, (nint)(manager + 0x20), 16);
        var begin = BitConverter.ToUInt64(list, 0);
        var end = BitConverter.ToUInt64(list, 8);
        if (!Pointer(begin) || end < begin || (end - begin) % 8 != 0 || (end - begin) > 8 * 64)
            throw new InvalidOperationException("The Tactics Manager list failed validation.");
        for (var at = begin; at < end; at += 8)
        {
            var managerObject = U64(handle, at);
            if (!Pointer(managerObject)) continue;
            var head = U64(handle, managerObject);
            if (!Pointer(head)) continue;
            var root = U64(handle, head + 8);
            var stack = new Stack<ulong>();
            stack.Push(root);
            var visited = 0;
            while (stack.Count > 0 && visited++ < 10_000)
            {
                var node = stack.Pop();
                if (!Pointer(node) || node == head) continue;
                if (Read(handle, (nint)(node + 0x19), 1)[0] != 0) continue;
                var key = BitConverter.ToUInt32(Read(handle, (nint)(node + 0x20), 4), 0);
                if (key == teamUid)
                {
                    var value = node + 0x28;
                    var tacticList = Read(handle, (nint)(value + 0x1010), 16);
                    var first = BitConverter.ToUInt64(tacticList, 0);
                    var last = BitConverter.ToUInt64(tacticList, 8);
                    if (!Pointer(first) || last < first || (last - first) % 8 != 0 || (last - first) / 8 > 16)
                        throw new InvalidOperationException("The active team's tactic list failed validation.");
                    var current = unchecked((sbyte)Read(handle, (nint)(value + 0x1028), 1)[0]);
                    var count = (last - first) / 8;
                    if (current < 0 || (ulong)current >= count)
                        throw new InvalidOperationException("FM has no selected tactic for the senior team.");
                    var tactic = U64(handle, first + (ulong)current * 8);
                    if (!Pointer(tactic)) throw new InvalidOperationException("The current FM tactic pointer is invalid.");
                    var instructions = Read(handle, (nint)(tactic + 0xE0), 16);
                    var optionValues = new[]
                    {
                        BitConverter.ToUInt64(instructions, 0),
                        BitConverter.ToUInt64(instructions, 8)
                    };
                    return (tactic, instructions, TacticalAreaOptions(optionValues));
                }
                stack.Push(U64(handle, node));
                stack.Push(U64(handle, node + 0x10));
            }
        }
        throw new InvalidOperationException("The human manager's Tactics Manager has no current tactic for this senior team.");
    }

    private static TacticalListState ReadTacticalList(nint handle, ulong header, int elementSize, int maximum)
    {
        var bytes = Read(handle, (nint)header, 24);
        var begin = BitConverter.ToUInt64(bytes, 0);
        var end = BitConverter.ToUInt64(bytes, 8);
        var capacity = BitConverter.ToUInt64(bytes, 16);
        if (begin == 0 && end == 0 && capacity == 0)
            return new TacticalListState(header, bytes, 0, 0, 0, Array.Empty<byte>());
        if (!Pointer(begin) || end < begin || capacity < end || (end - begin) % (ulong)elementSize != 0
            || (capacity - begin) % (ulong)elementSize != 0 || (end - begin) / (ulong)elementSize > (ulong)maximum
            || (capacity - begin) / (ulong)elementSize > (ulong)maximum * 4)
            throw new InvalidOperationException("A Tactical Effectiveness list has invalid bounds. Atlas will not change it.");
        return new TacticalListState(header, bytes, begin, end, capacity, Read(handle, (nint)begin, checked((int)(end - begin))));
    }

    private static bool ValidAreaEntries(byte[] bytes)
    {
        if (bytes.Length % 4 != 0) return false;
        for (var i = 0; i < bytes.Length; i += 4)
        {
            var code = TacticalEntryCode(bytes.AsSpan(i, 4));
            var value = BitConverter.ToUInt16(bytes, i) & 0x3FFF;
            if (code is < 1 or > 20 || value > 10_000) return false;
        }
        return true;
    }

    private static bool ValidShapeEntries(byte[] bytes)
    {
        if (bytes.Length % 16 != 0) return false;
        for (var i = 0; i < bytes.Length; i += 16)
            if (BitConverter.ToUInt16(bytes, i + 14) > 10_000) return false;
        return true;
    }

    private static int? RecordAreaValue(byte[] entries, int option)
    {
        if (entries.Length % 4 != 0) return null;
        var best = entries.Length == 0 ? 0 : int.MinValue;
        for (var i = 0; i < entries.Length; i += 4)
        {
            var code = TacticalEntryCode(entries.AsSpan(i, 4));
            var value = BitConverter.ToUInt16(entries, i) & 0x3FFF;
            best = Math.Max(best, value - 1_000 * Math.Abs(code - option));
        }
        return Math.Max(best, 1_000) / 100;
    }

    private static int? ShapeAreaValue(byte[]? entries)
    {
        if (entries is null || entries.Length == 0 || entries.Length % 16 != 0) return null;
        var best = 0;
        for (var i = 0; i < entries.Length; i += 16)
            best = Math.Max(best, BitConverter.ToUInt16(entries, i + 14));
        return best / 100;
    }

    private static byte[] SetAreaOption(byte[] old, int option, int target, byte lastSpare)
    {
        var bytes = old.ToArray();
        for (var i = 0; i < bytes.Length; i += 4)
        {
            if (TacticalEntryCode(bytes.AsSpan(i, 4)) != option) continue;
            var packed = (ushort)((BitConverter.ToUInt16(bytes, i) & ~0x3FFF) | (target * 100));
            BitConverter.GetBytes(packed).CopyTo(bytes, i);
            return bytes;
        }
        var entry = BitConverter.GetBytes(((uint)option & 0x3F) << 14 | (uint)(target * 100));
        Array.Resize(ref bytes, bytes.Length + 4);
        entry.CopyTo(bytes, bytes.Length - 4);
        bytes[^1] = lastSpare;
        return bytes;
    }

    private static byte[] SetShapeValues(byte[] old, int target)
    {
        var bytes = old.ToArray();
        for (var i = 0; i < bytes.Length; i += 16)
            BitConverter.GetBytes((ushort)(target * 100)).CopyTo(bytes, i + 14);
        return bytes;
    }

    private static TacticalListChange CreateTacticalListChange(TacticalPlayer player, TacticalListState list, byte[] target, int elementSize)
    {
        var needsGrowth = list.Begin == 0 || list.Begin + (ulong)target.Length > list.Capacity;
        return new TacticalListChange(player.Uid, player.Address, player.Shift, player.RecordAddress,
            list.HeaderAddress, list.Header, list.Bytes, target, elementSize, needsGrowth);
    }

    private static int TacticalEntryCode(ReadOnlySpan<byte> entry)
    {
        var packed = (uint)entry[0] | ((uint)entry[1] << 8) | ((uint)entry[2] << 16);
        return ((int)(packed << 12)) >> 26;
    }

    public static IReadOnlyList<int> DecodeTacticalAreaOptions(ulong first, ulong second)
        => TacticalAreaOptions(new[] { first, second });

    private static int[] TacticalAreaOptions(ulong[] words)
    {
        var first = words[0];
        var second = words[1];
        bool Bit(ulong word, int n) => ((word >> n) & 1) == 1;
        int Shift(ulong word, (int bit, int amount)[] options)
        {
            foreach (var item in options) if (Bit(word, item.bit)) return item.amount;
            return 0;
        }
        int One(int n) => Math.Max(1, n);
        var line = (Bit(first,61) ? 17 : Bit(first,63) ? 3 : 10)
            + Shift(first,new[]{(56,3),(57,2),(58,-2),(59,-3)});
        var intensity = (Bit(second,1) ? 17 : Bit(second,2) ? 14 : Bit(second,3) ? 6 : Bit(second,4) ? 3 : 10)
            + Shift(second,new[]{(6,3),(7,-3)});
        var passing = Bit(second,16) ? 20 : Bit(second,17) ? 1 : 10;
        var chance = (Bit(first,1) ? 17 : Bit(first,2) ? 14 : Bit(first,3) ? 6 : Bit(first,4) ? 3 : 10)
            + Shift(first,new[]{(46,3),(47,2),(48,-2),(49,-3)});
        var build = (Bit(first,7) ? 17 : Bit(first,8) ? 3 : 10)
            + Shift(first,new[]{(44,3),(45,-3)});
        var trap = (Bit(first,9) ? 13 : Bit(first,17) ? 7 : 10)
            + Shift(first,new[]{(15,2),(16,-2)}) + Shift(first,new[]{(5,2),(6,-2)})
            + Shift(first,new[]{(54,3),(55,-3)});
        return new[]{One(line),One(intensity),passing,One(chance),One(build),One(trap)};
    }
}
