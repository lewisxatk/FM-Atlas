using System.Diagnostics;
using FMAtlas.Application.Abstractions;
using FMAtlas.Application.Models;
using FMAtlas.Domain.Connection;

namespace FMAtlas.Infrastructure.Connection;

/// <summary>
/// Connects to the exact FM27 Game Pass 0.9.2435.0 package and imports its live player,
/// staff and club lists into the local index. It never writes to the game process.
/// </summary>
public sealed class WindowsFm27LiveGameConnector : ILiveGameConnector
{
    private sealed record LiveSession(
        Process Process,
        WindowsProcessMemoryAccessor Memory,
        FM27GamePass0935Reader Reader,
        int ProcessId,
        string ProviderKey,
        long SaveGeneration,
        string BuildName);

    private sealed record ScanWork(
        LiveSession Session,
        IReadOnlyList<FMAtlas.Domain.Entities.IEntityRecord> Records,
        int Players,
        int Staff,
        int Clubs);

    private readonly IEntityRecordBatchSnapshotStore _snapshotStore;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LiveSession? _session;
    private bool _disconnectRequested;
    private bool _disposed;

    public WindowsFm27LiveGameConnector(IEntityRecordBatchSnapshotStore snapshotStore)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
    }

    public Task<LiveGameConnectionResult> ConnectAndImportAsync(
        IProgress<LiveGameLoadProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        LoadAsync(progress, cancellationToken);

    public Task<LiveGameConnectionResult> ReloadAsync(
        IProgress<LiveGameLoadProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        LoadAsync(progress, cancellationToken);

    private async Task<LiveGameConnectionResult> LoadAsync(
        IProgress<LiveGameLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        LiveSession? candidate = null;
        try
        {
            DisconnectCore();
            _disconnectRequested = false;
            progress?.Report(new LiveGameLoadProgress(2, "Finding Football Manager", "Looking for the running FM27 Game Pass process."));

            var work = await Task.Run(
                () => ScanLiveGame(progress, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            candidate = work.Session;

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new LiveGameLoadProgress(96, "Building the search index",
                $"Saving {work.Records.Count:N0} player, staff and club records in one local database transaction."));

            await _snapshotStore.SaveSnapshotsAsync(work.Records, cancellationToken).ConfigureAwait(false);

            // The game could load another save while SQLite was being populated. Do not leave those rows searchable.
            var generationAfterImport = await Task.Run(
                () => candidate.Reader.ReadSaveGeneration(),
                cancellationToken).ConfigureAwait(false);
            var emptyAfterImport = await Task.Run(
                () => candidate.Reader.IsPlayerArrayEmpty(),
                cancellationToken).ConfigureAwait(false);
            if (generationAfterImport != candidate.SaveGeneration || emptyAfterImport)
            {
                await _snapshotStore.DeleteProviderAsync(candidate.ProviderKey, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("Football Manager changed or unloaded the career while FM Atlas was importing it. Nothing from that partial session has been kept. Load the career fully, then press Load Game again.");
            }

            if (_disconnectRequested)
            {
                await _snapshotStore.DeleteProviderAsync(candidate.ProviderKey, cancellationToken).ConfigureAwait(false);
                DisconnectSession(candidate);
                throw new OperationCanceledException("FM Atlas was asked to disconnect while loading.");
            }

            _session = candidate;
            candidate = null;
            progress?.Report(new LiveGameLoadProgress(100, "Live data ready",
                $"Indexed {work.Players:N0} players, {work.Staff:N0} staff and {work.Clubs:N0} clubs."));

            return new LiveGameConnectionResult(
                work.Session.ProcessId,
                work.Session.BuildName,
                work.Session.ProviderKey,
                work.Session.SaveGeneration,
                work.Players,
                work.Staff,
                work.Clubs,
                LiveWritesEnabled: false,
                Message: "Live FM27 data has been read and indexed. The current build is read-only in FM Atlas until our own field-by-field write verification is complete.");
        }
        catch
        {
            if (candidate is not null)
                DisconnectSession(candidate);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ScanWork ScanLiveGame(
        IProgress<LiveGameLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
            throw new PlatformNotSupportedException("Live FM27 connection requires the 64-bit Windows build of FM Atlas.");

        var processes = Process.GetProcessesByName("fm");
        if (processes.Length == 0)
            throw new InvalidOperationException("Football Manager was not found. Start FM27, load a career, and press Load Game again.");
        if (processes.Length > 1)
        {
            var processIds = string.Join(", ", processes.Select(process => process.Id));
            foreach (var process in processes)
                process.Dispose();
            throw new InvalidOperationException($"More than one Football Manager process is running (PIDs {processIds}). Close the extra copy and try again.");
        }

        var gameProcess = processes[0];
        WindowsProcessMemoryAccessor? memory = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessModule? plugin = null;
            try
            {
                plugin = gameProcess.Modules.Cast<ProcessModule>().FirstOrDefault(
                    module => string.Equals(module.ModuleName, "game_plugin.dll", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                throw new InvalidOperationException($"FM Atlas found FM (PID {gameProcess.Id}) but could not inspect its game_plugin.dll module: {exception.Message}", exception);
            }

            if (plugin is null)
                throw new InvalidOperationException("FM27 is still starting or game_plugin.dll is not loaded. Wait for the game to reach its main menu, load your career, then try again.");

            var modulePath = plugin.FileName;
            var moduleBase = unchecked((ulong)plugin.BaseAddress.ToInt64());
            WindowsProcessMemoryAccessor.ValidateUserPointer(moduleBase, "game_plugin.dll base address");
            var fingerprint = PortableExecutableFingerprintReader.TryRead(modulePath);
            if (fingerprint is null)
                throw new InvalidOperationException("FM Atlas found game_plugin.dll but could not read its on-disk PE fingerprint. The Game Pass package cannot be confirmed, so no live data was read.");

            if (!FM27GamePass0935Reader.Matches(fingerprint))
            {
                var known = GameBuildProfileCatalog.MatchReference(fingerprint);
                var description = known is null ? "This package is not in the current reference catalogue." : $"Detected {known.DisplayName}, but this scanner is pinned to FM27 Game Pass 0.9.2435.0.";
                throw new InvalidOperationException($"{description} PE timestamp 0x{fingerprint.PeTimestamp:X8}, image size 0x{fingerprint.SizeOfImage:X8}. Update FM Atlas's build profile before reading this package.");
            }

            var profile = GameBuildProfileCatalog.MatchReference(fingerprint);
            if (profile is null || profile.EvidenceStatus != GameBuildEvidenceStatus.ExternalReferenceOnly)
                throw new InvalidOperationException("The Game Pass fingerprint matched, but its evidence status was unexpected. FM Atlas has stopped rather than guessing.");

            progress?.Report(new LiveGameLoadProgress(10, "Checking FM27 build", "Exact Game Pass package 0.9.2435.0 matched. Opening a read-only process handle."));

            memory = new WindowsProcessMemoryAccessor(gameProcess.Id);
            var initialGeneration = FM27GamePass0935Reader.ReadSaveGeneration(memory, moduleBase);
            if (initialGeneration <= 0)
                throw new InvalidDataException("FM27's save-generation counter is not valid. Make sure a career has finished loading.");

            var providerKey = $"live-fm27-gamepass-{FM27GamePass0935Reader.BuildVersionForProvider}:{gameProcess.Id}:{initialGeneration}:{Guid.NewGuid():N}";
            var reader = new FM27GamePass0935Reader(memory, moduleBase, providerKey);
            var initial = reader.InspectLoadedSave();
            if (initial.SaveGeneration != initialGeneration)
                throw new InvalidOperationException("FM27 changed its loaded save during connection. Press Load Game again after the loading screen finishes.");

            if (initial.Staff == 0 || initial.Clubs == 0)
                throw new InvalidDataException("FM27's player list was found, but the staff or club list is still empty. The career may still be loading; wait and try again.");

            progress?.Report(new LiveGameLoadProgress(16, "Reading the active career",
                $"Found {initial.Players:N0} player pointers, {initial.Staff:N0} staff pointers and {initial.Clubs:N0} club pointers."));

            var session = new LiveSession(
                gameProcess,
                memory,
                reader,
                gameProcess.Id,
                providerKey,
                initial.SaveGeneration,
                profile.DisplayName);
            memory = null; // Ownership moves to LiveSession, which remains open for save-generation monitoring.
            gameProcess = null!;

            try
            {
                var records = reader.ReadAllRecords(progress, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (session.Process.HasExited)
                    throw new InvalidOperationException("Football Manager closed while FM Atlas was reading the career.");

                var finalGeneration = reader.ReadSaveGeneration();
                if (finalGeneration != session.SaveGeneration || reader.IsPlayerArrayEmpty())
                    throw new InvalidOperationException("The loaded career changed while FM Atlas was reading it. The imported data has been discarded.");

                var playerCount = records.Count(record => record.Identity.Kind == FMAtlas.Domain.Entities.EntityKind.Player);
                var staffCount = records.Count(record => record.Identity.Kind == FMAtlas.Domain.Entities.EntityKind.Staff);
                var clubCount = records.Count(record => record.Identity.Kind == FMAtlas.Domain.Entities.EntityKind.Club);
                if (playerCount == 0 || staffCount == 0 || clubCount == 0)
                    throw new InvalidDataException($"The scan was incomplete (players {playerCount:N0}, staff {staffCount:N0}, clubs {clubCount:N0}). FM Atlas has discarded the scan rather than indexing partial results.");

                return new ScanWork(session, records, playerCount, staffCount, clubCount);
            }
            catch
            {
                DisconnectSession(session);
                throw;
            }
        }
        catch
        {
            memory?.Dispose();
            gameProcess?.Dispose();
            throw;
        }
    }

    public async Task<LiveGameSessionStatus> CheckSessionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = _session;
            if (session is null)
                return new LiveGameSessionStatus(false, false, null, "No live FM27 session is loaded.");

            if (session.Process.HasExited)
            {
                await InvalidateSessionAsync(session, "Football Manager closed. Live records have been removed from the active search index.", cancellationToken).ConfigureAwait(false);
                return new LiveGameSessionStatus(false, true, null, "Football Manager closed. Press Load Game after restarting the game and loading a career.");
            }

            try
            {
                var generation = await Task.Run(() => session.Reader.ReadSaveGeneration(), cancellationToken).ConfigureAwait(false);
                var empty = await Task.Run(() => session.Reader.IsPlayerArrayEmpty(), cancellationToken).ConfigureAwait(false);
                if (generation != session.SaveGeneration || empty)
                {
                    await InvalidateSessionAsync(session, "FM27 loaded another save or returned to its main menu. Old live data has been removed from search.", cancellationToken).ConfigureAwait(false);
                    return new LiveGameSessionStatus(false, true, generation, "The active FM27 save changed. Press Load Game to read the new career.");
                }

                return new LiveGameSessionStatus(true, false, generation, "The same FM27 career is still loaded.");
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or InvalidDataException)
            {
                await InvalidateSessionAsync(session, "FM Atlas lost its safe connection to the active game. Live data has been removed from search.", cancellationToken).ConfigureAwait(false);
                return new LiveGameSessionStatus(false, true, null, $"The live session could not be verified: {exception.Message}. Press Load Game to reconnect.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task InvalidateSessionAsync(
        LiveSession session,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await _snapshotStore.DeleteProviderAsync(session.ProviderKey, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(_session, session))
                _session = null;
            DisconnectSession(session);
        }
        _ = reason;
    }

    public void Disconnect()
    {
        if (_disposed)
            return;
        if (_gate.Wait(0))
        {
            try
            {
                DisconnectCore();
            }
            finally
            {
                _gate.Release();
            }
        }
        else
        {
            _disconnectRequested = true;
        }
    }

    private void DisconnectCore()
    {
        var session = _session;
        _session = null;
        if (session is not null)
            DisconnectSession(session);
    }

    private static void DisconnectSession(LiveSession session)
    {
        session.Memory.Dispose();
        session.Process.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Disconnect();
        _disposed = true;
        _gate.Dispose();
    }
}
