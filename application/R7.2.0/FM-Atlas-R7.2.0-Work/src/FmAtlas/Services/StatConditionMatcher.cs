using FmAtlas.Models;

namespace FmAtlas.Services;

public sealed record StatConditionMatchResult(bool Matches, int MatchedCount, IReadOnlyList<string> MatchedValues);

/// <summary>
/// Shared ALL/ANY matcher for advanced statistical search. Missing values are non-matches,
/// never zero. Per-90 conversion uses the same player-period minutes for every criterion.
/// </summary>
public static class StatConditionMatcher
{
    public static StatConditionMatchResult Evaluate(
        IReadOnlyDictionary<string, double> values,
        IReadOnlyList<StatFilterCriterion> criteria,
        bool matchAny,
        bool per90 = false,
        double? minutes = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count == 0)
            return new StatConditionMatchResult(false, 0, Array.Empty<string>());

        if (per90 && (!minutes.HasValue || !double.IsFinite(minutes.Value) || minutes.Value <= 0))
            return new StatConditionMatchResult(false, 0, Array.Empty<string>());

        var matched = new List<string>();
        foreach (var criterion in criteria)
        {
            if (string.IsNullOrWhiteSpace(criterion.Key)
                || (!criterion.Minimum.HasValue && !criterion.Maximum.HasValue))
                throw new ArgumentException("Each statistical condition needs a field and at least one boundary.", nameof(criteria));

            if ((criterion.Minimum.HasValue && !double.IsFinite(criterion.Minimum.Value))
                || (criterion.Maximum.HasValue && !double.IsFinite(criterion.Maximum.Value)))
                throw new ArgumentOutOfRangeException(nameof(criteria), "Statistical boundaries must be finite numbers.");

            if (criterion.Minimum.HasValue && criterion.Maximum.HasValue && criterion.Minimum.Value > criterion.Maximum.Value)
                throw new ArgumentException("A metric minimum cannot exceed its maximum.", nameof(criteria));

            if (!TryGetValue(values, criterion.Key, out var raw) || !double.IsFinite(raw))
                continue;

            var value = per90 ? raw * 90d / minutes!.Value : raw;
            if (!double.IsFinite(value)) continue;

            var withinBounds =
                (!criterion.Minimum.HasValue || value >= criterion.Minimum.Value)
                && (!criterion.Maximum.HasValue || value <= criterion.Maximum.Value);

            if (withinBounds)
                matched.Add($"{criterion.DisplayName}: {value:0.##}");
        }

        var matchedCount = matched.Count;
        return new StatConditionMatchResult(
            matchAny ? matchedCount > 0 : matchedCount == criteria.Count,
            matchedCount,
            matched);
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, double> values, string key, out double value)
    {
        if (values.TryGetValue(key, out value)) return true;
        foreach (var pair in values)
        {
            if (!pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            value = pair.Value;
            return true;
        }
        value = default;
        return false;
    }
}
