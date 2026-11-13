namespace FmAtlas.Services;

public sealed record MetricObservation(string Id, string Name, double Value);

public sealed record MetricPercentileResult(
    string Id,
    string Name,
    double Value,
    double? Percentile,
    int SampleSize,
    string Basis);

/// <summary>
/// Calculates a transparent mid-rank percentile within the exact supplied cohort.
/// It does not adjust for competition strength or claim to reproduce FM's own rating.
/// </summary>
public static class CohortPercentileService
{
    public static IReadOnlyList<MetricPercentileResult> Calculate(
        IReadOnlyList<MetricObservation> observations,
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

        return valid
            .Select(item =>
            {
                var below = valid.Count(x => x.Value < item.Value);
                var tied = valid.Count(x => x.Value.Equals(item.Value));
                double? percentile = hasReliableSample
                    ? 100d * (below + (tied / 2d)) / sampleSize
                    : null;

                return new MetricPercentileResult(
                    item.Id,
                    item.Name,
                    item.Value,
                    percentile,
                    sampleSize,
                    hasReliableSample
                        ? $"Mid-rank within {sampleSize:N0} returned rows; not league-strength adjusted."
                        : $"Insufficient sample: {sampleSize:N0}/{minimumSampleSize:N0} minimum rows.");
            })
            .OrderByDescending(x => x.Percentile ?? double.MinValue)
            .ThenByDescending(x => x.Value)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
