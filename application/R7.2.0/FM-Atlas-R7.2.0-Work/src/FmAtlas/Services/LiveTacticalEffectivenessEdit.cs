using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;

namespace FmAtlas.Services;

/// <summary>
/// FM27 Tactical Effectiveness editor. It stages the game's eight tactical-familiarity areas
/// for the selected player's current team tactic. It only operates on existing, validated
/// buffers with sufficient capacity; it never allocates or relocates game-owned lists.
/// </summary>
public static partial class LiveMemoryEditService
{
    private const ulong TacticTeamIdOffset = 0x08;
    private const ulong TacticTeamMapOffset = 0x00;
    private const ulong TacticMapManagersOffset = 0x20;
    private const ulong TacticMapKeyOffset = 0x20;
    private const ulong TacticMapValueOffset = 0x28;
    private const ulong TacticListOffset = 0x1010;
    private const ulong TacticCurrentOffset = 0x1028;
    private const ulong TacticInstructionsOffset = 0xE0;
    private const ulong TrainingManagersOffset = 0x40;
    private const ulong TrainingClubOffset = 0x78;
    private const ulong TrainingPlayersMapOffset = 0x60;
    private const ulong TrainingNodePersonOffset = 0x20;
    private const ulong TrainingNodeHistoryOffset = 0x28;
    private const ulong PlayerFamiliarityRecordOffset = 0x100;
    private const ulong PlayerPersonIdOffset = 0x290;
    private const ulong RecordAreaStep = 0x20;
    private const ulong RecordAreaIndex = 0x18;
    private const ulong RecordShapesOffset = 0xC0;
    private const int RecordEntrySize = 4;
    private const int ShapeEntrySize = 16;
    private const int HistoryEntrySize = 12;
    private const int MaxRecordEntries = 64;
    private const int MaxShapeEntries = 64;
    private const int MaxHistoryEntries = 4_000;
    private const int MaxManagers = 64;
    private const int MaxTreeNodes = 10_000;
    private const int EntriesPerArea = 18;
    private const ushort BestFamiliarity = 10_000;
    private const ushort OptionTag = 0x0C8C;

    public sealed record TacticalPatch(ulong Address, string Field, byte[] OriginalBytes, byte[] TargetBytes);
    public sealed record TacticalPlan(
        int ProcessId, uint PlayerUid, string PlayerName, string ProfileId, string ProfileFingerprint,
        ulong SaveLoadCounter, DateTime CreatedUtc, string ContextSignature,
        IReadOnlyList<TacticalPatch> Patches);
    public sealed record TacticalReceipt(TacticalPlan Plan, DateTime AppliedUtc, bool ReadBackVerified);

    public static bool SupportsTacticalEffectiveness(Fm27BuildProfile profile)
        => profile.TacticsManagerRva != 0 && profile.TrainingManagerRva != 0;

