namespace FmAtlas.Services;

public sealed record ScoutingMetricGroup(string Name, IReadOnlyList<ScoutingMetricDefinition> Metrics);
public sealed record SimilarPlayerResult(ScoutingPlayerSeason Player, double Similarity, double Coverage, IReadOnlyList<string> SharedMetrics);
public sealed record UndervaluedPlayerResult(ScoutingPlayerSeason Player, double PerformancePercentile, double AgeScore,
    double ValueScore, double CompositeScore, double? MarketValue, string Explanation);

/// <summary>Shared metric catalogue and transparent recruitment discovery algorithms.</summary>
public static class ScoutingRecruitmentService
{
    private static ScoutingMetricDefinition D(string key, string label, string unit = "actions", bool higher = true,
        double minutes = 450, string family = "Event data") =>
        new(key, label, unit, true, higher, minutes, family);

    public static IReadOnlyList<ScoutingMetricGroup> MetricGroups { get; } = new[]
    {
        new ScoutingMetricGroup("Attacking", new[]
        {
            D("goals","Goals / 90"), D("xg","Expected goals / 90","xG"),
            D("shots","Shots / 90"), D("assists","Assists / 90"), D("key_passes","Key passes / 90"),
            D("dribbles_completed","Successful dribbles / 90"), D("carries","Carries / 90")
        }),
        new ScoutingMetricGroup("Passing & progression", new[]
        {
            D("passes_attempted","Passes attempted / 90"), D("passes_completed","Passes completed / 90"),
            D("long_passes","Long passes / 90"), D("progressive_passes","Progressive passes / 90"),
            D("progressive_runs","Progressive runs / 90"), D("passes_to_final_third","Passes to final third / 90")
        }),
        new ScoutingMetricGroup("Defending & pressing", new[]
        {
            D("pressures","Pressures / 90"), D("tackles","Tackles / 90"), D("interceptions","Interceptions / 90"),
            D("recoveries","Ball recoveries / 90"), D("clearances","Clearances / 90"),
            D("blocks","Blocks / 90"), D("aerial_duels","Aerial duels / 90"),
            D("aerial_duels_won","Aerial duels won / 90"), D("fouls_committed","Fouls committed / 90", higher:false)
        }),
        new ScoutingMetricGroup("Discipline & security", new[]
        {
            D("yellow_cards","Yellow cards / 90", higher:false), D("red_cards","Red cards / 90", higher:false),
            D("miscontrols","Miscontrols / 90", higher:false), D("dispossessions","Dispossessed / 90", higher:false),
            D("offsides","Offsides / 90", higher:false)
        })
    };

    public static IReadOnlyList<ScoutingMetricDefinition> DefinitionsFor(IEnumerable<ScoutingPlayerSeason> players)
    {
        var present = players.SelectMany(p => p.Metrics.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return MetricGroups.SelectMany(g => g.Metrics).Where(m => present.Contains(m.Key)).ToList();
    }

    public static IReadOnlyList<SimilarPlayerResult> FindSimilar(
        ScoutingPlayerSeason target, IEnumerable<ScoutingPlayerSeason> candidates,
        IReadOnlyList<ScoutingMetricDefinition> metrics, int limit = 25, double minimumCoverage = .6)
    {
        var targetValues = Normalized(target, metrics);
        var output = new List<SimilarPlayerResult>();
        foreach (var candidate in candidates.Where(x => x.SourceId != target.SourceId))
        {
            var values = Normalized(candidate, metrics);
            var shared = targetValues.Keys.Intersect(values.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            var coverage = metrics.Count == 0 ? 0 : shared.Count / (double)metrics.Count;
            if (coverage < minimumCoverage || shared.Count < 2) continue;
            var distance = Math.Sqrt(shared.Average(k => Math.Pow(targetValues[k] - values[k], 2)));
            var similarity = Math.Clamp(100 * (1 - distance / 100), 0, 100);
            output.Add(new SimilarPlayerResult(candidate, similarity, coverage, shared));
        }
        return output.OrderByDescending(x => x.Similarity).ThenByDescending(x => x.Coverage).Take(Math.Clamp(limit, 1, 200)).ToList();
    }

    public static IReadOnlyList<UndervaluedPlayerResult> FindUndervalued(
        IEnumerable<ScoutingPlayerSeason> players, ScoutingMetricDefinition performanceMetric,
        string? competition = null, string? season = null, string? positionGroup = null,
        double maximumMarketValue = double.PositiveInfinity, int minimumMinutes = 450,
        int minimumCohortSize = 5, int minimumAge = 16, int maximumAge = 34)
    {
        var rows = players.Where(p =>
                (string.IsNullOrWhiteSpace(competition) || p.Competition == competition) &&
                (string.IsNullOrWhiteSpace(season) || p.Season == season) &&
                (string.IsNullOrWhiteSpace(positionGroup) || p.PositionGroup == positionGroup) &&
                p.MinutesPlayed >= minimumMinutes &&
                p.Metrics.TryGetValue("market_value", out var mv) && mv.HasValue && mv.Value >= 0 && mv.Value <= maximumMarketValue &&
                p.Metrics.TryGetValue("age", out var age) && age.HasValue && age.Value >= minimumAge && age.Value <= maximumAge)
            .ToList();
        var ranked = ScoutingAnalyticsService.RankMetric(rows, performanceMetric, competition, season, positionGroup, minimumCohortSize);
        if (ranked.Count == 0) return Array.Empty<UndervaluedPlayerResult>();
        var maxValue = rows.Max(p => p.Metrics["market_value"]!.Value);
        return ranked.Where(r => r.Percentile.HasValue).Select(r =>
        {
            var p = rows.First(x => x.SourceId == r.SourceId);
            var age = p.Metrics["age"]!.Value;
            var marketValue = p.Metrics["market_value"]!.Value;
            var ageScore = Math.Clamp(100 - Math.Abs(age - 23) * 5, 0, 100);
            var valueScore = maxValue <= 0 ? 0 : 100 * (1 - marketValue / maxValue);
            var composite = .65 * r.Percentile!.Value + .20 * ageScore + .15 * valueScore;
            return new UndervaluedPlayerResult(p, r.Percentile.Value, ageScore, valueScore, composite, marketValue,
                $"Performance percentile {r.Percentile:0}; age {age:0}; market value {marketValue:0.##}. Atlas discovery score weights performance 65%, age 20%, relative value 15%; not a transfer-fee prediction.");
        }).OrderByDescending(x => x.CompositeScore).ToList();
    }

    private static Dictionary<string, double> Normalized(ScoutingPlayerSeason p, IReadOnlyList<ScoutingMetricDefinition> metrics)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in metrics)
        {
            if (!p.Metrics.TryGetValue(m.Key, out var raw) || !raw.HasValue || !double.IsFinite(raw.Value)) continue;
            var value = m.IsRatePer90
                ? p.MinutesPlayed is > 0 && p.MinutesPlayed >= m.MinimumSampleMinutes ? raw.Value * 90 / p.MinutesPlayed.Value : double.NaN
                : raw.Value;
            if (double.IsFinite(value)) result[m.Key] = value;
        }
        return result;
    }
}
