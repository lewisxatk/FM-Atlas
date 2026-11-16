using System.Diagnostics;
using System.ComponentModel;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Connection;

namespace FMAtlas.Infrastructure.Connection;

/// <summary>
/// Detects a likely FM process and the game_plugin.dll module only.
/// This class does not read or write process memory and does not claim that
/// a detected module/build is supported.
/// </summary>
public sealed class WindowsGameProcessProbe : IGameProcessProbe
{
    private static readonly string[] CandidateProcessNames = ["fm"];

    public Task<GameProcessProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
            return Task.FromResult(GameProcessProbeResult.Unsupported());

        var candidates = new List<GameProcessCandidate>();

        foreach (var processName in CandidateProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    string? executablePath = null;
                    string? modulePath = null;
                    string? diagnostic = null;

                    try
                    {
                        executablePath = process.MainModule?.FileName;
                        foreach (ProcessModule module in process.Modules)
                        {
                            if (string.Equals(module.ModuleName, "game_plugin.dll", StringComparison.OrdinalIgnoreCase))
                            {
                                modulePath = module.FileName;
                                break;
                            }
                        }
                    }
                    catch (Win32Exception ex)
                    {
                        diagnostic = $"Module inspection was denied or unavailable: {ex.Message}";
                    }
                    catch (InvalidOperationException ex)
                    {
                        diagnostic = $"The process exited during inspection: {ex.Message}";
                    }

                    candidates.Add(new GameProcessCandidate(
                        process.Id,
                        process.ProcessName,
                        executablePath,
                        modulePath is not null,
                        modulePath,
                        diagnostic));
                }
                catch (InvalidOperationException)
                {
                    // The process may exit between enumeration and inspection.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        if (candidates.Count == 0)
            return Task.FromResult(GameProcessProbeResult.NotFound());

        var state = candidates.Any(candidate => candidate.HasGamePluginModule)
            ? GameProcessProbeState.ProcessAndModuleFound
            : GameProcessProbeState.ProcessFoundModuleUnconfirmed;

        return Task.FromResult(new GameProcessProbeResult(state, candidates, DateTimeOffset.UtcNow));
    }
}
