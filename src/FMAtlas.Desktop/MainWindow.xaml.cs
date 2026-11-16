using System.Windows;
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
    private readonly Dictionary<string, Window> _openPlayerProfiles = new(StringComparer.Ordinal);
    private AdvancedSearchWindow? _advancedSearchWindow;

    public MainWindow(
        MainWindowViewModel viewModel,
        IAdvancedSearchProvider advancedSearchProvider,
        IEntityRecordSnapshotStore snapshotStore)
    {
        InitializeComponent();
        DataContext = viewModel;
        _advancedSearchProvider = advancedSearchProvider ?? throw new ArgumentNullException(nameof(advancedSearchProvider));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
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

        var searchWindow = new AdvancedSearchWindow(_advancedSearchProvider)
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
