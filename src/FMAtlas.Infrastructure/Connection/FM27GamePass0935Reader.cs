using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using FMAtlas.Application.Models;
using FMAtlas.Domain.Clubs;
using FMAtlas.Domain.Connection;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Staff;

namespace FMAtlas.Infrastructure.Connection;

/// <summary>
/// Read-only reader for the specifically fingerprinted FM27 Game Pass 0.9.2435.0 package.
/// Offsets are kept separate from the rest of the reader and are never reused for another fingerprint.
/// </summary>
internal sealed class FM27GamePass0935Reader
{
    private const string BuildVersion = "0.9.2435.0";
    public const string BuildVersionForProvider = BuildVersion;
    private const uint PeTimestamp = 0x6AE8CF77;
    private const uint SizeOfImage = 0x17969000;
    private const ulong PlayersArrayRva = 0x53B7DE0;
    private const ulong StaffArrayRva = 0x53B7E28;
    private const ulong ClubsArrayRva = 0x53B7F98;
    private const ulong GameUiRva = 0x53B4160;
    private const ulong CurrentDateRva = 0x53597C8;
    private const ulong GameLoadsOffset = 0x100;
    private const int MaximumEntitiesPerKind = 250_000;
    private const int MaximumFmStringBytes = 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly IReadOnlyDictionary<string, ulong> PlayerAttributeOffsets =
        new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase)
        {
            ["corners"] = 0x17A, ["crossing"] = 0x15F, ["dribbling"] = 0x160,
            ["finishing"] = 0x161, ["first_touch"] = 0x175, ["free_kicks"] = 0x182,
            ["heading"] = 0x162, ["long_shots"] = 0x163, ["long_throws"] = 0x17D,
            ["marking"] = 0x164, ["passing"] = 0x166, ["penalty_taking"] = 0x167,
            ["tackling"] = 0x168, ["technique"] = 0x176,
            ["aggression"] = 0x18C, ["anticipation"] = 0x170, ["bravery"] = 0x18A,
            ["composure"] = 0x193, ["concentration"] = 0x194, ["decisions"] = 0x171,
            ["determination"] = 0x192, ["flair"] = 0x179, ["leadership"] = 0x187,
            ["off_the_ball"] = 0x165, ["positioning"] = 0x173, ["teamwork"] = 0x17B,
            ["vision"] = 0x169, ["work_rate"] = 0x17C,
            ["acceleration"] = 0x181, ["agility"] = 0x18D, ["balance"] = 0x189,
            ["jumping_reach"] = 0x186, ["natural_fitness"] = 0x191, ["pace"] = 0x185,
            ["stamina"] = 0x184, ["strength"] = 0x183,
            ["aerial_reach"] = 0x16B, ["command_of_area"] = 0x16C, ["communication"] = 0x16D,
            ["eccentricity"] = 0x17E, ["handling"] = 0x16A, ["kicking"] = 0x16E,
            ["one_on_ones"] = 0x172, ["punching"] = 0x180, ["reflexes"] = 0x174,
            ["rushing_out"] = 0x17F, ["throwing"] = 0x16F,
            ["consistency"] = 0x18B, ["dirtiness"] = 0x188, ["important_matches"] = 0x18E,
            ["injury_proneness"] = 0x18F, ["versatility"] = 0x190,
            ["left_foot"] = 0x177, ["right_foot"] = 0x178
        };

    private static readonly (string Key, ulong Offset)[] StaffAttributeOffsets =
    [
        ("attribute_goalkeeping", 0x2B), ("knowledge_judging_ability", 0x2C),
        ("knowledge_judging_potential", 0x2D), ("knowledge_people_management", 0x2E),
        ("attribute_motivating", 0x2F), ("knowledge_physiotherapy", 0x30),
        ("knowledge_tactical_knowledge", 0x31), ("attribute_attacking", 0x32),
        ("attribute_defending", 0x33), ("attribute_fitness", 0x34),
        ("attribute_possession", 0x35), ("attribute_technical", 0x36),
        ("attribute_tactical", 0x37), ("knowledge_data_analysis", 0x3C),
        ("knowledge_sports_science", 0x3F), ("knowledge_negotiating", 0x41),
        ("knowledge_judging_staff", 0x42), ("attribute_set_pieces", 0x43),
        ("attribute_determination", 0x1D)
    ];

    private static readonly string[] PositionKeys =
        ["gk", "sw", "dl", "dc", "dr", "dm", "ml", "mc", "mr", "aml", "amc", "amr", "st", "wbl", "wbr"];

    private static readonly string[] PersonalityKeys =
        ["adaptability", "ambition", "loyalty", "pressure", "professionalism", "sportsmanship", "temperament", "controversy"];

    private readonly WindowsProcessMemoryAccessor _memory;
    private readonly ulong _moduleBase;
    private readonly string _fingerprint;
    private readonly string _providerKey;
    private readonly Dictionary<ulong, string?> _strings = [];
    private readonly Dictionary<ulong, string?> _nameEntries = [];

    public FM27GamePass0935Reader(
        WindowsProcessMemoryAccessor memory,
        ulong moduleBase,
        string providerKey)
    {
        _memory = memory;
        _moduleBase = moduleBase;
        _fingerprint = $"PE:{PeTimestamp:X8}:{SizeOfImage:X8}";
        _providerKey = providerKey;
    }

    public static bool Matches(GameModuleFingerprint fingerprint) =>
        fingerprint.PeTimestamp == PeTimestamp && fingerprint.SizeOfImage == SizeOfImage;

    public string BuildDisplayName => $"FM27 Game Pass {BuildVersion}";

    public (int Players, int Staff, int Clubs, long SaveGeneration, DateOnly InGameDate) InspectLoadedSave()
    {
        var generation = ReadSaveGeneration();
        var players = ReadArray(PlayersArrayRva, "players");
        var staff = ReadArray(StaffArrayRva, "staff");
        var clubs = ReadArray(ClubsArrayRva, "clubs");
        if (players.Length == 0)
            throw new InvalidOperationException("Football Manager is at the main menu or the player list is not loaded. Load a career in FM27, then select Load Game again.");
        if (generation <= 0)
            throw new InvalidDataException("The FM27 save-generation marker did not contain a valid loaded-save count.");
        var date = ReadInGameDate();
        return (players.Length, staff.Length, clubs.Length, checked((long)generation), date);
    }

    public long ReadSaveGeneration() => ReadSaveGeneration(_memory, _moduleBase);

    public static long ReadSaveGeneration(WindowsProcessMemoryAccessor memory, ulong moduleBase)
    {
        var gameUiStatic = checked(moduleBase + GameUiRva);
        var gameUiObject = memory.ReadUInt64(gameUiStatic);
        WindowsProcessMemoryAccessor.ValidateUserPointer(gameUiObject, "GAME_UI object");
        return checked((long)memory.ReadUInt64(gameUiObject + GameLoadsOffset));
    }

    public bool IsPlayerArrayEmpty()
    {
        try { return ReadArray(PlayersArrayRva, "players").Length == 0; }
        catch { return true; }
    }

    public IReadOnlyList<IEntityRecord> ReadAllRecords(
        IProgress<LiveGameLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var playerPointers = ReadArray(PlayersArrayRva, "players");
        var staffPointers = ReadArray(StaffArrayRva, "staff");
        var clubPointers = ReadArray(ClubsArrayRva, "clubs");
        var inGameDate = ReadInGameDate();
        var records = new List<IEntityRecord>(playerPointers.Length + staffPointers.Length + clubPointers.Length);

        var evidence = new FieldProvenance(
            DataSourceKind.LiveGameMemory,
            FieldEvidenceStatus.Mapped,
            SourceVersion: BuildVersion,
            TargetBuildFingerprint: _fingerprint,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            Description: "Read from the externally mapped FM27 Game Pass 0.9.2435.0 layout. FM Atlas has not yet independently verified edit/write behaviour.");

        var processed = 0;
        var total = Math.Max(1, playerPointers.Length + staffPointers.Length + clubPointers.Length);

        progress?.Report(new LiveGameLoadProgress(18, "Reading players", $"Preparing {playerPointers.Length:N0} player records."));
        foreach (var address in playerPointers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var player = ReadPlayer(address, evidence, inGameDate);
                if (player is not null)
                    records.Add(player);
            }
            catch (Exception ex) when (IsRecoverableEntityReadException(ex))
            {
                // A single malformed/deleted object does not abort an otherwise valid scan.
            }

            processed++;
            if (processed % 250 == 0 || processed == playerPointers.Length)
            {
                var percent = 18 + (int)(48.0 * processed / total);
                progress?.Report(new LiveGameLoadProgress(percent, "Reading players", $"Read {processed:N0} of {playerPointers.Length:N0} player entries."));
            }
        }

        var playerCount = records.Count;
        progress?.Report(new LiveGameLoadProgress(67, "Reading staff", $"Preparing {staffPointers.Length:N0} staff records."));
        foreach (var address in staffPointers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var staff = ReadStaff(address, evidence, inGameDate);
                if (staff is not null)
                    records.Add(staff);
            }
            catch (Exception ex) when (IsRecoverableEntityReadException(ex))
            {
            }

            processed++;
            if (processed % 250 == 0 || processed == playerPointers.Length + staffPointers.Length)
            {
                var percent = 67 + (int)(16.0 * (processed - playerPointers.Length) / Math.Max(1, total - playerPointers.Length));
                progress?.Report(new LiveGameLoadProgress(percent, "Reading staff", $"Read {processed - playerPointers.Length:N0} of {staffPointers.Length:N0} staff entries."));
            }
        }

        var staffCount = records.Count - playerCount;
        progress?.Report(new LiveGameLoadProgress(84, "Reading clubs", $"Preparing {clubPointers.Length:N0} club records."));
        foreach (var address in clubPointers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var club = ReadClub(address, evidence);
                if (club is not null)
                    records.Add(club);
            }
            catch (Exception ex) when (IsRecoverableEntityReadException(ex))
            {
            }

            processed++;
            if (processed % 250 == 0 || processed == total)
            {
                var percent = 84 + (int)(11.0 * (processed - playerPointers.Length - staffPointers.Length) / Math.Max(1, clubPointers.Length));
                progress?.Report(new LiveGameLoadProgress(percent, "Reading clubs", $"Read {processed - playerPointers.Length - staffPointers.Length:N0} of {clubPointers.Length:N0} club entries."));
            }
        }

        var clubCount = records.Count - playerCount - staffCount;
        progress?.Report(new LiveGameLoadProgress(96, "Indexing live data", $"Preparing {records.Count:N0} records for the local search index."));
        _ = clubCount;
        return records;
    }

    private PlayerRecord? ReadPlayer(ulong address, FieldProvenance evidence, DateOnly inGameDate)
    {
        WindowsProcessMemoryAccessor.ValidateUserPointer(address, "player object");
        // One contiguous object read supplies the identity, attributes, ability, contract
        // pointer and all person-block fields, including the player-coach +0xF8 variant.
        var data = _memory.ReadBytes(address, 0x4B8);
        var shift = FindPlayerPersonShift(address, data);
        if (shift is null)
            return null;
        var offset = shift.Value;

        byte B(ulong field, ulong personShift = 0) => data[checked((int)(field + personShift))];
        ushort U16(ulong field, ulong personShift = 0) => BitConverter.ToUInt16(data, checked((int)(field + personShift)));
        short I16(ulong field, ulong personShift = 0) => BitConverter.ToInt16(data, checked((int)(field + personShift)));
        uint U32(ulong field, ulong personShift = 0) => BitConverter.ToUInt32(data, checked((int)(field + personShift)));
        ulong U64(ulong field, ulong personShift = 0) => BitConverter.ToUInt64(data, checked((int)(field + personShift)));

        var uid = U32(0x294, offset);
        if (uid == 0)
            return null;

        var fullName = ReadFmString(U64(0x2C8, offset));
        var firstName = ReadNameEntry(U64(0x2D8, offset));
        var lastName = ReadNameEntry(U64(0x2E0, offset));
        var commonName = ReadNameEntry(U64(0x2E8, offset));
        var displayName = commonName ??
            (firstName is not null && lastName is not null ? $"{firstName} {lastName}" : firstName ?? lastName) ??
            fullName ?? $"UID {uid}";

        var dateOfBirth = MakeFmDate(U16(0x310, offset), U16(0x312, offset));
        var age = dateOfBirth is DateOnly birth ? CalculateAge(birth, inGameDate) : null;
        var nationality = ReadNationName(U64(0x2F0, offset));
        var club = ReadPlayerClubName(U64(0x130));
        var genderFlags = B(0x2A1, offset);
        var madeByGameFlags = B(0x2A0, offset);

        var attributes = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var hidden = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var feet = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, fieldOffset) in PlayerAttributeOffsets)
        {
            var raw = B(fieldOffset);
            if (raw > 100)
                continue;
            var value = (raw + 2) / 5;
            var definition = PlayerAttributeCatalog.TryGetByKey(key, out var found) ? found : null;
            if (definition?.Group == PlayerAttributeGroupKind.Hidden)
                hidden[key] = new SourcedValue<int>(value, evidence);
            else if (definition?.Group == PlayerAttributeGroupKind.Feet)
                feet[key] = new SourcedValue<int>(value, evidence);
            else
                attributes[key] = new SourcedValue<int>(value, evidence);
        }

        var positions = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < PositionKeys.Length; index++)
        {
            var rating = B(0x150 + (ulong)index);
            if (rating <= 20)
                positions[PositionKeys[index]] = new SourcedValue<int>(rating, evidence);
        }

        var personality = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < PersonalityKeys.Length; index++)
        {
            var rating = B(0x2F8 + (ulong)index, offset);
            if (rating is >= 1 and <= 20)
                personality[PersonalityKeys[index]] = new SourcedValue<int>(rating, evidence);
        }

        var contract = U64(0x330, offset);
        var wage = WindowsProcessMemoryAccessor.IsUserPointer(contract) ? TryReadInt32(contract + 0x20) : null;
        var contractExpiry = WindowsProcessMemoryAccessor.IsUserPointer(contract) ? ReadFmDateSafe(contract + 0x48) : null;
        var height = U16(0x22E);
        var currentAbility = U16(0x264);
        var rawPotential = I16(0x266);
        var transferValue = BitConverter.ToInt32(data, 0x234);
        var caps = U16(0x3BC, offset);
        var goals = U16(0x3BE, offset);
        var primaryPosition = positions.OrderByDescending(pair => pair.Value.Value).FirstOrDefault();
        var positionName = primaryPosition.Key is null ? null : primaryPosition.Key.ToUpperInvariant();

        return new PlayerRecord(new EntityIdentity(EntityKind.Player, uid.ToString(CultureInfo.InvariantCulture), _providerKey))
        {
            FullName = new SourcedValue<string>(displayName, evidence),
            GameUid = new SourcedValue<string>(uid.ToString(CultureInfo.InvariantCulture), evidence),
            DateOfBirth = dateOfBirth is DateOnly dob ? new SourcedValue<DateOnly>(dob, evidence) : null,
            Age = age is int playerAge ? new SourcedValue<int>(playerAge, evidence) : null,
            Nationality = nationality is not null ? new SourcedValue<string>(nationality, evidence) : null,
            Gender = new SourcedValue<string>((genderFlags & 0x10) != 0 ? "Female" : "Male", evidence),
            ClubName = club is not null ? new SourcedValue<string>(club, evidence) : null,
            PrimaryPosition = positionName is not null ? new SourcedValue<string>(positionName, evidence) : null,
            HeightCentimetres = height is > 0 and < 300 ? new SourcedValue<decimal>(height, evidence) : null,
            CurrentAbility = currentAbility is > 0 and <= 200 ? new SourcedValue<int>(currentAbility, evidence) : null,
            PotentialAbility = rawPotential is > 0 and <= 200 ? new SourcedValue<int>(rawPotential, evidence) : null,
            InternationalCaps = new SourcedValue<int>(caps, evidence),
            InternationalGoals = new SourcedValue<int>(goals, evidence),
            ContractExpires = contractExpiry is DateOnly expiry ? new SourcedValue<DateOnly>(expiry, evidence) : null,
            Wage = wage is int playerWage and >= 0 ? new SourcedValue<decimal>(playerWage, evidence) : null,
            TransferValue = transferValue >= 0 ? new SourcedValue<decimal>(transferValue, evidence) : null,
            Attributes = attributes,
            HiddenAttributes = hidden,
            Personality = personality,
            FootRatings = feet,
            PositionRatings = positions,
            IsNewgen = new SourcedValue<bool>((madeByGameFlags & 0x08) != 0, evidence)
        };
    }

    private StaffRecord? ReadStaff(ulong address, FieldProvenance evidence, DateOnly inGameDate)
    {
        WindowsProcessMemoryAccessor.ValidateUserPointer(address, "staff object");
        // A single object read also covers the human-manager person-block shift (+0x340).
        var data = _memory.ReadBytes(address, 0x4F0);
        var shift = FindStaffPersonShift(address, data);
        if (shift is null)
            return null;
        var offset = shift.Value;

        byte B(ulong field, ulong personShift = 0) => data[checked((int)(field + personShift))];
        ushort U16(ulong field, ulong personShift = 0) => BitConverter.ToUInt16(data, checked((int)(field + personShift)));
        uint U32(ulong field, ulong personShift = 0) => BitConverter.ToUInt32(data, checked((int)(field + personShift)));
        ulong U64(ulong field, ulong personShift = 0) => BitConverter.ToUInt64(data, checked((int)(field + personShift)));

        var uid = U32(0x10C, offset);
        if (uid == 0)
            return null;

        var fullName = ReadFmString(U64(0x140, offset));
        var firstName = ReadNameEntry(U64(0x150, offset));
        var lastName = ReadNameEntry(U64(0x158, offset));
        var commonName = ReadNameEntry(U64(0x160, offset));
        var displayName = commonName ??
            (firstName is not null && lastName is not null ? $"{firstName} {lastName}" : firstName ?? lastName) ??
            fullName ?? $"UID {uid}";

        var dateOfBirth = MakeFmDate(U16(0x188, offset), U16(0x18A, offset));
        var age = dateOfBirth is DateOnly birth ? CalculateAge(birth, inGameDate) : null;
        var genderFlags = B(0x119, offset);
        var nationality = ReadNationName(U64(0x168, offset));

        var attributes = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var knowledge = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, fieldOffset) in StaffAttributeOffsets)
        {
            var raw = B(fieldOffset);
            if (raw > 100)
                continue;
            var value = (raw + 2) / 5;
            attributes[key] = new SourcedValue<int>(value, evidence);
            if (key.StartsWith("knowledge_", StringComparison.Ordinal))
                knowledge[key["knowledge_".Length..]] = new SourcedValue<int>(value, evidence);
        }

        var personality = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < PersonalityKeys.Length; index++)
        {
            var rating = B(0x170 + (ulong)index, offset);
            if (rating is >= 1 and <= 20)
                personality[PersonalityKeys[index]] = new SourcedValue<int>(rating, evidence);
        }

        var contract = U64(0x1A8, offset);
        var wage = WindowsProcessMemoryAccessor.IsUserPointer(contract) ? TryReadInt32(contract + 0x20) : null;
        var contractExpiry = WindowsProcessMemoryAccessor.IsUserPointer(contract) ? ReadFmDateSafe(contract + 0x48) : null;
        var club = WindowsProcessMemoryAccessor.IsUserPointer(contract) ? ReadContractClubName(contract) : null;

        var currentAbility = U16(0xDA);
        var potentialAbility = U16(0xDC);
        return new StaffRecord(new EntityIdentity(EntityKind.Staff, uid.ToString(CultureInfo.InvariantCulture), _providerKey))
        {
            FullName = new SourcedValue<string>(displayName, evidence),
            GameUid = new SourcedValue<string>(uid.ToString(CultureInfo.InvariantCulture), evidence),
            Gender = new SourcedValue<string>((genderFlags & 0x10) != 0 ? "Female" : "Male", evidence),
            DateOfBirth = dateOfBirth is DateOnly dob ? new SourcedValue<DateOnly>(dob, evidence) : null,
            Age = age is int staffAge ? new SourcedValue<int>(staffAge, evidence) : null,
            Nationality = nationality is not null ? new SourcedValue<string>(nationality, evidence) : null,
            ClubName = club is not null ? new SourcedValue<string>(club, evidence) : null,
            CurrentAbility = currentAbility is > 0 and <= 200 ? new SourcedValue<int>(currentAbility, evidence) : null,
            PotentialAbility = potentialAbility is > 0 and <= 200 ? new SourcedValue<int>(potentialAbility, evidence) : null,
            ContractExpires = contractExpiry is DateOnly expiry ? new SourcedValue<DateOnly>(expiry, evidence) : null,
            Wage = wage is int staffWage and >= 0 ? new SourcedValue<decimal>(staffWage, evidence) : null,
            Attributes = attributes,
            Knowledge = knowledge,
            Personality = personality
        };
    }

    private ClubRecord? ReadClub(ulong address, FieldProvenance evidence)
    {
        WindowsProcessMemoryAccessor.ValidateUserPointer(address, "club object");
        var data = _memory.ReadBytes(address, 0xF0);
        var uid = BitConverter.ToUInt32(data, 0x0C);
        if (uid == 0)
            return null;
        var name = ReadFmString(BitConverter.ToUInt64(data, 0xC0));
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var shortName = ReadFmString(BitConverter.ToUInt64(data, 0xC8));
        var nation = ReadNationName(BitConverter.ToUInt64(data, 0xE8));
        var info = SafeReadPointer(checked(address + 0x100));
        var founded = info == 0 ? null : TryReadUInt16(info + 0xD8);

        var facilities = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var indexedFields = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        if (info != 0)
        {
            AddFacility(facilities, "training_facilities", info + 0x120, evidence);
            AddFacility(facilities, "youth_facilities", info + 0x12B, evidence);
            AddFacility(facilities, "youth_coaching", info + 0x12C, evidence);
            AddFacility(facilities, "youth_recruitment", info + 0x12D, evidence);

            AddFacility(indexedFields, "club_training_facilities", info + 0x120, evidence);
            AddFacility(indexedFields, "club_youth_facilities", info + 0x12B, evidence);
            AddFacility(indexedFields, "club_youth_coaching", info + 0x12C, evidence);
            AddFacility(indexedFields, "club_youth_recruitment", info + 0x12D, evidence);
        }

        return new ClubRecord(new EntityIdentity(EntityKind.Club, uid.ToString(CultureInfo.InvariantCulture), _providerKey))
        {
            Name = new SourcedValue<string>(name, evidence),
            ShortName = shortName is not null ? new SourcedValue<string>(shortName, evidence) : null,
            NationName = nation is not null ? new SourcedValue<string>(nation, evidence) : null,
            FoundedYear = founded is ushort year and >= 1800 and <= 2100 ? new SourcedValue<int>(year, evidence) : null,
            Facilities = facilities,
            SearchIntegerFields = indexedFields
        };
    }

    private ulong[] ReadArray(ulong rva, string label)
    {
        var pairAddress = checked(_moduleBase + rva);
        var begin = _memory.ReadUInt64(pairAddress);
        var end = _memory.ReadUInt64(pairAddress + 8);
        if (begin == 0 && end == 0)
            return [];
        WindowsProcessMemoryAccessor.ValidateUserPointer(begin, $"{label} array start");
        WindowsProcessMemoryAccessor.ValidateUserPointer(end, $"{label} array end");
        if (end < begin || (end - begin) % 8 != 0)
            throw new InvalidDataException($"The {label} array pointers do not form a valid 64-bit pointer list.");
        var count = (end - begin) / 8;
        if (count > MaximumEntitiesPerKind)
            throw new InvalidDataException($"The {label} array count ({count:N0}) exceeds the safety limit.");
        if (count == 0)
            return [];
        var bytes = _memory.ReadBytes(begin, checked((int)(count * 8)));
        var pointers = new ulong[checked((int)count)];
        for (var index = 0; index < pointers.Length; index++)
            pointers[index] = BitConverter.ToUInt64(bytes, index * 8);
        return pointers;
    }

    private DateOnly ReadInGameDate()
    {
        var day = _memory.ReadUInt16(checked(_moduleBase + CurrentDateRva));
        var year = _memory.ReadUInt16(checked(_moduleBase + CurrentDateRva + 2));
        if (year is < 1900 or > 2200 || day is < 1 or > 366)
            throw new InvalidDataException($"The FM27 in-game date did not validate (raw day {day}, year {year}).");
        return new DateOnly(year, 1, 1).AddDays(day - 1);
    }

    private ulong? FindPlayerPersonShift(ulong address)
    {
        ulong? bestShift = null;
        var bestScore = 0;
        foreach (var shift in new ulong[] { 0, 0xF8 })
        {
            var uid = TryReadUInt32(address + 0x294 + shift);
            if (uid is null or 0)
                continue;
            var score = 1;
            if (ReadFmString(SafeReadPointer(address + 0x2C8 + shift)) is not null) score += 2;
            if (ReadNameEntry(SafeReadPointer(address + 0x2D8 + shift)) is not null) score++;
            if (ReadNameEntry(SafeReadPointer(address + 0x2E0 + shift)) is not null) score++;
            if (score > bestScore)
            {
                bestScore = score;
                bestShift = shift;
            }
        }
        return bestScore >= 2 ? bestShift : null;
    }

    private ulong? FindStaffPersonShift(ulong address)
    {
        ulong? bestShift = null;
        var bestScore = 0;
        foreach (var shift in new ulong[] { 0, 0x340 })
        {
            var uid = TryReadUInt32(address + 0x10C + shift);
            if (uid is null or 0)
                continue;
            var score = 1;
            if (ReadFmString(SafeReadPointer(address + 0x140 + shift)) is not null) score += 2;
            if (ReadNameEntry(SafeReadPointer(address + 0x150 + shift)) is not null) score++;
            if (ReadNameEntry(SafeReadPointer(address + 0x158 + shift)) is not null) score++;
            if (score > bestScore)
            {
                bestScore = score;
                bestShift = shift;
            }
        }
        return bestScore >= 2 ? bestShift : null;
    }

    private string? ReadNationName(ulong nation)
    {
        if (!WindowsProcessMemoryAccessor.IsUserPointer(nation))
            return null;
        return ReadFmString(SafeReadPointer(nation + 0x18));
    }

    private string? ReadPlayerClubName(ulong team)
    {
        if (!WindowsProcessMemoryAccessor.IsUserPointer(team))
            return null;
        var club = SafeReadPointer(team + 0x30);
        if (!WindowsProcessMemoryAccessor.IsUserPointer(club))
            return null;
        return ReadFmString(SafeReadPointer(club + 0xC0));
    }

    private string? ReadContractClubName(ulong contract)
    {
        var team = SafeReadPointer(contract + 0x10);
        if (!WindowsProcessMemoryAccessor.IsUserPointer(team))
            return null;
        var club = SafeReadPointer(team + 0x30);
        if (!WindowsProcessMemoryAccessor.IsUserPointer(club))
            return null;
        return ReadFmString(SafeReadPointer(club + 0xC0));
    }

    private string? ReadNameEntry(ulong entry)
    {
        if (!WindowsProcessMemoryAccessor.IsUserPointer(entry))
            return null;
        if (_nameEntries.TryGetValue(entry, out var cached))
            return cached;
        string? value = null;
        try
        {
            value = ReadFmString(_memory.ReadUInt64(entry));
        }
        catch
        {
        }
        _nameEntries[entry] = value;
        return value;
    }

    private string? ReadFmString(ulong pointer)
    {
        if (!WindowsProcessMemoryAccessor.IsUserPointer(pointer))
            return null;
        if (_strings.TryGetValue(pointer, out var cached))
            return cached;
        string? value = null;
        try
        {
            var length = _memory.ReadUInt32(pointer);
            if (length is > 0 and <= MaximumFmStringBytes)
            {
                var bytes = _memory.ReadBytes(pointer + 4, checked((int)length));
                value = StrictUtf8.GetString(bytes);
                if (value.Any(char.IsControl))
                    value = null;
            }
        }
        catch
        {
        }
        _strings[pointer] = value;
        return value;
    }

    private ulong SafeReadPointer(ulong address)
    {
        try
        {
            var pointer = _memory.ReadUInt64(address);
            return WindowsProcessMemoryAccessor.IsUserPointer(pointer) ? pointer : 0;
        }
        catch
        {
            return 0;
        }
    }

    private int? TryReadInt32(ulong address)
    {
        try { return _memory.ReadInt32(address); }
        catch { return null; }
    }

    private ushort? TryReadUInt16(ulong address)
    {
        try { return _memory.ReadUInt16(address); }
        catch { return null; }
    }

    private DateOnly? ReadFmDateSafe(ulong address)
    {
        try { return ReadFmDate(address); }
        catch { return null; }
    }

    private DateOnly? ReadFmDate(ulong address)
    {
        var day = _memory.ReadUInt16(address);
        var year = _memory.ReadUInt16(address + 2);
        if (year is < 1900 or > 2200 || day is < 1 or > 366)
            return null;
        return new DateOnly(year, 1, 1).AddDays(day - 1);
    }

    private static int CalculateAge(DateOnly birth, DateOnly today)
    {
        var age = today.Year - birth.Year;
        if (today < birth.AddYears(age))
            age--;
        return Math.Max(0, age);
    }

    private void AddFacility(
        IDictionary<string, SourcedValue<int>> fields,
        string key,
        ulong address,
        FieldProvenance evidence)
    {
        try
        {
            var value = _memory.ReadByte(address);
            if (value is >= 1 and <= 20)
                fields[key] = new SourcedValue<int>(value, evidence);
        }
        catch
        {
            // A missing club information block remains an unknown value, never zero.
        }
    }

    private static bool IsRecoverableEntityReadException(Exception exception) =>
        exception is Win32Exception or InvalidDataException or ArgumentException or OverflowException;
}
