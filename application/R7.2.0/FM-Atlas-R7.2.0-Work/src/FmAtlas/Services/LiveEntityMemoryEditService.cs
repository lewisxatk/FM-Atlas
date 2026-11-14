using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FmAtlas.Services;

/// <summary>
/// Live scalar editor for FM27 player, staff and club objects. It writes only explicitly
/// mapped fields, after exact-build/session/UID/record checks, preflight and read-back.
/// Pointer/list operations are deliberately separate from this scalar writer.
/// </summary>
public static class LiveEntityMemoryEditService
{
    private const uint QueryInformation = 0x0400;
    private const uint VmRead = 0x0010;
    private const uint VmWrite = 0x0020;
    private const uint VmOperation = 0x0008;
    private const ulong SaveCounterOffset = 0x100;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inherit, int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint process, nint address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(nint process, nint address, byte[] buffer, nuint size, out nuint written);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    public enum EntityKind { Player, Staff, Club }
    public sealed record FieldDescriptor(string Key, string Label, int Minimum, int Maximum, string Storage);
    public sealed record TargetValue(string FieldKey, int Value);
    public sealed record PlannedValue(string FieldKey, ulong Address, int CurrentValue, int TargetValue, byte[] OriginalBytes, byte[] TargetBytes);
    public sealed record BatchPlan(
        int ProcessId, EntityKind Entity, uint Uid, string Name, ulong ObjectAddress, int IdentityShift,
        string ProfileId, string ProfileFingerprint, ulong SaveLoadCounter, DateTime CreatedUtc,
        IReadOnlyList<PlannedValue> Values);
    public sealed record BatchReceipt(BatchPlan Plan, DateTime AppliedUtc, bool ReadBackVerified);

    private enum Place { Object, Contract, SeniorTeam, ClubInfo, ClubFinance, Stadium }
    private enum EncodingKind { ScaledAttribute, DirectByte, UInt8, Int8, UInt16, Int16, UInt32, Int32, Date, Flag }
    private sealed record FieldSpec(
        string Key, string Label, EntityKind Entity, Place Place, ulong Offset, int Size,
        EncodingKind Encoding, int Minimum, int Maximum, int PlausibleMinimum, int PlausibleMaximum,
        bool Shifted = false, int Bit = -1, int BlankValue = int.MinValue,
        int[]? Choices = null);

    private sealed class Handle : IDisposable
    {
        public nint Value { get; }
        public Handle(nint value) => Value = value;
        public void Dispose() { if (Value != 0) CloseHandle(Value); }
    }

    public static bool IsAvailable => Fm27BuildProfiles.All.Any(x => x.GameUiRva != 0);

    public static IReadOnlyList<FieldDescriptor> GetEditableFields(EntityKind entity)
        => Fields(entity).Select(x => new FieldDescriptor(x.Key, x.Label, x.Minimum, x.Maximum, x.Encoding.ToString())).ToList();

    public static bool SupportsField(EntityKind entity, string key)
    {
        try { _ = ResolveField(entity, key); return true; }
        catch (InvalidOperationException) { return false; }
    }

    /// <summary>Finds the one running FM27 process matching an exact registered game_plugin build.</summary>
    public static int FindSupportedProcessId()
    {
        var matches = new List<int>();
        foreach (var process in Process.GetProcessesByName("fm"))
        {
            try
            {
                var module = FindModule(process);
                using var handle = new Handle(OpenProcess(QueryInformation | VmRead, false, process.Id));
                if (handle.Value == 0) continue;
                if (ReadProfile(handle.Value, (ulong)module.BaseAddress.ToInt64()) is { GameUiRva: not 0 })
                    matches.Add(process.Id);
            }
            catch { }
            finally { process.Dispose(); }
        }
        if (matches.Count == 0)
            throw new InvalidOperationException("No running FM27 process matched an exact supported Windows build profile. Load an offline career and let the game finish loading.");
        if (matches.Count != 1)
            throw new InvalidOperationException("More than one supported FM27 process is running. Close the duplicate instance to avoid editing the wrong career.");
        return matches[0];
    }

    /// <summary>Reads all currently mapped values for a selected entity. This does not write to FM.</summary>
    public static IReadOnlyDictionary<string, int> ReadCurrentValues(
        int processId, EntityKind entity, uint uid, string name, IReadOnlyCollection<string>? requestedFields = null)
    {
        using var process = Process.GetProcessById(processId);
        var module = FindModule(process);
        using var handle = new Handle(OpenProcess(QueryInformation | VmRead, false, processId));
        if (handle.Value == 0) throw Win32("read the FM process");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("The running FM27 build is not in Atlas's exact profile registry.");
        var counter = ReadSaveCounter(handle.Value, moduleBase, profile);
        var target = ResolveEntity(handle.Value, moduleBase, profile, entity, uid, name);
        var financeClass = entity == EntityKind.Club ? FindRecordClass(handle.Value, moduleBase, profile, Place.ClubFinance) : 0UL;
        var stadiumClass = entity == EntityKind.Club ? FindRecordClass(handle.Value, moduleBase, profile, Place.Stadium) : 0UL;
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var fields = requestedFields is null ? Fields(entity) : requestedFields.Select(x => ResolveField(entity, x)).ToList();
        foreach (var field in fields)
        {
            try
            {
                var address = ResolveFieldAddress(handle.Value, target, field, financeClass, stadiumClass);
                var bytes = Read(handle.Value, address, field.Size);
                var value = Decode(field, bytes);
                ValidateCurrent(field, value);
                result[field.Key] = value;
            }
            catch (InvalidOperationException)
            {
                // Optional records are field groups: a player without a contract, a
                // non-league club without full finances, or a club without a stadium
                // simply has no editable values in that group.
            }
        }
        if (ReadSaveCounter(handle.Value, moduleBase, profile) != counter)
            throw new InvalidOperationException("FM loaded or unloaded a career while Atlas was reading the entity. Refresh the live record and try again.");
        return result;
    }

