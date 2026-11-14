namespace FmAtlas.Services;

/// <summary>Canonical metric facts supplied by an imported season or event-data adapter.
/// Values are never inferred from unrelated aggregate fields.</summary>
public sealed record ScoutingMetricDefinition(
    string Key, string Label, string Unit, bool IsRatePer90 = false,
    bool HigherIsBetter = true, double MinimumSampleMinutes = 0,
    string SourceFamily = "FM aggregate");

public sealed record ScoutingPlayerSeason(
    string SourceId, string PlayerName, string? Club, string? Competition,
    string Season, string? PositionGroup, double? MinutesPlayed,
    IReadOnlyDictionary<string, double?> Metrics,
    string SourceName, string SourceVersion, bool IsEventLevel = false);

public sealed record ScoutingMetricResult(
    string SourceId, string PlayerName, string? Club, string? Competition,
    string Season, string? PositionGroup, string MetricKey, string MetricLabel,
    double RawValue, double? Per90Value, double? Percentile,
    int CohortSize, double Coverage, string SourceName, bool IsEventLevel);

/// <summary>
/// Reproducible Wyscout/StatsBomb-style cohort analysis primitives. This service does not
/// manufacture event metrics: adapters must supply the metric and its source provenance.
/// Percentiles are computed only within an explicitly selected competition/season/position cohort.
/// </summary>
public static class ScoutingAnalyticsService
{
    public static IReadOnlyList<ScoutingMetricResult> RankMetric(
        IEnumerable<ScoutingPlayerSeason> players,
        ScoutingMetricDefinition definition,
        string? competition = null,
        string? season = null,
        string? positionGroup = null,
        int minimumCohortSize = 5)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Key))
            throw new ArgumentException("A stable metric key is required.", nameof(definition));
        if (minimumCohortSize < 2)
            throw new ArgumentOutOfRangeException(nameof(minimumCohortSize), "A percentile cohort needs at least two players.");

        var candidates = players.Where(p =>
                (string.IsNullOrWhiteSpace(competition) || string.Equals(p.Competition, competition, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(season) || string.Equals(p.Season, season, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(positionGroup) || string.Equals(p.PositionGroup, positionGroup, StringComparison.OrdinalIgnoreCase)))
            .Select(p =>
            {
                if (!p.Metrics.TryGetValue(definition.Key, out var raw) || raw is null || !double.IsFinite(raw.Value))
                    return (Player: p, Raw: (double?)null, Value: (double?)null);
                var minutes = p.MinutesPlayed;
                if (definition.IsRatePer90 && (!minutes.HasValue || !double.IsFinite(minutes.Value) ||
                    minutes.Value <= 0 || minutes.Value < Math.Max(0, definition.MinimumSampleMinutes)))
                    return (Player: p, Raw: raw, Value: (double?)null);
                var normalized = definition.IsRatePer90 ? raw.Value * 90d / minutes!.Value : raw.Value;
                return (Player: p, Raw: raw, Value: double.IsFinite(normalized) ? normalized : null);
            })
            .Where(x => x.Value.HasValue)
            .ToList();

        var canRank = candidates.Count >= minimumCohortSize;
        var ordered = candidates.OrderBy(x => x.Value!.Value).ToList();
        var output = new List<ScoutingMetricResult>(ordered.Count);
        foreach (var item in ordered)
        {
            double? percentile = null;
            if (canRank)
            {
                var less = ordered.Count(x => x.Value!.Value < item.Value!.Value);
                var equal = ordered.Count(x => x.Value!.Value == item.Value!.Value);
                var ascending = (less + (equal - 1) / 2d) / (ordered.Count - 1d) * 100d;
                percentile = definition.HigherIsBetter ? ascending : 100d - ascending;
            }
            output.Add(new ScoutingMetricResult(
                item.Player.SourceId, item.Player.PlayerName, item.Player.Club,
                item.Player.Competition, item.Player.Season, item.Player.PositionGroup,
                definition.Key, definition.Label, item.Raw!.Value,
                definition.IsRatePer90 ? item.Value : null, percentile,
                ordered.Count, 1d, item.Player.SourceName, item.Player.IsEventLevel));
        }
        return output.OrderByDescending(x => x.Percentile ?? (definition.HigherIsBetter ? x.Per90Value ?? x.RawValue : -(x.Per90Value ?? x.RawValue)))
            .ThenBy(x => x.PlayerName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Returns a transparent 0–100 completeness ratio for requested metric keys.</summary>
    public static double CalculateCoverage(
        IReadOnlyDictionary<string, double?> metrics, IEnumerable<string> requestedKeys)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(requestedKeys);
        var keys = requestedKeys.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (keys.Count == 0) return 0;
        var present = keys.Count(key => metrics.TryGetValue(key, out var value) &&
            value.HasValue && double.IsFinite(value.Value));
        return present / (double)keys.Count;
    }
}
