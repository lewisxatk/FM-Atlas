namespace FMAtlas.Domain.Graphics;

public sealed record GraphicsIndexSummary(
    GraphicsIndexState State,
    string RootPath,
    int ConfigFiles,
    int Portraits,
    int ClubLogos,
    int NationLogos,
    IReadOnlyList<string> Warnings)
{
    public static GraphicsIndexSummary Off { get; } =
        new(GraphicsIndexState.Off, string.Empty, 0, 0, 0, 0, Array.Empty<string>());
}
