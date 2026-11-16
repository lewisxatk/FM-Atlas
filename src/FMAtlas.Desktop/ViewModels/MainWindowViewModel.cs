using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Connection;
using FMAtlas.Domain.Entities;
using FMAtlas.Desktop.Commands;

namespace FMAtlas.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IGameProcessProbe _processProbe;
    private readonly IAtlasDatabase _atlasDatabase;
    private readonly IEntitySearchProvider _entitySearchProvider;
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

    public MainWindowViewModel(
        IGameProcessProbe processProbe,
        IAtlasDatabase atlasDatabase,
        IEntitySearchProvider entitySearchProvider,
        string databasePath)
    {
        _processProbe = processProbe ?? throw new ArgumentNullException(nameof(processProbe));
        _atlasDatabase = atlasDatabase ?? throw new ArgumentNullException(nameof(atlasDatabase));
        _entitySearchProvider = entitySearchProvider ?? throw new ArgumentNullException(nameof(entitySearchProvider));
        _databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? throw new ArgumentException("A local database path is required.", nameof(databasePath))
            : Path.GetFullPath(databasePath);
        _searchPreferencesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FM Atlas", "Settings", "include-women-players.txt");
        _includeWomenPlayers = LoadIncludeWomenPlayersPreference();

        LoadGameCommand = new AsyncRelayCommand(ProbeGameAsync, () => !IsBusy);
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
        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _quickSearchCancellation, cancellation);
        previous?.Cancel();
        previous?.Dispose();

        var text = QuickSearchText.Trim();
        if (text.Length < 2)
        {
            QuickSearchResults.Clear();
            IsQuickSearchBusy = false;
            IsQuickSearchOpen = !string.IsNullOrWhiteSpace(text);
            QuickSearchMessage = string.IsNullOrWhiteSpace(text)
                ? "Search imported FM Atlas records."
                : "Type at least two characters.";
            return;
        }

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
