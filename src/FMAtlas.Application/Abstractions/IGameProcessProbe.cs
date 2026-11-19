using FMAtlas.Domain.Connection;

namespace FMAtlas.Application.Abstractions;

public interface IGameProcessProbe
{
    Task<GameProcessProbeResult> ProbeAsync(CancellationToken cancellationToken = default);
}
