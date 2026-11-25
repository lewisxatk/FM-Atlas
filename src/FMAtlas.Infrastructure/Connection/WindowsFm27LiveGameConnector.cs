using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using FMAtlas.Application.Abstractions;
using FMAtlas.Application.Models;
using FMAtlas.Domain.Connection;

namespace FMAtlas.Infrastructure.Connection;

/// <summary>
/// Connects to the exact FM27 27.1.0 Game Pass 0.9.2524.0 package and imports its live player,
/// staff and club lists into the local index. It never writes to the game process.
/// </summary>
public sealed class WindowsFm27LiveGameConnector : ILiveGameConnector
{
    public const string LiveProviderKey = "live-fm27-gamepass-0.9.2524.0";
    private static readonly string[] KnownLiveProviderKeys =
    [
        "live-fm27-gamepass-0.9.2524.0",
        "live-fm27-gamepass-0.9.2562.0"
    ];
    private sealed record LiveSession(
        Process Process,
        WindowsProcessMemoryAccessor Memory,
        FM27GamePassReader Reader,
        int ProcessId,
        string ProviderKey,
        long SaveGeneration,
        string BuildName);

    private sealed record ScanWork(
        LiveSession Session,
        IEnumerable<FMAtlas.Domain.Entities.IEntityRecord> Records,
        int PlayerPointers,
        int StaffPointers,
        int ClubPointers)
    {
        public int ExpectedRecordCount => checked(PlayerPointers + StaffPointers + ClubPointers);
    }

    private readonly IEntityRecordBatchSnapshotStore _snapshotStore;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LiveSession? _session;
    private volatile bool _disconnectRequested;
    private bool _disposed;
    private static readonly object DiagnosticLock = new();

    private static string DiagnosticPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FM Atlas", "Logs", "live-connection.log");

