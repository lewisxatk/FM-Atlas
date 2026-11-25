using FMAtlas.Application.Models;

namespace FMAtlas.Application.Abstractions;

/// <summary>
/// Read-only live Football Manager session connector.
/// A changed save generation invalidates all imported live-session data until ReloadAsync completes.
/// </summary>
public interface ILiveGameConnector : IDisposable
{
    Task<LiveGameConnectionResult> ConnectAndImportAsync(
        IProgress<LiveGameLoadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<LiveGameConnectionResult> ReloadAsync(
        IProgress<LiveGameLoadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<LiveGameSessionStatus> CheckSessionAsync(CancellationToken cancellationToken = default);

    void Disconnect();
}
