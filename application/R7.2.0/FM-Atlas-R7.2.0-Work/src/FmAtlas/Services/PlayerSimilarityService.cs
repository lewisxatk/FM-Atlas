namespace FmAtlas.Services;

public sealed record PlayerSimilarityCandidate(
    string SourceId,
    string Name,
    string? Club,
    string? Position,
    IReadOnlyDictionary<string, double> Attributes);

public sealed record PlayerSimilarityResult(
    string SourceId,
    string Name,
    string? Club,
    string? Position,
    double Similarity,
    int SharedAttributeCount,
    int TargetAttributeCount,
    int CandidateAttributeCount,
    double TargetCoverage,
    double CandidateCoverage,
    string Basis);

/// <summary>
/// Transparent nearest-player comparison using only common, finite FM 1–20 attributes.
/// Similarity is 100 × (1 − mean absolute difference / 19). The service does not use
/// name, age, club, CA/PA, reputation, position familiarity or any Football Manager hidden formula.
/// </summary>
public static class PlayerSimilarityService
{
    public static IReadOnlyList<PlayerSimilarityResult> Rank(
        string targetSourceId,
        IReadOnlyDictionary<string, double> targetAttributes,
        IEnumerable<PlayerSimilarityCandidate> candidates,
        int minimumCommonAttributes = 5,
        double minimumTargetCoverage = 0.35)
    {
        if (string.IsNullOrWhiteSpace(targetSourceId))
            throw new ArgumentException("A target player source ID is required.", nameof(targetSourceId));
        ArgumentNullException.ThrowIfNull(targetAttributes);
        ArgumentNullException.ThrowIfNull(candidates);
        if (minimumCommonAttributes < 1 || minimumCommonAttributes > 100)
            throw new ArgumentOutOfRangeException(nameof(minimumCommonAttributes));
        if (!double.IsFinite(minimumTargetCoverage) || minimumTargetCoverage < 0 || minimumTargetCoverage > 1)
            throw new ArgumentOutOfRangeException(nameof(minimumTargetCoverage));

        var target = Normalize(targetAttributes);
        if (target.Count == 0)
            return Array.Empty<PlayerSimilarityResult>();

        var results = new List<PlayerSimilarityResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (candidate is null || string.IsNullOrWhiteSpace(candidate.SourceId)
                || candidate.SourceId.Equals(targetSourceId, StringComparison.OrdinalIgnoreCase)
                || !seen.Add(candidate.SourceId))
                continue;

            if (candidate.Attributes is null)
                continue;
            var candidateValues = Normalize(candidate.Attributes);
            if (candidateValues.Count == 0)
                continue;

            var common = target.Keys
                .Where(candidateValues.ContainsKey)
                .ToArray();
            var targetCoverage = common.Length / (double)target.Count;
            var candidateCoverage = common.Length / (double)candidateValues.Count;
            if (common.Length < minimumCommonAttributes || targetCoverage < minimumTargetCoverage)
                continue;

            var meanAbsoluteDifference = common.Average(key => Math.Abs(target[key] - candidateValues[key]));
            var similarity = Math.Clamp(100d * (1d - meanAbsoluteDifference / 19d), 0d, 100d);
            if (!double.IsFinite(similarity))
                continue;

            var basis = $"{common.Length:N0} shared 1–20 attributes · target coverage {targetCoverage:P0} · candidate coverage {candidateCoverage:P0} · mean absolute difference";
            results.Add(new PlayerSimilarityResult(
                candidate.SourceId,
                candidate.Name,
                candidate.Club,
                candidate.Position,
                similarity,
                common.Length,
                target.Count,
                candidateValues.Count,
                targetCoverage,
                candidateCoverage,
                basis));
        }

        return results
            .OrderByDescending(x => x.Similarity)
            .ThenByDescending(x => x.SharedAttributeCount)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Dictionary<string, double> Normalize(IReadOnlyDictionary<string, double> source)
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            var key = new string((pair.Key ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
            if (key.Length > 0 && double.IsFinite(pair.Value) && pair.Value >= 1 && pair.Value <= 20)
                values.TryAdd(key, pair.Value);
        }
        return values;
    }
}
