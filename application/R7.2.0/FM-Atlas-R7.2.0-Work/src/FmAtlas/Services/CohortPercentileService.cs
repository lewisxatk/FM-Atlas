namespace FmAtlas.Services;

public sealed record CohortMetricObservation(string Id, string Name, double Value);

public sealed record CohortPercentileResult(
    string Id,
    string Name,
    double Value,
    double? Percentile,
    int SampleSize,
    string Basis);

/// <summary>
/// Calculates a transparent mid-rank percentile within the exact supplied cohort.
/// It does not adjust for competition strength or claim to reproduce FM's own rating.
/// Sorting and tie grouping keep this practical for several thousand observations.
/// </summary>
public static class CohortPercentileService
{
    public static IReadOnlyList<CohortPercentileResult> Calculate(
        IReadOnlyList<CohortMetricObservation> observations,
        int minimumSampleSize = 5)
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
        var results = new List<CohortPercentileResult>(sampleSize);
        var basis = hasReliableSample
            ? $"Mid-rank within {sampleSize:N0} returned rows; not league-strength adjusted."
            : $"Insufficient sample: {sampleSize:N0}/{minimumSampleSize:N0} minimum rows.";

        // All equal values share the midpoint of their occupied rank interval.
        // This is O(n log n) for sorting plus O(n) for grouping, rather than
        // recounting the full cohort once for every player.
        for (var start = 0; start < ordered.Length;)
        {
            var end = start + 1;
            while (end < ordered.Length && ordered[end].Value.Equals(ordered[start].Value))
                end++;

            double? percentile = hasReliableSample
                ? 100d * (start + ((end - start) / 2d)) / sampleSize
                : null;

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
            .ThenByDescending(x => x.Value)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