    private static void WriteDiagnostic(string message)
    {
        try
        {
            lock (DiagnosticLock)
            {
                var path = DiagnosticPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never prevent connection cleanup or change FM behaviour.
        }
    }

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
        WriteDiagnostic("Load request started. Read-only mode; no process-write or terminate rights are requested.");
        try
        {
            var previousSession = _session;
            DisconnectCore();
            _disconnectRequested = false;

            // A live provider is a disposable snapshot, never a fallback cache.
            // Clear it before scanning so a failed/cancelled reload cannot leave the
            // previous career visible as if it were still the active save.
            foreach (var providerKey in KnownLiveProviderKeys)
                await _snapshotStore.DeleteProviderAsync(providerKey, CancellationToken.None).ConfigureAwait(false);
            if (previousSession is not null &&
                !string.Equals(previousSession.ProviderKey, LiveProviderKey, StringComparison.Ordinal))
            {
                await _snapshotStore.DeleteProviderAsync(previousSession.ProviderKey, CancellationToken.None).ConfigureAwait(false);
            }

            if (_disconnectRequested)
                throw new OperationCanceledException("FM Atlas was asked to disconnect before the live scan started.");
            progress?.Report(new LiveGameLoadProgress(2, "Finding Football Manager", "Looking for the running FM27 Game Pass process."));

            var work = await Task.Run(
                () => ScanLiveGame(progress, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            candidate = work.Session;
            WriteDiagnostic($"Live read-only session prepared: pointerEntries={work.ExpectedRecordCount}; " +
                $"saveGeneration={work.Session.SaveGeneration}; build={work.Session.BuildName}.");

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new LiveGameLoadProgress(
                16,
                "Reading and indexing the active career",
                $"Streaming up to {work.ExpectedRecordCount:N0} player, staff and club entries in bounded batches."));

            var importStopwatch = Stopwatch.StartNew();
            using var atlasProcess = Process.GetCurrentProcess();
            var workingSetBeforeImport = atlasProcess.WorkingSet64;

            var validationAccumulator = new FM27LiveScanValidator.StreamingAccumulator(work.Session.ProviderKey);
            var validatedRecords = ValidateAsRead(work.Records, validationAccumulator, cancellationToken);
            FM27LiveScanValidationResult? validation = null;
            FM27LiveReadSummary? readSummary = null;

            var saveProgress = new Progress<int>(percent =>
            {
                var safePercent = Math.Clamp(percent, 0, 100);
                var overallPercent = safePercent >= 100
                    ? 99
                    : 16 + Math.Min(78, safePercent * 78 / 100);
                progress?.Report(new LiveGameLoadProgress(
                    overallPercent,
                    safePercent >= 99 ? "Finalising the active career" : "Reading and indexing the active career",
                    $"Read, validated and indexed approximately {safePercent}% of {work.ExpectedRecordCount:N0} career entries"));
            });

            await _snapshotStore.SaveSnapshotsAsync(
                validatedRecords,
                cancellationToken,
                saveProgress,
                replaceProvider: true,
                beforeCommit: async token =>
                {
                    token.ThrowIfCancellationRequested();
                    readSummary = work.Session.Reader.LastReadSummary
                        ?? throw new InvalidDataException("The live reader ended without producing a read summary.");

                    validation = validationAccumulator.Complete(readSummary);
                    WriteDiagnostic($"Streaming validation passed: {validation.Summary}; " +
                        $"memoryReads={work.Session.Memory.ReadOperations}; bytesRead={work.Session.Memory.BytesRead}; " +
                        $"warnings={validation.Warnings.Count}.");
                    WriteDiagnostic($"Pre-commit performance: elapsed={importStopwatch.Elapsed}; " +
                        $"workingSetMB={atlasProcess.WorkingSet64 / (1024d * 1024d):N0}; " +
                        $"workingSetBeforeMB={workingSetBeforeImport / (1024d * 1024d):N0}; " +
                        $"managedHeapMB={GC.GetTotalMemory(forceFullCollection: false) / (1024d * 1024d):N0}.");
                    foreach (var sample in readSummary.FailureSamples)
                        WriteDiagnostic($"Skipped entity sample: {sample}");

                    if (work.Session.Process.HasExited)
                        throw new InvalidOperationException("Football Manager closed while FM Atlas was streaming the career.");

                    var generationAfterRead = work.Session.Reader.ReadSaveGeneration();
                    var emptyAfterRead = work.Session.Reader.IsPlayerArrayEmpty();
                    if (generationAfterRead != work.Session.SaveGeneration || emptyAfterRead)
                    {
                        throw new InvalidOperationException("Football Manager changed or unloaded the career while FM Atlas was reading it. The uncommitted import has been discarded. Load the career fully, then press Load Game again.");
                    }

                    if (_disconnectRequested)
                        throw new OperationCanceledException("FM Atlas was asked to disconnect while loading.");

                    progress?.Report(new LiveGameLoadProgress(
                        99,
                        "Verifying the active career",
                        $"Validated {validation.Players:N0} players, {validation.Staff:N0} staff and {validation.Clubs:N0} clubs before committing the index."));
                    await Task.CompletedTask.ConfigureAwait(false);
                },
                expectedRecordCount: work.ExpectedRecordCount).ConfigureAwait(false);

            if (validation is null || readSummary is null)
                throw new InvalidDataException("The streaming career import completed without final validation.");

            if (_disconnectRequested)
                throw new OperationCanceledException("FM Atlas was asked to disconnect while loading.");

            importStopwatch.Stop();
            WriteDiagnostic($"Live import committed: elapsed={importStopwatch.Elapsed}; " +
                $"workingSetMB={atlasProcess.WorkingSet64 / (1024d * 1024d):N0}; " +
                $"managedHeapMB={GC.GetTotalMemory(forceFullCollection: false) / (1024d * 1024d):N0}; " +
                $"players={validation.Players}; staff={validation.Staff}; clubs={validation.Clubs}.");

            _session = candidate;
            candidate = null;
            progress?.Report(new LiveGameLoadProgress(100, "Live data ready",
                $"Indexed {validation.Players:N0} players, {validation.Staff:N0} staff and {validation.Clubs:N0} clubs."));

            return new LiveGameConnectionResult(
                work.Session.ProcessId,
                work.Session.BuildName,
                work.Session.ProviderKey,
                work.Session.SaveGeneration,
                validation.Players,
                validation.Staff,
                validation.Clubs,
                LiveWritesEnabled: false,
                Message: "Live FM27 data was streamed into the local index and passed pre-commit identity, ability, position and club-link checks. The fields remain marked Mapped, not AtlasReadVerified, until values are compared with your in-game career. No live writes are enabled.");
        }
        catch (Exception loadException)
        {
            WriteDiagnostic($"Load failed: {loadException.GetType().Name}: {loadException.Message}");
            if (candidate is not null)
            {
                try
                {
                    // Cleanup must not inherit a cancelled load token. If this fails,
                    // surface both errors rather than silently leaving stale live rows.
                    await _snapshotStore.DeleteProviderAsync(candidate.ProviderKey, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception cleanupException)
                {
                    DisconnectSession(candidate);
                    throw new AggregateException(
                        "The live load failed and FM Atlas could not remove its temporary live snapshot. Do not use live search until the database has been repaired or the app has restarted successfully.",
                        loadException,
                        cleanupException);
                }

                DisconnectSession(candidate);
            }

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
            WriteDiagnostic($"FM process found: pid={gameProcess.Id}; processName={gameProcess.ProcessName}.");
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

            WriteDiagnostic($"game_plugin.dll fingerprint: machine=0x{fingerprint.Machine:X4}; timestamp=0x{fingerprint.PeTimestamp:X8}; " +
                $"imageSize=0x{fingerprint.SizeOfImage:X8}; fileVersion={fingerprint.FileVersion ?? "unavailable"}.");

            var profile = GameBuildProfileCatalog.MatchReference(fingerprint);
            if (fingerprint.Machine != 0x8664)
                throw new InvalidOperationException($"FM Atlas found a {fingerprint.Architecture} game_plugin.dll. This reader requires the exact x64 Game Pass package profile.");
            if (profile is null)
                throw new InvalidOperationException($"This FM27 Game Pass package is not in the reference catalogue. PE timestamp 0x{fingerprint.PeTimestamp:X8}, image size 0x{fingerprint.SizeOfImage:X8}. No process memory was read.");
            if (!FM27GamePassReader.Matches(fingerprint, profile) || !profile.CanAttemptReadOnlyScan || profile.CanWriteLiveData)
                throw new InvalidOperationException($"FM Atlas recognises {profile.DisplayName}, but has no complete guarded read-only mapping for this exact package. No process memory was read.");

            progress?.Report(new LiveGameLoadProgress(10, "Checking FM27 build", $"Exact Game Pass package {profile.PackageVersion} matched against its external layout. Opening a read-only process handle; FM Atlas field verification is still pending."));

            memory = new WindowsProcessMemoryAccessor(gameProcess.Id);
            var initialGeneration = FM27GamePassReader.ReadSaveGeneration(memory, moduleBase, profile.GameUiRva, profile.GameLoadsOffset);
            if (initialGeneration <= 0)
                throw new InvalidDataException("FM27's save-generation counter is not valid. Make sure a career has finished loading.");

            var providerKey = $"live-fm27-gamepass-{profile.PackageVersion}";
            var reader = new FM27GamePassReader(memory, moduleBase, providerKey, profile);
            var initial = reader.InspectLoadedSave();
            WriteDiagnostic($"Initial save probe: generation={initial.SaveGeneration}; players={initial.Players}; staff={initial.Staff}; clubs={initial.Clubs}; inGameDate={initial.InGameDate:yyyy-MM-dd}.");
            if (initial.SaveGeneration != initialGeneration)
                throw new InvalidOperationException("FM27 changed its loaded save during connection. Press Load Game again after the loading screen finishes.");

            if (initial.Staff == 0 || initial.Clubs == 0)
                throw new InvalidDataException("FM27's player list was found, but the staff or club list is still empty. The career may still be loading; wait and try again.");

            // Pointer-array sizes are only an early structural probe, not verified entity totals.
            // Keep them out of user-facing counts; publish totals only after records are read,
            // validated and successfully imported into the active career snapshot.
            progress?.Report(new LiveGameLoadProgress(16, "Reading the active career",
                "Career data structures detected. Discovering player, staff and club records…"));

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

            var records = reader.EnumerateRecords(
                progress: null,
                cancellationToken: cancellationToken);

            // Enumerate only once, downstream, into the transactional SQLite writer.
            // This prevents a second 92k-record object list from being retained in memory.
            return new ScanWork(
                session,
                records,
                initial.Players,
                initial.Staff,
                initial.Clubs);
        }
        catch
        {
            memory?.Dispose();
            gameProcess?.Dispose();
            throw;
        }
    }

    private static IEnumerable<FMAtlas.Domain.Entities.IEntityRecord> ValidateAsRead(
        IEnumerable<FMAtlas.Domain.Entities.IEntityRecord> records,
        FM27LiveScanValidator.StreamingAccumulator accumulator,
        CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            accumulator.Add(record);
            yield return record;
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
                var cleanupFailure = await InvalidateSessionAsync(
                    session,
                    "Football Manager closed.",
                    CancellationToken.None).ConfigureAwait(false);
                var message = "Football Manager closed. Press Load Game after restarting the game and loading a career.";
                if (cleanupFailure is not null)
                    message += $" Live snapshot cleanup also failed: {cleanupFailure}";
                return new LiveGameSessionStatus(false, true, null, message);
            }

            try
            {
                var generation = await Task.Run(() => session.Reader.ReadSaveGeneration(), cancellationToken).ConfigureAwait(false);
                var empty = await Task.Run(() => session.Reader.IsPlayerArrayEmpty(), cancellationToken).ConfigureAwait(false);
                if (generation != session.SaveGeneration || empty)
                {
                    var cleanupFailure = await InvalidateSessionAsync(
                        session,
                        "FM27 loaded another save or returned to its main menu.",
                        CancellationToken.None).ConfigureAwait(false);
                    var message = "The active FM27 save changed. Press Load Game to read the new career.";
                    if (cleanupFailure is not null)
                        message += $" Live snapshot cleanup also failed: {cleanupFailure}";
                    return new LiveGameSessionStatus(false, true, generation, message);
                }

                if (_disconnectRequested)
                {
                    var cleanupFailure = await InvalidateSessionAsync(
                        session,
                        "FM Atlas is closing the live session.",
                        CancellationToken.None).ConfigureAwait(false);
                    _disconnectRequested = false;
                    var message = "The live FM27 session has been disconnected.";
                    if (cleanupFailure is not null)
                        message += $" Live snapshot cleanup also failed: {cleanupFailure}";
                    return new LiveGameSessionStatus(false, false, null, message);
                }

                return new LiveGameSessionStatus(true, false, generation, "The same FM27 career is still loaded.");
            }
            catch (Exception exception)
            {
                var cleanupFailure = await InvalidateSessionAsync(
                    session,
                    "FM Atlas lost its safe connection to the active game.",
                    CancellationToken.None).ConfigureAwait(false);
                var message = $"The live session could not be verified: {exception.Message}. Press Load Game to reconnect.";
                if (cleanupFailure is not null)
                    message += $" Live snapshot cleanup also failed: {cleanupFailure}";
                return new LiveGameSessionStatus(false, true, null, message);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string?> InvalidateSessionAsync(
        LiveSession session,
        string reason,
        CancellationToken cancellationToken)
    {
        string? cleanupFailure = null;
        try
        {
            await _snapshotStore.DeleteProviderAsync(session.ProviderKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            cleanupFailure = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(_session, session))
                _session = null;
            DisconnectSession(session);
        }

        return cleanupFailure is null
            ? null
            : $"{reason} {cleanupFailure}";
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
        // Do not dispose the semaphore while an in-flight load/check may still
        // execute its finally { _gate.Release(); }. Disconnect marks an active
        // operation for teardown; the short-lived semaphore is left for GC.
    }
}
