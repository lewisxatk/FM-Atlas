namespace FMAtlas.Domain.Connection;

public enum GameBuildEvidenceStatus
{
    ExternalReferenceOnly,
    AtlasReadVerified,
    AtlasWriteVerified
}

/// <summary>
/// A known external build fingerprint. ExternalReferenceOnly allows a narrowly
/// scoped read-only probe where FM Atlas has an exact-layout scanner; it does not
/// mean that imported values are Atlas-verified and never authorizes writes.
/// </summary>
public sealed record GameBuildProfile(
    string Id,
    string DisplayName,
    string Storefront,
    string PackageVersion,
    uint PeTimestamp,
    uint SizeOfImage,
    GameBuildEvidenceStatus EvidenceStatus,
    string EvidenceSource)
{
    /// <summary>True only when FM Atlas currently contains a scanner for this exact package build.</summary>
    public bool HasReadOnlyScanner => Id == "fm27-gamepass-0.9.2524.0";

    /// <summary>
    /// Whether the existing exact-build scanner may attempt a read-only probe. This is
    /// not the same as AtlasReadVerified and never authorizes a process-memory write.
    /// </summary>
    public bool CanAttemptReadOnlyScan =>
        HasReadOnlyScanner &&
        (EvidenceStatus is GameBuildEvidenceStatus.ExternalReferenceOnly
            or GameBuildEvidenceStatus.AtlasReadVerified
            or GameBuildEvidenceStatus.AtlasWriteVerified);

    /// <summary>True only after FM Atlas has independently verified the read mapping on this build.</summary>
    public bool CanReadLiveData => EvidenceStatus is GameBuildEvidenceStatus.AtlasReadVerified
        or GameBuildEvidenceStatus.AtlasWriteVerified;

    public bool CanWriteLiveData => EvidenceStatus == GameBuildEvidenceStatus.AtlasWriteVerified;
}

public static class GameBuildProfileCatalog
{
    public static IReadOnlyList<GameBuildProfile> Profiles { get; } =
    [
        new(
            "fm27-gamepass-0.9.2524.0",
            "Football Manager 27 Game Pass 27.1.0 (0.9.2524.0)",
            "Xbox / Microsoft Store",
            "0.9.2524.0",
            0x6AF5C8C6,
            0x179AD000,
            GameBuildEvidenceStatus.ExternalReferenceOnly,
            "FMLibreEditor 0.9.2524.0 profile (12 checks passed) and Football Manager 27 Editor 27.1.0+2505117 Game Pass offsets"),
        new(
            "fm27-gamepass-0.9.2435.0",
            "Football Manager 27 Game Pass 0.9.2435.0",
            "Xbox / Microsoft Store",
            "0.9.2435.0",
            0x6AE8CF77,
            0x17969000,
            GameBuildEvidenceStatus.ExternalReferenceOnly,
            "FMLibreEditor FM27 Game Pass reverse-engineering notes"),
        new(
            "fm27-gamepass-0.9.2409.0",
            "Football Manager 27 Game Pass 0.9.2409.0",
            "Xbox / Microsoft Store",
            "0.9.2409.0",
            0x6AE563A1,
            0x175DA000,
            GameBuildEvidenceStatus.ExternalReferenceOnly,
            "FMLibreEditor FM27 Game Pass reverse-engineering notes")
    ];

    /// <summary>
    /// Matches exact PE timestamp and SizeOfImage to an external reference. A match
    /// is informational only until FM Atlas verifies the user's build itself.
    /// </summary>
    public static GameBuildProfile? MatchReference(GameModuleFingerprint fingerprint) =>
        Profiles.FirstOrDefault(profile =>
            profile.PeTimestamp == fingerprint.PeTimestamp &&
            profile.SizeOfImage == fingerprint.SizeOfImage);
}
