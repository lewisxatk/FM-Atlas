using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Desktop.ViewModels;
using FMAtlas.Desktop.Views;

namespace FMAtlas.Desktop;

public partial class MainWindow : Window
{
    private readonly IAdvancedSearchProvider _advancedSearchProvider;
    private readonly IEntityRecordSnapshotStore _snapshotStore;
    private readonly IPlayerExportImporter _playerExportImporter;
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly Dictionary<string, Window> _openPlayerProfiles = new(StringComparer.Ordinal);
    private AdvancedSearchWindow? _advancedSearchWindow;
    private readonly DispatcherTimer _liveSessionMonitorTimer;

    public MainWindow(
        MainWindowViewModel viewModel,
        IAdvancedSearchProvider advancedSearchProvider,
        IEntityRecordSnapshotStore snapshotStore,
        IPlayerExportImporter playerExportImporter)
    {
        InitializeComponent();
        DataContext = viewModel;
        _mainWindowViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _advancedSearchProvider = advancedSearchProvider ?? throw new ArgumentNullException(nameof(advancedSearchProvider));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _playerExportImporter = playerExportImporter ?? throw new ArgumentNullException(nameof(playerExportImporter));

        _liveSessionMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _liveSessionMonitorTimer.Tick += async (_, _) =>
            await _mainWindowViewModel.CheckLiveSessionAsync();
        _liveSessionMonitorTimer.Start();

        Closed += (_, _) =>
        {
            _liveSessionMonitorTimer.Stop();
            _mainWindowViewModel.DisconnectLiveSession();
        };
    }

    private async void ImportPlayerExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import a Football Manager player export",
            Filter = "FM View Exporter CSV (*.csv)|*.csv|CSV files (*.csv)|*.csv",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            Cursor = Cursors.Wait;
            var result = await _playerExportImporter.ImportAsync(dialog.FileName);
            _mainWindowViewModel.NotifyPlayerExportImported(result.ImportedPlayers, result.ProviderKey);

            var message = $"""
                Imported players: {result.ImportedPlayers:N0}
                Rows read: {result.RowsRead:N0}
                Rows skipped: {result.SkippedRows:N0}
                Duplicate IDs: {result.DuplicateIds:N0}
                Additional columns indexed: {result.DynamicFieldsIndexed:N0}

                The imported records are stored locally and can now be found in Quick Search and Advanced Search.
                They are marked as Game Export / Mapped, not live-memory verified. No Football Manager process or career file was changed.
                """;

            if (result.Warnings.Count > 0)
            {
                message += Environment.NewLine + Environment.NewLine +
                    "Import notes:" + Environment.NewLine +
                    string.Join(Environment.NewLine, result.Warnings.Take(6));
                if (result.Warnings.Count > 6)
                    message += Environment.NewLine + $"…and {result.Warnings.Count - 6} more note(s).";
            }

            MessageBox.Show(this, message, "FM Atlas — Import complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                $"The player export could not be imported: {exception.Message}",
                "FM Atlas — Import failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private void AdvancedSearchButton_Click(object sender, RoutedEventArgs e)
    {
        // Advanced Search is a workspace, not a modal dialog: keep the main Atlas
        // window usable while the user builds and reviews a filter query.
        if (_advancedSearchWindow is { } openWindow)
        {
            if (openWindow.WindowState == WindowState.Minimized)
                openWindow.WindowState = WindowState.Normal;

            openWindow.Activate();
            return;
        }

        var searchWindow = new AdvancedSearchWindow(_advancedSearchProvider, () => _mainWindowViewModel.IncludeWomenPlayers)
        {
            Owner = this
        };

        _advancedSearchWindow = searchWindow;
        searchWindow.ResultOpenRequested += OpenSearchResult;
        searchWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_advancedSearchWindow, searchWindow))
                _advancedSearchWindow = null;
        };

        searchWindow.Show();
        searchWindow.Activate();
    }

    private async void OpenSearchResult(EntitySearchResult result)
    {
        if (result.Identity.Kind != EntityKind.Player)
        {
            MessageBox.Show(
                this,
                "The current profile workspace opens Player records. Coach and Club profiles are still in progress.",
                "FM Atlas",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var key = $"{result.Identity.Kind}|{result.Identity.ProviderKey}|{result.Identity.SourceId}";
        if (_openPlayerProfiles.TryGetValue(key, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        try
        {
            var player = await _snapshotStore.GetSnapshotAsync<PlayerRecord>(result.Identity);
            if (player is null)
            {
                MessageBox.Show(
                    this,
                    "This search result exists in the local entity index, but its typed player snapshot is not available. Re-import the source record before opening its full profile.",
                    "Player snapshot unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var profileWindow = new Window
            {
                Title = $"FM Atlas — {player.FullName?.Value ?? result.DisplayName}",
                Owner = this,
                Width = 1480,
                Height = 940,
                MinWidth = 1080,
                MinHeight = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (System.Windows.Media.Brush)FindResource("Atlas.Canvas"),
                Content = new PlayerProfileView
                {
                    DataContext = new PlayerProfileViewModel(player)
                }
            };

            _openPlayerProfiles[key] = profileWindow;
            profileWindow.Closed += (_, _) => _openPlayerProfiles.Remove(key);
            profileWindow.Show();
            profileWindow.Activate();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"The player profile could not be loaded: {exception.Message}",
                "FM Atlas",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
