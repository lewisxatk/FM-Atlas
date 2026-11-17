using System.Globalization;
using FMAtlas.Domain.Clubs;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Staff;

namespace FMAtlas.Infrastructure.Connection;

/// <summary>
/// Independent layout sanity checks applied to a complete read-only FM27 snapshot
/// before any records are committed to the local database.
/// </summary>
public sealed record FM27LiveScanValidationResult(
    int Players,
    int Staff,
    int Clubs,
    int PlayersWithCurrentAbility,
    int PlayersWithNaturalPosition,
    int PlayersWithTwentyInAtLeastOnePosition,
    IReadOnlyList<string> Warnings)
{
    public string Summary =>
        $"Layout checks passed: {Players:N0} players, {Staff:N0} staff and {Clubs:N0} clubs; " +
        $"{PlayersWithCurrentAbility:N0} players have a plausible CA, " +
        $"{PlayersWithNaturalPosition:N0} have position ratings and " +
        $"{PlayersWithTwentyInAtLeastOnePosition:N0} have a 20-rated natural position.";
}

public sealed record FM27LiveReadSummary(
    int PlayerPointers, int PlayersRead, int PlayersSkipped,
    int StaffPointers, int StaffRead, int StaffSkipped,
    int ClubPointers, int ClubsRead, int ClubsSkipped,
    IReadOnlyList<string> FailureSamples)
{
    public string Summary =>
        $"Players {PlayersRead:N0}/{PlayerPointers:N0} (skipped {PlayersSkipped:N0}); " +
        $"staff {StaffRead:N0}/{StaffPointers:N0} (skipped {StaffSkipped:N0}); " +
        $"clubs {ClubsRead:N0}/{ClubPointers:N0} (skipped {ClubsSkipped:N0}).";

    public IReadOnlyList<string> CoverageWarnings
    {
        get
        {
            var warnings = new List<string>();
            AddWarning("players", PlayerPointers, PlayersRead, PlayersSkipped);
            AddWarning("staff", StaffPointers, StaffRead, StaffSkipped);
            AddWarning("clubs", ClubPointers, ClubsRead, ClubsSkipped);
            return warnings.AsReadOnly();

            void AddWarning(string name, int pointers, int read, int skipped)
            {
                if (pointers > 0 && skipped > 0)
                    warnings.Add($"FM27 {name}: {read:N0} of {pointers:N0} entries were readable; {skipped:N0} entries were skipped.");
            }
        }
    }
}

public static class FM27LiveScanValidator
{
    private const string ExpectedProvider = WindowsFm27LiveGameConnector.LiveProviderKey;

    /// <summary>
    /// Collects only counts and compact identity sets while records stream through the importer.
    /// It never materialises a second copy of the player/staff/club object graph.
    /// </summary>
    public sealed class StreamingAccumulator
    {
        private readonly HashSet<string> _playerIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _staffIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _clubIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _playerClubReferences = new(StringComparer.Ordinal);

        private int _players;
        private int _staff;
        private int _clubs;
        private int _playersWithCa;
        private int _playersWithPositions;
        private int _playersWithTwentyPositions;
        private int _staffWithCa;

        public void Add(IEntityRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            switch (record)
            {
                case PlayerRecord player:
                    ValidateIdentity(player.Identity, EntityKind.Player, _playerIds);
                    if (string.IsNullOrWhiteSpace(player.FullName?.Value))
                        throw new InvalidDataException("At least one player record has no usable name. The live layout has not passed validation.");

                    _players++;
                    if (player.CurrentAbility?.Value is >= 1 and <= 200)
                        _playersWithCa++;
                    if (player.PositionRatings.Count > 0)
                    {
                        _playersWithPositions++;
                        if (player.PositionRatings.Values.Any(value => value.Value == 20))
                            _playersWithTwentyPositions++;
                    }
                    if (player.SearchIntegerFields.TryGetValue("club_uid", out var clubUid))
                        _playerClubReferences.Add(clubUid.Value.ToString(CultureInfo.InvariantCulture));

                    ValidatePlayerRanges(player);
                    break;

                case StaffRecord staff:
                    ValidateIdentity(staff.Identity, EntityKind.Staff, _staffIds);
                    if (string.IsNullOrWhiteSpace(staff.FullName?.Value))
                        throw new InvalidDataException("At least one staff record has no usable name. The live layout has not passed validation.");

                    _staff++;
                    if (staff.CurrentAbility?.Value is >= 1 and <= 200)
                        _staffWithCa++;
                    ValidateStaffRanges(staff);
                    break;

                case ClubRecord club:
                    ValidateIdentity(club.Identity, EntityKind.Club, _clubIds);
                    if (string.IsNullOrWhiteSpace(club.Name?.Value))
                        throw new InvalidDataException("At least one club record has no usable name. The live layout has not passed validation.");
                    _clubs++;
                    break;

                default:
                    throw new InvalidDataException($"Unexpected live record type {record.GetType().Name}; only player, staff and club records are supported.");
            }
        }

