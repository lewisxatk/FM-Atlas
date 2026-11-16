namespace FMAtlas.Application.Abstractions;

public interface IAtlasDatabase
{
    int CurrentSchemaVersion { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default);
}
