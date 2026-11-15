using FmAtlas.Models;

namespace FmAtlas.Services;

public sealed record ClubSquadPositionCount(string Position, int Count);
public sealed record ClubSquadAgeBand(string Label, int Count);

public sealed record ClubSquadAnalysis(
    int PlayerCount,
    int AgeKnownCount, double? AverageAge,
    int CurrentAbilityKnownCount, double? AverageCurrentAbility,
    int PotentialAbilityKnownCount, double? AveragePotentialAbility,
    int ValueKnownCount, decimal? TotalValue,
    int WageKnownCount, double? TotalWage, double? AverageWage,
    int HomeGrownClubYes, int HomeGrownClubNo, int HomeGrownClubUnknown,
    int HomeGrownNationYes, int HomeGrownNationNo, int HomeGrownNationUnknown,
    IReadOnlyList<ClubSquadPositionCount> PositionCounts,
    IReadOnlyList<ClubSquadAgeBand> AgeBands);

/// <summary>Descriptive aggregates over explicitly linked club squad rows.</summary>
public static class ClubSquadAnalysisService
{
    public static ClubSquadAnalysis Analyse(
        IReadOnlyCollection<PlayerRecord> players,
        IReadOnlyDictionary<string, double>? wageByPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(players);
        wageByPlayer ??= new Dictionary<string, double>();
        var ages = players.Where(x => x.Age is > 0).Select(x => (double)x.Age!.Value).ToList();
        var ca = players.Where(x => x.CurrentAbility is >= 1 and <= 200).Select(x => (double)x.CurrentAbility!.Value).ToList();
        var pa = players.Where(x => x.PotentialAbility is >= 1 and <= 200).Select(x => (double)x.PotentialAbility!.Value).ToList();
        var values = players.Where(x => x.Value is > 0).Select(x => x.Value!.Value).ToList();
        var wages = players.Select(x => wageByPlayer.TryGetValue(x.SourceId, out var value) && double.IsFinite(value) && value > 0 ? (double?)value : null)
            .Where(x => x.HasValue).Select(x => x!.Value).ToList();

        var positions = players
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Position) ? "Unspecified" : x.Position.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => new ClubSquadPositionCount(x.Key, x.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Position, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var bands = new[]
        {
            ("U21", 0, 20), ("21–24", 21, 24), ("25–29", 25, 29), ("30+", 30, 120)
        }.Select(x => new ClubSquadAgeBand(x.Item1,
            players.Count(p => p.Age is > 0 && p.Age.Value >= x.Item2 && p.Age.Value <= x.Item3)))
         .Append(new ClubSquadAgeBand("Unknown", players.Count(p => p.Age is null or <= 0)))
         .ToList();

        return new ClubSquadAnalysis(
            players.Count,
            ages.Count, ages.Count == 0 ? null : ages.Average(),
            ca.Count, ca.Count == 0 ? null : ca.Average(),
            pa.Count, pa.Count == 0 ? null : pa.Average(),
            values.Count, values.Count == 0 ? null : values.Sum(),
            wages.Count, wages.Count == 0 ? null : wages.Sum(), wages.Count == 0 ? null : wages.Average(),
            players.Count(x => x.HomeGrownClub == true), players.Count(x => x.HomeGrownClub == false), players.Count(x => !x.HomeGrownClub.HasValue),
            players.Count(x => x.HomeGrownNation == true), players.Count(x => x.HomeGrownNation == false), players.Count(x => !x.HomeGrownNation.HasValue),
            positions, bands);
    }
}
