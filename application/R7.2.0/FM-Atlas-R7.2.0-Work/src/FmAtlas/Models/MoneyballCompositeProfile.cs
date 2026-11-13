namespace FmAtlas.Models;

/// <summary>
/// Reusable Moneyball composite criteria only. No player rows or career-save data are persisted.
/// </summary>
public sealed record MoneyballCompositeProfile(
    string Id,
    string Name,
    IReadOnlyList<string> MetricKeys,
    int MinimumMetrics,
    IReadOnlyDictionary<string, double> MetricWeights,
    string CohortMode,
    bool HigherIsBetter,
    bool Per90,
    int MinimumMinutes,
    int MinimumSampleSize,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record MoneyballCompositeProfileImportResult(
    int Added,
    int Skipped,
    IReadOnlyList<string> Conflicts);
