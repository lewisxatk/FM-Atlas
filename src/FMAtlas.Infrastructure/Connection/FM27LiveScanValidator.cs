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

public static class FM27LiveScanValidator
{
    private const string ExpectedProvider = WindowsFm27LiveGameConnector.LiveProviderKey;

    public static FM27LiveScanValidationResult Validate(IReadOnlyList<IEntityRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var players = records.OfType<PlayerRecord>().ToArray();
        var staff = records.OfType<StaffRecord>().ToArray();
        var clubs = records.OfType<ClubRecord>().ToArray();
        var warnings = new List<string>();

        if (players.Length == 0 || staff.Length == 0 || clubs.Length == 0)
            throw new InvalidDataException(
                $"The scan did not produce all three required entity families (players {players.Length:N0}, staff {staff.Length:N0}, clubs {clubs.Length:N0}). Nothing has been imported.");

        ValidateIdentity(players.Select(record => record.Identity), EntityKind.Player);
        ValidateIdentity(staff.Select(record => record.Identity), EntityKind.Staff);
        ValidateIdentity(clubs.Select(record => record.Identity), EntityKind.Club);

        if (players.Any(record => string.IsNullOrWhiteSpace(record.FullName?.Value)))
            throw new InvalidDataException("At least one player record has no usable name. The live layout has not passed validation.");
        if (staff.Any(record => string.IsNullOrWhiteSpace(record.FullName?.Value)))
            throw new InvalidDataException("At least one staff record has no usable name. The live layout has not passed validation.");
        if (clubs.Any(record => string.IsNullOrWhiteSpace(record.Name?.Value)))
            throw new InvalidDataException("At least one club record has no usable name. The live layout has not passed validation.");

        var playersWithCa = players.Count(record =>
            record.CurrentAbility?.Value is >= 1 and <= 200);
        RequireCoverage(playersWithCa, players.Length, 0.70,
            "Current Ability values did not validate for enough players.");

        var playersWithPositions = players
            .Where(record => record.PositionRatings.Count > 0)
            .ToArray();
        RequireCoverage(playersWithPositions.Length, players.Length, 0.70,
            "Position ratings were missing from too many player records.");

        var playersWithNaturalPosition = playersWithPositions.Count(record =>
            record.PositionRatings.Values.Any(value => value.Value == 20));
        RequireCoverage(playersWithNaturalPosition, playersWithPositions.Length, 0.80,
            "The natural-position sanity check failed: too few players have a 20-rated position.");

        var staffWithCa = staff.Count(record =>
            record.CurrentAbility?.Value is >= 1 and <= 200);
        RequireCoverage(staffWithCa, staff.Length, 0.50,
            "Staff Current Ability values did not validate for enough records.");

        var clubIds = clubs.Select(record => record.Identity.SourceId)
            .ToHashSet(StringComparer.Ordinal);
        var playerClubReferences = players
            .Select(record => record.SearchIntegerFields.TryGetValue("club_uid", out var uid) ? uid.Value.ToString() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (playerClubReferences.Length >= 100)
        {
            var linked = playerClubReferences.Count(clubIds.Contains);
            var coverage = linked / (double)playerClubReferences.Length;
            if (coverage < 0.75)
                throw new InvalidDataException(
                    $"Player-to-club identity validation failed ({linked:N0} of {playerClubReferences.Length:N0} club links matched the imported club list). The scan was discarded.");
        }
        else
        {
            warnings.Add("Too few player-to-club links were present for a meaningful cross-check.");
        }

        ValidateValueRanges(players, staff);

        return new FM27LiveScanValidationResult(
            players.Length,
            staff.Length,
            clubs.Length,
            playersWithCa,
            playersWithPositions.Length,
            playersWithNaturalPosition,
            warnings.AsReadOnly());
    }

    private static void ValidateIdentity(IEnumerable<EntityIdentity> identities, EntityKind expectedKind)
    {
        var values = identities.ToArray();
        if (values.Any(identity =>
                identity.Kind != expectedKind ||
                !identity.IsUsable ||
                !string.Equals(identity.ProviderKey, ExpectedProvider, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"The {expectedKind} records contained an invalid or unexpected source identity. Nothing has been imported.");
        }

        var duplicate = values.GroupBy(identity => identity.SourceId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException(
                $"The {expectedKind} array contained a duplicate entity identity. The scan was discarded.");
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

    private static void ValidateValueRanges(
        IEnumerable<PlayerRecord> players,
        IEnumerable<StaffRecord> staff)
    {
        foreach (var player in players)
        {
            if (player.CurrentAbility is { Value: < 1 or > 200 } ||
                player.PotentialAbility is { Value: < 1 or > 200 } ||
                player.Attributes.Values.Any(value => value.Value is < 1 or > 20) ||
                player.HiddenAttributes.Values.Any(value => value.Value is < 1 or > 20) ||
                player.Personality.Values.Any(value => value.Value is < 1 or > 20) ||
                player.FootRatings.Values.Any(value => value.Value is < 1 or > 20) ||
                player.PositionRatings.Values.Any(value => value.Value is < 1 or > 20))
            {
                throw new InvalidDataException("A player value fell outside its expected FM range. The scan was discarded.");
            }
        }

        foreach (var member in staff)
        {
            if (member.CurrentAbility is { Value: < 1 or > 200 } ||
                member.PotentialAbility is { Value: < 1 or > 200 } ||
                member.Attributes.Values.Any(value => value.Value is < 1 or > 20) ||
                member.Knowledge.Values.Any(value => value.Value is < 1 or > 20) ||
                member.Personality.Values.Any(value => value.Value is < 1 or > 20))
            {
                throw new InvalidDataException("A staff value fell outside its expected FM range. The scan was discarded.");
            }
        }
    }
}
