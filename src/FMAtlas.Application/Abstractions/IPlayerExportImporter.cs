using FMAtlas.Application.Models;

namespace FMAtlas.Application.Abstractions;

/// <summary>
/// Imports read-only Football Manager player-list exports into the local Atlas database.
/// This does not attach to, read from, or write to the running game process.
/// </summary>
public interface IPlayerExportImporter
{
    Task<PlayerExportImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
