namespace FMAtlas.Domain.Data;

/// <summary>Identifies where a value was obtained. Source type is not a confidence level.</summary>
public enum DataSourceKind
{
    Unknown,
    LiveGameMemory,
    CareerSave,
    GameExport,
    LocalAtlas,
    GraphicsConfig,
    Calculated,
    UserProvided
}
