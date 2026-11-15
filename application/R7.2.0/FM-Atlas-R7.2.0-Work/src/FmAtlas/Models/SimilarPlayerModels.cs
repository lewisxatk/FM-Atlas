namespace FmAtlas.Models;

public sealed record SimilarPlayerCandidate(
    PlayerRecord Player,
    double SimilarityScore,
    int AttributesCompared,
    int ReferenceAttributes,
    double AttributeCoverage)
{
    public string DisplaySimilarity => SimilarityScore.ToString("0.0");
    public string DisplayCoverage => AttributeCoverage.ToString("P0");
    public string EvidenceSummary => $"{AttributesCompared:N0}/{ReferenceAttributes:N0} shared attributes";
}
