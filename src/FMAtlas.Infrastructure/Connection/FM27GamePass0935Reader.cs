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
        ("knowledge_judging_potential", 0x2D), ("attribute_people_management", 0x2E),
        ("attribute_motivating", 0x2F), ("attribute_physiotherapy", 0x30),
        ("knowledge_tactical_knowledge", 0x31), ("attribute_attacking", 0x32),
        ("attribute_defending", 0x33), ("attribute_fitness", 0x34),
        ("attribute_possession", 0x35), ("attribute_technical", 0x36),
        ("attribute_tactical", 0x37), ("knowledge_data_analysis", 0x3C),
        ("attribute_sports_science", 0x3F), ("knowledge_negotiating", 0x41),
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
    private readonly Dictionary<ulong, ulong?> _playerPersonShifts = [];
    private readonly Dictionary<ulong, ulong?> _staffPersonShifts = [];
    private readonly Dictionary<ulong, (string? ClubName, string? LeagueName, string? TeamKind, uint? ClubUid)> _teamInfo = [];
    private readonly Dictionary<ulong, string?> _nationNames = [];
    private readonly Dictionary<ulong, string?> _cityNames = [];

    public FM27LiveReadSummary? LastReadSummary { get; private set; }

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

    public IEnumerable<IEntityRecord> EnumerateRecords(
        IProgress<LiveGameLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        LastReadSummary = null;
        var playerPointers = ReadArray(PlayersArrayRva, "players");
        var staffPointers = ReadArray(StaffArrayRva, "staff");
        var clubPointers = ReadArray(ClubsArrayRva, "clubs");
        var inGameDate = ReadInGameDate();

        // Reuse object buffers. Records are yielded directly into the transactional
        // importer; the whole career is never retained as a managed object graph.
        var playerBuffer = new byte[0x4C0];
        var staffBuffer = new byte[0x4F0];
        var clubBuffer = new byte[0xF0];
        var contractBuffer = new byte[0xC8];
        var evidence = new FieldProvenance(
            DataSourceKind.LiveGameMemory,
            FieldEvidenceStatus.Mapped,
            SourceVersion: BuildVersion,
            TargetBuildFingerprint: _fingerprint,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            Description: "Read from the externally mapped FM27 Game Pass 0.9.2435.0 layout. FM Atlas has not yet independently verified edit/write behaviour.");

        var playerRead = 0;
        var staffRead = 0;
        var clubRead = 0;
        var playerSkipped = 0;
        var staffSkipped = 0;
        var clubSkipped = 0;
        var failures = new List<string>(capacity: 8);
        var processed = 0;
        var total = Math.Max(1, playerPointers.Length + staffPointers.Length + clubPointers.Length);

        void NoteFailure(string entity, ulong address, Exception? exception)
        {
            if (failures.Count >= 8)
                return;
            var reason = exception is null ? "no usable identity/name block" : $"{exception.GetType().Name}: {exception.Message}";
            failures.Add($"{entity} 0x{address:X}: {reason}");
        }

        progress?.Report(new LiveGameLoadProgress(18, "Reading players",
            $"Preparing {playerPointers.Length:N0} player entries; caching FM classes, shared teams and nations."));
        foreach (var address in playerPointers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PlayerRecord? player = null;
            try
            {
                player = ReadPlayer(address, evidence, inGameDate, playerBuffer, contractBuffer);
            }
            catch (Exception exception) when (IsRecoverableEntityReadException(exception))
            {
                NoteFailure("Player", address, exception);
            }

            if (player is null)
            {
                playerSkipped++;
                NoteFailure("Player", address, null);
            }
            else
            {
                playerRead++;
                yield return player;
            }

            processed++;
            if (processed % 100 == 0 || processed == playerPointers.Length)
            {
                var percent = 18 + (int)(48.0 * processed / Math.Max(1, playerPointers.Length));
                progress?.Report(new LiveGameLoadProgress(percent, "Reading players",
                    $"Read {processed:N0} of {playerPointers.Length:N0} entries · kept {playerRead:N0} · skipped {playerSkipped:N0}."));
            }
        }

        var staffStart = processed;
        progress?.Report(new LiveGameLoadProgress(67, "Reading staff",
            $"Preparing {staffPointers.Length:N0} staff entries."));
        foreach (var address in staffPointers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StaffRecord? staff = null;
            try
            {
                staff = ReadStaff(address, evidence, inGameDate, staffBuffer, contractBuffer);
            }
            catch (Exception exception) when (IsRecoverableEntityReadException(exception))
            {
                NoteFailure("Staff", address, exception);
            }

            if (staff is null)
            {
                staffSkipped++;
                NoteFailure("Staff", address, null);
            }
            else
            {
                staffRead++;
                yield return staff;
            }

            processed++;
            if (processed % 100 == 0 || processed == playerPointers.Length + staffPointers.Length)
            {
                var percent = 67 + (int)(16.0 * (processed - staffStart) / Math.Max(1, staffPointers.Length));
                progress?.Report(new LiveGameLoadProgress(percent, "Reading staff",
                    $"Read {processed - playerPointers.Length:N0} of {staffPointers.Length:N0} entries · kept {staffRead:N0} · skipped {staffSkipped:N0}."));
            }
        }

        var clubStart = processed;
        progress?.Report(new LiveGameLoadProgress(84, "Reading clubs",
            $"Preparing {clubPointers.Length:N0} club entries."));
        foreach (var address in clubPointers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClubRecord? club = null;
            try
            {
                club = ReadClub(address, evidence, clubBuffer);
            }
            catch (Exception exception) when (IsRecoverableEntityReadException(exception))
            {
                NoteFailure("Club", address, exception);
            }

            if (club is null)
            {
                clubSkipped++;
                NoteFailure("Club", address, null);
            }
            else
            {
                clubRead++;
                yield return club;
            }

            processed++;
            if (processed % 100 == 0 || processed == total)
            {
                var percent = 84 + (int)(11.0 * (processed - clubStart) / Math.Max(1, clubPointers.Length));
                progress?.Report(new LiveGameLoadProgress(percent, "Reading clubs",
                    $"Read {processed - playerPointers.Length - staffPointers.Length:N0} of {clubPointers.Length:N0} entries · kept {clubRead:N0} · skipped {clubSkipped:N0}."));
            }
        }

        LastReadSummary = new FM27LiveReadSummary(
            playerPointers.Length, playerRead, playerSkipped,
            staffPointers.Length, staffRead, staffSkipped,
            clubPointers.Length, clubRead, clubSkipped,
            failures.AsReadOnly());

        progress?.Report(new LiveGameLoadProgress(96, "Preparing live index",
            $"{playerRead + staffRead + clubRead:N0} usable records collected. {LastReadSummary.Summary}"));
    }

    private PlayerRecord? ReadPlayer(
        ulong address,
        FieldProvenance evidence,
        DateOnly inGameDate,
        byte[] data,
        byte[] contractBuffer)
    {
        WindowsProcessMemoryAccessor.ValidateUserPointer(address, "player object");
        // One contiguous object read supplies identity, attributes, ability, contracts
        // and shifted person-block fields, including the player-coach +0xF8 variant.
        _memory.ReadBytes(address, data);
        var objectClass = BitConverter.ToUInt64(data, 0);
        if (!WindowsProcessMemoryAccessor.IsUserPointer(objectClass))
            return null;
        if (!_playerPersonShifts.TryGetValue(objectClass, out var shift))
        {
            shift = FindPlayerPersonShift(address, data);
            // Do not cache an unresolved class: its first array member may be a
            // placeholder. A later member of the same class may expose the UID.
            if (shift is null)
                return null;
            _playerPersonShifts[objectClass] = shift;
        }
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
        int? age = dateOfBirth is DateOnly birth ? CalculateAge(birth, inGameDate) : null;
        var nationality = ReadNationName(U64(0x2F0, offset));
        var (secondNationality, relationSearchText) = ReadPlayerRelations(U64(0x300, offset), evidence);
        var cityOfBirth = ReadCityName(U64(0x308, offset));
        var teamInfo = ReadPlayerTeam(U64(0x130));
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
            try
            {
                _memory.ReadBytes(contract, contractBuffer);
                contractData = contractBuffer;
            }
            catch (Exception exception) when (IsRecoverableEntityReadException(exception))
            {
                // Contract data is optional for a searchable player record.
            }
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
        AddSearchInt("club_uid", teamInfo.ClubUid is uint clubUid && clubUid <= int.MaxValue ? (int)clubUid : null);
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

        var searchText = new Dictionary<string, SourcedValue<string>>(relationSearchText, StringComparer.OrdinalIgnoreCase);
        if (teamInfo.TeamKind is not null)
            searchText["team_kind"] = new SourcedValue<string>(teamInfo.TeamKind, evidence);

        var searchDates = new Dictionary<string, SourcedValue<DateOnly>>(StringComparer.OrdinalIgnoreCase)
        {
            ["current_game_date"] = new SourcedValue<DateOnly>(inGameDate, evidence)
        };
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
            SecondNationality = secondNationality is not null ? new SourcedValue<string>(secondNationality, evidence) : null,
            CityOfBirth = cityOfBirth is not null ? new SourcedValue<string>(cityOfBirth, evidence) : null,
            Gender = new SourcedValue<string>((genderFlags & 0x10) != 0 ? "Female" : "Male", evidence),
            ClubName = teamInfo.ClubName is not null ? new SourcedValue<string>(teamInfo.ClubName, evidence) : null,
            TeamName = teamInfo.TeamKind is not null ? new SourcedValue<string>(teamInfo.TeamKind, evidence) : null,
            LeagueName = teamInfo.LeagueName is not null ? new SourcedValue<string>(teamInfo.LeagueName, evidence) : null,
            PrimaryPosition = positionName is not null ? new SourcedValue<string>(positionName, evidence) : null,
            HeightCentimetres = height is > 0 and < 300 ? new SourcedValue<decimal>(height, evidence) : null,
            CurrentAbility = currentAbility is > 0 and <= 200 ? new SourcedValue<int>(currentAbility, evidence) : null,
            PotentialAbility = rawPotential is >= -200 and <= 200 ? new SourcedValue<int>(rawPotential, evidence) : null,
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
            SearchTextFields = searchText,
            IsNewgen = new SourcedValue<bool>((madeByGameFlags & 0x08) != 0, evidence)
        };
    }

    private StaffRecord? ReadStaff(
        ulong address,
        FieldProvenance evidence,
        DateOnly inGameDate,
        byte[] data,
        byte[] contractBuffer)
    {
        WindowsProcessMemoryAccessor.ValidateUserPointer(address, "staff object");
        _memory.ReadBytes(address, data);
        var objectClass = BitConverter.ToUInt64(data, 0);
        if (!WindowsProcessMemoryAccessor.IsUserPointer(objectClass))
            return null;
        if (!_staffPersonShifts.TryGetValue(objectClass, out var shift))
        {
            shift = FindStaffPersonShift(address, data);
            // A null/empty object must not poison the class-level shift cache.
            if (shift is null)
                return null;
            _staffPersonShifts[objectClass] = shift;
        }
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
        // The dedicated human-manager class can also hold an account placeholder
        // that has no person record. Do not index that placeholder as a staff member.
        if (offset != 0 && dateOfBirth is null)
            return null;
        int? age = dateOfBirth is DateOnly birth ? CalculateAge(birth, inGameDate) : null;
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
            try
            {
                _memory.ReadBytes(contract, contractBuffer);
                contractData = contractBuffer;
            }
            catch (Exception exception) when (IsRecoverableEntityReadException(exception))
            {
                // Free agents and partly mapped contracts are still searchable staff records.
            }
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
        var contractClubUid = WindowsProcessMemoryAccessor.IsUserPointer(contract) ? ReadContractClubUid(contract) : null;

        var searchIntegers = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        void AddSearchInt(string key, int? value)
        {
            if (value is int number)
                searchIntegers[key] = new SourcedValue<int>(number, evidence);
        }
        AddSearchInt("club_uid", contractClubUid is uint clubUid && clubUid <= int.MaxValue ? (int)clubUid : null);
        AddSearchInt("home_reputation", U16(0xD4));
        AddSearchInt("current_reputation", U16(0xD6));
        AddSearchInt("world_reputation", U16(0xD8));
        AddSearchInt("job_code", jobCode);
        AddSearchInt("contract_type", contractType);

        var searchDates = new Dictionary<string, SourcedValue<DateOnly>>(StringComparer.OrdinalIgnoreCase)
        {
            ["current_game_date"] = new SourcedValue<DateOnly>(inGameDate, evidence)
        };
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
            IsHumanManager = new SourcedValue<bool>(offset == 0x340, evidence),
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

    private ClubRecord? ReadClub(ulong address, FieldProvenance evidence, byte[] data)
    {
        WindowsProcessMemoryAccessor.ValidateUserPointer(address, "club object");
        _memory.ReadBytes(address, data);
        var uid = BitConverter.ToUInt32(data, 0x0C);
        if (uid == 0)
            return null;

        var name = ReadFmString(BitConverter.ToUInt64(data, 0xC0));
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var shortName = ReadFmString(BitConverter.ToUInt64(data, 0xC8));
        var nation = ReadNationName(BitConverter.ToUInt64(data, 0xE8));
        var womenFlag = data[0xB9];
        var info = SafeReadPointer(checked(address + 0x100));
        var finance = SafeReadPointer(checked(address + 0x150));
        var teams = BitConverter.ToUInt64(data, 0x18);
        var seniorTeam = SafeReadPointer(teams);

        var founded = info == 0 ? null : TryReadUInt16(info + 0xD8);
        var facilities = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var indexedIntegers = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var indexedBooleans = new Dictionary<string, SourcedValue<bool>>(StringComparer.OrdinalIgnoreCase);
        var indexedText = new Dictionary<string, SourcedValue<string>>(StringComparer.OrdinalIgnoreCase);
        var indexedDecimals = new Dictionary<string, SourcedValue<decimal>>(StringComparer.OrdinalIgnoreCase);
        var finances = new Dictionary<string, SourcedValue<decimal>>(StringComparer.OrdinalIgnoreCase);

        if (info != 0)
        {
            AddFacility(facilities, "training_facilities", info + 0x120, evidence);
            AddFacility(facilities, "youth_facilities", info + 0x12B, evidence);
            AddFacility(facilities, "youth_coaching", info + 0x12C, evidence);
            AddFacility(facilities, "youth_recruitment", info + 0x12D, evidence);

            AddFacility(indexedIntegers, "club_training_facilities", info + 0x120, evidence);
            AddFacility(indexedIntegers, "club_youth_facilities", info + 0x12B, evidence);
            AddFacility(indexedIntegers, "club_youth_coaching", info + 0x12C, evidence);
            AddFacility(indexedIntegers, "club_youth_recruitment", info + 0x12D, evidence);

            AddUInt32Field(indexedIntegers, "club_season_tickets", TryReadUInt32(info + 0xC0), evidence, 0, 100_000_000);
            AddUInt32Field(indexedIntegers, "club_social_followers", TryReadUInt32(info + 0xF8), evidence, 0, 2_000_000_000);

            var supporterMixKeys = new[]
            {
                "supporter_mix_hardcore", "supporter_mix_core", "supporter_mix_family",
                "supporter_mix_fair_weather", "supporter_mix_corporate", "supporter_mix_casual"
            };
            var supporterTraitKeys = new[]
            {
                "supporter_trait_loyalty", "supporter_trait_passion", "supporter_trait_patience",
                "supporter_trait_affluence", "supporter_trait_temperament", "supporter_trait_expectations"
            };
            try
            {
                var mix = _memory.ReadBytes(info + 0x106, 6);
                for (var index = 0; index < supporterMixKeys.Length; index++)
                    if (mix[index] <= 100)
                        indexedIntegers[supporterMixKeys[index]] = new SourcedValue<int>(mix[index], evidence);
            }
            catch (Win32Exception) { }

            try
            {
                var traits = _memory.ReadBytes(info + 0x114, 6);
                for (var index = 0; index < supporterTraitKeys.Length; index++)
                    if (traits[index] is >= 1 and <= 20)
                        indexedIntegers[supporterTraitKeys[index]] = new SourcedValue<int>(traits[index], evidence);
            }
            catch (Win32Exception) { }
        }

        var reputation = seniorTeam == 0 ? null : TryReadUInt16(seniorTeam + 0xA8);
        if (reputation is ushort clubReputation && clubReputation <= 10_000)
            indexedIntegers["club_reputation"] = new SourcedValue<int>(clubReputation, evidence);

        string? competitionName = null;
        if (seniorTeam != 0)
        {
            var competition = SafeReadPointer(seniorTeam + 0x50);
            if (competition != 0)
            {
                var competitionUid = TryReadUInt32(competition + 0x0C);
                competitionName = ReadFmString(SafeReadPointer(competition + 0x40));
                AddUInt32Field(indexedIntegers, "competition_uid", competitionUid, evidence, 1, int.MaxValue);
            }

            var stadium = SafeReadPointer(seniorTeam + 0x78);
            if (stadium != 0)
            {
                var stadiumName = ReadFmString(SafeReadPointer(stadium + 0x40));
                if (stadiumName is not null)
                    indexedText["stadium_name"] = new SourcedValue<string>(stadiumName, evidence);

                AddUInt32Field(indexedIntegers, "stadium_capacity", TryReadUInt32(stadium + 0x7C), evidence, 0, 2_000_000);
                AddUInt32Field(indexedIntegers, "stadium_seating", TryReadUInt32(stadium + 0x80), evidence, 0, 2_000_000);
                AddUInt32Field(indexedIntegers, "stadium_used", TryReadUInt32(stadium + 0x84), evidence, 0, 2_000_000);
                AddUInt32Field(indexedIntegers, "stadium_expansion", TryReadUInt32(stadium + 0x8C), evidence, 0, 2_000_000);
            }
        }

        if (womenFlag is 0 or 1)
            indexedBooleans["club_is_women"] = new SourcedValue<bool>(womenFlag == 1, evidence);

        if (finance != 0)
        {
            AddFinanceField(finances, "balance", TryReadInt32(finance + 0x0C), evidence, -100_000_000_000m, 100_000_000_000m);
            AddFinanceField(finances, "transfer_budget", TryReadInt32(finance + 0x7C4), evidence, 0, 100_000_000_000m);
            AddFinanceField(finances, "total_wage_budget", TryReadInt32(finance + 0x808), evidence, 0, 10_000_000_000m);
            AddFinanceField(finances, "wage_budget", TryReadInt32(finance + 0x814), evidence, 0, 10_000_000_000m);
            var ticketPrice = TryReadSingle(finance + 0x864);
            if (ticketPrice is float price && float.IsFinite(price) && price is >= 0 and <= 100_000)
                finances["ticket_price"] = new SourcedValue<decimal>(Convert.ToDecimal(price), evidence);

            CopyFinanceToSearch(finances, "balance", "club_balance", indexedDecimals);
            CopyFinanceToSearch(finances, "transfer_budget", "club_transfer_budget", indexedDecimals);
            CopyFinanceToSearch(finances, "total_wage_budget", "club_total_wage_budget", indexedDecimals);
            CopyFinanceToSearch(finances, "wage_budget", "club_wage_budget", indexedDecimals);
            CopyFinanceToSearch(finances, "ticket_price", "club_ticket_price", indexedDecimals);

            var corporate = TryReadByte(finance + 0x8A9);
            if (corporate is byte rating and >= 1 and <= 20)
                indexedIntegers["club_corporate_facilities"] = new SourcedValue<int>(rating, evidence);
        }

        return new ClubRecord(new EntityIdentity(EntityKind.Club, uid.ToString(CultureInfo.InvariantCulture), _providerKey))
        {
            Name = new SourcedValue<string>(name, evidence),
            ShortName = shortName is not null ? new SourcedValue<string>(shortName, evidence) : null,
            NationName = nation is not null ? new SourcedValue<string>(nation, evidence) : null,
            CompetitionName = competitionName is not null ? new SourcedValue<string>(competitionName, evidence) : null,
            StadiumName = indexedText.TryGetValue("stadium_name", out var stadiumValue) ? stadiumValue : null,
            StadiumCapacity = indexedIntegers.TryGetValue("stadium_capacity", out var capacityValue) ? capacityValue : null,
            FoundedYear = founded is ushort year and >= 1800 and <= 2100 ? new SourcedValue<int>(year, evidence) : null,
            Reputation = reputation is ushort reputationValue and <= 10_000 ? new SourcedValue<int>(reputationValue, evidence) : null,
            Facilities = facilities,
            Finances = finances,
            SearchIntegerFields = indexedIntegers,
            SearchDecimalFields = indexedDecimals,
            SearchBooleanFields = indexedBooleans,
            SearchTextFields = indexedText
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
            var commonName = BitConverter.ToUInt64(data, checked((int)(0x2E8 + shift)));
            if (ReadFmString(fullName) is not null) score += 2;
            if (ReadNameEntry(firstName) is not null) score++;
            if (ReadNameEntry(lastName) is not null) score++;
            // Some name-patched records rely on their common name when the
            // first/last-name entries or full-name string are absent.
            if (ReadNameEntry(commonName) is not null) score++;
            if (score > bestScore)
            {
                bestScore = score;
                bestShift = shift;
            }
        }

        // UID is sufficient to retain the record. Names can be absent on
        // name-patched saves; ReadPlayer supplies a UID fallback for display.
        return bestScore > 0 ? bestShift : null;
    }

    private ulong? FindStaffPersonShift(ulong address, byte[] data)
    {
        ulong? bestShift = null;
        var bestScore = 0;
        // The dedicated human-manager person block is stored at +0x340.
        // Check it first so an equal-confidence match does not hide the manager
        // behind the generic staff block at +0.
        foreach (var shift in new ulong[] { 0x340, 0 })
        {
            var uid = BitConverter.ToUInt32(data, checked((int)(0x10C + shift)));
            if (uid == 0)
                continue;

            var score = 1;
            var fullName = BitConverter.ToUInt64(data, checked((int)(0x140 + shift)));
            var firstName = BitConverter.ToUInt64(data, checked((int)(0x150 + shift)));
            var lastName = BitConverter.ToUInt64(data, checked((int)(0x158 + shift)));
            var commonName = BitConverter.ToUInt64(data, checked((int)(0x160 + shift)));
            if (ReadFmString(fullName) is not null) score += 2;
            if (ReadNameEntry(firstName) is not null) score++;
            if (ReadNameEntry(lastName) is not null) score++;
            if (ReadNameEntry(commonName) is not null) score++;
            if (score > bestScore)
            {
                bestScore = score;
                bestShift = shift;
            }
        }

        // Keep staff with a valid UID even when a name patch leaves every
        // string pointer empty. ReadStaff falls back to the UID for display.
        return bestScore > 0 ? bestShift : null;
    }

    private (string? SecondNationality, Dictionary<string, SourcedValue<string>> SearchFields) ReadPlayerRelations(
        ulong relationHeader,
        FieldProvenance evidence)
    {
        var fields = new Dictionary<string, SourcedValue<string>>(StringComparer.OrdinalIgnoreCase);
        if (!WindowsProcessMemoryAccessor.IsUserPointer(relationHeader))
            return (null, fields);

        try
        {
            // Relation record starts with (begin, end, capacity), followed by 16-byte entries.
            var header = _memory.ReadBytes(relationHeader, 24);
            var begin = BitConverter.ToUInt64(header, 0);
            var end = BitConverter.ToUInt64(header, 8);
            var capacity = BitConverter.ToUInt64(header, 16);
            if (begin == 0 && end == 0 && capacity == 0)
                return (null, fields);

            const ulong entrySize = 16;
            const ulong maximumEntries = 256;
            if (!WindowsProcessMemoryAccessor.IsUserPointer(begin) ||
                end < begin || capacity < end ||
                (end - begin) % entrySize != 0 ||
                (capacity - begin) % entrySize != 0 ||
                capacity - begin > entrySize * maximumEntries)
                return (null, fields);

            var count = (end - begin) / entrySize;
            if (count == 0)
                return (null, fields);

            var bytes = _memory.ReadBytes(begin, checked((int)(end - begin)));
            string? secondNationality = null;
            var trainedNation = 0;
            var trainedClub = 0;
            var workPermitNation = 0;

            for (var index = 0; index < checked((int)count); index++)
            {
                var offset = index * (int)entrySize;
                var target = BitConverter.ToUInt64(bytes, offset);
                var targetKind = bytes[offset + 0x0A];
                var relationKind = bytes[offset + 0x0B];
                if (!WindowsProcessMemoryAccessor.IsUserPointer(target))
                    continue;

                if (targetKind == 8 && relationKind == 9)
                {
                    secondNationality ??= ReadNationName(target);
                }
                else if (targetKind == 8 && relationKind == 70)
                {
                    var name = ReadNationName(target);
                    if (name is not null)
                        fields[$"trained_in_nation_{++trainedNation}"] = new SourcedValue<string>(name, evidence);
                }
                else if (targetKind == 1 && relationKind == 72)
                {
                    var name = ReadFmString(SafeReadPointer(target + 0xC0));
                    if (name is not null)
                        fields[$"trained_at_club_{++trainedClub}"] = new SourcedValue<string>(name, evidence);
                }
                else if (targetKind == 8 && relationKind == 109)
                {
                    var name = ReadNationName(target);
                    if (name is not null)
                        fields[$"work_permit_nation_{++workPermitNation}"] = new SourcedValue<string>(name, evidence);
                }
            }

            return (secondNationality, fields);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidDataException or OverflowException or ArgumentException)
        {
            // A malformed relationship list is unknown, not a reason to invent a nationality or abort all player reads.
            return (null, fields);
        }
    }

    private string? ReadNationName(ulong nation)
    {
        if (!WindowsProcessMemoryAccessor.IsUserPointer(nation))
            return null;
        if (_nationNames.TryGetValue(nation, out var cached))
            return cached;

        var name = ReadFmString(SafeReadPointer(nation + 0x18));
        _nationNames[nation] = name;
        return name;
    }

    private string? ReadCityName(ulong city)
    {
        if (!WindowsProcessMemoryAccessor.IsUserPointer(city))
            return null;
        if (_cityNames.TryGetValue(city, out var cached))
            return cached;

        var name = ReadFmString(SafeReadPointer(city + 0x18));
        _cityNames[city] = name;
        return name;
    }

    private (string? ClubName, string? LeagueName, string? TeamKind, uint? ClubUid) ReadPlayerTeam(ulong team)
    {
        if (!WindowsProcessMemoryAccessor.IsUserPointer(team))
            return (null, null, null, null);
        if (_teamInfo.TryGetValue(team, out var cached))
            return cached;

        var clubObject = SafeReadPointer(team + 0x30);
        var clubName = clubObject == 0 ? null : ReadFmString(SafeReadPointer(clubObject + 0xC0));
        var clubUid = clubObject == 0 ? null : TryReadUInt32(clubObject + 0x0C);
        var competition = SafeReadPointer(team + 0x50);
        var leagueName = competition == 0 ? null : ReadFmString(SafeReadPointer(competition + 0x40));

        var kind = TryReadByte(team + 0x28);
        var teamKind = kind switch
        {
            0 => "First Team",
            10 => "Under 21s",
            12 => "Under 18s",
            _ => null
        };

        var result = (clubName, leagueName, teamKind, clubUid);
        _teamInfo[team] = result;
        return result;
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

    private uint? ReadContractClubUid(ulong contract)
    {
        var team = SafeReadPointer(contract + 0x10);
        return ReadPlayerTeam(team).ClubUid;
    }

    private string? ReadContractClubName(ulong contract)
    {
        var team = SafeReadPointer(contract + 0x10);
        return ReadPlayerTeam(team).ClubName;
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

    private uint? TryReadUInt32(ulong address)
    {
        try { return _memory.ReadUInt32(address); }
        catch { return null; }
    }

    private float? TryReadSingle(ulong address)
    {
        try { return BitConverter.Int32BitsToSingle(_memory.ReadInt32(address)); }
        catch { return null; }
    }

    private byte? TryReadByte(ulong address)
    {
        try { return _memory.ReadByte(address); }
        catch { return null; }
    }

    private static void AddUInt32Field(
        IDictionary<string, SourcedValue<int>> fields,
        string key,
        uint? rawValue,
        FieldProvenance evidence,
        uint minimum,
        uint maximum)
    {
        if (rawValue is uint value && value >= minimum && value <= maximum && value <= int.MaxValue)
            fields[key] = new SourcedValue<int>((int)value, evidence);
    }

    private static void CopyFinanceToSearch(
        IReadOnlyDictionary<string, SourcedValue<decimal>> source,
        string sourceKey,
        string searchKey,
        IDictionary<string, SourcedValue<decimal>> target)
    {
        if (source.TryGetValue(sourceKey, out var value))
            target[searchKey] = value;
    }

    private static void AddFinanceField(
        IDictionary<string, SourcedValue<decimal>> fields,
        string key,
        int? rawValue,
        FieldProvenance evidence,
        decimal minimum,
        decimal maximum)
    {
        if (rawValue is int value && value >= minimum && value <= maximum)
            fields[key] = new SourcedValue<decimal>(value, evidence);
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
