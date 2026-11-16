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

    private static readonly string[] PreferredMoveNames =
    [
        "Runs with ball down left", "Runs with ball down right", "Runs with ball through centre",
        "Gets into opposition area", "Moves into channels", "Gets forward whenever possible",
        "Plays short simple passes", "Tries killer balls often", "Shoots from distance", "Shoots with power",
        "Places shots", "Curls ball", "Likes to round keeper", "Likes to try to beat offside trap",
        "Uses outside of foot", "Marks opponent tightly", "Winds up opponents", "Argues with officials",
        "Plays with back to goal", "Comes deep to get ball", "Plays one-twos", "Likes to lob keeper",
        "Dictates tempo", "Attempts overhead kicks", "Looks for pass rather than attempting to score",
        "Plays no through balls", "Stops play", "Knocks ball past opponent",
        "Moves ball to right foot before dribble attempt", "Moves ball to left foot before dribble attempt",
        "Dwells on ball", "Arrives late in opponents' area", "Tries to play way out of trouble",
        "Stays back at all times", "Avoids using weaker foot", "Tries tricks", "Tries long range free kicks",
        "Dives into tackles", "Does not dive into tackles", "Cuts inside", "Hugs line", "Gets crowd going",
        "Tries first time shots", "Tries long range passes", "Likes ball played into feet",
        "Hits free kicks with power", "Likes to beat man repeatedly", "Likes to switch ball to other flank",
        "Will retire at top", "Will play football as long as possible", "Possesses long flat throw",
        "Runs with ball often", "Runs with ball rarely", "Attempts to develop weaker foot", "Static target man",
        "Uses long throw to start counter attacks", "Refrains from taking long shots",
        "Cuts inside from left wing", "Cuts inside from right wing", "Crosses early",
        "Brings ball out of defence", "Plays ball with feet"
    ];

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
        fingerprint.Machine == 0x8664 &&
        fingerprint.PeTimestamp == PeTimestamp &&
        fingerprint.SizeOfImage == SizeOfImage;

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
                 private PlayerRecord? ReadPlayer(ulong address, FieldProvenance evidence, DateOnly inGameDate)
    {
        WindowsProcessMemoryAccessor.ValidateUserPointer(address, "player object");
        // One contiguous object read supplies the identity, attributes, ability, contracts
        // and shifted person-block fields, including the player-coach +0xF8 variant.
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
        var cityOfBirth = ReadCityName(U64(0x308, offset));
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
            // FM stores the ratings 0–100; its displayed scale is rounded and clamped to 1–20.
            var value = Math.Clamp((raw + 2) / 5, 1, 20);
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
            if (rating is >= 1 and <= 20)
                positions[PositionKeys[index]] = new SourcedValue<int>(rating, evidence);
        }

        var personality = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < PersonalityKeys.Length; index++)
        {
            var rating = B(0x2F8 + (ulong)index, offset);
            if (rating is >= 1 and <= 20)
                personality[PersonalityKeys[index]] = new SourcedValue<int>(rating, evidence);
        }

        var preferredMoves = new List<SourcedValue<string>>();
        var traitMask = U64(0x348, offset);
        for (var bit = 0; bit < PreferredMoveNames.Length; bit++)
        {
            if ((traitMask & (1UL << bit)) != 0)
                preferredMoves.Add(new SourcedValue<string>(PreferredMoveNames[bit], evidence));
        }

        var contract = U64(0x330, offset);
        byte[]? contractData = null;
        if (WindowsProcessMemoryAccessor.IsUserPointer(contract))
        {
            try { contractData = _memory.ReadBytes(contract, 0xC8); }
            catch (Win32Exception) { }
        }

        var wage = contractData is null ? (int?)null : BitConverter.ToInt32(contractData, 0x20);
        var contractSigned = contractData is null
            ? null
            : MakeFmDate(BitConverter.ToUInt16(contractData, 0x44), BitConverter.ToUInt16(contractData, 0x46));
        var contractExpiry = contractData is null
            ? null
            : MakeFmDate(BitConverter.ToUInt16(contractData, 0x48), BitConverter.ToUInt16(contractData, 0x4A));
        var contractJoined = contractData is null
            ? null
            : MakeFmDate(BitConverter.ToUInt16(contractData, 0x4C), BitConverter.ToUInt16(contractData, 0x4E));
        var squadStatus = contractData is null ? (byte?)null : contractData[0x54];
        var listingFlags = contractData is null ? (byte)0 : contractData[0x57];
        var squadNumberRaw = contractData is null ? (byte)255 : contractData[0x5D];
        var contractType = contractData is null ? (byte?)null : contractData[0xC3];
        var loyaltyBonus = contractData is null ? (int?)null : BitConverter.ToInt32(contractData, 0x38);
        var happiness = contractData is null ? (sbyte?)null : unchecked((sbyte)contractData[0x5A]);
        var playingTimeHappiness = contractData is null ? (sbyte?)null : unchecked((sbyte)contractData[0x5C]);

        var height = U16(0x22E);
        var currentAbility = U16(0x264);
        var rawPotential = I16(0x266);
        var transferValue = BitConverter.ToInt32(data, 0x234);
        var rawAskingPrice = BitConverter.ToInt32(data, 0x238);
        var homeReputation = U16(0x25E);
        var currentReputation = U16(0x260);
        var worldReputation = U16(0x262);
        var condition = U16(0x25C);
        var matchSharpness = U16(0x258);
        var jadedness = I16(0x25A);
        var morale = B(0x26C);
        var caps = U16(0x3BC, offset);
        var goals = U16(0x3BE, offset);
        var u21Caps = B(0x3C0, offset);
        var u21Goals = B(0x3C1, offset);
        var primaryPosition = positions.OrderByDescending(pair => pair.Value.Value).FirstOrDefault();
        var positionName = primaryPosition.Key is null || primaryPosition.Value.Value <= 0
            ? null
            : primaryPosition.Key.ToUpperInvariant();

        var searchIntegers = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        void AddSearchInt(string key, int? value)
        {
            if (value is int number)
                searchIntegers[key] = new SourcedValue<int>(number, evidence);
        }
        AddSearchInt("home_reputation", homeReputation);
        AddSearchInt("current_reputation", currentReputation);
        AddSearchInt("world_reputation", worldReputation);
        AddSearchInt("condition", condition);
        AddSearchInt("match_sharpness", matchSharpness);
        AddSearchInt("jadedness", jadedness);
        AddSearchInt("morale", morale);
        AddSearchInt("asking_price", rawAskingPrice >= 0 ? rawAskingPrice : null);
        AddSearchInt("u21_caps", u21Caps);
        AddSearchInt("u21_goals", u21Goals);
        AddSearchInt("squad_status", squadStatus);
        AddSearchInt("squad_number", squadNumberRaw != 255 ? squadNumberRaw : null);
        AddSearchInt("contract_type", contractType);
        AddSearchInt("loyalty_bonus", loyaltyBonus);
        AddSearchInt("contract_happiness", happiness);
        AddSearchInt("playing_time_happiness", playingTimeHappiness);

        var searchBooleans = new Dictionary<string, SourcedValue<bool>>(StringComparer.OrdinalIgnoreCase);
        if (contractData is not null)
        {
            searchBooleans["transfer_listed"] = new SourcedValue<bool>((listingFlags & 0x01) != 0, evidence);
            searchBooleans["loan_listed"] = new SourcedValue<bool>((listingFlags & 0x02) != 0, evidence);
        }

        var searchDates = new Dictionary<string, SourcedValue<DateOnly>>(StringComparer.OrdinalIgnoreCase);
        if (contractSigned is DateOnly signed)
            searchDates["contract_signed"] = new SourcedValue<DateOnly>(signed, evidence);
        if (contractJoined is DateOnly joined)
            searchDates["contract_joined"] = new SourcedValue<DateOnly>(joined, evidence);

        return new PlayerRecord(new EntityIdentity(EntityKind.Player, uid.ToString(CultureInfo.InvariantCulture), _providerKey))
        {
            FullName = new SourcedValue<string>(displayName, evidence),
            GameUid = new SourcedValue<string>(uid.ToString(CultureInfo.InvariantCulture), evidence),
            DateOfBirth = dateOfBirth is DateOnly dob ? new SourcedValue<DateOnly>(dob, evidence) : null,
            Age = age is int playerAge ? new SourcedValue<int>(playerAge, evidence) : null,
            Nationality = nationality is not null ? new SourcedValue<string>(nationality, evidence) : null,
            CityOfBirth = cityOfBirth is not null ? new SourcedValue<string>(cityOfBirth, evidence) : null,
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
            PreferredMoves = preferredMoves.AsReadOnly(),
            SearchIntegerFields = searchIntegers,
            SearchBooleanFields = searchBooleans,
            SearchDateFields = searchDates,
            IsNewgen = new SourcedValue<bool>((madeByGameFlags & 0x08) != 0, evidence)
        };
    }

