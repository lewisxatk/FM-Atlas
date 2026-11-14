namespace FmAtlas.Services;

public sealed record ScoutingMetricGroup(string Name, IReadOnlyList<ScoutingMetricDefinition> Metrics);\npublic sealed record ScoutingRoleProfile(string Name, string PositionGroup, IReadOnlyList<string> PreferredMetricKeys);
public sealed record SimilarPlayerResult(ScoutingPlayerSeason Player, double Similarity, double Coverage, IReadOnlyList<string> SharedMetrics);
public sealed record UndervaluedPlayerResult(ScoutingPlayerSeason Player, double PerformancePercentile, double AgeScore,
    double ValueScore, double CompositeScore, double? MarketValue, string Explanation);

/// <summary>Shared metric catalogue and transparent recruitment discovery algorithms.</summary>
public static class ScoutingRecruitmentService
{
    private static ScoutingMetricDefinition D(string key, string label, string unit = "actions", bool higher = true,
        double minutes = 450, string family = "Event data") =>
        new(key, label, unit, true, higher, minutes, family);

    public static IReadOnlyList<ScoutingRoleProfile> RoleProfiles { get; } = new[]
    {
        new ScoutingRoleProfile("Goalkeeper", "Goalkeeper", new[] { "saves", "claims", "punches", "sweeper_actions", "goalkeeper_actions" }),
        new ScoutingRoleProfile("Centre-back", "Defender", new[] { "aerial_duels_won", "interceptions", "clearances", "blocks", "passes_completed", "long_passes" }),
        new ScoutingRoleProfile("Full-back / Wing-back", "Defender", new[] { "tackles", "recoveries", "pressures", "carries", "dribbles_completed", "key_passes" }),
        new ScoutingRoleProfile("Defensive midfielder", "Midfielder", new[] { "pressures", "tackles", "interceptions", "recoveries", "passes_completed" }),
        new ScoutingRoleProfile("Central midfielder", "Midfielder", new[] { "passes_completed", "key_passes", "carries", "pressures", "xg" }),
        new ScoutingRoleProfile("Attacking midfielder", "Attacker", new[] { "key_passes", "assists", "xg", "shots", "dribbles_completed" }),
        new ScoutingRoleProfile("Winger", "Attacker", new[] { "dribbles_completed", "carries", "key_passes", "xg", "shots" }),
        new ScoutingRoleProfile("Striker", "Attacker", new[] { "goals", "xg", "shots_on_target", "shots", "aerial_duels_won" })
    };

    public static IReadOnlyList<ScoutingMetricGroup> MetricGroups { get; } = new[]
    {
        new ScoutingMetricGroup("Attacking", new[]
        {
            D("goals","Goals / 90"), D("xg","Expected goals / 90","xG"),
            D("shots","Shots / 90"), D("shots_on_target","Shots on target / 90"), D("assists","Assists / 90"), D("key_passes","Key passes / 90"),
            new ScoutingMetricDefinition("pass_completion_pct", "Pass completion %", "%", false, true, 0, "StatsBomb event-derived"),
            new ScoutingMetricDefinition("dribble_success_pct", "Dribble success %", "%", false, true, 0, "StatsBomb event-derived"),
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
            D("aerial_duels_won","Aerial duels won / 90"),
            new ScoutingMetricDefinition("aerial_duel_win_pct", "Aerial duel win %", "%", false, true, 0, "StatsBomb event-derived"),
            D("fouls_committed","Fouls committed / 90", higher:false)
        }),
        new ScoutingMetricGroup("Goalkeeping", new[]
        {
            D("saves","Saves / 90"), D("claims","Claims / 90"), D("punches","Punches / 90"),
            D("sweeper_actions","Sweeper actions / 90"), D("goalkeeper_actions","Goalkeeper actions / 90")
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
        var pool = candidates.Where(x => x.SourceId != target.SourceId).ToList();
        var rawTarget = RawValues(target, metrics);
        var rawCandidates = pool.ToDictionary(x => x.SourceId, x => RawValues(x, metrics), StringComparer.Ordinal);
        var ranges = metrics.ToDictionary(m => m.Key, m =>
        {
            var values = rawCandidates.Values.Where(v => v.ContainsKey(m.Key)).Select(v => v[m.Key]).ToList();
            if (rawTarget.TryGetValue(m.Key, out var targetValue)) values.Add(targetValue);
            return values.Count == 0 ? (Min: 0d, Max: 0d) : (Min: values.Min(), Max: values.Max());
        }, StringComparer.OrdinalIgnoreCase);
        var normalizedTarget = Normalize(rawTarget, ranges);
        var output = new List<SimilarPlayerResult>();
        foreach (var candidate in pool)
        {
            var values = Normalize(rawCandidates[candidate.SourceId], ranges);
            var shared = normalizedTarget.Keys.Intersect(values.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            var coverage = metrics.Count == 0 ? 0 : shared.Count / (double)metrics.Count;
            if (coverage < minimumCoverage || shared.Count < 2) continue;
            var distance = Math.Sqrt(shared.Average(k => Math.Pow(normalizedTarget[k] - values[k], 2)));
            var similarity = Math.Clamp(100 - distance, 0, 100);
            output.Add(new SimilarPlayerResult(candidate, similarity, coverage, shared));
        }
        return output.OrderByDescending(x => x.Similarity).ThenByDescending(x => x.Coverage).Take(Math.Clamp(limit, 1, 200)).ToList();
    }

    private static Dictionary<string, double> RawValues(ScoutingPlayerSeason player, IReadOnlyList<ScoutingMetricDefinition> metrics)
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var metric in metrics)
        {
            if (!player.Metrics.TryGetValue(metric.Key, out var raw) || !raw.HasValue || !double.IsFinite(raw.Value)) continue;
            var value = metric.IsRatePer90
                ? player.MinutesPlayed is > 0 && player.MinutesPlayed >= metric.MinimumSampleMinutes ? raw.Value * 90d / player.MinutesPlayed.Value : double.NaN
                : raw.Value;
            if (double.IsFinite(value)) values[metric.Key] = value;
        }
        return values;
    }

    private static Dictionary<string, double> Normalize(
        IReadOnlyDictionary<string, double> values,
        IReadOnlyDictionary<string, (double Min, double Max)> ranges)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in values)
        {
            var range = ranges[item.Key];
            result[item.Key] = range.Max <= range.Min ? 50 : 100 * (item.Value - range.Min) / (range.Max - range.Min);
        }
        return result;
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

}
