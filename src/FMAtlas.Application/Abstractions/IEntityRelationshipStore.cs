using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Application.Abstractions;

/// <summary>
/// Persists source-backed relationships between Atlas entities. Relationships
/// are separate from scalar field values so direction and both identities remain intact.
/// </summary>
public interface IEntityRelationshipStore
{
    Task SaveRelationshipAsync(
        EntityIdentity from,
        EntityIdentity to,
        string relationKind,
        FieldProvenance provenance,
        CancellationToken cancellationToken = default);

    Task DeleteRelationshipAsync(
        EntityIdentity from,
        EntityIdentity to,
        string relationKind,
        CancellationToken cancellationToken = default);
}
