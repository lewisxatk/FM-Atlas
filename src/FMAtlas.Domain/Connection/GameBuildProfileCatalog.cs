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
    string EvidenceSource,
    ulong PlayersArrayRva = 0,
    ulong StaffArrayRva = 0,
    ulong ClubsArrayRva = 0,
    ulong GameUiRva = 0,
    ulong CurrentDateRva = 0,
    ulong GameLoadsOffset = 0x100)
{
    /// <summary>True only when FM Atlas has a build-specific, read-only entity scanner for this package.</summary>
    public bool HasReadOnlyScanner => Id is "fm27-gamepass-0.9.2524.0" or "fm27-gamepass-0.9.2562.0";

    /// <summary>Every module-relative root required by the read-only scanner must exist in the exact profile.</summary>
    public bool HasRequiredReadOnlyLayout =>
        HasReadOnlyScanner &&
        PlayersArrayRva != 0 &&
        StaffArrayRva != 0 &&
        ClubsArrayRva != 0 &&
        GameUiRva != 0 &&
        CurrentDateRva != 0 &&
        GameLoadsOffset != 0;

    /// <summary>
    /// Whether an externally mapped build may undergo a guarded read-only probe.
    /// This is not AtlasReadVerified and never authorizes a process-memory write.
    /// </summary>
    public bool CanAttemptReadOnlyScan =>
        HasRequiredReadOnlyLayout &&
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
            "FMLibreEditor Game Pass 0.9.2524.0 profile and Football Manager 27 Editor 27.1.0+2505117 offsets",
            0x53FAE88, 0x53FAED0, 0x53FB040, 0x53FBA40, 0x539DD10, 0x100),
        new(
            "fm27-gamepass-0.9.2562.0",
            "Football Manager 27 Game Pass 27.1.1 (0.9.2562.0)",
            "Xbox / Microsoft Store",
            "0.9.2562.0",
            0x6AFF583B,
            0x17624000,
            GameBuildEvidenceStatus.ExternalReferenceOnly,
            "User-supplied DLL fingerprint plus FMLibreEditor Game Pass 0.9.2562.0 profile. Its profile probe passed 12 checks on a separate loaded save. External mapping only; FM Atlas has not independently validated this user's career.",
            0x53F8EA8, 0x53F8EF0, 0x53F9060, 0x53F9A60, 0x539BD10, 0x100),
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