? new SourcedValue<DateOnly>(dob, evidence) : null,
            Age = age is int playerAge ? new SourcedValue<int>(playerAge, evidence) : null,
            Nationality = nationality is not null ? new SourcedValue<string>(nationality, evidence) : null,
            CityOfBirth = cityOfBirth is not null ? new SourcedValue<string>(cityOfBirth, evidence) : null,
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
            SearchIntegerFields = searchIntegers,
            SearchBooleanFields = searchBooleans,
            SearchDateFields = searchDates,
            IsNewgen = new SourcedValue<bool>((madeByGameFlags & 0x08) != 0, evidence)
        };
    }

    private StaffRecord? ReadStaff(ulong address, FieldProvenance evidence, DateOnly inGameDate)
    {
        WindowsProcessMemoryAccessor.ValidateUserPointer(address, "staff object");
        // One bulk read covers the person fields and the human-manager +0x340 shift.
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
            var value = Math.Clamp((raw + 2) / 5, 1, 20);
            if (key.StartsWith("knowledge_", StringComparison.Ordinal))
                knowledge[key["knowledge_".Length..]] = new SourcedValue<int>(value, evidence);
            else
                attributes[key] = new SourcedValue<int>(value, evidence);
        }

        var workingWithYoungsters = B(0x1C);
        if (workingWithYoungsters is >= 1 and <= 20)
            attributes["attribute_working_with_youngsters"] = new SourcedValue<int>(workingWithYoungsters, evidence);
        var authority = B(0x14);
        if (authority is >= 1 and <= 20)
            attributes["attribute_authority"] = new SourcedValue<int>(authority, evidence);

        var personality = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < PersonalityKeys.Length; index++)
        {
            var rating = B(0x170 + (ulong)index, offset);
            if (rating is >= 1 and <= 20)
                personality[PersonalityKeys[index]] = new SourcedValue<int>(rating, evidence);
        }

        var contract = U64(0x1A8, offset);
        byte[]? contractData = null;
        if (WindowsProcessMemoryAccessor.IsUserPointer(contract))
        {
            try { contractData = _memory.ReadBytes(contract, 0xC8); }
            catch (Win32Exception) { }
        }

        var wage = contractData is null ? (int?)null : BitConverter.ToInt32(contractData, 0x20);
        var contractSigned = contractData is null
            ? null
            : MakeFmDate(BitConverter.ToUInt16(contractData, 0x44), BitConverter.ToUInt16(contractData, 0x46));
        var contractExpiry = contractData is null
            ? null
            : MakeFmDate(BitConverter.ToUInt16(contractData, 0x48), BitConverter.ToUInt16(contractData, 0x4A));
        var contractJoined = contractData is null
            ? null
            : MakeFmDate(BitConverter.ToUInt16(contractData, 0x4C), BitConverter.ToUInt16(contractData, 0x4E));
        var jobCode = contractData is null ? (byte?)null : contractData[0x26];
        var contractType = contractData is null ? (byte?)null : contractData[0xC3];
        var primaryRole = jobCode is byte code ? StaffRoleName(code) : null;
        var clubName = WindowsProcessMemoryAccessor.IsUserPointer(contract) ? ReadContractClubName(contract) : null;

        var searchIntegers = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        void AddSearchInt(string key, int? value)
        {
            if (value is int number)
                searchIntegers[key] = new SourcedValue<int>(number, evidence);
        }
        AddSearchInt("home_reputation", U16(0xD4));
        AddSearchInt("current_reputation", U16(0xD6));
        AddSearchInt("world_reputation", U16(0xD8));
        AddSearchInt("job_code", jobCode);
        AddSearchInt("contract_type", contractType);

        var searchDates = new Dictionary<string, SourcedValue<DateOnly>>(StringComparer.OrdinalIgnoreCase);
        if (contractSigned is DateOnly signed)
            searchDates["contract_signed"] = new SourcedValue<DateOnly>(signed, evidence);
        if (contractJoined is DateOnly joined)
            searchDates["contract_joined"] = new SourcedValue<DateOnly>(joined, evidence);

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
            ClubName = clubName is not null ? new SourcedValue<string>(clubName, evidence) : null,
            PrimaryRole = primaryRole is not null ? new SourcedValue<string>(primaryRole, evidence) : null,
            CurrentAbility = currentAbility is > 0 and <= 200 ? new SourcedValue<int>(currentAbility, evidence) : null,
            PotentialAbility = potentialAbility is > 0 and <= 200 ? new SourcedValue<int>(potentialAbility, evidence) : null,
            ContractExpires = contractExpiry is DateOnly expiry ? new SourcedValue<DateOnly>(expiry, evidence) : null,
            Wage = wage is int staffWage and >= 0 ? new SourcedValue<decimal>(staffWage, evidence) : null,
            Attributes = attributes,
            Knowledge = knowledge,
            Personality = personality,
            SearchIntegerFields = searchIntegers,
            SearchDateFields = searchDates
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
        var dayField = _memory.ReadUInt16(checked(_moduleBase + CurrentDateRva));
        var day = (ushort)(dayField & 0x1FF);
        var year = _memory.ReadUInt16(checked(_moduleBase + CurrentDateRva + 2));
        if (year is <= 1900 or > 2200)
            throw new InvalidDataException($"The FM27 in-game date did not validate (raw day field 0x{dayField:X4}, year {year}).");
        var lastDay = DateTime.IsLeapYear(year) ? 366 : 365;
        if (day is < 1 || day > lastDay)
            throw new InvalidDataException($"The FM27 in-game date did not validate (raw day field 0x{dayField:X4}, year {year}).");
        return new DateOnly(year, 1, 1).AddDays(day - 1);
    }

    private ulong? FindPlayerPersonShift(ulong address, byte[] data)
    {
        ulong? bestShift = null;
        var bestScore = 0;
        foreach (var shift in new ulong[] { 0, 0xF8 })
        {
            var uid = BitConverter.ToUInt32(data, checked((int)(0x294 + shift)));
            if (uid == 0)
                continue;

            var score = 1;
            var fullName = BitConverter.ToUInt64(data, checked((int)(0x2C8 + shift)));
            var firstName = BitConverter.ToUInt64(data, checked((int)(0x2D8 + shift)));
            var lastName = BitConverter.ToUInt64(data, checked((int)(0x2E0 + shift)));
            if (ReadFmString(fullName) is not null) score += 2;
            if (ReadNameEntry(firstName) is not null) score++;
            if (ReadNameEntry(lastName) is not null) score++;
            if (score > bestScore)
            {
                bestScore = score;
                bestShift = shift;
            }
        }

        return bestScore >= 2 ? bestShift : null;
    }

    private ulong? FindStaffPersonShift(ulong address, byte[] data)
    {
        ulong? bestShift = null;
        var bestScore = 0;
        foreach (var shift in new ulong[] { 0, 0x340 })
        {
            var uid = BitConverter.ToUInt32(data, checked((int)(0x10C + shift)));
            if (uid == 0)
                continue;

            var score = 1;
            var fullName = BitConverter.ToUInt64(data, checked((int)(0x140 + shift)));
            var firstName = BitConverter.ToUInt64(data, checked((int)(0x150 + shift)));
            var lastName = BitConverter.ToUInt64(data, checked((int)(0x158 + shift)));
            if (ReadFmString(fullName) is not null) score += 2;
            if (ReadNameEntry(firstName) is not null) score++;
            if (ReadNameEntry(lastName) is not null) score++;
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

    private string? ReadCityName(ulong city)
    {
        if (!WindowsProcessMemoryAccessor.IsUserPointer(city))
            return null;
        return ReadFmString(SafeReadPointer(city + 0x18));
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

    private static string? StaffRoleName(byte code)
    {
        var baseCode = code > 1 && code % 2 == 1 ? code - 1 : code;
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

    private static DateOnly? MakeFmDate(ushort dayField, ushort year)
    {
        // FM27 reserves the upper bits of its day field for flags; only the low
        // nine bits carry the one-based day of year.
        var day = (ushort)(dayField & 0x1FF);
        if (year is <= 1900 or > 2200 || day == 0)
            return null;
        var lastDay = DateTime.IsLeapYear(year) ? 366 : 365;
        if (day > lastDay)
            return null;
        return new DateOnly(year, 1, 1).AddDays(day - 1);
    }

    private DateOnly? ReadFmDateSafe(ulong address)
    {
        try { return ReadFmDate(address); }
        catch { return null; }
    }

    private DateOnly? ReadFmDate(ulong address) =>
        MakeFmDate(_memory.ReadUInt16(address), _memory.ReadUInt16(address + 2));

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