    public static TacticalPlan StageMaxTacticalEffectiveness(int processId, uint playerUid, string playerName)
    {
        if (playerUid == 0) throw new ArgumentOutOfRangeException(nameof(playerUid));
        if (string.IsNullOrWhiteSpace(playerName)) throw new ArgumentException("A player name is required.", nameof(playerName));
        using var process = Process.GetProcessById(processId);
        var module = FindGameModule(process);
        using var handle = new ProcessModuleHandle(OpenProcess(ProcessQueryInformation | ProcessVmRead, false, processId));
        if (handle.Value == 0) throw new InvalidOperationException($"Windows denied read-only FM access (Win32 {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadMatchedProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("This FM27 build is not in Atlas's exact profile registry.");
        if (!SupportsTacticalEffectiveness(profile))
            throw new InvalidOperationException("This FM27 build has no independently mapped tactics-manager and training-manager pointers. Tactical Effectiveness editing is unavailable for this build.");
        var generation = ReadSaveLoadCounter(handle.Value, moduleBase, profile);
        var playerAddress = ResolvePlayer(handle.Value, moduleBase, profile, playerUid, playerName);
        var shift = DetectPlayerIdentityShift(handle.Value, playerAddress);
        var built = BuildTacticalPatches(handle.Value, moduleBase, profile, playerAddress, shift);
        return new TacticalPlan(processId, playerUid, playerName, profile.Id, profile.Fingerprint,
            generation, DateTime.UtcNow, built.ContextSignature, built.Patches);
    }

    public static TacticalReceipt ApplyMaxTacticalEffectiveness(TacticalPlan plan)
    {
        EnsureLiveWritesEnabled();
        if (DateTime.UtcNow - plan.CreatedUtc > TimeSpan.FromMinutes(2) || plan.CreatedUtc > DateTime.UtcNow.AddSeconds(10))
            throw new InvalidOperationException("The tactical preview has expired. Stage it again before applying.");
        using var process = Process.GetProcessById(plan.ProcessId);
        var module = FindGameModule(process);
        using var handle = new ProcessModuleHandle(OpenProcess(ProcessQueryInformation | ProcessVmRead | ProcessVmWrite | ProcessVmOperation, false, plan.ProcessId));
        if (handle.Value == 0) throw new InvalidOperationException($"Windows denied FM live-write access (Win32 {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadMatchedProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("FM27 build profile no longer matches.");
        if (profile.Id != plan.ProfileId || profile.Fingerprint != plan.ProfileFingerprint
            || ReadSaveLoadCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
            throw new InvalidOperationException("The FM build or loaded career changed after the tactical preview. No writes were made.");
        var playerAddress = ResolvePlayer(handle.Value, moduleBase, profile, plan.PlayerUid, plan.PlayerName);
        var shift = DetectPlayerIdentityShift(handle.Value, playerAddress);
        var fresh = BuildTacticalPatches(handle.Value, moduleBase, profile, playerAddress, shift);
        if (!string.Equals(fresh.ContextSignature, plan.ContextSignature, StringComparison.Ordinal)
            || !SamePatchAddresses(fresh.Patches, plan.Patches))
            throw new InvalidOperationException("The active team tactic, player familiarity record or training-history record changed after preview. Stage the tactical edit again.");
        foreach (var patch in plan.Patches)
        {
            var current = Read(handle.Value, (nint)patch.Address, patch.OriginalBytes.Length);
            if (!current.SequenceEqual(patch.OriginalBytes))
                throw new InvalidOperationException($"Tactical Effectiveness preflight failed at {patch.Field}: the original bytes changed. No writes were made.");
        }
        ApplyPatchesWithRollback(handle.Value, moduleBase, profile, plan.SaveLoadCounter, plan.Patches);
        return new TacticalReceipt(plan, DateTime.UtcNow, true);
    }

    public static void UndoMaxTacticalEffectiveness(TacticalReceipt receipt)
    {
        EnsureLiveWritesEnabled();
        var plan = receipt.Plan;
        using var process = Process.GetProcessById(plan.ProcessId);
        var module = FindGameModule(process);
        using var handle = new ProcessModuleHandle(OpenProcess(ProcessQueryInformation | ProcessVmRead | ProcessVmWrite | ProcessVmOperation, false, plan.ProcessId));
        if (handle.Value == 0) throw new InvalidOperationException($"Windows denied FM live-write access during tactical Undo (Win32 {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadMatchedProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("FM27 build profile no longer matches; tactical Undo is blocked.");
        if (profile.Id != plan.ProfileId || profile.Fingerprint != plan.ProfileFingerprint
            || ReadSaveLoadCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
            throw new InvalidOperationException("The loaded FM career changed after the tactical edit. Undo is blocked to protect the new save.");
        var playerAddress = ResolvePlayer(handle.Value, moduleBase, profile, plan.PlayerUid, plan.PlayerName);
        var shift = DetectPlayerIdentityShift(handle.Value, playerAddress);
        var fresh = BuildTacticalPatches(handle.Value, moduleBase, profile, playerAddress, shift);
        if (!string.Equals(fresh.ContextSignature, plan.ContextSignature, StringComparison.Ordinal)
            || !SamePatchAddresses(fresh.Patches, plan.Patches))
            throw new InvalidOperationException("The active tactic or its player records have moved. Tactical Undo is blocked; reload the live data and inspect the career.");
        foreach (var patch in plan.Patches)
        {
            var current = Read(handle.Value, (nint)patch.Address, patch.TargetBytes.Length);
            if (!current.SequenceEqual(patch.TargetBytes))
                throw new InvalidOperationException($"Tactical Undo blocked: {patch.Field} changed after Apply. No values were reverted.");
        }
        var reverse = plan.Patches.Select(p => new TacticalPatch(p.Address, p.Field, p.TargetBytes, p.OriginalBytes)).Reverse().ToArray();
        ApplyPatchesWithRollback(handle.Value, moduleBase, profile, plan.SaveLoadCounter, reverse);
    }

    private sealed record TacticalList(ulong Header, ulong Begin, ulong End, ulong Capacity, byte[] Bytes);
    private sealed record TacticalBuild(string ContextSignature, IReadOnlyList<TacticalPatch> Patches);

    private static TacticalBuild BuildTacticalPatches(nint handle, ulong moduleBase, Fm27BuildProfile profile, ulong player, int personShift)
    {
        var team = U64(handle, player + 0x130);
        if (!Pointer(team)) throw new InvalidOperationException("The selected player has no valid team pointer. Tactical Effectiveness cannot be resolved.");
        var club = U64(handle, team + 0x30);
        if (!Pointer(club)) throw new InvalidOperationException("The player's team does not point to a valid club.");
        var teamId = BitConverter.ToUInt32(Read(handle, (nint)(team + TacticTeamIdOffset), 4), 0);
        var tactic = FindCurrentTactic(handle, moduleBase, profile, teamId);
        var options = TacticalAreaOptions(Read(handle, (nint)(tactic + TacticInstructionsOffset), 16));
        var gameDay = ReadGameDay(handle, moduleBase, profile);
        var record = U64(handle, player + PlayerFamiliarityRecordOffset);
        if (!Pointer(record)) throw new InvalidOperationException("This player has no FM27 Tactical Effectiveness record yet. The game must create one before Atlas can edit it.");
        var areas = new List<TacticalList>(6);
        for (var area = 0; area < 6; area++)
        {
            var header = record + (ulong)area * RecordAreaStep;
            if (Read(handle, (nint)(header + RecordAreaIndex), 1)[0] != area)
                throw new InvalidOperationException($"The player's tactical area {area} does not match the FM27 layout. No changes were staged.");
            areas.Add(ReadTacticalList(handle, header, RecordEntrySize, MaxRecordEntries));
        }
        var shapeHeader = record + RecordShapesOffset;
        var shapeHeaderBytes = Read(handle, (nint)shapeHeader, 16);
        if (shapeHeaderBytes.All(x => x == 0))
            throw new InvalidOperationException("The player's record has no verified in/out-of-possession shape familiarity. Atlas will not fabricate shape IDs.");
        var shapes = ReadTacticalList(handle, shapeHeader, ShapeEntrySize, MaxShapeEntries);
        if (shapes.Bytes.Length == 0)
            throw new InvalidOperationException("The player has no shape familiarity entries. Atlas cannot safely invent formation identifiers.");

        var personId = BitConverter.ToUInt32(Read(handle, (nint)(player + PlayerPersonIdOffset + (ulong)personShift), 4), 0);
        var trainingManager = U64(handle, moduleBase + profile.TrainingManagerRva);
        var keeper = FindTrainingKeeper(handle, moduleBase, profile, club);
        if (!Pointer(keeper)) throw new InvalidOperationException("No training-history keeper exists for the player's club. Tactical Effectiveness is read-only for this player.");
        var historyNode = FindHistoryNode(handle, keeper, personId);
        if (!Pointer(historyNode)) throw new InvalidOperationException("The FM27 training manager has no tactical history for this player. Atlas cannot safely set all eight Tactical Effectiveness areas.");
        var historyHeader = historyNode + TrainingNodeHistoryOffset;
        var history = ReadTacticalList(handle, historyHeader, HistoryEntrySize, MaxHistoryEntries);
        if (history.Bytes.Length == 0)
            throw new InvalidOperationException("The player's tactical training history is empty. Atlas will not invent training records.");
        var shapeIds = ReadShapeIds(history.Bytes);
        if (shapeIds is null)
            throw new InvalidOperationException("The player's training history does not contain both formation-shape witnesses. All eight Tactical Effectiveness areas cannot be safely edited.");

        var patches = new List<TacticalPatch>();
        for (var area = 0; area < 6; area++)
        {
            var list = areas[area];
            var newBytes = list.Bytes.ToArray();
            var entryIndex = FindOptionEntry(newBytes, options[area]);
            ulong targetEnd;
            if (entryIndex >= 0)
            {
                SetEntryValue(newBytes.AsSpan(entryIndex * RecordEntrySize, RecordEntrySize), BestFamiliarity);
                AddPatch(patches, list.Begin, "tactical.area." + area + ".record", list.Bytes, newBytes);
                targetEnd = list.End;
            }
            else
            {
                if (list.Begin == 0 || list.End > list.Capacity || list.Capacity - list.End < RecordEntrySize)
                    throw new InvalidOperationException($"Tactical area {area} has no entry for the current tactic and its list has no spare capacity. Atlas refuses to allocate or relocate FM-owned memory.");
                var entry = new byte[RecordEntrySize];
                var packed = ((uint)options[area] & 0x3F) << 14 | BestFamiliarity;
                BinaryPrimitives.WriteUInt32LittleEndian(entry, packed);
                if (list.Bytes.Length > 0) entry[3] = list.Bytes[^1];
                var oldExtended = Read(handle, (nint)list.Begin, list.Bytes.Length + RecordEntrySize);
                var newExtended = oldExtended.ToArray();
                entry.CopyTo(newExtended, list.Bytes.Length);
                AddPatch(patches, list.Begin, "tactical.area." + area + ".record.append", oldExtended, newExtended);
                targetEnd = list.End + RecordEntrySize;
            }
            AddPatch(patches, list.Header + 8, "tactical.area." + area + ".end", BitConverter.GetBytes(list.End), BitConverter.GetBytes(targetEnd));
        }

        var maxShapes = shapes.Bytes.ToArray();
        for (var offset = 0; offset < maxShapes.Length; offset += ShapeEntrySize)
            BinaryPrimitives.WriteUInt16LittleEndian(maxShapes.AsSpan(offset + 14, 2), BestFamiliarity);
        AddPatch(patches, shapes.Begin, "tactical.formation_shapes", shapes.Bytes, maxShapes);

        var targetHistory = BuildMaxTacticalHistory(options, shapeIds, history.Bytes, gameDay);
        if (history.Begin == 0 || history.Capacity < history.Begin || history.Capacity - history.Begin < (ulong)targetHistory.Length)
            throw new InvalidOperationException($"The player's tactical history buffer has {history.Capacity - history.Begin:N0} bytes free, but {targetHistory.Length:N0} are required. Atlas will not allocate or relocate FM-owned memory.");
        var historyOriginal = Read(handle, (nint)history.Begin, targetHistory.Length);
        AddPatch(patches, history.Begin, "tactical.training_history", historyOriginal, targetHistory);
        AddPatch(patches, history.Header + 8, "tactical.training_history.end",
            BitConverter.GetBytes(history.End), BitConverter.GetBytes(history.Begin + (ulong)targetHistory.Length));

        var signature = string.Join("|", profile.Id, player.ToString("X"), team.ToString("X"), club.ToString("X"),
            tactic.ToString("X"), record.ToString("X"), historyHeader.ToString("X"), personId.ToString());
        if (patches.Count == 0) throw new InvalidOperationException("The player's Tactical Effectiveness values already match the staged maximum.");
        return new TacticalBuild(signature, patches);
    }

    private static void ApplyPatchesWithRollback(nint handle, ulong moduleBase, Fm27BuildProfile profile,
        ulong saveCounter, IReadOnlyList<TacticalPatch> patches)
    {
        var written = new List<(TacticalPatch Patch, int BytesWritten)>();
        try
        {
            foreach (var patch in patches)
            {
                if (ReadSaveLoadCounter(handle, moduleBase, profile) != saveCounter)
                    throw new InvalidOperationException("The FM career changed during the tactical batch. Atlas stopped writing.");
                var ok = Write(handle, (nint)patch.Address, patch.TargetBytes, out var writtenBytes);
                if (writtenBytes > 0) written.Add((patch, writtenBytes));
                if (!ok || writtenBytes != patch.TargetBytes.Length)
                    throw new InvalidOperationException($"FM rejected a tactical write for {patch.Field} ({writtenBytes}/{patch.TargetBytes.Length} bytes).");
                if (!Read(handle, (nint)patch.Address, patch.TargetBytes.Length).SequenceEqual(patch.TargetBytes))
                    throw new InvalidOperationException($"Tactical read-back verification failed for {patch.Field}.");
            }
        }
        catch (Exception failure)
        {
            var rollbackErrors = new List<string>();
            foreach (var item in written.AsEnumerable().Reverse())
            {
                try
                {
                    if (ReadSaveLoadCounter(handle, moduleBase, profile) != saveCounter)
                    { rollbackErrors.Add(item.Patch.Field + " rollback skipped after career change"); continue; }
                    var expected = item.Patch.OriginalBytes.ToArray();
                    var current = Read(handle, (nint)item.Patch.Address, item.Patch.TargetBytes.Length);
                    Array.Copy(item.Patch.TargetBytes, 0, expected, 0, item.BytesWritten);
                    if (!current.SequenceEqual(expected))
                    { rollbackErrors.Add(item.Patch.Field + " changed after partial write"); continue; }
                    if (!Write(handle, (nint)item.Patch.Address, item.Patch.OriginalBytes)
                        || !Read(handle, (nint)item.Patch.Address, item.Patch.OriginalBytes.Length).SequenceEqual(item.Patch.OriginalBytes))
                        rollbackErrors.Add(item.Patch.Field + " rollback/read-back failed");
                }
                catch (Exception ex) { rollbackErrors.Add(item.Patch.Field + " rollback error: " + ex.GetType().Name); }
            }
            throw new InvalidOperationException(failure.Message +
                (rollbackErrors.Count == 0 ? " Previously written tactical values were rolled back." :
                    " ATTENTION: rollback needs inspection: " + string.Join("; ", rollbackErrors)), failure);
        }
    }

    private static bool SamePatchAddresses(IReadOnlyList<TacticalPatch> left, IReadOnlyList<TacticalPatch> right)
        => left.Count == right.Count && left.Select(x => x.Address).SequenceEqual(right.Select(x => x.Address));

    private static void AddPatch(List<TacticalPatch> patches, ulong address, string field, byte[] original, byte[] target)
    {
        if (original.Length != target.Length) throw new InvalidOperationException("Tactical patch lengths do not match.");
        if (original.Length > 0) patches.Add(new TacticalPatch(address, field, original.ToArray(), target.ToArray()));
    }

    private static ulong FindCurrentTactic(nint handle, ulong moduleBase, Fm27BuildProfile profile, uint teamId)
    {
        var manager = U64(handle, moduleBase + profile.TacticsManagerRva);
        if (!Pointer(manager)) throw new InvalidOperationException("FM27's tactics manager pointer is not valid.");
        var first = U64(handle, manager + TacticMapManagersOffset);
        var last = U64(handle, manager + TacticMapManagersOffset + 8);
        if (!Pointer(first) || last < first || last - first > 8 * MaxManagers)
            throw new InvalidOperationException("FM27's tactics-manager list failed validation.");
        for (var at = first; at < last; at += 8)
        {
            var managerEntry = U64(handle, at);
            if (!Pointer(managerEntry)) continue;
            var node = FindMsvcNode(handle, managerEntry + TacticTeamMapOffset, TacticMapKeyOffset, teamId);
            if (!Pointer(node)) continue;
            var value = node + TacticMapValueOffset;
            var listBegin = U64(handle, value + TacticListOffset);
            var listEnd = U64(handle, value + TacticListOffset + 8);
            var current = unchecked((sbyte)Read(handle, (nint)(value + TacticCurrentOffset), 1)[0]);
            var count = Pointer(listBegin) && listEnd >= listBegin ? (listEnd - listBegin) / 8 : 0;
            if (current < 0 || (ulong)current >= Math.Min(count, 8))
                throw new InvalidOperationException("The selected team's active tactic index is outside the verified tactic list.");
            var block = U64(handle, listBegin + (ulong)current * 8);
            if (!Pointer(block)) throw new InvalidOperationException("The active tactic block is not a valid FM object.");
            return block;
        }
        throw new InvalidOperationException("The selected player's team has no active tactic registered to the human manager.");
    }

    private static ulong FindTrainingKeeper(nint handle, ulong moduleBase, Fm27BuildProfile profile, ulong club)
    {
        var manager = U64(handle, moduleBase + profile.TrainingManagerRva);
        if (!Pointer(manager)) throw new InvalidOperationException("FM27's training manager pointer is not valid.");
        var first = U64(handle, manager + TrainingManagersOffset);
        var last = U64(handle, manager + TrainingManagersOffset + 8);
        if (!Pointer(first) || last < first || last - first > 8 * MaxManagers)
            throw new InvalidOperationException("FM27's training-manager list failed validation.");
        for (var at = first; at < last; at += 8)
        {
            var keeper = U64(handle, at);
            if (Pointer(keeper) && U64(handle, keeper + TrainingClubOffset) == club) return keeper;
        }
        return 0;
    }

    private static ulong FindHistoryNode(nint handle, ulong keeper, uint personId)
        => FindMsvcNode(handle, keeper + TrainingPlayersMapOffset, TrainingNodePersonOffset, personId);

    private static ulong FindMsvcNode(nint handle, ulong mapHeader, ulong keyOffset, uint wanted)
    {
        var head = U64(handle, mapHeader);
        if (!Pointer(head)) return 0;
        var node = U64(handle, head + 8);
        var stack = new Stack<ulong>();
        if (Pointer(node)) stack.Push(node);
        var visited = 0;
        while (stack.Count > 0 && visited++ < MaxTreeNodes)
        {
            var current = stack.Pop();
            if (!Pointer(current) || current == head || Read(handle, (nint)(current + 0x19), 1)[0] != 0) continue;
            var key = BitConverter.ToUInt32(Read(handle, (nint)(current + keyOffset), 4), 0);
            if (key == wanted) return current;
            var left = U64(handle, current);
            var right = U64(handle, current + 0x10);
            if (Pointer(left) && left != head) stack.Push(left);
            if (Pointer(right) && right != head) stack.Push(right);
        }
        if (visited >= MaxTreeNodes) throw new InvalidOperationException("FM27's internal tactical map exceeded the validated node limit.");
        return 0;
    }

    private static TacticalList ReadTacticalList(nint handle, ulong header, int entrySize, int maximum)
    {
        var bytes = Read(handle, (nint)header, 24);
        var begin = BitConverter.ToUInt64(bytes, 0);
        var end = BitConverter.ToUInt64(bytes, 8);
        var capacity = BitConverter.ToUInt64(bytes, 16);
        if (begin == 0 && end == 0 && capacity == 0)
            return new TacticalList(header, 0, 0, 0, Array.Empty<byte>());
        if (!Pointer(begin) || end < begin || capacity < end || (end - begin) % (ulong)entrySize != 0
            || (capacity - begin) % (ulong)entrySize != 0 || (end - begin) / (ulong)entrySize > (ulong)maximum)
            throw new InvalidOperationException($"An FM27 tactical list at 0x{header:X} failed its pointer, capacity or entry-count checks.");
        return new TacticalList(header, begin, end, capacity, Read(handle, (nint)begin, checked((int)(end - begin))));
    }

    private static int FindOptionEntry(byte[] bytes, int option)
    {
        for (var i = 0; i + 4 <= bytes.Length; i += 4)
            if (DecodeOptionCode(bytes.AsSpan(i, 4)) == option) return i / 4;
        return -1;
    }

    private static int DecodeOptionCode(ReadOnlySpan<byte> entry)
    {
        var packed = (uint)(entry[0] | entry[1] << 8 | entry[2] << 16);
        return (int)((packed << 12) >> 26);
    }

    private static void SetEntryValue(Span<byte> entry, ushort value)
    {
        var old = BinaryPrimitives.ReadUInt16LittleEndian(entry[..2]);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[..2], (ushort)((old & ~0x3FFF) | value));
    }

    private static ushort[]? ReadShapeIds(byte[] history)
    {
        ushort? inPossession = null, outPossession = null;
        ushort inDay = 0, outDay = 0;
        for (var i = 0; i + HistoryEntrySize <= history.Length; i += HistoryEntrySize)
        {
            var area = history[i + 4];
            if (history[i + 8] != 1 || area is not (6 or 7)) continue;
            var day = BinaryPrimitives.ReadUInt16LittleEndian(history.AsSpan(i, 2));
            var id = BinaryPrimitives.ReadUInt16LittleEndian(history.AsSpan(i + 2, 2));
            if (area == 6 && (!inPossession.HasValue || day >= inDay)) { inPossession = id; inDay = day; }
            if (area == 7 && (!outPossession.HasValue || day >= outDay)) { outPossession = id; outDay = day; }
        }
        return inPossession.HasValue && outPossession.HasValue ? new[] { inPossession.Value, outPossession.Value } : null;
    }

    private static byte[] BuildMaxTacticalHistory(byte[] options, ushort[] shapeIds, byte[] oldHistory, ushort today)
    {
        var oldCount = oldHistory.Length / HistoryEntrySize;
        var count = Math.Max(oldCount, EntriesPerArea * 8);
        var result = new byte[count * HistoryEntrySize];
        for (var i = 0; i < count; i++)
        {
            var area = i % 8;
            var offset = i * HistoryEntrySize;
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset, 2), today);
            if (area < 6)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 2, 2), OptionTag);
                result[offset + 6] = options[area];
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 2, 2), shapeIds[area - 6]);
                result[offset + 6] = 0xFF;
                result[offset + 8] = 1;
            }
            result[offset + 4] = (byte)area;
            result[offset + 9] = 20;
            result[offset + 10] = 20;
            result[offset + 11] = 156;
        }
        return result;
    }

    private static byte[] TacticalAreaOptions(byte[] instructions)
    {
        var first = BitConverter.ToUInt64(instructions, 0);
        var second = BitConverter.ToUInt64(instructions, 8);
        static bool Bit(ulong word, int n) => ((word >> n) & 1) != 0;
        static int Shift(ulong word, params (int Bit, int Amount)[] shifts)
        {
            foreach (var item in shifts) if (Bit(word, item.Bit)) return item.Amount;
            return 0;
        }
        var line = Bit(first, 61) ? 17 : Bit(first, 63) ? 3 : 10;
        line += Shift(first, (56, 3), (57, 2), (58, -2), (59, -3));
        var intensity = Bit(second, 1) ? 17 : Bit(second, 2) ? 14 : Bit(second, 3) ? 6 : Bit(second, 4) ? 3 : 10;
        intensity += Shift(second, (6, 3), (7, -3));
        var area2 = Bit(second, 16) ? 20 : Bit(second, 17) ? 1 : 10;
        var area3 = (Bit(first, 1) ? 17 : Bit(first, 2) ? 14 : Bit(first, 3) ? 6 : Bit(first, 4) ? 3 : 10)
            + Shift(first, (46, 3), (47, 2), (48, -2), (49, -3));
        var area4 = (Bit(first, 7) ? 17 : Bit(first, 8) ? 3 : 10) + Shift(first, (44, 3), (45, -3));
        var area5 = (Bit(first, 9) ? 13 : Bit(first, 17) ? 7 : 10)
            + Shift(first, (15, 2), (16, -2)) + Shift(first, (5, 2), (6, -2)) + Shift(first, (54, 3), (55, -3));
        return new[] { (byte)Math.Max(1, line), (byte)Math.Max(1, intensity), (byte)area2,
            (byte)Math.Max(1, area3), (byte)Math.Max(1, area4), (byte)Math.Max(1, area5) };
    }

    private static ushort ReadGameDay(nint handle, ulong moduleBase, Fm27BuildProfile profile)
    {
        if (profile.CurrentDateRva == 0 || profile.CurrentDateRva >= profile.SizeOfImage)
            throw new InvalidOperationException("This FM27 build has no verified in-game date field for tactical training history.");
        var bytes = Read(handle, (nint)(moduleBase + profile.CurrentDateRva), 4);
        var day = (ushort)(BitConverter.ToUInt16(bytes, 0) & 0x01FF);
        var year = BitConverter.ToUInt16(bytes, 2);
        if (year is < 1900 or > 2200 || day < 1 || day > (DateTime.IsLeapYear(year) ? 366 : 365))
            throw new InvalidOperationException("FM27's current date did not pass the tactical history date check.");
        return checked((ushort)(day - 1));
    }
}
