using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Staff;
using FMAtlas.Domain.Clubs;
using FMAtlas.Desktop.ViewModels;
using FMAtlas.Desktop.Views;

namespace FMAtlas.Desktop;

public partial class MainWindow : Window
{
    private readonly IAdvancedSearchProvider _advancedSearchProvider;
    private readonly IEntityRecordSnapshotStore _snapshotStore;
    private readonly IPlayerExportImporter _playerExportImporter;
    private readonly IEntitySearchProvider _entitySearchProvider;
    private readonly IGraphicsLocator _graphicsLocator;
    private readonly Task? _graphicsIndexTask;
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly Dictionary<string, TabItem> _openWorkspaceTabs = new(StringComparer.Ordinal);
    private AdvancedSearchWindow? _advancedSearchWindow;
    private readonly DispatcherTimer _liveSessionMonitorTimer;

    public MainWindow(
        MainWindowViewModel viewModel,
        IAdvancedSearchProvider advancedSearchProvider,
        IEntityRecordSnapshotStore snapshotStore,
        IPlayerExportImporter playerExportImporter,
        IEntitySearchProvider entitySearchProvider,
        IGraphicsLocator graphicsLocator,
        Task? graphicsIndexTask = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _mainWindowViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _mainWindowViewModel.LiveDataInvalidated += (_, _) =>
        {
            var staleKeys = _openWorkspaceTabs.Keys
                .Where(key => key.StartsWith("directory|", StringComparison.Ordinal) ||
                              key.Contains("|live-fm27-gamepass-", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            foreach (var key in staleKeys)
                CloseWorkspaceTab(key);
        };

        _mainWindowViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(MainWindowViewModel.IsLiveLoadOverlayVisible))
                return;

            var isLoading = _mainWindowViewModel.IsLiveLoadOverlayVisible;

            // Advanced Search is a separate modal window. The in-window overlay
            // also blocks workspace tabs while a live snapshot is being replaced.
            foreach (Window ownedWindow in OwnedWindows)
                ownedWindow.IsEnabled = !isLoading;
            WorkspaceTabControl.IsEnabled = !isLoading;

            if (isLoading)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    Keyboard.ClearFocus();
                    LiveLoadOverlay.Focus();
                }, DispatcherPriority.Input);
            }
        };
        _advancedSearchProvider = advancedSearchProvider ?? throw new ArgumentNullException(nameof(advancedSearchProvider));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _playerExportImporter = playerExportImporter ?? throw new ArgumentNullException(nameof(playerExportImporter));
        _entitySearchProvider = entitySearchProvider ?? throw new ArgumentNullException(nameof(entitySearchProvider));
        _graphicsLocator = graphicsLocator ?? throw new ArgumentNullException(nameof(graphicsLocator));
        _graphicsIndexTask = graphicsIndexTask;

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
        // REF-002 specifies Advanced Search as a centred modal dialog over the
        // current workspace. Its own search state remains local until the user runs it.
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

        searchWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        searchWindow.ShowDialog();
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string destination })
            return;

        if (destination == "Dashboard")
        {
            WorkspaceTabControl.SelectedIndex = 0;
            return;
        }

        if (destination is "Players" or "Staff" or "Clubs")
        {
            var kind = destination switch
            {
                "Players" => EntityKind.Player,
                "Staff" => EntityKind.Staff,
                _ => EntityKind.Club
            };
            var key = $"directory|{kind}";
            if (_openWorkspaceTabs.TryGetValue(key, out var existingDirectory))
            {
                WorkspaceTabControl.SelectedItem = existingDirectory;
                return;
            }

            var browser = new EntityBrowserView
            {
                DataContext = new EntityBrowserViewModel(
                    _entitySearchProvider,
                    kind,
                    () => _mainWindowViewModel.IncludeWomenPlayers)
            };
            browser.ResultOpenRequested += (_, result) => OpenSearchResult(result);
            AddWorkspaceTab(destination, key, browser);
            return;
        }

        if (destination == "Save Manager")
        {
            OpenInformationTab("Save Manager",
                "The live career is currently loaded as a read-only snapshot. Local Atlas storage is separate from the Football Manager career file. Backup, reload and save-file management actions will be enabled only after their data paths are implemented and verified.");
            return;
        }

        if (destination == "Settings")
        {
            OpenInformationTab("Settings",
                "The global Include women preference is saved locally. Additional application, data-source and appearance settings are still being implemented.");
        }
    }

    private void OpenInformationTab(string title, string message)
    {
        var key = $"workspace|{title}";
        if (_openWorkspaceTabs.TryGetValue(key, out var existing))
        {
            WorkspaceTabControl.SelectedItem = existing;
            return;
        }

        var panel = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("Atlas.Surface"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("Atlas.Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(24),
            Margin = new Thickness(26)
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 9, Foreground = (System.Windows.Media.Brush)FindResource("Atlas.Accent") });
        stack.Children.Add(new TextBlock { Text = title, FontFamily = new System.Windows.Media.FontFamily("Georgia"), FontSize = 28, Foreground = (System.Windows.Media.Brush)FindResource("Atlas.Text"), Margin = new Thickness(0, 7, 0, 12) });
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = (System.Windows.Media.Brush)FindResource("Atlas.Muted"), MaxWidth = 760 });
        panel.Child = stack;
        AddWorkspaceTab(title, key, panel);
    }

    private void AddWorkspaceTab(string title, string key, object content)
    {
        if (_openWorkspaceTabs.TryGetValue(key, out var existing))
        {
            WorkspaceTabControl.SelectedItem = existing;
            return;
        }

        var tab = new TabItem
        {
            Tag = key,
            Content = content,
            Header = CreateWorkspaceTabHeader(title, key)
        };
        _openWorkspaceTabs[key] = tab;
        WorkspaceTabControl.Items.Add(tab);
        WorkspaceTabControl.SelectedItem = tab;
    }

    private object CreateWorkspaceTabHeader(string title, string key)
    {
        var header = new DockPanel { LastChildFill = false };
        header.Children.Add(new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            FontSize = 10
        });
        var close = new Button
        {
            Content = "×",
            ToolTip = "Close workspace tab",
            Padding = new Thickness(5, 0, 5, 1),
            MinWidth = 20,
            MinHeight = 18,
            FontSize = 13,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (System.Windows.Media.Brush)FindResource("Atlas.Muted"),
            Cursor = Cursors.Hand
        };
        close.Click += (_, _) => CloseWorkspaceTab(key);
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        return header;
    }

    private void CloseWorkspaceTab(string key)
    {
        if (!_openWorkspaceTabs.Remove(key, out var tab))
            return;
        WorkspaceTabControl.Items.Remove(tab);
    }

    private void QuickSearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (QuickSearchResultsListBox.SelectedItem is QuickSearchResultViewModel result)
            OpenSearchResult(new EntitySearchResult(result.Identity, result.DisplayName));
    }

    private async void OpenSearchResult(EntitySearchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var key = $"{result.Identity.Kind}|{result.Identity.ProviderKey}|{result.Identity.SourceId}";
        if (_openWorkspaceTabs.TryGetValue(key, out var openTab))
        {
            WorkspaceTabControl.SelectedItem = openTab;
            _mainWindowViewModel.NotifyProfileOpened(result);
            return;
        }

        try
        {
            object? profile = result.Identity.Kind switch
            {
                EntityKind.Player => await LoadPlayerProfileAsync(result.Identity),
                EntityKind.Staff => await LoadStaffProfileAsync(result.Identity),
                EntityKind.Club => await LoadClubProfileAsync(result.Identity),
                _ => null
            };

            if (profile is null)
            {
                MessageBox.Show(
                    this,
                    $"The {result.Identity.Kind} profile snapshot is not available in the local record store. Reload the active career or import the source data again.",
                    "FM Atlas — Profile unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var title = result.DisplayName;
            AddWorkspaceTab(title, key, profile);
            _mainWindowViewModel.NotifyProfileOpened(result);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"The profile could not be opened: {exception.Message}",
                "FM Atlas",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RecentProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RecentProfileViewModel recent })
            OpenSearchResult(recent.Result);
    }

    private async Task<PlayerProfileView?> LoadPlayerProfileAsync(EntityIdentity identity)
    {
        var player = await _snapshotStore.GetSnapshotAsync<PlayerRecord>(identity);
        if (player is null)
            return null;

        await EnsureGraphicsIndexReadyAsync();
        var portraitUid = player.GameUid?.Value ?? identity.SourceId;
        var portraitPath = _graphicsLocator.FindPortrait(portraitUid)?.ImagePath;
        return new PlayerProfileView { DataContext = new PlayerProfileViewModel(player, portraitPath) };
    }

    private async Task<StaffProfileView?> LoadStaffProfileAsync(EntityIdentity identity)
    {
        var staff = await _snapshotStore.GetSnapshotAsync<StaffRecord>(identity);
        return staff is null ? null : new StaffProfileView { DataContext = new StaffProfileViewModel(staff) };
    }

    private async Task<ClubProfileView?> LoadClubProfileAsync(EntityIdentity identity)
    {
        var club = await _snapshotStore.GetSnapshotAsync<ClubRecord>(identity);
        if (club is null)
            return null;

        await EnsureGraphicsIndexReadyAsync();
        var logoPath = uint.TryParse(identity.SourceId, out var clubId)
            ? _graphicsLocator.FindClubLogo(clubId)?.ImagePath
            : null;
        var view = new ClubProfileView
        {
            DataContext = new ClubProfileViewModel(club, logoPath, _advancedSearchProvider)
        };
        view.SquadPlayerOpenRequested += (_, result) => OpenSearchResult(result);
        return view;
    }

    private async Task EnsureGraphicsIndexReadyAsync()
    {
        if (_graphicsIndexTask is null)
            return;

        try
        {
            await _graphicsIndexTask;
        }
        catch (Exception exception)
        {
            // A graphics-pack problem must never prevent opening a player or club profile.
            System.Diagnostics.Debug.WriteLine($"FM Atlas graphics index unavailable: {exception.Message}");
        }
    }
}
