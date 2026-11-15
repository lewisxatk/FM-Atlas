using FmAtlas.Models;

namespace FmAtlas.Services;

public sealed record PlayerStatSample(
    string PlayerId, string PlayerName, string? Position, string? League,
    IReadOnlyDictionary<string, double?> Metrics, string SourceName,
    DateTime? SourceUpdatedUtc, IReadOnlyList<string>? MissingFields = null);

public sealed record PlayerStatPercentile(
    string PlayerId, string PlayerName, string MetricKey, double? Value,
    double? Percentile, int CohortSize, string CohortLabel,
    string SourceName, DateTime? SourceUpdatedUtc, IReadOnlyList<string> Warnings);

/// <summary>Calculates tie-aware empirical percentiles from the supplied cohort only.</summary>
public static class PlayerStatComparisonService
{
    public static IReadOnlyList<PlayerStatPercentile> Compare(
        IReadOnlyList<PlayerStatSample> players,
        IReadOnlyList<PlayerStatSample> referencePopulation,
        IReadOnlyList<string> metricKeys,
        ScoutingCohortBasis basis)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(referencePopulation);
        ArgumentNullException.ThrowIfNull(metricKeys);
        var output = new List<PlayerStatPercentile>();
        foreach (var player in players)
        foreach (var metric in metricKeys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            player.Metrics.TryGetValue(metric, out var value);
            var cohort = referencePopulation.Where(candidate => InCohort(player, candidate, basis))
                .Select(candidate => candidate.Metrics.TryGetValue(metric, out var sample) ? sample : null)
                .Where(x => x.HasValue && double.IsFinite(x.Value)).Select(x => x!.Value).OrderBy(x => x).ToArray();
            double? percentile = null;
            var warnings = new List<string>();
            if (!value.HasValue || !double.IsFinite(value.Value))
                warnings.Add("Player value missing; percentile not calculated.");
            else if (cohort.Length < 2)
                warnings.Add("Fewer than two valid cohort observations; percentile not calculated.");
            else
            {
                var below = cohort.Count(x => x < value.Value);
                var equal = cohort.Count(x => x == value.Value);
                percentile = 100d * (below + equal / 2d) / cohort.Length;
            }
            if (cohort.Length == 0) warnings.Add("No reference cohort data for this metric.");
            if (player.MissingFields?.Count > 0) warnings.Add("Some source fields are missing.");
            var label = basis switch
            {
                ScoutingCohortBasis.Overall => "Overall pool",
                ScoutingCohortBasis.Position => "Same position",
                ScoutingCohortBasis.League => "Same league",
                _ => "Same position + league"
            };
            output.Add(new PlayerStatPercentile(player.PlayerId, player.PlayerName, metric, value,
                percentile, cohort.Length, label, player.SourceName, player.SourceUpdatedUtc, warnings));
        }
        return output;
    }

    private static bool InCohort(PlayerStatSample target, PlayerStatSample candidate, ScoutingCohortBasis basis)
        => basis switch
        {
            ScoutingCohortBasis.Overall => true,
            ScoutingCohortBasis.Position => !string.IsNullOrWhiteSpace(target.Position)
                && string.Equals(target.Position, candidate.Position, StringComparison.OrdinalIgnoreCase),
            ScoutingCohortBasis.League => !string.IsNullOrWhiteSpace(target.League)
                && string.Equals(target.League, candidate.League, StringComparison.OrdinalIgnoreCase),
            ScoutingCohortBasis.PositionAndLeague => !string.IsNullOrWhiteSpace(target.Position)
                && !string.IsNullOrWhiteSpace(target.League)
                && string.Equals(target.Position, candidate.Position, StringComparison.OrdinalIgnoreCase)
                && string.Equals(target.League, candidate.League, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
}