    /// <summary>Builds a two-minute, read-only live edit plan. The target values are preflighted against current memory.</summary>
    public static BatchPlan StageBatch(
        int processId, EntityKind entity, uint uid, string name, IReadOnlyList<TargetValue> targets)
    {
        if (uid == 0) throw new ArgumentOutOfRangeException(nameof(uid));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An entity name is required for identity verification.", nameof(name));
        if (targets.Count == 0 || targets.Count > 300) throw new ArgumentOutOfRangeException(nameof(targets), "A batch must contain between 1 and 300 fields.");
        var duplicate = targets.GroupBy(x => x.FieldKey, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) throw new ArgumentException("The field " + duplicate.Key + " appears more than once in this batch.");

        using var process = Process.GetProcessById(processId);
        var module = FindModule(process);
        using var handle = new Handle(OpenProcess(QueryInformation | VmRead, false, processId));
        if (handle.Value == 0) throw Win32("read the FM process");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("The running FM27 build is not in Atlas's exact profile registry. Live editing is blocked.");
        var counter = ReadSaveCounter(handle.Value, moduleBase, profile);
        var resolved = ResolveEntity(handle.Value, moduleBase, profile, entity, uid, name);
        var financeClass = entity == EntityKind.Club ? FindRecordClass(handle.Value, moduleBase, profile, Place.ClubFinance) : 0UL;
        var stadiumClass = entity == EntityKind.Club ? FindRecordClass(handle.Value, moduleBase, profile, Place.Stadium) : 0UL;
        var planned = new List<PlannedValue>(targets.Count);

        foreach (var requested in targets)
        {
            var field = ResolveField(entity, requested.FieldKey);
            if (entity == EntityKind.Player && field.Key == "contract.squad_number")
                CheckSquadNumber(handle.Value, resolved, requested.Value);
            var address = ResolveFieldAddress(handle.Value, resolved, field, financeClass, stadiumClass);
            var original = Read(handle.Value, address, field.Size);
            var current = Decode(field, original);
            ValidateCurrent(field, current);
            ValidateTarget(field, requested.Value);
            var next = Encode(field, requested.Value, original);
            planned.Add(new PlannedValue(field.Key, address, current, requested.Value, original, next));
        }
        ValidateCrossFieldConstraints(handle.Value, resolved, planned, financeClass, stadiumClass);
        if (ReadSaveCounter(handle.Value, moduleBase, profile) != counter)
            throw new InvalidOperationException("FM loaded or unloaded a career during staging. No edit was prepared.");
        return new BatchPlan(processId, entity, uid, name, resolved.Address, resolved.Shift,
            profile.Id, profile.Fingerprint, counter, DateTime.UtcNow, planned);
    }

