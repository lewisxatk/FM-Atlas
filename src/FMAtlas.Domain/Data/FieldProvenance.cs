using FMAtlas.Domain.Players;

namespace FMAtlas.Domain.Data;

/// <summary>
/// Evidence attached to one field value. A documented mapping is not a verified
/// read, and a verified read is not permission to write.
/// </summary>
public sealed record FieldProvenance(
    DataSourceKind Source,
    FieldEvidenceStatus EvidenceStatus,
    string? SourceVersion = null,
    string? TargetBuildFingerprint = null,
    DateTimeOffset? ObservedAtUtc = null,
    string? Description = null)
{
    /// <summary>True only when the field has been read-verified or write-verified.</summary>
    public bool IsReadVerified =>
        EvidenceStatus is FieldEvidenceStatus.ReadVerified or FieldEvidenceStatus.WriteVerified;

    /// <summary>
    /// Conservative UI/domain hint. The live writer must still repeat all
    /// process, save-generation, identity, address and value preflight checks.
    /// </summary>
    public bool IsWriteVerifiedForBuild =>
        Source == DataSourceKind.LiveGameMemory &&
        EvidenceStatus == FieldEvidenceStatus.WriteVerified &&
        !string.IsNullOrWhiteSpace(TargetBuildFingerprint);
}
