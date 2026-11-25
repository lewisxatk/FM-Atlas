namespace FMAtlas.Domain.Graphics;

public sealed record GraphicsAssetReference(
    GraphicsAssetKind Kind,
    uint GameId,
    string ImagePath,
    string ConfigPath);