    /// <summary>Applies a staged batch only after a fresh identity/session/record/value preflight and confirms every write by read-back.</summary>
    public static BatchReceipt ApplyBatch(BatchPlan plan)
    {
        if (DateTime.UtcNow - plan.CreatedUtc > TimeSpan.FromMinutes(2) || plan.CreatedUtc > DateTime.UtcNow.AddSeconds(10))
            throw new InvalidOperationException("The live edit preview expired. Refresh the values and stage the batch again.");
        using var process = Process.GetProcessById(plan.ProcessId);
        var module = FindModule(process);
        using var handle = new Handle(OpenProcess(QueryInformation | VmRead | VmWrite | VmOperation, false, plan.ProcessId));
        if (handle.Value == 0) throw Win32("write to the FM process");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("FM27 build profile mismatch. No live values were changed.");
        if (profile.Id != plan.ProfileId || profile.Fingerprint != plan.ProfileFingerprint)
            throw new InvalidOperationException("FM27's executable build changed after staging. Restage your edit.");
        if (ReadSaveCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
            throw new InvalidOperationException("FM loaded a different save after staging. No live values were changed.");

        var resolved = ResolveEntity(handle.Value, moduleBase, profile, plan.Entity, plan.Uid, plan.Name);
        if (resolved.Address != plan.ObjectAddress || resolved.Shift != plan.IdentityShift)
            throw new InvalidOperationException("The selected FM object moved or its identity layout changed after staging. Refresh and restage.");
        var financeClass = plan.Entity == EntityKind.Club ? FindRecordClass(handle.Value, moduleBase, profile, Place.ClubFinance) : 0UL;
        var stadiumClass = plan.Entity == EntityKind.Club ? FindRecordClass(handle.Value, moduleBase, profile, Place.Stadium) : 0UL;

        foreach (var change in plan.Values)
        {
            var field = ResolveField(plan.Entity, change.FieldKey);
            var address = ResolveFieldAddress(handle.Value, resolved, field, financeClass, stadiumClass);
            if (address != change.Address || change.OriginalBytes.Length != field.Size || change.TargetBytes.Length != field.Size)
                throw new InvalidOperationException("The live record for " + field.Key + " changed after preview. No fields were written.");
            var current = Read(handle.Value, address, field.Size);
            if (!current.SequenceEqual(change.OriginalBytes) || Decode(field, current) != change.CurrentValue)
                throw new InvalidOperationException("Live value conflict for " + field.Key + ". Refresh the record and stage again.");
            ValidateCurrent(field, change.CurrentValue);
            ValidateTarget(field, change.TargetValue);
        }
        ValidateCrossFieldConstraints(handle.Value, resolved, plan.Values, financeClass, stadiumClass);

        var written = new List<(PlannedValue Change, int BytesWritten)>();
        try
        {
            foreach (var change in plan.Values)
            {
                if (ReadSaveCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
                    throw new InvalidOperationException("FM switched career during this edit batch. Atlas stopped before the next field.");
                var current = Read(handle.Value, (nint)change.Address, change.OriginalBytes.Length);
                if (!current.SequenceEqual(change.OriginalBytes))
                    throw new InvalidOperationException("The value of " + change.FieldKey + " changed during Apply. The batch was stopped.");
                var success = Write(handle.Value, (nint)change.Address, change.TargetBytes, out var bytesWritten);
                if (bytesWritten > 0) written.Add((change, bytesWritten));
                if (!success || bytesWritten != change.TargetBytes.Length)
                    throw new InvalidOperationException("FM rejected or partially completed the write for " + change.FieldKey + ".");
                if (!Read(handle.Value, (nint)change.Address, change.TargetBytes.Length).SequenceEqual(change.TargetBytes))
                    throw new InvalidOperationException("FM did not return the written value for " + change.FieldKey + ".");
            }
        }
        catch (Exception failure)
        {
            var rollbackErrors = RollBack(handle.Value, moduleBase, profile, plan.SaveLoadCounter, written);
            var suffix = rollbackErrors.Count == 0
                ? " Any partial writes were rolled back and read-back checked."
                : " ROLLBACK NEEDS INSPECTION: " + string.Join("; ", rollbackErrors);
            throw new InvalidOperationException(failure.Message + suffix, failure);
        }
        return new BatchReceipt(plan, DateTime.UtcNow, true);
    }

    /// <summary>Undoes a live batch only while every affected field still contains the value Atlas wrote.</summary>
    public static void UndoBatch(BatchReceipt receipt)
    {
        var plan = receipt.Plan;
        using var process = Process.GetProcessById(plan.ProcessId);
        var module = FindModule(process);
        using var handle = new Handle(OpenProcess(QueryInformation | VmRead | VmWrite | VmOperation, false, plan.ProcessId));
        if (handle.Value == 0) throw Win32("undo a live FM edit");
        var moduleBase = (ulong)module.BaseAddress.ToInt64();
        var profile = ReadProfile(handle.Value, moduleBase) ?? throw new InvalidOperationException("FM27 build profile mismatch. Live Undo is blocked.");
        if (profile.Id != plan.ProfileId || profile.Fingerprint != plan.ProfileFingerprint
            || ReadSaveCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
            throw new InvalidOperationException("The game build or career session changed after Apply. Live Undo is blocked.");
        var resolved = ResolveEntity(handle.Value, moduleBase, profile, plan.Entity, plan.Uid, plan.Name);
        if (resolved.Address != plan.ObjectAddress || resolved.Shift != plan.IdentityShift)
            throw new InvalidOperationException("The selected FM object moved. Live Undo is blocked to protect the wrong entity.");
        var undoFinanceClass = plan.Entity == EntityKind.Club ? FindRecordClass(handle.Value, moduleBase, profile, Place.ClubFinance) : 0UL;
        var undoStadiumClass = plan.Entity == EntityKind.Club ? FindRecordClass(handle.Value, moduleBase, profile, Place.Stadium) : 0UL;
        foreach (var change in plan.Values)
        {
            var field = ResolveField(plan.Entity, change.FieldKey);
            var address = ResolveFieldAddress(handle.Value, resolved, field, undoFinanceClass, undoStadiumClass);
            if (address != change.Address || !Read(handle.Value, address, change.TargetBytes.Length).SequenceEqual(change.TargetBytes))
                throw new InvalidOperationException("Undo conflict for " + change.FieldKey + ". Its value has changed in FM since Apply.");
        }

        var undone = new List<(PlannedValue Change, int BytesWritten)>();
        try
        {
            foreach (var change in plan.Values.Reverse())
            {
                if (ReadSaveCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
                    throw new InvalidOperationException("FM switched career during Undo. Atlas stopped.");
                var success = Write(handle.Value, (nint)change.Address, change.OriginalBytes, out var bytesWritten);
                if (bytesWritten > 0) undone.Add((change, bytesWritten));
                if (!success || bytesWritten != change.OriginalBytes.Length
                    || !Read(handle.Value, (nint)change.Address, change.OriginalBytes.Length).SequenceEqual(change.OriginalBytes))
                    throw new InvalidOperationException("Undo could not restore " + change.FieldKey + " with read-back verification.");
            }
        }
        catch (Exception failure)
        {
            var errors = new List<string>();
            foreach (var item in undone.AsEnumerable().Reverse())
            {
                try
                {
                    if (ReadSaveCounter(handle.Value, moduleBase, profile) != plan.SaveLoadCounter)
                    { errors.Add(item.Change.FieldKey + " restore skipped after career change"); continue; }
                    var expectedPartial = item.Change.OriginalBytes.ToArray();
                    Array.Copy(item.Change.TargetBytes, expectedPartial, item.BytesWritten);
                    var current = Read(handle.Value, (nint)item.Change.Address, expectedPartial.Length);
                    if (!current.SequenceEqual(expectedPartial))
                    { errors.Add(item.Change.FieldKey + " changed again; restore skipped"); continue; }
                    if (!Write(handle.Value, (nint)item.Change.Address, item.Change.TargetBytes)
                        || !Read(handle.Value, (nint)item.Change.Address, item.Change.TargetBytes.Length).SequenceEqual(item.Change.TargetBytes))
                        errors.Add(item.Change.FieldKey + " could not be rolled forward");
                }
                catch (Exception ex) { errors.Add(item.Change.FieldKey + ": " + ex.Message); }
            }
            throw new InvalidOperationException(failure.Message + (errors.Count == 0 ? " Earlier undo writes were rolled forward." : " UNDO ROLLBACK NEEDS INSPECTION: " + string.Join("; ", errors)), failure);
        }
    }

    private sealed record ResolvedEntity(ulong Address, int Shift);
    private static IReadOnlyList<FieldSpec> Fields(EntityKind entity)
    {
        var fields = new List<FieldSpec>();
        if (entity == EntityKind.Player)
        {
            fields.Add(F("ability.current_ability", "Current Ability", entity, Place.Object, 0x264, 2, EncodingKind.UInt16, 1, 200));
            fields.Add(F("ability.potential_ability", "Potential Ability", entity, Place.Object, 0x266, 2, EncodingKind.Int16, 1, 200, -200, 200));
            foreach (var a in PlayerAttributeSchema.Fields)
                fields.Add(F("attribute." + a.Key, a.Label, entity, Place.Object, (ulong)a.Offset, 1, EncodingKind.ScaledAttribute, 1, 20, 0, 20));
            var positions = new[] { "gk","sw","dl","dc","dr","dm","ml","mc","mr","aml","amc","amr","st","wbl","wbr" };
            for (var i = 0; i < positions.Length; i++)
                if (positions[i] != "sw")
                    fields.Add(F("position." + positions[i], "Position " + positions[i].ToUpperInvariant(), entity, Place.Object, 0x150UL + (ulong)i, 1, EncodingKind.DirectByte, 1, 20, 0, 20));
            foreach (var p in PlayerAttributeSchema.PersonalityFields)
                fields.Add(F("personality." + p.Key, p.Label, entity, Place.Object, (ulong)p.Offset, 1, EncodingKind.DirectByte, 1, 20, 0, 20, shifted: true));
            fields.Add(F("left_foot","Left Foot",entity,Place.Object,0x177,1,EncodingKind.DirectByte,1,20,0,20));
            fields.Add(F("right_foot","Right Foot",entity,Place.Object,0x178,1,EncodingKind.DirectByte,1,20,0,20));
            fields.Add(F("height","Height (cm)",entity,Place.Object,0x22E,2,EncodingKind.UInt16,140,220,100,250));
            fields.Add(F("asking_price","Asking Price (GBP)",entity,Place.Object,0x238,4,EncodingKind.Int32,0,2_000_000_000,-1,int.MaxValue,blank:-1));
            fields.Add(F("condition","Condition",entity,Place.Object,0x25C,2,EncodingKind.UInt16,0,10_000,0,10_000));
            fields.Add(F("match_sharpness","Match Sharpness",entity,Place.Object,0x258,2,EncodingKind.UInt16,3_500,10_000,0,10_000));
            fields.Add(F("jadedness","Jadedness",entity,Place.Object,0x25A,2,EncodingKind.Int16,-500,1_000,-500,short.MaxValue));
            fields.Add(F("morale","Morale",entity,Place.Object,0x26C,1,EncodingKind.UInt8,1,20,0,20));
            fields.Add(F("reputation.home","Home Reputation",entity,Place.Object,0x25E,2,EncodingKind.UInt16,0,10_000,0,10_000));
            fields.Add(F("reputation.current","Current Reputation",entity,Place.Object,0x260,2,EncodingKind.UInt16,0,10_000,0,10_000));
            fields.Add(F("reputation.world","World Reputation",entity,Place.Object,0x262,2,EncodingKind.UInt16,0,10_000,0,10_000));
            fields.Add(F("international.caps","International Caps",entity,Place.Object,0x3BC,2,EncodingKind.UInt16,0,1_000,0,1_000));
            fields.Add(F("international.goals","International Goals",entity,Place.Object,0x3BE,2,EncodingKind.UInt16,0,1_000,0,1_000));
            fields.Add(F("international.u21_caps","U21 Caps",entity,Place.Object,0x3C0,1,EncodingKind.UInt8,0,255,0,255));
            fields.Add(F("international.u21_goals","U21 Goals",entity,Place.Object,0x3C1,1,EncodingKind.UInt8,0,255,0,255));
            for (var bit = 0; bit < 64; bit++)
                fields.Add(F("trait." + bit, "Preferred Move " + bit, entity, Place.Object, 0x348UL + (ulong)(bit / 8), 1, EncodingKind.Flag, 0, 1, 0, 1, shifted: true, bit: bit % 8));
            AddContractFields(fields, entity);
        }
        else if (entity == EntityKind.Staff)
        {
            fields.Add(F("ability.current_ability","Current Ability",entity,Place.Object,0xDA,2,EncodingKind.UInt16,1,200,0,200));
            fields.Add(F("ability.potential_ability","Potential Ability",entity,Place.Object,0xDC,2,EncodingKind.Int16,1,200,-200,200));
            fields.Add(F("reputation.home","Home Reputation",entity,Place.Object,0xD4,2,EncodingKind.UInt16,0,10_000,0,10_000));
            fields.Add(F("reputation.current","Current Reputation",entity,Place.Object,0xD6,2,EncodingKind.UInt16,0,10_000,0,10_000));
            fields.Add(F("reputation.world","World Reputation",entity,Place.Object,0xD8,2,EncodingKind.UInt16,0,10_000,0,10_000));
            var staffScaled = new (string Key,string Label,int Offset)[]
            {
                ("goalkeeping","Goalkeeping",0x2B),("judging_ability","Judging Ability",0x2C),("judging_potential","Judging Potential",0x2D),
                ("people_management","People Management",0x2E),("motivating","Motivating",0x2F),("physiotherapy","Physiotherapy",0x30),
                ("tactical_knowledge","Tactical Knowledge",0x31),("attacking","Attacking",0x32),("defending","Defending",0x33),
                ("fitness","Fitness",0x34),("possession","Possession",0x35),("technical","Technical",0x36),
                ("tactical","Tactical",0x37),("data_analysis","Data Analysis",0x3C),("sports_science","Sports Science",0x3F),
                ("negotiating","Negotiating",0x41),("judging_staff","Judging Staff",0x42),("set_pieces","Set Pieces",0x43),
                ("determination","Determination",0x1D)
            };
            foreach(var a in staffScaled)
                fields.Add(F("attribute."+a.Key,a.Label,entity,Place.Object,(ulong)a.Offset,1,EncodingKind.ScaledAttribute,1,20,0,20));
            fields.Add(F("attribute.authority","Authority",entity,Place.Object,0x14,1,EncodingKind.DirectByte,1,20,0,20));
            fields.Add(F("attribute.working_with_youngsters","Working With Youngsters",entity,Place.Object,0x1C,1,EncodingKind.DirectByte,1,20,0,20));
            var personality = new[] {"adaptability","ambition","loyalty","pressure","professionalism","sportsmanship","temperament","controversy"};
            for(var i=0;i<personality.Length;i++)
                fields.Add(F("personality."+personality[i],Humanize(personality[i]),entity,Place.Object,0x170UL+(ulong)i,1,EncodingKind.DirectByte,1,20,0,20,shifted:true));
            AddContractFields(fields, entity);
        }
        else
        {
            fields.Add(F("reputation","Club Reputation",entity,Place.SeniorTeam,0xA8,2,EncodingKind.UInt16,0,10_000,0,10_000));
            fields.Add(F("facilities.training","Training Facilities",entity,Place.ClubInfo,0x120,1,EncodingKind.DirectByte,1,20,0,20));
            fields.Add(F("facilities.youth_facilities","Youth Facilities",entity,Place.ClubInfo,0x12B,1,EncodingKind.DirectByte,1,20,0,20));
            fields.Add(F("facilities.youth_coaching","Youth Coaching",entity,Place.ClubInfo,0x12C,1,EncodingKind.DirectByte,1,20,0,20));
            fields.Add(F("facilities.youth_recruitment","Youth Recruitment",entity,Place.ClubInfo,0x12D,1,EncodingKind.DirectByte,1,20,0,20));
            fields.Add(F("finance.balance","Balance (GBP)",entity,Place.ClubFinance,0x0C,4,EncodingKind.Int32,-2_000_000_000,2_000_000_000,int.MinValue,int.MaxValue));
            fields.Add(F("finance.transfer_budget","Transfer Budget (GBP)",entity,Place.ClubFinance,0x7C4,4,EncodingKind.Int32,0,2_000_000_000,0,int.MaxValue));
            fields.Add(F("finance.total_wage_budget","Total Wage Budget (GBP/week)",entity,Place.ClubFinance,0x808,4,EncodingKind.Int32,0,100_000_000,0,int.MaxValue));
            fields.Add(F("finance.wage_budget","Wage Budget (GBP/week)",entity,Place.ClubFinance,0x814,4,EncodingKind.Int32,0,100_000_000,0,int.MaxValue));
            fields.Add(F("facilities.corporate","Corporate Facilities",entity,Place.ClubFinance,0x8A9,1,EncodingKind.DirectByte,1,20,0,20));
            fields.Add(F("stadium.capacity","Stadium Capacity",entity,Place.Stadium,0x7C,4,EncodingKind.UInt32,0,250_000,0,int.MaxValue));
            fields.Add(F("stadium.seating","Stadium Seating",entity,Place.Stadium,0x80,4,EncodingKind.UInt32,0,250_000,0,int.MaxValue));
            fields.Add(F("stadium.used","Stadium Used Capacity",entity,Place.Stadium,0x84,4,EncodingKind.UInt32,0,250_000,0,int.MaxValue));
            fields.Add(F("stadium.expansion","Stadium Expansion Capacity",entity,Place.Stadium,0x8C,4,EncodingKind.UInt32,0,250_000,0,int.MaxValue));
        }
        return fields;
    }

    private static FieldSpec F(string key,string label,EntityKind entity,Place place,ulong offset,int size,EncodingKind encoding,
        int min,int max,int? plausibleMin=null,int? plausibleMax=null,bool shifted=false,int bit=-1,int blank=int.MinValue,int[]? choices=null)
        => new(key,label,entity,place,offset,size,encoding,min,max,plausibleMin??min,plausibleMax??max,shifted,bit,blank,choices);

    private static void AddContractFields(List<FieldSpec> fields, EntityKind entity)
    {
        var prefix = "contract.";
        fields.Add(F(prefix+"wage","Contract Wage (GBP/week)",entity,Place.Contract,0x20,4,EncodingKind.Int32,0,10_000_000,0,int.MaxValue));
        fields.Add(F(prefix+"expires","Contract Expiry",entity,Place.Contract,0x48,4,EncodingKind.Date,20000101,21001231,0,22001231));
        if(entity==EntityKind.Player)
        {
            fields.Add(F(prefix+"squad_number","Squad Number",entity,Place.Contract,0x5D,1,EncodingKind.UInt8,1,99,1,255,blank:255));
            fields.Add(F(prefix+"squad_status","Squad Status",entity,Place.Contract,0x54,1,EncodingKind.UInt8,1,5,0,40,choices:new[]{1,2,3,4,5}));
            fields.Add(F(prefix+"transfer_listed","Transfer Listed",entity,Place.Contract,0x57,1,EncodingKind.Flag,0,1,0,1,bit:0));
            fields.Add(F(prefix+"loan_listed","Loan Listed",entity,Place.Contract,0x57,1,EncodingKind.Flag,0,1,0,1,bit:1));
            fields.Add(F(prefix+"loyalty_bonus","Loyalty Bonus (GBP/week)",entity,Place.Contract,0x38,4,EncodingKind.Int32,0,10_000_000,0,int.MaxValue));
            fields.Add(F(prefix+"happiness","Contract Happiness",entity,Place.Contract,0x5A,1,EncodingKind.Int8,-100,100,sbyte.MinValue,sbyte.MaxValue));
            fields.Add(F(prefix+"playing_time_happiness","Playing Time Happiness",entity,Place.Contract,0x5C,1,EncodingKind.Int8,-100,100,sbyte.MinValue,sbyte.MaxValue));
        }
    }

    private static string Humanize(string key) => string.Join(" ", key.Split('_').Select(x => char.ToUpperInvariant(x[0]) + x[1..]));

    private static FieldSpec ResolveField(EntityKind entity,string key)
    {
        var normalized=(key??string.Empty).Trim();
        if(entity==EntityKind.Player && normalized.StartsWith("attributes.",StringComparison.OrdinalIgnoreCase))
            normalized="attribute."+normalized["attributes.".Length..];
        if(entity==EntityKind.Player && normalized.StartsWith("positions.",StringComparison.OrdinalIgnoreCase))
            normalized="position."+normalized["positions.".Length..];
        if(entity==EntityKind.Staff && normalized.StartsWith("staff_attributes.",StringComparison.OrdinalIgnoreCase))
            normalized="attribute."+normalized["staff_attributes.".Length..];
        if(entity==EntityKind.Staff && normalized.StartsWith("staff_personality.",StringComparison.OrdinalIgnoreCase))
            normalized="personality."+normalized["staff_personality.".Length..];
        return Fields(entity).FirstOrDefault(x=>x.Key.Equals(normalized,StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Field '" + key + "' is not part of the verified " + entity + " live-edit catalogue.");
    }

    private static void ValidateCurrent(FieldSpec field,int value)
    {
        if(value<field.PlausibleMinimum||value>field.PlausibleMaximum)
            throw new InvalidOperationException("The current value for "+field.Key+" ("+value+") is outside its FM27 plausible range. No write is allowed.");
        if(field.Encoding==EncodingKind.Date && value!=0 && !DateOnly.TryParseExact(value.ToString(CultureInfo.InvariantCulture),"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))
            throw new InvalidOperationException("The FM contract date for "+field.Key+" could not be decoded.");
    }

    private static void ValidateTarget(FieldSpec field,int value)
    {
        var blankOk=field.BlankValue!=int.MinValue && value==field.BlankValue;
        if(!blankOk&&(value<field.Minimum||value>field.Maximum))
            throw new ArgumentOutOfRangeException(nameof(value),field.Key+" must be "+field.Minimum+"–"+field.Maximum+".");
        if(field.Choices is not null && !field.Choices.Contains(value))
            throw new InvalidOperationException("The squad status value for "+field.Key+" must be one of "+string.Join(", ",field.Choices)+".");
        if(field.Encoding==EncodingKind.Date&&!DateOnly.TryParseExact(value.ToString(CultureInfo.InvariantCulture),"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))
            throw new InvalidOperationException("Enter "+field.Key+" as a real date in YYYYMMDD format.");
    }

    private static int Decode(FieldSpec field,byte[] bytes)
    {
        if(field.Encoding==EncodingKind.Flag) return (bytes[0]>>field.Bit)&1;
        return field.Encoding switch
        {
            EncodingKind.ScaledAttribute => (bytes[0]+2)/5,
            EncodingKind.DirectByte or EncodingKind.UInt8 => bytes[0],
            EncodingKind.Int8 => unchecked((sbyte)bytes[0]),
            EncodingKind.UInt16 => BitConverter.ToUInt16(bytes,0),
            EncodingKind.Int16 => BitConverter.ToInt16(bytes,0),
            EncodingKind.UInt32 => checked((int)BitConverter.ToUInt32(bytes,0)),
            EncodingKind.Int32 => BitConverter.ToInt32(bytes,0),
            EncodingKind.Date => DecodeDate(bytes),
            _ => throw new InvalidOperationException("Unsupported live field encoding.")
        };
    }

    private static int DecodeDate(byte[] bytes)
    {
        var day=(ushort)(BitConverter.ToUInt16(bytes,0)&0x01FF);
        var year=BitConverter.ToUInt16(bytes,2);
        if(year==0||day==0)return 0;
        if(year<1901||year>2200)return -1;
        var date=new DateTime(year,1,1).AddDays(day-1);
        return date.Year*10000+date.Month*100+date.Day;
    }

    private static byte[] Encode(FieldSpec field,int value,byte[] old)
    {
        if(field.Encoding==EncodingKind.Flag)
        {
            var mask=(byte)(1<<field.Bit);
            return new[]{value==0?(byte)(old[0]&~mask):(byte)(old[0]|mask)};
        }
        return field.Encoding switch
        {
            EncodingKind.ScaledAttribute => new[]{checked((byte)(value*5-2))},
            EncodingKind.DirectByte or EncodingKind.UInt8 => new[]{checked((byte)value)},
            EncodingKind.Int8 => new[]{unchecked((byte)checked((sbyte)value))},
            EncodingKind.UInt16 => BitConverter.GetBytes(checked((ushort)value)),
            EncodingKind.Int16 => BitConverter.GetBytes(checked((short)value)),
            EncodingKind.UInt32 => BitConverter.GetBytes(checked((uint)value)),
            EncodingKind.Int32 => BitConverter.GetBytes(value),
            EncodingKind.Date => EncodeDate(value),
            _ => throw new InvalidOperationException("Unsupported live field encoding.")
        };
    }

    private static byte[] EncodeDate(int number)
    {
        var date=DateOnly.ParseExact(number.ToString(CultureInfo.InvariantCulture),"yyyyMMdd",CultureInfo.InvariantCulture);
        var day=checked((ushort)date.DayOfYear);
        var year=checked((ushort)date.Year);
        var bytes=new byte[4];
        BitConverter.GetBytes(day).CopyTo(bytes,0);
        BitConverter.GetBytes(year).CopyTo(bytes,2);
        return bytes;
    }

    private static ResolvedEntity ResolveEntity(nint handle,ulong moduleBase,Fm27BuildProfile profile,EntityKind kind,uint uid,string expectedName)
    {
        var arrayRva=kind switch {EntityKind.Player=>profile.PlayersArrayRva,EntityKind.Staff=>profile.StaffArrayRva,_=>profile.ClubsArrayRva};
        var bounds=ReadBounds(handle,moduleBase,arrayRva,kind.ToString());
        ulong found=0; int foundShift=0; var matches=0;
        for(var i=0;i<bounds.Count;i++)
        {
            var obj=U64(handle,bounds.Data+(ulong)i*8);
            if(!Pointer(obj))continue;
            int shift=0;uint actualUid=0;string? name=null;
            try
            {
                if(kind==EntityKind.Player)
                {
                    shift=DetectPlayerShift(handle,obj);
                    actualUid=BitConverter.ToUInt32(Read(handle,(nint)(obj+0x294+(ulong)shift),4),0);
                    if(actualUid!=uid)continue;
                    var common=ReadNameSub(handle,obj+(ulong)(0x2E8+shift));
                    var first=ReadNameSub(handle,obj+(ulong)(0x2D8+shift));
                    var last=ReadNameSub(handle,obj+(ulong)(0x2E0+shift));
                    name=!string.IsNullOrWhiteSpace(common)?common:Join(first,last);
                }
                else if(kind==EntityKind.Staff)
                {
                    var plain=ScoreStaffNames(handle,obj,0);
                    var shifted=ScoreStaffNames(handle,obj,0x340);
                    shift=shifted>plain&&shifted>=2?0x340:0;
                    actualUid=BitConverter.ToUInt32(Read(handle,(nint)(obj+0x10C+(ulong)shift),4),0);
                    if(actualUid!=uid)continue;
                    var common=ReadNameSub(handle,obj+(ulong)(0x160+shift));
                    var first=ReadNameSub(handle,obj+(ulong)(0x150+shift));
                    var last=ReadNameSub(handle,obj+(ulong)(0x158+shift));
                    name=!string.IsNullOrWhiteSpace(common)?common:Join(first,last);
                }
                else
                {
                    actualUid=BitConverter.ToUInt32(Read(handle,(nint)(obj+0x0C),4),0);
                    if(actualUid!=uid)continue;
                    var namePtr=U64(handle,obj+0xC0);
                    name=ReadLengthString(handle,namePtr,128);
                }
            }
            catch { continue; }
            if(actualUid!=uid)continue;
            if(!string.Equals(name,expectedName,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Identity mismatch for "+kind+" UID "+uid+": FM now reports '"+(name??"unnamed")+"', not '"+expectedName+"'. Refresh the directory before editing.");
            found=obj;foundShift=shift;matches++;
            if(matches>1)break;
        }
        if(matches!=1)throw new InvalidOperationException(matches==0?kind+" UID "+uid+" was not found in this career. Refresh the live index.":"The selected "+kind+" UID appeared more than once; Atlas refuses an ambiguous edit.");
        return new ResolvedEntity(found,foundShift);
    }

    private static int ScoreStaffNames(nint handle,ulong obj,int shift)
    {
        var score=0;
        foreach(var off in new[]{0x150,0x158,0x160})
        {
            var value=ReadNameSub(handle,obj+(ulong)(off+shift));
            if(!string.IsNullOrWhiteSpace(value)&&value.Count(char.IsLetter)>=2)score++;
        }
        return score;
    }

    private static ulong ResolveFieldAddress(nint handle,ResolvedEntity target,FieldSpec field,ulong financeClass,ulong stadiumClass)
    {
        ulong record=target.Address;
        switch(field.Place)
        {
            case Place.Object:
                return target.Address+field.Offset+(field.Shifted?(ulong)target.Shift:0);
            case Place.Contract:
                var contractOffset=field.Entity==EntityKind.Player?0x330UL:0x1A8UL;
                var identityOffset=field.Entity==EntityKind.Player?0x288UL:0x100UL;
                record=U64(handle,target.Address+contractOffset+(ulong)target.Shift);
                if(!Pointer(record))throw new InvalidOperationException("The selected "+field.Entity+" does not have a live contract record. Nothing was written.");
                var owner=U64(handle,record+0x08);
                if(owner!=target.Address+identityOffset+(ulong)target.Shift)
                    throw new InvalidOperationException("The contract's owner pointer does not match the selected entity. Live editing is blocked.");
                break;
            case Place.SeniorTeam:
                var teams=U64(handle,target.Address+0x18);
                if(!Pointer(teams))throw new InvalidOperationException("The club has no senior team record.");
                record=U64(handle,teams);
                if(!Pointer(record)||U64(handle,record+0x30)!=target.Address)
                    throw new InvalidOperationException("The club's senior-team link failed its back-reference check.");
                break;
            case Place.ClubInfo:
                record=U64(handle,target.Address+0x100);
                if(!Pointer(record))throw new InvalidOperationException("The club has no club-info record.");
                break;
            case Place.ClubFinance:
                record=U64(handle,target.Address+0x150);
                if(!Pointer(record)||financeClass==0||U64(handle,record)!=financeClass)
                    throw new InvalidOperationException("The club does not have a verified full FM27 finance record. Atlas will not write a smaller or unknown finance class.");
                break;
            case Place.Stadium:
                var teamList=U64(handle,target.Address+0x18);
                if(!Pointer(teamList))throw new InvalidOperationException("The club has no senior team.");
                var senior=U64(handle,teamList);
                if(!Pointer(senior)||U64(handle,senior+0x30)!=target.Address)throw new InvalidOperationException("The senior team failed its club relationship check.");
                record=U64(handle,senior+0x78);
                if(!Pointer(record)||stadiumClass==0||U64(handle,record)!=stadiumClass)
                    throw new InvalidOperationException("The club's stadium record is not a verified FM27 stadium class.");
                break;
        }
        return record+field.Offset;
    }

    private static void ValidateCrossFieldConstraints(nint handle,ResolvedEntity target,IReadOnlyList<PlannedValue> values,ulong financeClass,ulong stadiumClass)
    {
        if(target.Address==0)return;
        var cap=values.FirstOrDefault(x=>x.FieldKey=="stadium.capacity");
        var seats=values.FirstOrDefault(x=>x.FieldKey=="stadium.seating");
        var used=values.FirstOrDefault(x=>x.FieldKey=="stadium.used");
        if(cap is not null||seats is not null||used is not null)
        {
            var capacity=cap?.TargetValue??ReadCurrentForKey(handle,target,"stadium.capacity",financeClass,stadiumClass);
            var seating=seats?.TargetValue??ReadCurrentForKey(handle,target,"stadium.seating",financeClass,stadiumClass);
            var usedValue=used?.TargetValue??ReadCurrentForKey(handle,target,"stadium.used",financeClass,stadiumClass);
            if(seating>capacity)throw new InvalidOperationException("Stadium seating cannot exceed stadium capacity.");
            if(usedValue>capacity)throw new InvalidOperationException("Used stadium capacity cannot exceed total capacity.");
        }
    }

    private static int ReadCurrentForKey(nint handle,ResolvedEntity target,string key,ulong financeClass,ulong stadiumClass)
    {
        var field=ResolveField(EntityKind.Club,key);
        var addr=ResolveFieldAddress(handle,target,field,financeClass,stadiumClass);
        return Decode(field,Read(handle,addr,field.Size));
    }

    private static ulong FindRecordClass(nint handle,ulong moduleBase,Fm27BuildProfile profile,Place place)
    {
        var bounds=ReadBounds(handle,moduleBase,profile.ClubsArrayRva,"Club");
        var counts=new Dictionary<ulong,int>();
        var checkedCount=0;
        for(var i=0;i<bounds.Count&&checkedCount<512;i++)
        {
            var club=U64(handle,bounds.Data+(ulong)i*8);
            if(!Pointer(club))continue;
            try
            {
                ulong record=0;
                if(place==Place.ClubFinance)
                {
                    record=U64(handle,club+0x150);
                    if(!Pointer(record))continue;
                    var bytes=Read(handle,(nint)record,0x8AD);
                    var bal=BitConverter.ToInt32(bytes,0x0C);
                    var transfer=BitConverter.ToInt32(bytes,0x7C4);
                    var wages=BitConverter.ToInt32(bytes,0x814);
                    if(bal < -2_000_000_000 || bal > 2_000_000_000 || transfer < 0 || transfer > 2_000_000_000 || wages < 0 || wages > 100_000_000)continue;
                }
                else if(place==Place.Stadium)
                {
                    var teamList=U64(handle,club+0x18);
                    if(!Pointer(teamList))continue;
                    var team=U64(handle,teamList);
                    if(!Pointer(team)||U64(handle,team+0x30)!=club)continue;
                    record=U64(handle,team+0x78);
                    if(!Pointer(record))continue;
                    var bytes=Read(handle,(nint)record,0xC0);
                    var cap=BitConverter.ToUInt32(bytes,0x7C);
                    var seating=BitConverter.ToUInt32(bytes,0x80);
                    if(cap is 0 or >250_000 || seating>cap)continue;
                }
                else continue;
                var cls=U64(handle,record);
                if(!Pointer(cls))continue;
                counts[cls]=counts.GetValueOrDefault(cls)+1;
                checkedCount++;
            }
            catch { }
        }
        if(counts.Count==0)return 0;
        var top=counts.OrderByDescending(x=>x.Value).First();
        return top.Value>=5&&top.Value>=Math.Ceiling(checkedCount*0.70)?top.Key:0;
    }

    private static void CheckSquadNumber(nint handle,ResolvedEntity target,int newNumber)
    {
        if(newNumber==255)return;
        var team=U64(handle,target.Address+0x130);
        if(!Pointer(team))return;
        var header=Read(handle,(nint)(team+0x38),24);
        var begin=BitConverter.ToUInt64(header,0);
        var end=BitConverter.ToUInt64(header,8);
        var capacity=BitConverter.ToUInt64(header,16);
        if(!Pointer(begin)||end<begin||capacity<end||(end-begin)%8!=0||(end-begin)>8*2_000)return;
        for(var at=begin;at<end;at+=8)
        {
            var other=U64(handle,at);
            if(!Pointer(other)||other==target.Address)continue;
            if(U64(handle,other+0x130)!=team)continue;
            var shift=DetectPlayerShift(handle,other);
            var contract=U64(handle,other+0x330+(ulong)shift);
            if(!Pointer(contract))continue;
            if(Read(handle,(nint)(contract+0x5D),1)[0]==newNumber)
                throw new InvalidOperationException("Squad number "+newNumber+" is already used by another player at this club.");
        }
    }

    private static int DetectPlayerShift(nint handle,ulong address)
    {
        var plain=0;var shifted=0;
        foreach(var offset in new[]{0x2D8,0x2E0,0x2E8})
        {
            var a=ReadNameSub(handle,address+(ulong)offset);
            var b=ReadNameSub(handle,address+(ulong)(offset+0xF8));
            if(!string.IsNullOrWhiteSpace(a)&&a.Count(char.IsLetter)>=2)plain++;
            if(!string.IsNullOrWhiteSpace(b)&&b.Count(char.IsLetter)>=2)shifted++;
        }
        return shifted>=2&&shifted>plain?0xF8:0;
    }

    private static (ulong Data,int Count) ReadBounds(nint handle,ulong moduleBase,ulong rva,string kind)
    {
        if(rva==0)throw new InvalidOperationException("The exact FM27 profile has no "+kind+" array.");
        var data=U64(handle,moduleBase+rva);
        var end=U64(handle,moduleBase+rva+8);
        if(!Pointer(data)||!Pointer(end)||end<=data||(end-data)%8!=0)
            throw new InvalidOperationException("The FM "+kind+" array bounds failed validation.");
        var count=(end-data)/8;
        var (min,max)=kind switch { "Player"=>(20_000UL,200_000UL),"Staff"=>(5_000UL,200_000UL),_=>(1_000UL,100_000UL)};
        if(count<min||count>max)throw new InvalidOperationException("The FM "+kind+" array count "+count+" is outside the expected range.");
        return(data,(int)count);
    }

    private static ProcessModule FindModule(Process process)
        => process.Modules.Cast<ProcessModule>().FirstOrDefault(x=>string.Equals(Path.GetFileName(x.ModuleName),"game_plugin.dll",StringComparison.OrdinalIgnoreCase))
           ??throw new InvalidOperationException("game_plugin.dll was not found in Football Manager.");

    private static Fm27BuildProfile? ReadProfile(nint handle,ulong moduleBase)
    {
        var pe=Read(handle,(nint)moduleBase,0x40);
        if(pe[0]!='M'||pe[1]!='Z')return null;
        var offset=BitConverter.ToInt32(pe,0x3C);
        if(offset<0x40||offset>0x1000)return null;
        var nt=Read(handle,(nint)(moduleBase+(ulong)offset),24);
        if(nt[0]!='P'||nt[1]!='E'||BitConverter.ToUInt16(nt,4)!=0x8664)return null;
        var timestamp=BitConverter.ToUInt32(nt,8);
        var size=BitConverter.ToUInt16(nt,20);
        var opt=Read(handle,(nint)(moduleBase+(ulong)offset+24),size);
        return Fm27BuildProfiles.Match(BitConverter.ToUInt32(opt,56),timestamp);
    }

    private static ulong ReadSaveCounter(nint handle,ulong moduleBase,Fm27BuildProfile profile)
    {
        if(profile.GameUiRva==0||profile.GameUiRva>=profile.SizeOfImage)
            throw new InvalidOperationException("This FM27 build has no verified save-load generation pointer. Live writing is unavailable.");
        var gameUi=U64(handle,moduleBase+profile.GameUiRva);
        if(!Pointer(gameUi))throw new InvalidOperationException("The FM save-generation object could not be resolved.");
        return U64(handle,gameUi+SaveCounterOffset);
    }

    private static bool Pointer(ulong value)=>value>=0x10000&&value<=0x7FFF_FFFF_FFFF;
    private static ulong U64(nint handle,ulong address)=>BitConverter.ToUInt64(Read(handle,(nint)address,8),0);
    private static byte[] Read(nint handle,nint address,int size)
    {
        var bytes=new byte[size];
        if(!ReadProcessMemory(handle,address,bytes,(nuint)size,out var got)||got!=(nuint)size)
            throw Win32("read FM memory");
        return bytes;
    }
    private static bool Write(nint handle,nint address,byte[] bytes)
        =>Write(handle,address,bytes,out _);
    private static bool Write(nint handle,nint address,byte[] bytes,out int bytesWritten)
    {
        var ok=WriteProcessMemory(handle,address,bytes,(nuint)bytes.Length,out var wrote);
        bytesWritten=checked((int)Math.Min(wrote,(nuint)bytes.Length));
        return ok&&bytesWritten==bytes.Length;
    }
    private static InvalidOperationException Win32(string action)
        =>new InvalidOperationException("Windows denied permission to "+action+" (Win32 "+Marshal.GetLastWin32Error()+"). Run FM Atlas and Football Manager at the same permission level.");
    private static string? ReadNameSub(nint handle,ulong address)
    {
        try
        {
            var sub=U64(handle,address);if(!Pointer(sub))return null;
            var text=U64(handle,sub);if(!Pointer(text))return null;
            var len=BitConverter.ToUInt32(Read(handle,(nint)text,4),0);
            if(len is 0 or >64)return null;
            var value=Encoding.UTF8.GetString(Read(handle,(nint)(text+4),(int)len));
            return value.Any(char.IsControl)?null:value;
        }
        catch{return null;}
    }
    private static string? ReadLengthString(nint handle,ulong address,int max)
    {
        if(!Pointer(address))return null;
        try
        {
            var len=BitConverter.ToUInt32(Read(handle,(nint)address,4),0);
            if(len is 0 || len>(uint)max)return null;
            var value=Encoding.UTF8.GetString(Read(handle,(nint)(address+4),(int)len));
            return value.Any(char.IsControl)?null:value;
        }
        catch{return null;}
    }
    private static string? Join(string? first,string? last)
        =>string.IsNullOrWhiteSpace(first)?last:string.IsNullOrWhiteSpace(last)?first:first+" "+last;

}
