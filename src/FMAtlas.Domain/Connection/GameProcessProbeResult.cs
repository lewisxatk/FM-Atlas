namespace FMAtlas.Domain.Connection;

public enum GameProcessProbeState
{
    UnsupportedPlatform,
    NotFound,
    ProcessFoundModuleUnconfirmed,
    ProcessAndModuleFound
}

public sealed record GameProcessCandidate(
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    bool HasGamePluginModule,
    string? GamePluginModulePath,
    string? Diagnostic);

public sealed record GameProcessProbeResult(
    GameProcessProbeState State,
    IReadOnlyList<GameProcessCandidate> Candidates,
    DateTimeOffset CheckedAtUtc)
{
    public static GameProcessProbeResult Unsupported() =>
        new(GameProcessProbeState.UnsupportedPlatform, Array.Empty<GameProcessCandidate>(), DateTimeOffset.UtcNow);

    public static GameProcessProbeResult NotFound() =>
        new(GameProcessProbeState.NotFound, Array.Empty<GameProcessCandidate>(), DateTimeOffset.UtcNow);
}
