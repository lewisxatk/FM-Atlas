namespace FMAtlas.Domain.Entities;

/// <summary>
/// Stable identity within a provider. Keep the provider key because IDs from
/// different data sources are not assumed to share a namespace.
/// </summary>
public sealed record EntityIdentity(
    EntityKind Kind,
    string SourceId,
    string ProviderKey)
{
    public bool IsUsable =>
        Kind != EntityKind.Unknown &&
        !string.IsNullOrWhiteSpace(SourceId) &&
        !string.IsNullOrWhiteSpace(ProviderKey);
}
