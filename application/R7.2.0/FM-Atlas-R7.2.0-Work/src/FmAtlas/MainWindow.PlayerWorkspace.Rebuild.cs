using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FmAtlas;

public partial class MainWindow
{
    // Full-width, record-tab player experience.
    // The player pool remains open as its own tab; each selected player opens
    // as a sibling tab in the main workspace, not as a narrow side inspector.
    private async Task<UIElement> PlayerWorkspaceV9Async(bool recruitmentMode)
    {
        var workspaceTabs = new TabControl
        {
            FontSize = 11,
            Background = Brush("#11161D"),
            BorderBrush = Brush("#303943"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5)
        };

        _profileTabs = workspaceTabs;
        _profilePanel = null;
        workspaceTabs.SelectionChanged += (_, _) =>
        {
            if (workspaceTabs.SelectedItem is TabItem selected
                && selected.Content is ScrollViewer scroller
                && scroller.Content is StackPanel selectedPanel)
                _profilePanel = selectedPanel;
            else
                _profilePanel = null;
        };

        var poolTab = new TabItem
        {
            Header = recruitmentMode ? "RECRUITMENT POOL" : "PLAYER DATABASE",
            Tag = "__player-pool",
            Content = BuildPlayerPoolPageV9(recruitmentMode)
        };
        workspaceTabs.Items.Add(poolTab);
        workspaceTabs.SelectedItem = poolTab;

        // Profiles are restored into full-width record tabs after the pool is visible.
        var profilesToRestore = _openPlayerRecords.Values.ToList();
        if (profilesToRestore.Count > 0)
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,
                new Action(() => _ = RestoreOpenPlayerProfilesSafelyAsync(profilesToRestore)));

