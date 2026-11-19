using FMAtlas.Domain.Graphics;

namespace FMAtlas.Application.Abstractions;

/// <summary>
/// Read-only lookup into an installed Football Manager graphics folder.
/// Indexing never modifies, renames or moves user graphics.
/// </summary>
public interface IGraphicsLocator
{
    GraphicsIndexSummary Status { get; }

    Task<GraphicsIndexSummary> IndexAsync(
        string graphicsFolder,
        CancellationToken cancellationToken = default);

    GraphicsAssetReference? FindPortrait(string playerUid);
    GraphicsAssetReference? FindClubLogo(uint clubId);
    GraphicsAssetReference? FindNationLogo(uint nationId);

    void Clear();
}