        public FM27LiveScanValidationResult Complete(FM27LiveReadSummary? readSummary = null)
        {
            var warnings = new List<string>();
            if (readSummary is not null)
            {
                ValidateReadCoverage("players", readSummary.PlayerPointers, readSummary.PlayersRead, 0.90);
                ValidateReadCoverage("staff", readSummary.StaffPointers, readSummary.StaffRead, 0.90);
                ValidateReadCoverage("clubs", readSummary.ClubPointers, readSummary.ClubsRead, 0.90);
                warnings.AddRange(readSummary.CoverageWarnings);
            }

            if (_players == 0 || _staff == 0 || _clubs == 0)
                throw new InvalidDataException(
                    $"The scan did not produce all three required entity families (players {_players:N0}, staff {_staff:N0}, clubs {_clubs:N0}). Nothing has been imported.");

            RequireCoverage(_playersWithCa, _players, 0.70,
                "Current Ability values did not validate for enough players.");
            RequireCoverage(_playersWithPositions, _players, 0.70,
                "Position ratings were missing from too many player records.");
            RequireCoverage(_playersWithTwentyPositions, _playersWithPositions, 0.80,
                "The natural-position sanity check failed: too few players have a 20-rated position.");
            RequireCoverage(_staffWithCa, _staff, 0.50,
                "Staff Current Ability values did not validate for enough records.");

            if (_playerClubReferences.Count >= 100)
            {
                var linked = _playerClubReferences.Count(_clubIds.Contains);
                var coverage = linked / (double)_playerClubReferences.Count;
                if (coverage < 0.75)
                    throw new InvalidDataException(
                        $"Player-to-club identity validation failed ({linked:N0} of {_playerClubReferences.Count:N0} club links matched the imported club list). The scan was discarded.");
            }
            else
            {
                warnings.Add("Too few player-to-club links were present for a meaningful cross-check.");
            }

            return new FM27LiveScanValidationResult(
                _players,
                _staff,
                _clubs,
                _playersWithCa,
                _playersWithPositions,
                _playersWithTwentyPositions,
                warnings.AsReadOnly());
        }
    }

    public static FM27LiveScanValidationResult Validate(
        IEnumerable<IEntityRecord> records,
        FM27LiveReadSummary? readSummary = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        var accumulator = new StreamingAccumulator();
        foreach (var record in records)
            accumulator.Add(record);
        return accumulator.Complete(readSummary);
    }

    private static void ValidateIdentity(
        EntityIdentity identity,
        EntityKind expectedKind,
        HashSet<string> seen)
    {
        if (identity.Kind != expectedKind ||
            !identity.IsUsable ||
            !string.Equals(identity.ProviderKey, ExpectedProvider, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The {expectedKind} records contained an invalid or unexpected source identity. Nothing has been imported.");
        }

        if (!seen.Add(identity.SourceId))
            throw new InvalidDataException(
                $"The {expectedKind} array contained a duplicate entity identity. The scan was discarded.");
    }

    private static void ValidateReadCoverage(string entity, int pointers, int read, double minimum)
    {
        if (pointers <= 0 || read / (double)pointers < minimum)
        {
            var percentage = pointers <= 0 ? 0 : read / (double)pointers * 100d;
            throw new InvalidDataException(
                $"Only {read:N0} of {pointers:N0} {entity} entries passed the individual read checks ({percentage:0.0}%). The scan was discarded instead of indexing an incomplete or mismatched career.");
        }
    }

    private static void RequireCoverage(int observed, int total, double minimum, string message)
    {
        if (total <= 0 || observed / (double)total < minimum)
        {
            var percentage = total <= 0 ? 0 : observed / (double)total * 100d;
            throw new InvalidDataException(
                $"{message} Validated {observed:N0} of {total:N0} ({percentage:0.0}%). The live data has not been imported.");
        }
    }

    private static void ValidatePlayerRanges(PlayerRecord player)
    {
        if (player.CurrentAbility is { Value: < 1 or > 200 } ||
            player.PotentialAbility is { Value: < -200 or > 200 } ||
            player.Attributes.Values.Any(value => value.Value is < 1 or > 20) ||
            player.HiddenAttributes.Values.Any(value => value.Value is < 1 or > 20) ||
            player.Personality.Values.Any(value => value.Value is < 1 or > 20) ||
            player.FootRatings.Values.Any(value => value.Value is < 1 or > 20) ||
            player.PositionRatings.Values.Any(value => value.Value is < 1 or > 20))
        {
            throw new InvalidDataException("A player value fell outside its expected FM range. The scan was discarded.");
        }
    }

    private static void ValidateStaffRanges(StaffRecord staff)
    {
        if (staff.CurrentAbility is { Value: < 1 or > 200 } ||
            staff.PotentialAbility is { Value: < 1 or > 200 } ||
            staff.Attributes.Values.Any(value => value.Value is < 1 or > 20) ||
            staff.Knowledge.Values.Any(value => value.Value is < 1 or > 20) ||
            staff.Personality.Values.Any(value => value.Value is < 1 or > 20))
        {
            throw new InvalidDataException("A staff value fell outside its expected FM range. The live scan was discarded.");
        }
    }
}
