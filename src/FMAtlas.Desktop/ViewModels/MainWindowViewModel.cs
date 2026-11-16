using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FMAtlas.Application.Abstractions;
using FMAtlas.Application.Models;
using FMAtlas.Domain.Connection;
using FMAtlas.Domain.Entities;
using FMAtlas.Desktop.Commands;

namespace FMAtlas.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IGameProcessProbe _processProbe;
    private readonly IAtlasDatabase _atlasDatabase;
    private readonly IEntitySearchProvider _entitySearchProvider;
    private readonly ILiveGameConnector _liveGameConnector;
    private readonly string _databasePath;
    private readonly string _searchPreferencesPath;
    private CancellationTokenSource? _quickSearchCancellation;
    private string _connectionStatus = "Not connected";
    private string _connectionDetail = "Detect Game checks for the FM process and game module only. Loading a career is still In Progress.";
    private string _lastAction = "Fresh application foundation";
    private string _quickSearchText = string.Empty;
    private string _quickSearchMessage = "Search imported FM Atlas records.";
    private bool _isQuickSearchOpen;
    private bool _isQuickSearchBusy;
    private bool _isBusy;
    private bool _includeWomenPlayers;
    private string _lastImportCountText = "—";
    private string _lastImportStatusText = "No player export imported";
    private bool _isLiveLoadOverlayVisible;
    private int _liveLoadProgress;
    private string _liveLoadStage = "CONNECTING TO FOOTBALL MANAGER";
    private string _liveLoadDetail = "Checking the running game and active career.";
    private bool _liveSessionActive;
    private bool _sessionCheckRunning;
    private int _livePlayerCount;
    private int _liveStaffCount;
    private int _liveClubCount;
    private bool _liveWritesEnabled;

    public MainWindowViewModel(
        IGameProcessProbe processProbe,
        IAtlasDatabase atlasDatabase,
        IEntitySearchProvider entitySearchProvider,
        ILiveGameConnector liveGameConnector,
        string databasePath)
    {
        _processProbe = processProbe ?? throw new ArgumentNullException(nameof(processProbe));
        _atlasDatabase = atlasDatabase ?? throw new ArgumentNullException(nameof(atlasDatabase));
        _entitySearchProvider = entitySearchProvider ?? throw new ArgumentNullException(nameof(entitySearchProvider));
        _liveGameConnector = liveGameConnector ?? throw new ArgumentNullException(nameof(liveGameConnector));
        _databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? throw new ArgumentException("A local database path is required.", nameof(databasePath))
            : Path.GetFullPath(databasePath);
        _searchPreferencesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FM Atlas", "Settings", "include-women-players.txt");
        _includeWomenPlayers = LoadIncludeWomenPlayersPreference();

        LoadGameCommand = new AsyncRelayCommand(LoadLiveGameAsync, () => !IsBusy);
        SaveAtlasCommand = new AsyncRelayCommand(SaveAtlasAsync, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand LoadGameCommand { get; }
    public ICommand SaveAtlasCommand { get; }

    public ObservableCollection<QuickSearchResultViewModel> QuickSearchResults { get; } = [];

    /// <summary>
    /// Global player-search preference. Off excludes records explicitly identified
    /// as female; records with unknown gender remain visible until their source is verified.
    /// </summary>
    public bool IncludeWomenPlayers
    {
        get => _includeWomenPlayers;
        set
        {
            if (!SetField(ref _includeWomenPlayers, value))
                return;

            SaveIncludeWomenPlayersPreference();
            ScheduleQuickSearch();
        }
    }

    public string QuickSearchText
    {
        get => _quickSearchText;
        set
        {
            if (SetField(ref _quickSearchText, value))
                ScheduleQuickSearch();
        }
    }

    public string QuickSearchMessage
    {
        get => _quickSearchMessage;
        private set => SetField(ref _quickSearchMessage, value);
    }

    public bool IsQuickSearchOpen
    {
        get => _isQuickSearchOpen;
        private set => SetField(ref _isQuickSearchOpen, value);
    }

    public bool IsQuickSearchBusy
    {
        get => _isQuickSearchBusy;
        private set => SetField(ref _isQuickSearchBusy, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetField(ref _connectionStatus, value);
    }

    public string ConnectionDetail
    {
        get => _connectionDetail;
        private set => SetField(ref _connectionDetail, value);
    }

    public string LastAction
    {
        get => _lastAction;
        private set => SetField(ref _lastAction, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public string LastImportCountText
    {
        get => _lastImportCountText;
        private set => SetField(ref _lastImportCountText, value);
    }

    public string LastImportStatusText
    {
        get => _lastImportStatusText;
        private set => SetField(ref _lastImportStatusText, value);
    }

    public bool IsLiveLoadOverlayVisible
    {
        get => _isLiveLoadOverlayVisible;
        private set => SetField(ref _isLiveLoadOverlayVisible, value);
    }

    public int LiveLoadProgress
    {
        get => _liveLoadProgress;
        private set => SetField(ref _liveLoadProgress, Math.Clamp(value, 0, 100));
    }

    public string LiveLoadStage
    {
        get => _liveLoadStage;
        private set => SetField(ref _liveLoadStage, value);
    }

    public string LiveLoadDetail
    {
        get => _liveLoadDetail;
        private set => SetField(ref _liveLoadDetail, value);
    }

    public bool LiveSessionActive
    {
        get => _liveSessionActive;
        private set => SetField(ref _liveSessionActive, value);
    }

    public int LivePlayerCount
    {
        get => _livePlayerCount;
        private set => SetField(ref _livePlayerCount, value);
    }

    public int LiveStaffCount
    {
        get => _liveStaffCount;
        private set => SetField(ref _liveStaffCount, value);
    }

    public int LiveClubCount
    {
        get => _liveClubCount;
        private set => SetField(ref _liveClubCount, value);
    }

    public bool LiveWritesEnabled
    {
        get => _liveWritesEnabled;
        private set => SetField(ref _liveWritesEnabled, value);
    }

    public async Task CheckLiveSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!LiveSessionActive || _sessionCheckRunning || IsBusy)
            return;

        _sessionCheckRunning = true;
        try
        {
            var status = await _liveGameConnector.CheckSessionAsync(cancellationToken);
            if (!status.IsConnected)
            {
                LiveSessionActive = false;
                LiveWritesEnabled = false;
                ConnectionStatus = "Career changed — reload";
                ConnectionDetail = status.Message;
                LastAction = "Live career changed; stale live records invalidated";
                QuickSearchResults.Clear();
                QuickSearchMessage = "The active career changed. Press Load Game to read the new save before searching live data.";
                IsQuickSearchOpen = !string.IsNullOrWhiteSpace(QuickSearchText);
                LivePlayerCount = 0;
                LiveStaffCount = 0;
                LiveClubCount = 0;
                LastImportCountText = "—";
                LastImportStatusText = "Live session needs reloading";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LiveSessionActive = false;
            LiveWritesEnabled = false;
            ConnectionStatus = "Live session unavailable";
            ConnectionDetail = exception.Message;
            LastAction = "Live session monitor stopped";
        }
        finally
        {
            _sessionCheckRunning = false;
        }
    }

    public void DisconnectLiveSession()
    {
        _liveGameConnector.Disconnect();
        LiveSessionActive = false;
        LiveWritesEnabled = false;
    }

    public void NotifyPlayerExportImported(int importedPlayers, string providerKey)
    {
        var dataset = providerKey.Length > 8 ? providerKey[^8..] : providerKey;
        LastImportCountText = importedPlayers.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        LastImportStatusText = importedPlayers switch
        {
            0 => $"No players imported from latest CSV · {dataset}",
            1 => "1 player imported from latest CSV",
            _ => $"{importedPlayers:N0} players imported from latest CSV · {dataset}"
        };
        LastAction = $"Imported {importedPlayers:N0} FM export player(s) · dataset {dataset}";
        ScheduleQuickSearch();
    }

    public void CloseQuickSearch()
    {
        IsQuickSearchOpen = false;
    }

    private bool LoadIncludeWomenPlayersPreference()
    {
        try
        {
            return File.Exists(_searchPreferencesPath) &&
                   bool.TryParse(File.ReadAllText(_searchPreferencesPath).Trim(), out var enabled) &&
                   enabled;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void SaveIncludeWomenPlayersPreference()
    {
        try
        {
            var directory = Path.GetDirectoryName(_searchPreferencesPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(_searchPreferencesPath, IncludeWomenPlayers ? "true" : "false");
        }
        catch (IOException)
        {
            // Search remains usable for this session if preferences cannot be saved.
        }
        catch (UnauthorizedAccessException)
        {
            // Search remains usable for this session if preferences cannot be saved.
        }
    }

    private void ScheduleQuickSearch()
    {
        var text = QuickSearchText.Trim();
        if (text.Length < 2)
        {
            var pending = Interlocked.Exchange(ref _quickSearchCancellation, null);
            pending?.Cancel();
            pending?.Dispose();

            QuickSearchResults.Clear();
            IsQuickSearchBusy = false;
            IsQuickSearchOpen = !string.IsNullOrWhiteSpace(text);
            QuickSearchMessage = string.IsNullOrWhiteSpace(text)
                ? "Search imported FM Atlas records."
                : "Type at least two characters.";
            return;
        }

        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _quickSearchCancellation, cancellation);
        previous?.Cancel();
        previous?.Dispose();

        _ = RunDebouncedQuickSearchAsync(text, cancellation);
    }

    private async Task RunDebouncedQuickSearchAsync(string searchText, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        IsQuickSearchBusy = true;
        IsQuickSearchOpen = true;
        QuickSearchMessage = "Searching imported records…";

        try
        {
            await Task.Delay(250, token);
            await _atlasDatabase.InitializeAsync(token);

            var results = await _entitySearchProvider.QuickSearchAsync(
                searchText,
                entityKinds: null,
                limit: 30,
                cancellationToken: token,
                includeWomenPlayers: IncludeWomenPlayers);

            token.ThrowIfCancellationRequested();

            QuickSearchResults.Clear();
            foreach (var result in results)
                QuickSearchResults.Add(new QuickSearchResultViewModel(result));

            QuickSearchMessage = results.Count switch
            {
                0 => "No matching imported records. Load or import a career before searching.",
                1 => "1 matching imported record.",
                _ => $"{results.Count} matching imported records."
            };
            IsQuickSearchOpen = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer keystroke superseded this search.
        }
        catch (Exception ex)
        {
            QuickSearchResults.Clear();
            QuickSearchMessage = $"Quick Search could not run: {ex.Message}";
            IsQuickSearchOpen = true;
        }
        finally
        {
            if (ReferenceEquals(Volatile.Read(ref _quickSearchCancellation), cancellation))
            {
                IsQuickSearchBusy = false;
                Interlocked.CompareExchange(ref _quickSearchCancellation, null, cancellation);
                cancellation.Dispose();
            }
        }
    }

    private async Task LoadLiveGameAsync()
    {
        var refreshSearchAfterLoad = false;
        IsBusy = true;
        LiveSessionActive = false;
        LiveWritesEnabled = false;
        LivePlayerCount = 0;
        LiveStaffCount = 0;
        LiveClubCount = 0;
        LastImportCountText = "—";
        LastImportStatusText = "Live session loading";
        QuickSearchResults.Clear();
        QuickSearchMessage = "Loading the active FM27 career. Search is paused until the snapshot is verified.";
        IsQuickSearchOpen = false;
        IsLiveLoadOverlayVisible = true;
        LiveLoadProgress = 0;
        LiveLoadStage = "CONNECTING TO FOOTBALL MANAGER";
        LiveLoadDetail = "Finding FM27 and checking its exact game package.";
        ConnectionStatus = "Connecting to FM27";
        ConnectionDetail = "FM Atlas is opening a read-only connection and checking the active career.";
        LastAction = "Loading the active Football Manager career…";

        var progress = new Progress<LiveGameLoadProgress>(update =>
        {
            LiveLoadProgress = update.Percent;
            LiveLoadStage = update.Stage.ToUpperInvariant();
            LiveLoadDetail = update.Detail;
        });

        try
        {
            var result = await _liveGameConnector.ConnectAndImportAsync(progress);
            LiveSessionActive = true;
            LiveWritesEnabled = result.LiveWritesEnabled;
            LivePlayerCount = result.PlayersImported;
            LiveStaffCount = result.StaffImported;
            LiveClubCount = result.ClubsImported;
            ConnectionStatus = $"FM27 connected · PID {result.ProcessId}";
            ConnectionDetail = $"{result.BuildName}. {result.PlayersImported:N0} players, {result.StaffImported:N0} staff and {result.ClubsImported:N0} clubs loaded from the active career. Live writing remains disabled pending FM Atlas verification.";
            LastAction = $"Loaded {result.PlayersImported:N0} players, {result.StaffImported:N0} staff and {result.ClubsImported:N0} clubs from FM27.";
            LastImportCountText = result.PlayersImported.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
            LastImportStatusText = "Live FM27 player records loaded";
            QuickSearchResults.Clear();
            QuickSearchMessage = "Live FM27 career loaded. Search players, staff and clubs.";
            refreshSearchAfterLoad = QuickSearchText.Trim().Length >= 2;
        }
        catch (OperationCanceledException)
        {
            LiveSessionActive = false;
            LiveWritesEnabled = false;
            LivePlayerCount = 0;
            LiveStaffCount = 0;
            LiveClubCount = 0;
            LastImportCountText = "—";
            LastImportStatusText = "No active live snapshot";
            QuickSearchResults.Clear();
            ConnectionStatus = "Load cancelled";
            ConnectionDetail = "FM Atlas cancelled the live load. No partial live dataset was kept.";
            LastAction = "Live career load cancelled";
        }
        catch (Exception exception)
        {
            LiveSessionActive = false;
            LiveWritesEnabled = false;
            LivePlayerCount = 0;
            LiveStaffCount = 0;
            LiveClubCount = 0;
            LastImportCountText = "—";
            LastImportStatusText = "No active live snapshot";
            QuickSearchResults.Clear();
            ConnectionStatus = "Not connected";
            ConnectionDetail = exception.Message;
            LastAction = "Live career load failed";
            QuickSearchResults.Clear();
            QuickSearchMessage = "Live connection failed. Check the FM27 version and active career, then try Load Game again.";
        }
        finally
        {
            IsLiveLoadOverlayVisible = false;
            IsBusy = false;
            if (refreshSearchAfterLoad)
                ScheduleQuickSearch();
        }
    }

    private async Task SaveAtlasAsync()
    {
        IsBusy = true;
        LastAction = "Saving the local FM Atlas database…";

        try
        {
            await _atlasDatabase.InitializeAsync();
            var version = await _atlasDatabase.GetSchemaVersionAsync();

            ConnectionDetail = $"Local Atlas database is ready at {_databasePath}. Schema version {version}. No live career file has been written.";
            LastAction = "Local Atlas database saved and verified";
        }
        catch (Exception ex)
        {
            ConnectionDetail = $"The local Atlas database could not be saved: {ex.Message}";
            LastAction = "Local Atlas save failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ProbeGameAsync()
    {
        IsBusy = true;
        ConnectionStatus = "Checking game process";
        ConnectionDetail = "Checking for Football Manager and its game module. No memory values will be read or changed in this step.";
        LastAction = "Checking game process…";

        try
        {
            var result = await _processProbe.ProbeAsync();

            switch (result.State)
            {
                case GameProcessProbeState.UnsupportedPlatform:
                    ConnectionStatus = "Windows required";
                    ConnectionDetail = "Live Football Manager process detection is available in the Windows desktop build.";
                    LastAction = "Process check unavailable on this platform";
                    break;

                case GameProcessProbeState.NotFound:
                    ConnectionStatus = "Not detected";
                    ConnectionDetail = "No FM process was found. Start Football Manager 27 and load a career, then try again.";
                    LastAction = "No Football Manager process found";
                    break;

                case GameProcessProbeState.ProcessFoundModuleUnconfirmed:
                    ConnectionStatus = "Process found";
                    ConnectionDetail = "An FM process was found, but game_plugin.dll could not be confirmed. Build verification and career loading remain In Progress.";
                    LastAction = "Process detected; module not confirmed";
                    break;

                case GameProcessProbeState.ProcessAndModuleFound:
                    var candidate = result.Candidates.First(x => x.HasGamePluginModule);
                    ConnectionStatus = "Game module detected";
                    var buildReference = candidate.ModuleFingerprint is null
                        ? null
                        : GameBuildProfileCatalog.MatchReference(candidate.ModuleFingerprint);
                    ConnectionDetail = candidate.ModuleFingerprint is null
                        ? $"FM process {candidate.ProcessId} and game_plugin.dll were detected, but the static module fingerprint could not be read. Exact build support, career identity and live data import remain In Progress. No player data has been read."
                        : buildReference is null
                            ? $"FM process {candidate.ProcessId} and game_plugin.dll were detected. Static module metadata: {candidate.ModuleFingerprint.Summary}. No known reference profile matches; no live data has been read or written."
                            : $"FM process {candidate.ProcessId} matches the external reference fingerprint for {buildReference.DisplayName}. {candidate.ModuleFingerprint.Summary}. This is reference-only evidence, not an FM Atlas verified build. Live reads and writes remain disabled.";
                    LastAction = $"Detected FM process {candidate.ProcessId}; build fingerprint checked, live editing disabled";
                    break;
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Check failed";
            ConnectionDetail = $"The process check failed: {ex.Message}";
            LastAction = "Process check failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class QuickSearchResultViewModel
{
    public QuickSearchResultViewModel(EntitySearchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        DisplayName = result.DisplayName;
        EntityType = result.Identity.Kind switch
        {
            EntityKind.Player => "PLAYER",
            EntityKind.Staff => "COACH",
            EntityKind.Club => "CLUB",
            EntityKind.Nation => "NATION",
            EntityKind.Competition => "COMPETITION",
            _ => "RECORD"
        };
        SourceId = result.Identity.SourceId;
        ProviderKey = result.Identity.ProviderKey;
        IsPlayer = result.Identity.Kind == EntityKind.Player;
    }

    public string DisplayName { get; }
    public string EntityType { get; }
    public string SourceId { get; }
    public string ProviderKey { get; }
    public bool IsPlayer { get; }
}
