namespace FmAtlas.Services;

public sealed record CohortMetricObservation(string Id, string Name, double Value, string? GroupKey = null);

public sealed record CohortPercentileResult(
    string Id,
    string Name,
    double Value,
    double? Percentile,
    int SampleSize,
    string Basis);

/// <summary>
/// Calculates transparent mid-rank percentiles within the exact supplied cohort.
/// Direction is explicit; no league-strength adjustment or Football Manager formula is inferred.
/// </summary>
public static class CohortPercentileService
{
    public static IReadOnlyList<CohortPercentileResult> CalculateByGroup(
        IReadOnlyList<CohortMetricObservation> observations,
        int minimumSampleSize = 5,
        bool higherIsBetter = true)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (minimumSampleSize < 1 || minimumSampleSize > 100000)
            throw new ArgumentOutOfRangeException(nameof(minimumSampleSize));

        return observations
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.GroupKey) && double.IsFinite(x.Value))
            .GroupBy(x => x.GroupKey!.Trim(), StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => Calculate(group.ToArray(), minimumSampleSize, higherIsBetter)
                .Select(result => result with { Basis = $"Primary-position cohort '{group.Key}': {result.Basis}" }))
            .OrderByDescending(x => x.Percentile ?? double.MinValue)
            .ThenByDescending(x => higherIsBetter ? x.Value : -x.Value)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<CohortPercentileResult> Calculate(
        IReadOnlyList<CohortMetricObservation> observations,
        int minimumSampleSize = 5,
        bool higherIsBetter = true)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (minimumSampleSize < 1 || minimumSampleSize > 100000)
            throw new ArgumentOutOfRangeException(nameof(minimumSampleSize));

        var valid = observations
            .Where(x => x is not null && double.IsFinite(x.Value))
            .ToArray();

        var sampleSize = valid.Length;
        var hasReliableSample = sampleSize >= minimumSampleSize;
        var ordered = valid
            .OrderBy(x => x.Value)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var direction = higherIsBetter ? "higher values rank higher" : "lower values rank higher";
        var basis = hasReliableSample
            ? $"Mid-rank within {sampleSize:N0} returned rows; {direction}; not league-strength adjusted."
            : $"Insufficient sample: {sampleSize:N0}/{minimumSampleSize:N0} minimum rows; {direction}.";

        var results = new List<CohortPercentileResult>(sampleSize);
        for (var start = 0; start < ordered.Length;)
        {
            var end = start + 1;
            while (end < ordered.Length && ordered[end].Value.Equals(ordered[start].Value))
                end++;

            double? percentile = hasReliableSample
                ? 100d * (start + ((end - start) / 2d)) / sampleSize
                : null;
            if (percentile.HasValue && !higherIsBetter)
                percentile = 100d - percentile.Value;

            for (var index = start; index < end; index++)
            {
                var item = ordered[index];
                results.Add(new CohortPercentileResult(
                    item.Id,
                    item.Name,
                    item.Value,
                    percentile,
                    sampleSize,
                    basis));
            }

            start = end;
        }

        return results
            .OrderByDescending(x => x.Percentile ?? double.MinValue)
            .ThenByDescending(x => higherIsBetter ? x.Value : -x.Value)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
