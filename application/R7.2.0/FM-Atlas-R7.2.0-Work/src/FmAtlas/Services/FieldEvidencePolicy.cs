namespace FmAtlas.Services;

public enum FieldEvidenceLevel
{
    Unverified,
    Candidate,
    ProfileMatchedProvisional,
    FieldVerified
}

public sealed record FieldEvidenceAssessment(
    FieldEvidenceLevel Level,
    string Label,
    bool CanUseAsVerifiedDecisionInput,
    bool CanWriteToGame,
    string Explanation);

/// <summary>
/// One policy for interpreting the persisted LiveFieldRegistry status strings. A profile match
/// is not field verification, and the global live-write kill switch remains authoritative.
/// </summary>
public static class FieldEvidencePolicy
{
    public static FieldEvidenceAssessment Assess(string? validationStatus, string? evidence = null)
    {
        var status = (validationStatus ?? string.Empty).Trim();
        var normalized = status
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        FieldEvidenceLevel level;
        string label;
        // Explicitly fail closed when a status contains "unverified", even if it
        // also contains words such as "profile matched" or "provisional".
        if (normalized.Contains("unverified", StringComparison.Ordinal)
            || normalized.Contains("unknown", StringComparison.Ordinal)
            || normalized.Length == 0)
        {
            level = FieldEvidenceLevel.Unverified;
            label = "Unverified";
        }
        // Accept only explicit, canonical verification states. Prefix matches such as
        // "Verified-Provisional" or "Verified-Needs-Witness" must fail closed.
        else if (normalized is "verified"
            or "fieldverified"
            or "verifieduseruiwitnessreadback"
            or "fieldverifieduseruiwitnessreadback")
        {
            level = FieldEvidenceLevel.FieldVerified;
            label = "Field verified";
        }
        else if (normalized.Contains("profilematched", StringComparison.Ordinal)
            || normalized.Contains("provisional", StringComparison.Ordinal))
        {
            level = FieldEvidenceLevel.ProfileMatchedProvisional;
            label = "Profile matched · provisional field";
        }
        else if (normalized.Contains("candidate", StringComparison.Ordinal))
        {
            level = FieldEvidenceLevel.Candidate;
            label = "Candidate · unverified";
        }
        else
        {
            level = FieldEvidenceLevel.Unverified;
            label = "Unverified";
        }

        var decisionReady = level == FieldEvidenceLevel.FieldVerified;
        var explanation = !string.IsNullOrWhiteSpace(evidence)
            ? evidence.Trim()
            : level switch
            {
                FieldEvidenceLevel.FieldVerified => "The registry marks this field as verified. Confirm the game build and source provenance before using it.",
                FieldEvidenceLevel.ProfileMatchedProvisional => "Only the executable/module profile matched. Field meaning, offset and encoding are not independently certified.",
                FieldEvidenceLevel.Candidate => "This is a candidate discovery, not a verified field mapping.",
                _ => "No verification evidence has been recorded for this field."
            };

        return new FieldEvidenceAssessment(
            level,
            label,
            decisionReady,
            LiveWriteCertification.IsCertified && decisionReady,
            explanation);
    }

    public static FieldEvidenceAssessment Assess(Data.LiveFieldRegistryRecord field)
        => Assess(field.ValidationStatus, field.Evidence);

    public static bool CanUseForDecision(string? status)
        => Assess(status).CanUseAsVerifiedDecisionInput;

    public static bool CanWrite(string? status)
        => LiveWriteCertification.IsCertified && Assess(status).CanUseAsVerifiedDecisionInput;
}
