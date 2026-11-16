namespace FMAtlas.Application.Models;

/// <summary>Summary of one FM View Exporter CSV import.</summary>
public sealed record PlayerExportImportResult(
    string ProviderKey,
    int RowsRead,
    int ImportedPlayers,
    int SkippedRows,
    int DuplicateIds,
    int DynamicFieldsIndexed,
    IReadOnlyList<string> Warnings);
