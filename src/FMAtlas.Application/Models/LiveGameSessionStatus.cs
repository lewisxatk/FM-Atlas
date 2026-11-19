namespace FMAtlas.Application.Models;

public sealed record LiveGameSessionStatus(
    bool IsConnected,
    bool SaveChanged,
    long? SaveGeneration,
    string Message);
