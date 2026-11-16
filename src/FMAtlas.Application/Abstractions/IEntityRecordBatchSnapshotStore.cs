using FMAtlas.Domain.Entities;

namespace FMAtlas.Application.Abstractions;

/// <summary>
/// Persists a group of typed snapshots and their searchable projections in one local database transaction.
/// Implementations must preserve per-field evidence and provider-scoped identities.
/// </summary>
public interface IEntityRecordBatchSnapshotStore
{
    Task SaveSnapshotsAsync(
        IReadOnlyCollection<IEntityRecord> records,
        CancellationToken cancellationToken = default);
}
