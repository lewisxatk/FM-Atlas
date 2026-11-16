using FMAtlas.Domain.Entities;

namespace FMAtlas.Application.Abstractions;

/// <summary>
/// Stores lossless typed record snapshots by provider-scoped identity.
/// Searchable field projections are a separate store/service.
/// </summary>
public interface IEntityRecordSnapshotStore
{
    Task SaveSnapshotAsync<TRecord>(
        EntityIdentity identity,
        string? displayName,
        TRecord record,
        CancellationToken cancellationToken = default)
        where TRecord : class;

    Task<TRecord?> GetSnapshotAsync<TRecord>(
        EntityIdentity identity,
        CancellationToken cancellationToken = default)
        where TRecord : class;

    Task DeleteSnapshotAsync<TRecord>(
        EntityIdentity identity,
        CancellationToken cancellationToken = default)
        where TRecord : class;
}