        await Task.CompletedTask;
        return workspaceTabs;
    }

    private UIElement BuildPlayerPoolPageV9(bool recruitmentMode)
    {
        var root = new Grid { Margin = new Thickness(8, 8, 8, 4) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var heading = new Grid { Margin = new Thickness(8, 3, 8, 14) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headingCopy = new StackPanel();
        headingCopy.Children.Add(Txt(recruitmentMode ? "RECRUITMENT / PLAYER INTELLIGENCE" : "PLAYER INTELLIGENCE", 9, true, Teal));
        _playerCountText = Txt("Player database", 25, true, Ink);
        headingCopy.Children.Add(_playerCountText);
        headingCopy.Children.Add(Txt(
            "Search the loaded FM career. Select a player to open a full workspace tab; source and field confidence stay visible.",
            11, false, Muted, wrap: true));
        heading.Children.Add(headingCopy);

        var actions = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(SoftButton("ADVANCED SEARCH", (_, _) => ShowGlobalSearch()));
        actions.Children.Add(SoftButton("IMPORT REPORT", async (_, _) => await ImportIntelligenceReportAsync()));
        actions.Children.Add(PrimaryButton("ADD TO SHORTLIST", async (_, _) => await AddSelectedToShortlistAsync()));
        Grid.SetColumn(actions, 1);
        heading.Children.Add(actions);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var searchCard = Card(new StackPanel(), 14, new Thickness(0, 0, 0, 12));
        var searchStack = (StackPanel)searchCard.Child;
        var searchRow = new Grid { Margin = new Thickness(12, 10, 12, 5) };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _playerSearch = Input("Search by player, club, nation or UID");
        _playerSearch.Height = 42;
        _playerSearch.FontSize = 13;
        _playerSearch.Margin = new Thickness(0, 2, 10, 4);
        _playerSearch.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _playerOffset = 0;
                await RefreshPlayerResultsAsync();
            }
        };
        Grid.SetColumn(_playerSearch, 0);
        searchRow.Children.Add(_playerSearch);

        var positionOptions = new[] { "Any position", "ATTACKERS", "MIDFIELDERS", "DEFENDERS", "GOALKEEPERS" }
            .Concat(PositionSuitabilityService.PositionCodes)
            .Append("ST")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _positionPicker = new ComboBox
        {
            ItemsSource = positionOptions,
            SelectedIndex = 0,
            Margin = new Thickness(0, 2, 10, 4),
            Padding = new Thickness(10),
            MinHeight = 42,
            FontSize = 11
        };
        Grid.SetColumn(_positionPicker, 1);
        searchRow.Children.Add(_positionPicker);

        var searchButton = PrimaryButton("SEARCH", async (_, _) =>
        {
            _playerOffset = 0;
            await RefreshPlayerResultsAsync();
        });
        searchButton.MinWidth = 104;
        searchButton.Height = 42;
        Grid.SetColumn(searchButton, 2);
        searchRow.Children.Add(searchButton);
        searchStack.Children.Add(searchRow);

        var filterRow = new WrapPanel
        {
            Margin = new Thickness(12, 0, 12, 10),
            VerticalAlignment = VerticalAlignment.Center
        };
        _playerMinCaInput = Input("Min CA"); _playerMinCaInput.Width = 76; _playerMinCaInput.MaxLength = 3;
        _playerMaxCaInput = Input("Max CA"); _playerMaxCaInput.Width = 76; _playerMaxCaInput.MaxLength = 3;
        _playerMinPaInput = Input("Min PA"); _playerMinPaInput.Width = 76; _playerMinPaInput.MaxLength = 3;
        _playerMaxPaInput = Input("Max PA"); _playerMaxPaInput.Width = 76; _playerMaxPaInput.MaxLength = 3;
        filterRow.Children.Add(FilterControl("CA MIN · 1–200", _playerMinCaInput, 84));
        filterRow.Children.Add(FilterControl("CA MAX · 1–200", _playerMaxCaInput, 84));
        filterRow.Children.Add(FilterControl("PA MIN · -10–200", _playerMinPaInput, 84));
        filterRow.Children.Add(FilterControl("PA MAX · -10–200", _playerMaxPaInput, 84));
        filterRow.Children.Add(SoftButton("CLEAR ABILITY", (_, _) =>
        {
            _playerMinCaInput.Text = "";
            _playerMaxCaInput.Text = "";
            _playerMinPaInput.Text = "";
            _playerMaxPaInput.Text = "";
            _playerOffset = 0;
            _ = RefreshPlayerResultsAsync();
        }));
        searchStack.Children.Add(filterRow);
        Grid.SetRow(searchCard, 1);
        root.Children.Add(searchCard);

        var resultsCard = Card(new DockPanel(), 14);
        var resultsDock = (DockPanel)resultsCard.Child;
        var resultsHeader = new Grid { Margin = new Thickness(12, 10, 12, 8) };
        resultsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resultsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var resultsTitle = new StackPanel();
        resultsTitle.Children.Add(Txt("SEARCH RESULTS", 9, true, Teal));
        resultsTitle.Children.Add(Txt("Click a row to open its full player record in a new tab", 11, false, Muted));
        resultsHeader.Children.Add(resultsTitle);
        var evidence = Txt("SOURCE / CONFIDENCE · live fields remain provisional until witnessed against the FM27 UI", 9, false, Brush("#D69A63"));
        evidence.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(evidence, 1);
        resultsHeader.Children.Add(evidence);
        DockPanel.SetDock(resultsHeader, Dock.Top);
        resultsDock.Children.Add(resultsHeader);

        _playerResultsGrid = CreateGrid();
        _playerResultsGrid.FontSize = 11;
        _playerResultsGrid.RowHeight = 42;
        _playerResultsGrid.EnableRowVirtualization = true;
        _playerResultsGrid.EnableColumnVirtualization = true;
        VirtualizingPanel.SetIsVirtualizing(_playerResultsGrid, true);
        VirtualizingPanel.SetVirtualizationMode(_playerResultsGrid, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(_playerResultsGrid, true);
        _playerResultsGrid.Columns.Add(Column("PLAYER", "Name", 1.4));
        _playerResultsGrid.Columns.Add(Column("CLUB", "Club", 1.15));
        _playerResultsGrid.Columns.Add(Column("AGE", "Age", .48));
        _playerResultsGrid.Columns.Add(Column("POSITION", "Position", .68));
        _playerResultsGrid.Columns.Add(Column("LEAGUE", "League", .95));
        _playerResultsGrid.Columns.Add(Column("CA", "DisplayAbility", .42));
        _playerResultsGrid.Columns.Add(Column("PA", "DisplayPotential", .42));
        _playerResultsGrid.Columns.Add(Column("NATION", "Nation", .8));
        _playerResultsGrid.Columns.Add(Column("HOME-GROWN · CLUB", "DisplayHomeGrownClub", .78));
        _playerResultsGrid.Columns.Add(Column("HOME-GROWN · NATION", "DisplayHomeGrownNation", .8));
        _playerResultsGrid.Columns.Add(Column("SOURCE / CONFIDENCE", "DataOrigin", .98));
        _playerResultsGrid.ItemsSource = _players;
        _playerResultsGrid.SelectionChanged += async (_, _) =>
        {
            if (_playerResultsGrid.SelectedItem is PlayerRecord player)
            {
                _selectedPlayer = player;
                try
                {
                    await ShowPlayerProfileAsync(player);
                }
                catch (Exception ex)
                {
                    AtlasLog.Error("Could not open full player record tab.", ex);
                    ClearProfile("The player record could not be opened: " + ex.Message);
                }
            }
        };
        resultsDock.Children.Add(_playerResultsGrid);

        var pageBar = new DockPanel { Margin = new Thickness(12, 8, 12, 10) };
        _playerPageStatus = Txt("Search to begin", 10, false, Muted);
        DockPanel.SetDock(_playerPageStatus, Dock.Left);
        pageBar.Children.Add(_playerPageStatus);
        var pageButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _previousPlayerPageButton = SoftButton("← PREVIOUS", async (_, _) =>
        {
            _playerOffset = Math.Max(0, _playerOffset - PlayerPageSize);
            await RefreshPlayerResultsAsync();
        });
        _nextPlayerPageButton = SoftButton("NEXT →", async (_, _) =>
        {
            _playerOffset += PlayerPageSize;
            await RefreshPlayerResultsAsync();
        });
        _previousPlayerPageButton.IsEnabled = false;
        _nextPlayerPageButton.IsEnabled = false;
        pageButtons.Children.Add(_previousPlayerPageButton);
        pageButtons.Children.Add(_nextPlayerPageButton);
        DockPanel.SetDock(pageButtons, Dock.Right);
        pageBar.Children.Add(pageButtons);
        DockPanel.SetDock(pageBar, Dock.Bottom);
        resultsDock.Children.Add(pageBar);

        Grid.SetRow(resultsCard, 2);
        root.Children.Add(resultsCard);
        return root;
    }
}
