using FMAtlas.Domain.Search;

namespace FMAtlas.Application.Abstractions;

/// <summary>
/// Discovers typed field keys already stored in the local Atlas index.
/// This is a read-only catalogue of imported values, not a claim that every
/// field is available in the live game or verified for every FM27 build.
/// </summary>
public interface IIndexedAdvancedSearchFieldProvider
{
    Task<IReadOnlyList<AdvancedSearchFieldDefinition>> GetIndexedFieldsAsync(
        SearchEntityKind entity,
        CancellationToken cancellationToken = default);
}
