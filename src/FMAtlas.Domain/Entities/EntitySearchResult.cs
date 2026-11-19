namespace FMAtlas.Domain.Entities;

/// <summary>A lightweight row returned by entity search, before loading the full record.</summary>
public sealed record EntitySearchResult(EntityIdentity Identity, string DisplayName);
