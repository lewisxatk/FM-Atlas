using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private string _playerPoolSearchTextV9 = "";
    private string _playerPoolPositionV9 = "Any position";
    private string _playerPoolMinCaV9 = "";
    private string _playerPoolMaxCaV9 = "";
    private string _playerPoolMinPaV9 = "";
    private string _playerPoolMaxPaV9 = "";
    private string _playerPoolCountTextV9 = "Player database";
    private string _playerPoolStatusTextV9 = "Search to begin";

    private Style CreateProfileSectionTabStyle()
    {
        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(Control.WidthProperty, 158d));
        style.Setters.Add(new Setter(Control.MinHeightProperty, 42d));
        style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(4, 4, 10, 4)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(11, 8, 11, 8)));
        style.Setters.Add(new Setter(Control.BackgroundProperty,
            MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceRaised)));
        style.Setters.Add(new Setter(Control.ForegroundProperty,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Border)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, MonolithDesignSystem.BodyFont));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 10d));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        var selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty,
            MonolithDesignSystem.Solid(MonolithDesignSystem.SignalSoft)));
        selected.Setters.Add(new Setter(Control.ForegroundProperty,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Signal)));
        selected.Setters.Add(new Setter(Control.BorderBrushProperty,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Signal)));
        style.Triggers.Add(selected);
        return style;
    }

    private ContextMenu BuildPlayerRecordTabContextMenu(TabItem tab)
    {
        var menu = new ContextMenu();
        var close = new MenuItem { Header = "Close" };
        close.Click += (_, _) => CloseRecordTabV9(tab);
        menu.Items.Add(close);

        var closeOthers = new MenuItem { Header = "Close other player tabs" };
        closeOthers.Click += (_, _) => CloseOtherRecordTabsV9(tab, closeToRight: false);
        menu.Items.Add(closeOthers);

        var closeRight = new MenuItem { Header = "Close player tabs to the right" };
        closeRight.Click += (_, _) => CloseOtherRecordTabsV9(tab, closeToRight: true);
        menu.Items.Add(closeRight);
        return menu;
    }

    private void CloseRecordTabV9(TabItem tab)
    {
        if (_profileTabs is null) return;
        var tag = tab.Tag?.ToString() ?? "";
        if (Equals(tag, "__welcome")
            || tag.StartsWith("__", StringComparison.OrdinalIgnoreCase) && tag.EndsWith("-pool", StringComparison.OrdinalIgnoreCase))
            return;

        if (tag.StartsWith("STAFF|", StringComparison.OrdinalIgnoreCase))
            _openStaffRecordsV9.Remove(tag["STAFF|".Length..]);
        else if (tag.StartsWith("CLUB|", StringComparison.OrdinalIgnoreCase))
            _openClubRecordsV9.Remove(tag["CLUB|".Length..]);
        else
            _openPlayerRecords.Remove(tag);

        _profileTabs.Items.Remove(tab);
        if (_profileTabs.Items.Count == 0) AddWelcomeProfileTab();
    }

    private void CloseOtherRecordTabsV9(TabItem keep, bool closeToRight)
    {
        if (_profileTabs is null) return;
        var tabs = _profileTabs.Items.OfType<TabItem>().ToList();
        var keepIndex = tabs.IndexOf(keep);
        if (keepIndex < 0) return;

        var toClose = closeToRight
            ? tabs.Skip(keepIndex + 1)
            : tabs.Where(x => !ReferenceEquals(x, keep));

        foreach (var tab in toClose
            .Where(x => !Equals(x.Tag, "__welcome")
                && !(x.Tag?.ToString()?.StartsWith("__", StringComparison.OrdinalIgnoreCase) == true
                    && x.Tag.ToString()!.EndsWith("-pool", StringComparison.OrdinalIgnoreCase)))
            .ToList())
            CloseRecordTabV9(tab);

        if (_profileTabs.Items.Contains(keep))
            _profileTabs.SelectedItem = keep;
    }

    // Full-width, record-tab player experience.
    // The player pool remains open as its own tab; each selected player opens
    // as a sibling tab in the main workspace, not as a narrow side inspector.
    private async Task<UIElement> PlayerWorkspaceV9Async(bool recruitmentMode)
    {
        // Capture the current pool controls before navigating away/rebuilding the
        // workspace. Search state and the last result page survive tab changes.
        if (_playerSearch is not null) _playerPoolSearchTextV9 = _playerSearch.Text;
        if (_positionPicker?.SelectedItem is string selectedPosition) _playerPoolPositionV9 = selectedPosition;
        if (_playerMinCaInput is not null) _playerPoolMinCaV9 = _playerMinCaInput.Text;
        if (_playerMaxCaInput is not null) _playerPoolMaxCaV9 = _playerMaxCaInput.Text;
        if (_playerMinPaInput is not null) _playerPoolMinPaV9 = _playerMinPaInput.Text;
        if (_playerMaxPaInput is not null) _playerPoolMaxPaV9 = _playerMaxPaInput.Text;
        if (_playerCountText is not null) _playerPoolCountTextV9 = _playerCountText.Text;
        if (_playerPageStatus is not null) _playerPoolStatusTextV9 = _playerPageStatus.Text;

        var workspaceTabs = new TabControl
        {
            FontSize = 11,
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Canvas),
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
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,
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
        headingCopy.Children.Add(MonolithDesignSystem.MicroLabel(recruitmentMode ? "RECRUITMENT / PLAYER INTELLIGENCE" : "PLAYER INTELLIGENCE", true));
        _playerCountText = MonolithDesignSystem.DisplayText(_playerPoolCountTextV9, 25);
        headingCopy.Children.Add(_playerCountText);
        headingCopy.Children.Add(MonolithDesignSystem.BodyText(
            "Search the loaded FM career. Select a player to open a full workspace tab; source and field confidence stay visible.",
            11, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        heading.Children.Add(headingCopy);

        var actions = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(MonolithDesignSystem.ActionButton("ADVANCED SEARCH", async (_, _) => await NavigateAsync("Advanced Search")));
        actions.Children.Add(MonolithDesignSystem.ActionButton("IMPORT REPORT", async (_, _) => await ImportIntelligenceReportAsync()));
        actions.Children.Add(MonolithDesignSystem.ActionButton("ADD TO SHORTLIST", async (_, _) => await AddSelectedToShortlistAsync(), primary: true));
        Grid.SetColumn(actions, 1);
        heading.Children.Add(actions);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var searchCard = MonolithDesignSystem.Panel(new StackPanel(), 14, new Thickness(0, 0, 0, 12), new Thickness(0));
        var searchStack = (StackPanel)searchCard.Child;
        var searchRow = new Grid { Margin = new Thickness(12, 10, 12, 5) };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _playerSearch = MonolithDesignSystem.TextInput("Search by player, club, nation or UID", 13);
        _playerSearch.Text = _playerPoolSearchTextV9;
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
        var savedPositionIndex = Array.FindIndex(positionOptions,
            x => x.Equals(_playerPoolPositionV9, StringComparison.OrdinalIgnoreCase));
        _positionPicker.SelectedIndex = savedPositionIndex >= 0 ? savedPositionIndex : 0;
        Grid.SetColumn(_positionPicker, 1);
        searchRow.Children.Add(_positionPicker);

        var searchButton = MonolithDesignSystem.ActionButton("SEARCH", async (_, _) =>
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
        _playerMinCaInput = MonolithDesignSystem.TextInput("Min CA"); _playerMinCaInput.Text = _playerPoolMinCaV9; _playerMinCaInput.Width = 76; _playerMinCaInput.MaxLength = 3;
        _playerMaxCaInput = MonolithDesignSystem.TextInput("Max CA"); _playerMaxCaInput.Text = _playerPoolMaxCaV9; _playerMaxCaInput.Width = 76; _playerMaxCaInput.MaxLength = 3;
        _playerMinPaInput = MonolithDesignSystem.TextInput("Min PA"); _playerMinPaInput.Text = _playerPoolMinPaV9; _playerMinPaInput.Width = 76; _playerMinPaInput.MaxLength = 3;
        _playerMaxPaInput = MonolithDesignSystem.TextInput("Max PA"); _playerMaxPaInput.Text = _playerPoolMaxPaV9; _playerMaxPaInput.Width = 76; _playerMaxPaInput.MaxLength = 3;
        filterRow.Children.Add(FilterControl("CA MIN · 1–200", _playerMinCaInput, 84));
        filterRow.Children.Add(FilterControl("CA MAX · 1–200", _playerMaxCaInput, 84));
        filterRow.Children.Add(FilterControl("PA MIN · -10–200", _playerMinPaInput, 84));
        filterRow.Children.Add(FilterControl("PA MAX · -10–200", _playerMaxPaInput, 84));
        filterRow.Children.Add(MonolithDesignSystem.ActionButton("CLEAR ABILITY", (_, _) =>
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

        var resultsCard = MonolithDesignSystem.Panel(new DockPanel(), 14, padding: new Thickness(0));
        var resultsDock = (DockPanel)resultsCard.Child;
        var resultsHeader = new Grid { Margin = new Thickness(12, 10, 12, 8) };
        resultsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resultsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var resultsTitle = new StackPanel();
        resultsTitle.Children.Add(MonolithDesignSystem.MicroLabel("SEARCH RESULTS", true));
        resultsTitle.Children.Add(MonolithDesignSystem.BodyText("Click a row to open its full player record in a new tab", 11, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        resultsHeader.Children.Add(resultsTitle);
        var evidence = MonolithDesignSystem.DataText("SOURCE / CONFIDENCE · live fields remain provisional until witnessed against the FM27 UI", 9, MonolithDesignSystem.Solid("#D69A63"));
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
        _playerPageStatus = MonolithDesignSystem.DataText(_playerPoolStatusTextV9, 10, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        DockPanel.SetDock(_playerPageStatus, Dock.Left);
        pageBar.Children.Add(_playerPageStatus);
        var pageButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _previousPlayerPageButton = MonolithDesignSystem.ActionButton("← PREVIOUS", async (_, _) =>
        {
            _playerOffset = Math.Max(0, _playerOffset - PlayerPageSize);
            await RefreshPlayerResultsAsync();
        });
        _nextPlayerPageButton = MonolithDesignSystem.ActionButton("NEXT →", async (_, _) =>
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
