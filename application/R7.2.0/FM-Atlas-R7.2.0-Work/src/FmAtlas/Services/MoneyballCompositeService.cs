namespace FmAtlas.Services;

public sealed record MoneyballMetricPercentileObservation(
    string SourceId,
    string PlayerName,
    string Period,
    string MetricKey,
    double? Percentile,
    string? GroupKey = null);

public sealed record MoneyballCompositeResult(
    string SourceId,
    string PlayerName,
    string Period,
    string? GroupKey,
    double CompositePercentile,
    int MetricsAvailable,
    int MetricsRequired,
    string MetricCoverage,
    string Basis);

/// <summary>
/// Combines already-calculated within-cohort percentiles. This is an Atlas composite,
/// not Football Manager's internal player rating or a cross-league strength model.
/// </summary>
public static class MoneyballCompositeService
{
    public static IReadOnlyList<MoneyballCompositeResult> Calculate(
        IReadOnlyList<MoneyballMetricPercentileObservation> observations,
        IReadOnlyDictionary<string, double>? metricWeights = null,
        int minimumMetrics = 2)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (minimumMetrics < 1 || minimumMetrics > 100)
            throw new ArgumentOutOfRangeException(nameof(minimumMetrics));

        var validWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (metricWeights is not null)
        {
            foreach (var pair in metricWeights)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Metric weight keys cannot be blank.", nameof(metricWeights));
                if (!double.IsFinite(pair.Value) || pair.Value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(metricWeights), "Metric weights must be finite and greater than zero.");
                validWeights[pair.Key.Trim()] = pair.Value;
            }
        }

        var valid = observations.Where(x =>
            x is not null
            && !string.IsNullOrWhiteSpace(x.SourceId)
            && !string.IsNullOrWhiteSpace(x.PlayerName)
            && !string.IsNullOrWhiteSpace(x.Period)
            && !string.IsNullOrWhiteSpace(x.MetricKey)
            && x.Percentile.HasValue
            && double.IsFinite(x.Percentile.Value)
            && x.Percentile.Value >= 0
            && x.Percentile.Value <= 100).ToArray();

        var duplicate = valid
            .GroupBy(x => Identity(x.SourceId, x.Period, x.MetricKey), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException("Each player, period and metric may contribute only one percentile.", nameof(observations));

        var output = new List<MoneyballCompositeResult>();
        foreach (var player in valid.GroupBy(x => Identity(x.SourceId, x.Period), StringComparer.OrdinalIgnoreCase))
        {
            var rows = player.ToArray();
            var weightedTotal = 0d;
            var totalWeight = 0d;
            var used = new List<string>();
            foreach (var row in rows)
            {
                var weight = validWeights.TryGetValue(row.MetricKey, out var configured) ? configured : 1d;
                weightedTotal += row.Percentile!.Value * weight;
                totalWeight += weight;
                used.Add(row.MetricKey);
            }

            if (used.Count < minimumMetrics || totalWeight <= 0)
                continue;

            var first = rows[0];
            var groups = rows.Select(x => x.GroupKey?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var groupKey = groups.Length == 1 && rows.All(x => !string.IsNullOrWhiteSpace(x.GroupKey))
                ? groups[0]
                : null;
            var coverage = $"{used.Count}/{Math.Max(used.Count, minimumMetrics)} minimum metrics";
            output.Add(new MoneyballCompositeResult(
                first.SourceId,
                first.PlayerName,
                first.Period,
                groupKey,
                weightedTotal / totalWeight,
                used.Count,
                minimumMetrics,
                string.Join("; ", used.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)),
                $"Equal-weight unless configured otherwise; average of {used.Count} available within-cohort metric percentiles. Atlas composite only; no hidden FM formula or league-strength adjustment."));
        }

        return output
            .OrderByDescending(x => x.CompositePercentile)
            .ThenBy(x => x.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Period, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Identity(string sourceId, string period, string? metricKey = null)
        => string.Join("\\0", sourceId.Trim(), period.Trim(), metricKey?.Trim() ?? string.Empty);
}
