using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Search;

namespace FMAtlas.Application.Abstractions;

/// <summary>Read-only execution of validated Advanced Search criteria.</summary>
public interface IAdvancedSearchProvider
{
    Task<IReadOnlyList<EntitySearchResult>> ExecuteAsync(
        AdvancedSearchQuery query,
        int limit = 200,
        CancellationToken cancellationToken = default);
}
