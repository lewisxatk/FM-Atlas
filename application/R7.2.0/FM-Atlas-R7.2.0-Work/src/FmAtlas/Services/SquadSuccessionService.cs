using FmAtlas.Models;

namespace FmAtlas.Services;

public sealed record SquadPositionSuccession(
    string Position,
    int Players,
    int Under23,
    int Under21,
    int HomeGrownClub,
    int HomeGrownNation,
    double? AverageAge,
    string SuccessionStatus);

public sealed record SquadSuccessionSummary(
    int Players,
    int Under23,
    int Under21,
    int HomeGrownClub,
    int HomeGrownNation,
    IReadOnlyList<SquadPositionSuccession> Positions);

/// <summary>
/// Aggregates only imported squad age, primary-position and home-grown flags.
/// U23 means age 22 or younger; U21 means age 20 or younger. The imported home-grown
/// booleans are source flags, not a legal registration ruling. No wage, contract,
/// squad-role or future-potential predictions are inferred.
/// </summary>
public static class SquadSuccessionService
{
    public static SquadSuccessionSummary Analyse(IEnumerable<PlayerRecord> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        var squad = players.Where(x => x is not null).ToArray();
        var validAges = squad.Where(x => x.Age is >= 15 and <= 60).ToArray();

        var positions = squad
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Position) ? "Unspecified" : x.Position.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ages = group.Where(x => x.Age is >= 15 and <= 60)
                    .Select(x => (double)x.Age!.Value)
                    .ToArray();
                var under23 = group.Count(x => x.Age is >= 15 and <= 22);
                var under21 = group.Count(x => x.Age is >= 15 and <= 20);
                var homeGrownClub = group.Count(x => x.HomeGrownClub == true);
                var homeGrownNation = group.Count(x => x.HomeGrownNation == true);
                return new SquadPositionSuccession(
                    group.Key,
                    group.Count(),
                    under23,
                    under21,
                    homeGrownClub,
                    homeGrownNation,
                    ages.Length == 0 ? null : ages.Average(),
                    under23 > 0 ? "U23 cover present" : "No U23 cover in imported squad");
            })
            .OrderBy(x => x.Under23 == 0 ? 0 : 1)
            .ThenBy(x => x.Position, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SquadSuccessionSummary(
            squad.Length,
            validAges.Count(x => x.Age <= 22),
            validAges.Count(x => x.Age <= 20),
            squad.Count(x => x.HomeGrownClub == true),
            squad.Count(x => x.HomeGrownNation == true),
            positions);
    }
}
