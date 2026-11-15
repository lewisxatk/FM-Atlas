namespace FmAtlas.Models;

public enum ScoutingCohortBasis { Overall, Position, League, PositionAndLeague }

public sealed record ScoutingMetricRequirement(
    string MetricKey,
    string DisplayName,
    double? Minimum = null,
    double? Maximum = null,
    bool UsePer90 = false);

/// <summary>A reusable multi-metric recruitment brief. Values and provenance are always sourced from imported career data.</summary>
public sealed record ScoutingProfile(
    string Id,
    string Name,
    DateTime UpdatedUtc,
    IReadOnlyList<ScoutingMetricRequirement> Requirements,
    ScoutingCohortBasis CohortBasis = ScoutingCohortBasis.Position,
    string? PositionCode = null,
    string? League = null,
    int? MinimumAge = null,
    int? MaximumAge = null,
    double MinimumMinutes = 450,
    int MinimumSampleSize = 1,
    double MinimumDataCoverage = 0.70,
    decimal? MaximumValue = null,
    int ResultLimit = 100);

public sealed record ScoutingMetricObservation(
    string SourceId,
    string PlayerName,
    string? Club,
    string? Position,
    string? League,
    int? Age,
    decimal? Value,
    double Minutes,
    int SampleSize,
    double DataCoverage,
    string SourceName,
    DateTime? SourceUpdatedUtc,
    IReadOnlyDictionary<string, double?> Metrics,
    IReadOnlyList<string>? MissingFields = null);

public sealed record ScoutingEvidence(
    string SourceName,
    DateTime? SourceUpdatedUtc,
    double Coverage,
    int SampleSize,
    double Minutes,
    IReadOnlyList<string> MissingFields);

public sealed record ScoutingProfileMatch(
    ScoutingMetricObservation Player,
    double MatchScore,
    ScoutingEvidence Evidence,
    IReadOnlyList<string> FailedRequirements);
