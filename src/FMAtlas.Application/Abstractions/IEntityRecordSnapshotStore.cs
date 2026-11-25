using FMAtlas.Domain.Entities;

namespace FMAtlas.Application.Abstractions;

/// <summary>
/// Stores lossless typed record snapshots by the identity carried by the record.
/// Searchable field projections are a separate store/service.
/// </summary>
public interface IEntityRecordSnapshotStore
{
    Task SaveSnapshotAsync<TRecord>(
        TRecord record,
        string? displayName,
        CancellationToken cancellationToken = default)
        where TRecord : class, IEntityRecord;

    Task<TRecord?> GetSnapshotAsync<TRecord>(
        EntityIdentity identity,
        CancellationToken cancellationToken = default)
        where TRecord : class, IEntityRecord;

    Task DeleteSnapshotAsync<TRecord>(
        EntityIdentity identity,
        CancellationToken cancellationToken = default)
        where TRecord : class, IEntityRecord;
}
