using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FmAtlas.Services;

/// <summary>
/// Guarded FM27 live-memory player-field preview/writer. This is an independent implementation;
/// it does not embed or copy another editor's code. It only supports an exact known
/// Windows FM27 module profile and a player whose UID and name can be re-resolved.
/// </summary>
public static class LiveMemoryEditService
{
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessVmWrite = 0x0020;
    private const uint ProcessQueryInformation = 0x0400;
    private const ulong PlayerUidOffset = 0x294;
    private const ulong PlayerCommonNameOffset = 0x2E8;
    private const ulong PlayerFirstNameOffset = 0x2D8;
    private const ulong PlayerLastNameOffset = 0x2E0;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint process, nint baseAddress, [Out] byte[] buffer, nuint size, out nuint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(nint process, nint baseAddress, byte[] buffer, nuint size, out nuint bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    public sealed record Change(string FieldKey, int ExpectedCurrentValue, int TargetValue);
    public sealed record PlannedChange(string FieldKey, int Offset, int ExpectedCurrentValue, int TargetValue, byte[] OriginalBytes, byte[] TargetBytes);
    public sealed record BatchPlan(int ProcessId, uint PlayerUid, string PlayerName, string ProfileId, string ProfileFingerprint, ulong SaveLoadCounter, DateTime CreatedUtc, IReadOnlyList<PlannedChange> Changes);
    public sealed record BatchReceipt(BatchPlan Plan, DateTime AppliedUtc, bool ReadBackVerified);

    // V10 is the first controlled, read-only acceptance build. Keep this hard gate false
    // until exact-build field witnesses and disposable-save rollback checks are signed off.
    private const bool AllowLiveWritesForThisBuild = false;
    private const string LiveWritesDisabledMessage = "Live-memory Apply/Undo is hard-disabled in this FM Atlas V10 pre-testing build. The FM connection is read-only; do not attempt live edits.";
    public static bool LiveWritesEnabled => AllowLiveWritesForThisBuild;

    private static void EnsureLiveWritesEnabled()
    {
        if (!LiveWritesEnabled) throw new InvalidOperationException(LiveWritesDisabledMessage);
    }

    public static int FindSupportedProcessId()
    {
        var matching = new List<int>();
        foreach (var process in Process.GetProcessesByName("fm"))
        {
            try
            {
                var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(x =>
                    string.Equals(Path.GetFileName(x.ModuleName), "game_plugin.dll", StringComparison.OrdinalIgnoreCase));
                if (module is null) continue;
                using var handle = new ProcessModuleHandle(OpenProcess(ProcessQueryInformation | ProcessVmRead, false, process.Id));
                if (handle.Value == 0) continue;
                var profile = ReadMatchedProfile(handle.Value, (ulong)module.BaseAddress.ToInt64());
                if (profile is not null) matching.Add(process.Id);
            }
            catch { }
            finally { process.Dispose(); }
        }
        if (matching.Count == 0) throw new InvalidOperationException("No running FM27 process matched one of Atlas's exact Steam or Game Pass profiles. Load a career and check the game has finished starting.");
        if (matching.Count > 1) throw new InvalidOperationException("More than one supported FM27 process is running. Close the duplicate game instance so Atlas cannot edit the wrong process.");
        return matching[0];
    }

    public static BatchPlan StageBatch(int processId, uint playerUid, string expectedPlayerName, IReadOnlyList<Change> changes)
    {
        if (playerUid == 0) throw new ArgumentOutOfRangeException(nameof(playerUid));
        if (string.IsNullOrWhiteSpace(expectedPlayerName)) throw new ArgumentException("A player name is required for identity verification.", nameof(expectedPlayerName));
        if (changes.Count == 0) throw new ArgumentException("No live changes were supplied.", nameof(changes));
        if (changes.Count > 200) throw new ArgumentOutOfRangeException(nameof(changes), "A single staged player batch is limited to 200 fields.");
        var duplicate = changes.GroupBy(x => x.FieldKey, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"The field {duplicate.Key} appears more than once in this edit batch.");

        using var process = Process.GetProcessById(processId);
        var module = FindGameModule(process);
        using var handle = new ProcessModuleHandle(OpenProcess(ProcessQueryInformation | ProcessVmRead, false, processId));
        if (handle.Value == 0) throw new InvalidOperationException($"Windows denied read-only access to the FM process (error {Marshal.GetLastWin32Error()}).");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadMatchedProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("The running game build is not in Atlas's exact FM27 profile registry. Live editing is blocked.");
        if (profile.GameUiRva == 0) throw new InvalidOperationException("This game build has no verified save-load generation pointer in Atlas's profile. Live writing is disabled for this profile.");
        var saveLoadCounter = ReadSaveLoadCounter(handle.Value, moduleBase, profile);
        var playerAddress = ResolvePlayer(handle.Value, moduleBase, profile, playerUid, expectedPlayerName);
        var planRows = new List<PlannedChange>();
        foreach (var change in changes)
        {
            var field = ResolveWritableField(change.FieldKey);
            ValidateValue(field, change.ExpectedCurrentValue, change.TargetValue);
            var current = Read(handle.Value, (nint)(playerAddress + (ulong)field.Offset), field.Size);
            var displayed = DecodeValue(field, current);
            if (displayed != change.ExpectedCurrentValue)
                throw new InvalidOperationException($"Live preview blocked for {field.DisplayName}: Atlas expected {change.ExpectedCurrentValue}, but the running FM memory currently decodes to {displayed}. Refresh the player and verify the in-game value before trying again.");
            var target = EncodeValue(field, change.TargetValue);
            planRows.Add(new PlannedChange(field.Key, field.Offset, displayed, change.TargetValue, current, target));
        }
        return new BatchPlan(processId, playerUid, expectedPlayerName, profile.Id, profile.Fingerprint, saveLoadCounter, DateTime.UtcNow, planRows);
    }

    public static BatchReceipt ApplyBatch(BatchPlan plan)
    {
        EnsureLiveWritesEnabled();
        if (DateTime.UtcNow - plan.CreatedUtc > TimeSpan.FromMinutes(2) || plan.CreatedUtc > DateTime.UtcNow.AddSeconds(10))
            throw new InvalidOperationException("This staged live-edit preview has expired. Stage a fresh preview before writing.");
        using var process = Process.GetProcessById(plan.ProcessId);
        var module = FindGameModule(process);
        using var handle = new ProcessModuleHandle(OpenProcess(ProcessQueryInformation | ProcessVmRead | ProcessVmWrite | ProcessVmOperation, false, plan.ProcessId));
        if (handle.Value == 0) throw new InvalidOperationException($"Windows denied live-write access to Football Manager (error {Marshal.GetLastWin32Error()}). Run Atlas and FM at the same privilege level. No changes were made.");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadMatchedProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("FM27's exact build profile no longer matches. Live changes were not applied.");
        if (profile.Id != plan.ProfileId || profile.Fingerprint != plan.ProfileFingerprint)
            throw new InvalidOperationException("The running game build changed after the preview. The staged edit has expired.");
        if (profile.GameUiRva == 0 || ReadSaveLoadCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
            throw new InvalidOperationException("Football Manager loaded or unloaded a career after the preview. No live values were written; reload the live player data and stage again.");
        var playerAddress = ResolvePlayer(handle.Value, moduleBase, profile, plan.PlayerUid, plan.PlayerName);

        // Preflight every field before the first write, so a changed/stale player causes a full abort.
        foreach (var change in plan.Changes)
        {
            var field = ResolveWritableField(change.FieldKey);
            if (field.Offset != change.Offset || change.OriginalBytes.Length != field.Size || change.TargetBytes.Length != field.Size)
                throw new InvalidOperationException($"Staged edit integrity check failed for {change.FieldKey}; the field mapping no longer matches the plan.");
            ValidateValue(field, change.ExpectedCurrentValue, change.TargetValue);
            var current = Read(handle.Value, (nint)(playerAddress + (ulong)change.Offset), change.OriginalBytes.Length);
            if (!current.SequenceEqual(change.OriginalBytes))
                throw new InvalidOperationException($"Live write blocked: {change.FieldKey} changed after preview. No fields in this batch were written.");
        }

        var written = new List<(PlannedChange Change, int BytesWritten)>();
        try
        {
            foreach (var change in plan.Changes)
            {
                // Re-check the game session immediately before each write. If FM switched saves
                // between fields, never continue writing into the new session's address space.
                if (ReadSaveLoadCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
                    throw new InvalidOperationException("The FM career changed during this edit batch. Atlas stopped before the next field; automatic rollback will only be attempted if the original career session is still active.");
                var address = (nint)(playerAddress + (ulong)change.Offset);
                var succeeded = Write(handle.Value, address, change.TargetBytes, out var bytesWritten);
                if (bytesWritten > 0) written.Add((change, bytesWritten));
                if (!succeeded || bytesWritten != change.TargetBytes.Length)
                    throw new InvalidOperationException($"Windows rejected or partially completed the write for {change.FieldKey} ({bytesWritten}/{change.TargetBytes.Length} bytes; Win32 {Marshal.GetLastWin32Error()}).");
                var readBack = Read(handle.Value, address, change.TargetBytes.Length);
                if (!readBack.SequenceEqual(change.TargetBytes))
                    throw new InvalidOperationException($"Read-back verification failed for {change.FieldKey}.");
            }
        }
        catch (Exception failure)
        {
            var rollbackErrors = new List<string>();
            foreach (var writtenItem in written.AsEnumerable().Reverse())
            {
                var change = writtenItem.Change;
                try
                {
                    if (ReadSaveLoadCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
                    {
                        rollbackErrors.Add(change.FieldKey + " rollback skipped because the FM career session changed");
                        continue;
                    }
                    var address = (nint)(playerAddress + (ulong)change.Offset);
                    var expectedPartial = change.OriginalBytes.ToArray();
                    Array.Copy(change.TargetBytes, 0, expectedPartial, 0, writtenItem.BytesWritten);
                    var current = Read(handle.Value, address, change.TargetBytes.Length);
                    if (!current.SequenceEqual(expectedPartial)) { rollbackErrors.Add(change.FieldKey + " changed again; rollback skipped"); continue; }
                    if (!Write(handle.Value, address, change.OriginalBytes) || !Read(handle.Value, address, change.OriginalBytes.Length).SequenceEqual(change.OriginalBytes))
                        rollbackErrors.Add(change.FieldKey + " rollback/read-back failed");
                }
                catch (Exception rollback) { rollbackErrors.Add(change.FieldKey + " rollback error: " + rollback.GetType().Name); }
            }
            var suffix = rollbackErrors.Count == 0 ? " Any partial or earlier writes in the batch were rolled back and read-back checked." : " ATTENTION: rollback needs inspection: " + string.Join("; ", rollbackErrors);
            throw new InvalidOperationException(failure.Message + suffix, failure);
        }
        return new BatchReceipt(plan, DateTime.UtcNow, true);
    }

    public static void UndoBatch(BatchReceipt receipt)
    {
        EnsureLiveWritesEnabled();
        var plan = receipt.Plan;
        using var process = Process.GetProcessById(plan.ProcessId);
        var module = FindGameModule(process);
        using var handle = new ProcessModuleHandle(OpenProcess(ProcessQueryInformation | ProcessVmRead | ProcessVmWrite | ProcessVmOperation, false, plan.ProcessId));
        if (handle.Value == 0) throw new InvalidOperationException($"Windows denied live-write access during undo (error {Marshal.GetLastWin32Error()}).");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadMatchedProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("FM27's build profile no longer matches; live undo is blocked.");
        if (profile.Id != plan.ProfileId || profile.Fingerprint != plan.ProfileFingerprint)
            throw new InvalidOperationException("The game build changed after the edit; live undo is blocked.");
        if (profile.GameUiRva == 0 || ReadSaveLoadCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
            throw new InvalidOperationException("Football Manager has loaded or unloaded a career since this edit. Live undo is blocked to protect the new save.");
        var playerAddress = ResolvePlayer(handle.Value, moduleBase, profile, plan.PlayerUid, plan.PlayerName);
        foreach (var change in plan.Changes)
        {
            var field = ResolveWritableField(change.FieldKey);
            if (field.Offset != change.Offset || field.Size != change.TargetBytes.Length || field.Size != change.OriginalBytes.Length)
                throw new InvalidOperationException($"Undo integrity check failed for {change.FieldKey}.");
            var current = Read(handle.Value, (nint)(playerAddress + (ulong)change.Offset), change.TargetBytes.Length);
            if (!current.SequenceEqual(change.TargetBytes))
                throw new InvalidOperationException($"Undo blocked: {change.FieldKey} has changed again in FM. Refresh the player and inspect the edit history before attempting another undo.");
        }
        var undone = new List<(PlannedChange Change, int BytesWritten)>();
        try
        {
            foreach (var change in plan.Changes.AsEnumerable().Reverse())
            {
                if (ReadSaveLoadCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
                    throw new InvalidOperationException("The FM career changed during undo. Atlas stopped before the next field; rollback will only be attempted while the original career session remains active.");
                var address = (nint)(playerAddress + (ulong)change.Offset);
                var succeeded = Write(handle.Value, address, change.OriginalBytes, out var bytesWritten);
                if (bytesWritten > 0) undone.Add((change, bytesWritten));
                if (!succeeded || bytesWritten != change.OriginalBytes.Length)
                    throw new InvalidOperationException($"Windows rejected or partially completed undo for {change.FieldKey} ({bytesWritten}/{change.OriginalBytes.Length} bytes; Win32 {Marshal.GetLastWin32Error()}).");
                if (!Read(handle.Value, address, change.OriginalBytes.Length).SequenceEqual(change.OriginalBytes))
                    throw new InvalidOperationException($"Undo read-back failed for {change.FieldKey}.");
            }
        }
        catch (Exception failure)
        {
            var rollbackErrors = new List<string>();
            foreach (var undoneItem in undone.AsEnumerable().Reverse())
            {
                var change = undoneItem.Change;
                try
                {
                    if (ReadSaveLoadCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
                    {
                        rollbackErrors.Add(change.FieldKey + " restore skipped because the FM career session changed");
                        continue;
                    }
                    var address = (nint)(playerAddress + (ulong)change.Offset);
                    var expectedPartial = change.TargetBytes.ToArray();
                    Array.Copy(change.OriginalBytes, 0, expectedPartial, 0, undoneItem.BytesWritten);
                    var current = Read(handle.Value, address, change.OriginalBytes.Length);
                    if (!current.SequenceEqual(expectedPartial)) { rollbackErrors.Add(change.FieldKey + " changed again; restore skipped"); continue; }
                    if (!Write(handle.Value, address, change.TargetBytes) || !Read(handle.Value, address, change.TargetBytes.Length).SequenceEqual(change.TargetBytes))
                        rollbackErrors.Add(change.FieldKey + " restore/read-back failed");
                }
                catch (Exception rollback) { rollbackErrors.Add(change.FieldKey + " restore error: " + rollback.GetType().Name); }
            }
            var suffix = rollbackErrors.Count == 0 ? " The partial undo was rolled forward to the edited values." : " ATTENTION: undo rollback needs inspection: " + string.Join("; ", rollbackErrors);
            throw new InvalidOperationException(failure.Message + suffix, failure);
        }
    }

    private enum ValueEncoding { ScaledAttribute, DirectByte, UInt8, UInt16, Int16, Int32 }
    private sealed record WritableField(
        string Key, string DisplayName, int Offset, int Size,
        ValueEncoding Encoding, int Minimum, int Maximum);

    /// <summary>Returns whether a player field has a defined, range-checked live preview mapping. Apply/Undo remains separately gated.</summary>
    public static bool SupportsPlayerFieldPreview(string fieldKey)
    {
        try { _ = ResolveWritableField(fieldKey); return true; }
        catch (InvalidOperationException) { return false; }
    }

    private static WritableField ResolveWritableField(string key)
    {
        var normalized = key.Trim();
        if (normalized.StartsWith("attributes.", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["attributes.".Length..];
        if (normalized.StartsWith("positions.", StringComparison.OrdinalIgnoreCase))
            normalized = "position." + normalized["positions.".Length..];
        if (normalized.Equals("ability.current_ability", StringComparison.OrdinalIgnoreCase) || normalized.Equals("CurrentAbility", StringComparison.OrdinalIgnoreCase))
            return new WritableField("ability.current_ability", "Current Ability", 0x264, 2, ValueEncoding.UInt16, 1, 200);
        if (normalized.Equals("ability.potential_ability", StringComparison.OrdinalIgnoreCase) || normalized.Equals("PotentialAbility", StringComparison.OrdinalIgnoreCase))
            return new WritableField("ability.potential_ability", "Potential Ability", 0x266, 2, ValueEncoding.Int16, 1, 200);

        var attribute = PlayerAttributeSchema.Fields.FirstOrDefault(x => x.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (attribute is not null)
        {
            if (string.Equals(attribute.Category, "Hidden", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{attribute.Label} remains searchable when imported, but its FM27 live-memory offset is still an inherited lead rather than an independently witnessed mapping. Atlas will not stage or write it.");
            if (attribute.Offset >= 0x288) throw new InvalidOperationException("This field lies outside the standard player block and is not supported by the player live editor.");
            return new WritableField(attribute.Key, attribute.Label, attribute.Offset, 1, ValueEncoding.ScaledAttribute, 1, 20);
        }

        // FM27's 15 direct 1–20 position ratings. LibreEditor deliberately leaves
        // Sweeper out: the game no longer uses that position and its rating is always 1.
        var positions = new[] { "gk", "sw", "dl", "dc", "dr", "dm", "ml", "mc", "mr", "aml", "amc", "amr", "st", "wbl", "wbr" };
        var positionIndex = Array.FindIndex(positions, x => x.Equals(normalized.StartsWith("position.", StringComparison.OrdinalIgnoreCase)
            ? normalized["position.".Length..] : normalized, StringComparison.OrdinalIgnoreCase));
        if (positionIndex >= 0 && !positions[positionIndex].Equals("sw", StringComparison.OrdinalIgnoreCase))
        {
            var shortName = positions[positionIndex];
            return new WritableField("position." + shortName, "Position " + shortName.ToUpperInvariant(),
                0x150 + positionIndex, 1, ValueEncoding.DirectByte, 1, 20);
        }

        var personality = PlayerAttributeSchema.PersonalityFields.FirstOrDefault(x =>
            normalized.Equals("personality." + x.Key, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(x.Key, StringComparison.OrdinalIgnoreCase));
        if (personality is not null)
            return new WritableField("personality." + personality.Key, personality.Label,
                personality.Offset, 1, ValueEncoding.DirectByte, 1, 20);

        if (normalized.Equals("left_foot", StringComparison.OrdinalIgnoreCase))
            return new WritableField("left_foot", "Left Foot", 0x177, 1, ValueEncoding.DirectByte, 1, 20);
        if (normalized.Equals("right_foot", StringComparison.OrdinalIgnoreCase))
            return new WritableField("right_foot", "Right Foot", 0x178, 1, ValueEncoding.DirectByte, 1, 20);

        // Additional scalar player fields explicitly represented in the FM27 Libre
        // layout and edit schema. Dynamic pointers/collections are intentionally excluded.
        var scalarFields = new Dictionary<string, WritableField>(StringComparer.OrdinalIgnoreCase)
        {
            ["height"] = new("height", "Height (cm)", 0x22E, 2, ValueEncoding.UInt16, 140, 220),
            ["asking_price"] = new("asking_price", "Asking Price (GBP)", 0x238, 4, ValueEncoding.Int32, 0, 2_000_000_000),
            ["condition"] = new("condition", "Condition (0–10000)", 0x25C, 2, ValueEncoding.UInt16, 0, 10_000),
            ["match_sharpness"] = new("match_sharpness", "Match Sharpness (0–10000)", 0x258, 2, ValueEncoding.UInt16, 3_500, 10_000),
            ["jadedness"] = new("jadedness", "Jadedness", 0x25A, 2, ValueEncoding.Int16, -500, 1_000),
            ["morale"] = new("morale", "Morale", 0x26C, 1, ValueEncoding.UInt8, 1, 20),
            ["reputation.home"] = new("reputation.home", "Home Reputation", 0x25E, 2, ValueEncoding.UInt16, 0, 10_000),
            ["reputation.current"] = new("reputation.current", "Current Reputation", 0x260, 2, ValueEncoding.UInt16, 0, 10_000),
            ["reputation.world"] = new("reputation.world", "World Reputation", 0x262, 2, ValueEncoding.UInt16, 0, 10_000),
            ["international.caps"] = new("international.caps", "International Caps", 0x3BC, 2, ValueEncoding.UInt16, 0, 1_000),
            ["international.goals"] = new("international.goals", "International Goals", 0x3BE, 2, ValueEncoding.UInt16, 0, 1_000),
            ["international.u21_caps"] = new("international.u21_caps", "U21 Caps", 0x3C0, 1, ValueEncoding.UInt8, 0, 255),
            ["international.u21_goals"] = new("international.u21_goals", "U21 Goals", 0x3C1, 1, ValueEncoding.UInt8, 0, 255)
        };
        if (scalarFields.TryGetValue(normalized, out var scalar)) return scalar;

        throw new InvalidOperationException($"{key} is not in Atlas's verified player live-edit staging catalogue. Pointer-backed fields, contracts, staff, club, tactics and collection changes need their own object resolution and structural checks.");
    }

    private static void ValidateValue(WritableField field, int current, int target)
    {
        if (current < field.Minimum || current > field.Maximum || target < field.Minimum || target > field.Maximum)
            throw new ArgumentOutOfRangeException(nameof(target),
                $"{field.DisplayName} must be between {field.Minimum:N0} and {field.Maximum:N0} in this field's FM27 storage units.");
    }

    private static int DecodeValue(WritableField field, byte[] bytes)
    {
        return field.Encoding switch
        {
            ValueEncoding.ScaledAttribute => (bytes[0] <= 98 ? (bytes[0] + 2) / 5
                : throw new InvalidOperationException($"{field.DisplayName} raw byte {bytes[0]} is outside the FM 1–20 attribute encoding range.")),
            ValueEncoding.DirectByte or ValueEncoding.UInt8 => bytes[0],
            ValueEncoding.UInt16 => BitConverter.ToUInt16(bytes, 0),
            ValueEncoding.Int16 => BitConverter.ToInt16(bytes, 0),
            ValueEncoding.Int32 => BitConverter.ToInt32(bytes, 0),
            _ => throw new InvalidOperationException("Unsupported field encoding.")
        };
    }

    private static byte[] EncodeValue(WritableField field, int value)
    {
        return field.Encoding switch
        {
            ValueEncoding.ScaledAttribute => new[] { checked((byte)(value * 5 - 2)) },
            ValueEncoding.DirectByte or ValueEncoding.UInt8 => new[] { checked((byte)value) },
            ValueEncoding.UInt16 => BitConverter.GetBytes(checked((ushort)value)),
            ValueEncoding.Int16 => BitConverter.GetBytes(checked((short)value)),
            ValueEncoding.Int32 => BitConverter.GetBytes(value),
            _ => throw new InvalidOperationException("Unsupported field encoding.")
        };
    }

    private static ProcessModule FindGameModule(Process process)
        => process.Modules.Cast<ProcessModule>().FirstOrDefault(x => string.Equals(Path.GetFileName(x.ModuleName), "game_plugin.dll", StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException("game_plugin.dll is not visible in this FM process. Load the career and try again.");

    private static Fm27BuildProfile? ReadMatchedProfile(nint handle, ulong moduleBase)
    {
        var pe = Read(handle, (nint)moduleBase, 0x40);
        if (pe[0] != 'M' || pe[1] != 'Z') return null;
        var peOffset = BitConverter.ToInt32(pe, 0x3C);
        if (peOffset < 0x40 || peOffset > 0x1000) return null;
        var nt = Read(handle, (nint)(moduleBase + (ulong)peOffset), 24);
        if (nt[0] != 'P' || nt[1] != 'E' || BitConverter.ToUInt16(nt, 4) != 0x8664) return null;
        var timestamp = BitConverter.ToUInt32(nt, 8);
        var optionalSize = BitConverter.ToUInt16(nt, 20);
        var optional = Read(handle, (nint)(moduleBase + (ulong)peOffset + 24), optionalSize);
        return Fm27BuildProfiles.Match(BitConverter.ToUInt32(optional, 56), timestamp);
    }

    private static ulong ReadSaveLoadCounter(nint handle, ulong moduleBase, Fm27BuildProfile profile)
    {
        if (profile.GameUiRva == 0) throw new InvalidOperationException("No verified save-load counter is available for this game build.");
        if (profile.GameUiRva >= profile.SizeOfImage) throw new InvalidOperationException("The game UI profile address lies outside the supported module image.");
        var gameUi = U64(handle, moduleBase + profile.GameUiRva);
        if (!Pointer(gameUi)) throw new InvalidOperationException("The FM game-session identity object could not be read. Live writes are blocked.");
        return U64(handle, gameUi + Fm27BuildProfile.SaveLoadCounterOffset);
    }

    private static ulong ResolvePlayer(nint handle, ulong moduleBase, Fm27BuildProfile profile, uint uid, string expectedName)
    {
        var data = U64(handle, moduleBase + profile.PlayersArrayRva);
        var end = U64(handle, moduleBase + profile.PlayersArrayRva + 8);
        if (!Pointer(data) || !Pointer(end) || end <= data || (end - data) % 8 != 0)
            throw new InvalidOperationException("The current FM player-array bounds failed validation; live editing is blocked.");
        var count = (end - data) / 8;
        if (count < 20_000 || count > 200_000) throw new InvalidOperationException($"The player-array count {count:N0} is outside the expected safety range.");
        ulong found = 0; var matches = 0;
        for (ulong i = 0; i < count; i++)
        {
            var address = U64(handle, data + i * 8);
            if (!Pointer(address)) continue;
            uint candidateUid;
            try { candidateUid = BitConverter.ToUInt32(Read(handle, (nint)(address + PlayerUidOffset), 4), 0); }
            catch { continue; }
            if (candidateUid != uid) continue;
            var common = ReadNameSub(handle, address + PlayerCommonNameOffset);
            var first = ReadNameSub(handle, address + PlayerFirstNameOffset);
            var last = ReadNameSub(handle, address + PlayerLastNameOffset);
            var display = !string.IsNullOrWhiteSpace(common) ? common : Join(first, last);
            if (!string.Equals(display, expectedName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Player identity check failed for UID {uid}: the running game reports '{display ?? "unnamed"}', not '{expectedName}'. Reload the player list before editing.");
            found = address; matches++;
            if (matches > 1) break;
        }
        if (matches != 1) throw new InvalidOperationException(matches == 0
            ? $"Player UID {uid} was not found in the currently loaded FM save. Refresh the live player data."
            : $"Player UID {uid} appeared more than once; Atlas refuses an ambiguous edit.");
        return found;
    }

    private static string? ReadNameSub(nint handle, ulong pointerAddress)
    {
        try
        {
            var sub = U64(handle, pointerAddress); if (!Pointer(sub)) return null;
            var text = U64(handle, sub); if (!Pointer(text)) return null;
            var length = BitConverter.ToUInt32(Read(handle, (nint)text, 4), 0);
            if (length is 0 or > 64) return null;
            var value = Encoding.UTF8.GetString(Read(handle, (nint)(text + 4), (int)length));
            return value.Any(char.IsControl) ? null : value;
        }
        catch { return null; }
    }

    private static string? Join(string? first, string? last)
        => string.IsNullOrWhiteSpace(first) ? last : string.IsNullOrWhiteSpace(last) ? first : first + " " + last;

    private static byte[] Read(nint handle, nint address, int length)
    {
        var bytes = new byte[length];
        if (!ReadProcessMemory(handle, address, bytes, (nuint)length, out var read) || read != (nuint)length)
            throw new InvalidOperationException($"ReadProcessMemory failed or returned incomplete data (Win32 {Marshal.GetLastWin32Error()}).");
        return bytes;
    }

    private static ulong U64(nint handle, ulong address) => BitConverter.ToUInt64(Read(handle, (nint)address, 8), 0);

    private static bool Write(nint handle, nint address, byte[] bytes)
        => Write(handle, address, bytes, out _);

    private static bool Write(nint handle, nint address, byte[] bytes, out int bytesWritten)
    {
        var success = WriteProcessMemory(handle, address, bytes, (nuint)bytes.Length, out var written);
        bytesWritten = checked((int)(written > (nuint)bytes.Length ? (nuint)bytes.Length : written));
        return success && bytesWritten == bytes.Length;
    }

    private static bool Pointer(ulong value) => value >= 0x10000 && value <= 0x7FFF_FFFF_FFFF;

    private sealed class ProcessModuleHandle : IDisposable
    {
        public nint Value { get; }
        public ProcessModuleHandle(nint value) => Value = value;
        public void Dispose() { if (Value != 0) CloseHandle(Value); }
    }
}
