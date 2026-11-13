namespace FmAtlas.Services;

public enum MetricRankStatus
{
    Ranked,
    MissingValue,
    InsufficientMinutes,
    InsufficientCohort
}

public sealed record MetricObservation(string SourceId, double? Value, double? Minutes);

public sealed record MetricPercentileResult(
    string SourceId,
    double? Value,
    double? Minutes,
    int CohortSize,
    double? Percentile,
    MetricRankStatus Status);

public sealed record MetricPercentileOptions(
    double MinimumMinutes = 450,
    int MinimumCohortSize = 5,
    bool HigherIsBetter = true);

/// <summary>
/// Safe shared math for Football Manager performance metrics. This contains no assumed FM
/// performance formulas: callers must supply an observed metric value and define its cohort.
/// It handles per-90 denominators, missing data, minutes floors and tied percentiles.
/// </summary>
public static class FootballMetricMath
{
    public static double? ToPer90(double? total, double? minutes)
    {
        if (!total.HasValue || !minutes.HasValue) return null;
        if (!double.IsFinite(total.Value) || !double.IsFinite(minutes.Value))
            throw new ArgumentOutOfRangeException(nameof(total), "Metric inputs must be finite.");
        if (total.Value < 0) throw new ArgumentOutOfRangeException(nameof(total), "Observed event totals cannot be negative.");
        if (minutes.Value <= 0) return null;
        return total.Value * 90d / minutes.Value;
    }

    public static double? ToPercentage(double? numerator, double? denominator)
    {
        if (!numerator.HasValue || !denominator.HasValue) return null;
        if (!double.IsFinite(numerator.Value) || !double.IsFinite(denominator.Value))
            throw new ArgumentOutOfRangeException(nameof(numerator), "Metric inputs must be finite.");
        if (numerator.Value < 0 || denominator.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(numerator), "Observed counts cannot be negative.");
        if (denominator.Value <= 0) return null;
        return numerator.Value / denominator.Value * 100d;
    }

    public static IReadOnlyList<MetricPercentileResult> CalculatePercentiles(
        IEnumerable<MetricObservation> observations,
        MetricPercentileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(observations);
        options ??= new MetricPercentileOptions();

        if (!double.IsFinite(options.MinimumMinutes) || options.MinimumMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MinimumMinutes must be finite and non-negative.");
        if (options.MinimumCohortSize < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "MinimumCohortSize must be at least one.");

        var source = observations.ToArray();
        if (source.Any(x => x is null || string.IsNullOrWhiteSpace(x.SourceId)))
            throw new ArgumentException("Every metric observation must have a provider-specific SourceId.", nameof(observations));

        var duplicate = source.GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException("Metric observation SourceIds must be unique within a cohort: " + duplicate.Key, nameof(observations));

        foreach (var item in source)
        {
            if (item.Minutes.HasValue && (!double.IsFinite(item.Minutes.Value) || item.Minutes.Value < 0))
                throw new ArgumentOutOfRangeException(nameof(observations), $"Invalid minutes for {item.SourceId}.");
            if (item.Value.HasValue && !double.IsFinite(item.Value.Value))
                throw new ArgumentOutOfRangeException(nameof(observations), $"Non-finite metric value for {item.SourceId}.");
        }

        var eligible = source.Where(x =>
                x.Value.HasValue
                && x.Minutes.HasValue
                && x.Minutes.Value >= options.MinimumMinutes)
            .ToArray();

        var cohortSize = eligible.Length;
        var enoughCohort = cohortSize >= options.MinimumCohortSize;
        var orderedValues = eligible.Select(x => x.Value!.Value).OrderBy(x => x).ToArray();

        var results = new List<MetricPercentileResult>(source.Length);
        foreach (var item in source)
        {
            if (!item.Value.HasValue)
            {
                results.Add(new MetricPercentileResult(item.SourceId, null, item.Minutes, cohortSize, null, MetricRankStatus.MissingValue));
                continue;
            }

            if (!item.Minutes.HasValue || item.Minutes.Value < options.MinimumMinutes)
            {
                results.Add(new MetricPercentileResult(item.SourceId, item.Value, item.Minutes, cohortSize, null, MetricRankStatus.InsufficientMinutes));
                continue;
            }

            if (!enoughCohort)
            {
                results.Add(new MetricPercentileResult(item.SourceId, item.Value, item.Minutes, cohortSize, null, MetricRankStatus.InsufficientCohort));
                continue;
            }

            var less = 0;
            var equal = 0;
            foreach (var value in orderedValues)
            {
                var comparison = value.CompareTo(item.Value.Value);
                if (comparison < 0) less++;
                else if (comparison == 0) equal++;
            }

            var percentile = cohortSize == 1
                ? 50d
                : (less + (equal - 1) / 2d) / (cohortSize - 1d) * 100d;
            if (!options.HigherIsBetter) percentile = 100d - percentile;

            results.Add(new MetricPercentileResult(
                item.SourceId, item.Value, item.Minutes, cohortSize,
                Math.Clamp(percentile, 0d, 100d), MetricRankStatus.Ranked));
        }

        return results;
    }
}
