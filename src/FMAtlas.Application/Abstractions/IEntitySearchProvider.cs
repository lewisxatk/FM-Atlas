using FMAtlas.Domain.Entities;

namespace FMAtlas.Application.Abstractions;

/// <summary>Read-only search over imported Atlas entity records.</summary>
public interface IEntitySearchProvider
{
    Task<IReadOnlyList<EntitySearchResult>> QuickSearchAsync(
        string searchText,
        IReadOnlyCollection<EntityKind>? entityKinds = null,
        int limit = 50,
        CancellationToken cancellationToken = default,
        bool includeWomenPlayers = false);
}
