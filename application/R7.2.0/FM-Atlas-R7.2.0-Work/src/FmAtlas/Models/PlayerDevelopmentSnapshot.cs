namespace FmAtlas.Models;

public sealed record PlayerDevelopmentSnapshot(
    int SnapshotId,
    DateTime GameDate,
    string ImportedUtc,
    string SourceFileName,
    double? CurrentAbility,
    double? PotentialAbility,
    decimal? Value);
