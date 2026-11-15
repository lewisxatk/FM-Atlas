using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private string _clubPoolSearchTextV9 = "";
    private int _clubPoolOffsetV9;
    private int _clubPoolRequestVersionV9;
    private int _clubProfileRequestVersionV9;
    private DataGrid? _clubGridV9;
    private TextBlock? _clubPoolStatusV9;
    private Button? _clubPreviousV9;
    private Button? _clubNextV9;
    private const int ClubPoolPageSizeV9 = 200;

    private async Task<UIElement> ClubsWorkspaceV9Async()
    {
        var tabs = new TabControl
        {
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Surface),
            Foreground = MonolithDesignSystem.Solid(MonolithDesignSystem.Text),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5)
        };
        _profileTabs = tabs;
        _profilePanel = null;
        tabs.SelectionChanged += (_, _) =>
        {
            if (tabs.SelectedItem is TabItem selected
                && selected.Content is ScrollViewer scroll
                && scroll.Content is StackPanel panel)
                _profilePanel = panel;
            else
                _profilePanel = null;
        };

        var pool = new TabItem
        {
            Header = "CLUB DIRECTORY",
            Tag = "__club-pool",
            Content = BuildClubPoolPageV9()
        };
        tabs.Items.Add(pool);
        tabs.SelectedItem = pool;

        var restore = _openClubRecordsV9.Values.ToList();
        if (restore.Count > 0)
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,
                new Action(() => _ = RestoreOpenClubTabsV9Async(restore)));

        await RefreshClubPoolV9Async();
        return tabs;
    }

    private UIElement BuildClubPoolPageV9()
    {
        var root = new Grid { Margin = new Thickness(8, 8, 8, 4) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var heading = new Grid { Margin = new Thickness(8, 3, 8, 14) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var copy = new StackPanel();
        copy.Children.Add(MonolithDesignSystem.MicroLabel("CAREER INTELLIGENCE / CLUBS", true));
        copy.Children.Add(MonolithDesignSystem.DisplayText("Club directory", 25));
        copy.Children.Add(MonolithDesignSystem.BodyText(
            "Search the club index and open each club as a full workspace tab. Squad, staff, finance and facility details appear only when the source provides them.",
            12, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        heading.Children.Add(copy);
        var openManager = MonolithDesignSystem.ActionButton("OPEN MY CLUB",
            async (_, _) => await OpenMyClubAsync());
        openManager.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(openManager, 1);
        heading.Children.Add(openManager);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var searchCard = MonolithDesignSystem.Panel(new Grid(), 12,
            new Thickness(0, 0, 0, 12), new Thickness(10), raised: true);
        var searchGrid = (Grid)searchCard.Child;
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var search = MonolithDesignSystem.TextInput("Search club name or record ID", 13);
        search.Text = _clubPoolSearchTextV9;
        search.MinHeight = 42;
        search.Margin = new Thickness(0, 0, 10, 0);
        search.TextChanged += (_, _) =>
        {
            _clubPoolSearchTextV9 = search.Text;
            _clubPoolOffsetV9 = 0;
            _ = RefreshClubPoolV9Async();
        };
        search.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _clubPoolOffsetV9 = 0;
                await RefreshClubPoolV9Async();
            }
        };
        searchGrid.Children.Add(search);
        var searchButton = MonolithDesignSystem.ActionButton("SEARCH",
            async (_, _) => { _clubPoolOffsetV9 = 0; await RefreshClubPoolV9Async(); }, primary: true);
        searchButton.MinWidth = 100;
        Grid.SetColumn(searchButton, 1);
        searchGrid.Children.Add(searchButton);
        Grid.SetRow(searchCard, 1);
        root.Children.Add(searchCard);

        var resultCard = MonolithDesignSystem.Panel(new DockPanel(), 12, padding: new Thickness(0));
        var dock = (DockPanel)resultCard.Child;
        var resultHeader = new StackPanel { Margin = new Thickness(12, 10, 12, 8) };
        resultHeader.Children.Add(MonolithDesignSystem.MicroLabel("CLUB RESULTS", true));
        resultHeader.Children.Add(MonolithDesignSystem.BodyText(
            "Select a club to open its full record tab. The grid shows identity data, not inferred finances or facilities.",
            11, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        DockPanel.SetDock(resultHeader, Dock.Top);
        dock.Children.Add(resultHeader);

        _clubGridV9 = CreateGrid();
        _clubGridV9.FontSize = 11;
        _clubGridV9.RowHeight = 42;
        _clubGridV9.EnableRowVirtualization = true;
        _clubGridV9.EnableColumnVirtualization = true;
        VirtualizingPanel.SetIsVirtualizing(_clubGridV9, true);
        VirtualizingPanel.SetVirtualizationMode(_clubGridV9, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(_clubGridV9, true);
        _clubGridV9.Columns.Add(Column("CLUB", "Name", 1.65));
        _clubGridV9.Columns.Add(Column("NATION", "Nation", .85));
        _clubGridV9.Columns.Add(Column("LINKED PLAYERS", "PlayerCount", .7));
        _clubGridV9.Columns.Add(Column("SOURCE", "DataSource", 1.0));
        _clubGridV9.SelectionChanged += async (_, _) =>
        {
            if (_clubGridV9.SelectedItem is not AtlasClubRecord club) return;
            try { await ShowClubRecordTabV9Async(club); }
            catch (Exception ex)
            {
                AtlasLog.Error("Could not open full club record tab.", ex);
                if (_profileTabs?.Items.OfType<TabItem>().FirstOrDefault(x => Equals(x.Tag, "__club-pool")) is { } pool)
                    _profileTabs.SelectedItem = pool;
                if (_clubPoolStatusV9 is not null)
                    _clubPoolStatusV9.Text = "Club profile failed to load: " + ex.Message;
            }
        };
        dock.Children.Add(_clubGridV9);

        var footer = new DockPanel { Margin = new Thickness(12, 8, 12, 10) };
        _clubPoolStatusV9 = MonolithDesignSystem.DataText("Loading the club index…", 10,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        DockPanel.SetDock(_clubPoolStatusV9, Dock.Left);
        footer.Children.Add(_clubPoolStatusV9);
        var pageButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _clubPreviousV9 = MonolithDesignSystem.ActionButton("← PREVIOUS",
            async (_, _) => { _clubPoolOffsetV9 = Math.Max(0, _clubPoolOffsetV9 - ClubPoolPageSizeV9); await RefreshClubPoolV9Async(); });
        _clubNextV9 = MonolithDesignSystem.ActionButton("NEXT →",
            async (_, _) => { _clubPoolOffsetV9 += ClubPoolPageSizeV9; await RefreshClubPoolV9Async(); });
        _clubPreviousV9.IsEnabled = false;
        _clubNextV9.IsEnabled = false;
        pageButtons.Children.Add(_clubPreviousV9);
        pageButtons.Children.Add(_clubNextV9);
        DockPanel.SetDock(pageButtons, Dock.Right);
        footer.Children.Add(pageButtons);
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(footer);

        Grid.SetRow(resultCard, 2);
        root.Children.Add(resultCard);
        return root;
    }

    private async Task RefreshClubPoolV9Async()
    {
        if (_clubGridV9 is null || _clubPoolStatusV9 is null
            || _clubPreviousV9 is null || _clubNextV9 is null) return;
        var request = ++_clubPoolRequestVersionV9;
        var query = _clubPoolSearchTextV9.Trim();
        _clubPoolStatusV9.Text = "Searching clubs…";
        _clubPreviousV9.IsEnabled = false;
        _clubNextV9.IsEnabled = false;
        try
        {
            var (rows, count) = await Task.Run(async () =>
            {
                var page = await _database.SearchClubDirectoryAsync(query, ClubPoolPageSizeV9, _clubPoolOffsetV9).ConfigureAwait(false);
                var total = await _database.CountClubDirectoryAsync(query).ConfigureAwait(false);
                return (page, total);
            });
            if (request != _clubPoolRequestVersionV9) return;
            _clubGridV9.ItemsSource = rows;
            _clubPoolStatusV9.Text = rows.Count == 0
                ? "No clubs found. Try a shorter name or check the imported source."
                : $"{rows.Count:N0} shown · {count:N0} club source records · page {_clubPoolOffsetV9 / ClubPoolPageSizeV9 + 1:N0}";
            _clubPreviousV9.IsEnabled = _clubPoolOffsetV9 > 0;
            _clubNextV9.IsEnabled = _clubPoolOffsetV9 + rows.Count < count && rows.Count == ClubPoolPageSizeV9;

            if (!string.IsNullOrWhiteSpace(_pendingClubSourceId))
            {
                var target = rows.FirstOrDefault(x => x.SourceId == _pendingClubSourceId);
                if (target is not null)
                {
                    _clubGridV9.SelectedItem = target;
                    _clubGridV9.ScrollIntoView(target);
                }
                else if (!string.IsNullOrWhiteSpace(_pendingClubSearchQuery))
                {
                    _clubPoolSearchTextV9 = _pendingClubSearchQuery;
                }
                _pendingClubSourceId = null;
                _pendingClubSearchQuery = null;
            }
        }
        catch (Exception ex)
        {
            if (request != _clubPoolRequestVersionV9) return;
            _clubPoolStatusV9.Text = "Club search failed: " + ex.Message;
            AtlasLog.Error("V9 club directory search failed.", ex);
        }
    }

    private async Task ShowClubRecordTabV9Async(AtlasClubRecord club)
    {
        if (_profileTabs is null) return;
        var tag = "CLUB|" + club.SourceId;
        var existing = _profileTabs.Items.OfType<TabItem>()
            .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _profileTabs.SelectedItem = existing;
            _openClubRecordsV9[club.SourceId] = club;
            return;
        }

        var content = new StackPanel { Margin = new Thickness(18) };
        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var tab = new TabItem { Tag = tag, Content = scroll };
        var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(MonolithDesignSystem.BodyText(club.Name, 11,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Text)));
        var close = new Button
        {
            Content = "×",
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            Margin = new Thickness(8, 0, 2, 0),
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceRaised),
            Foreground = MonolithDesignSystem.Solid(MonolithDesignSystem.Text),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            BorderThickness = new Thickness(1),
            ToolTip = "Close club record"
        };
        close.Click += (_, _) => CloseRecordTabV9(tab);
        header.Children.Add(close);
        tab.Header = header;
        tab.ContextMenu = BuildClubRecordTabContextMenuV9(tab);
        tab.ContextMenuOpening += (_, _) => _profileTabs.SelectedItem = tab;
        _profileTabs.Items.Add(tab);
        _profileTabs.SelectedItem = tab;
        _openClubRecordsV9[club.SourceId] = club;
        _profilePanel = content;

        try
        {
            var isLive = club.DataSource.Contains("live", StringComparison.OrdinalIgnoreCase);
            var (facts, squadRows, clubStaff, squadWages) = await Task.Run(async () =>
            {
                var loadedFacts = isLive
                    ? await _database.GetLiveClubFactsAsync(club.SourceId).ConfigureAwait(false)
                    : await _database.GetFmsaveClubFactsAsync(club.SourceId).ConfigureAwait(false);
                var loadedSquad = await _database.GetPlayersForClubAsync(club.SourceId, isLive, 5000).ConfigureAwait(false);
                var loadedStaff = isLive
                    ? new List<AtlasStaffRecord>()
                    : (await _database.SearchFmsaveStaffAsync(club.Name).ConfigureAwait(false))
                        .Where(x => string.Equals(x.Club?.Trim(), club.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                        .Take(100).ToList();
                var loadedWages = isLive
                    ? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    : await _database.GetLatestPlayerWageFactsAsync(loadedSquad.Select(x => x.SourceId).ToList()).ConfigureAwait(false);
                return (loadedFacts, loadedSquad, loadedStaff, loadedWages);
            });
            content.Children.Add(MonolithDesignSystem.MicroLabel("CLUB RECORD", true));
            var clubUid = club.SourceId.StartsWith("FMSAVE:", StringComparison.OrdinalIgnoreCase)
                || club.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase)
                ? club.SourceId[(club.SourceId.IndexOf(':') + 1)..] : null;
            var clubLogoPath = await Task.Run(() => GraphicsResolver.Shared.ResolveClubLogo(clubUid));
            var hero = MonolithDesignSystem.Panel(new StackPanel(), 14,
                new Thickness(0, 10, 0, 12), new Thickness(14), raised: true);
            var heroStack = (StackPanel)hero.Child;
            var heroRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logo = new Border
            {
                Width = 62,
                Height = 62,
                Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceHover),
                BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 14, 0)
            };
            if (!string.IsNullOrWhiteSpace(clubLogoPath) && File.Exists(clubLogoPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 128;
                    bitmap.UriSource = new Uri(clubLogoPath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    logo.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform };
                }
                catch { logo.Child = MonolithDesignSystem.MicroLabel("CLUB", true); }
            }
            else logo.Child = MonolithDesignSystem.MicroLabel("CLUB", true);
            heroRow.Children.Add(logo);
            var heroCopy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            heroCopy.Children.Add(MonolithDesignSystem.DisplayText(club.Name, 25));
            heroCopy.Children.Add(MonolithDesignSystem.BodyText(
                $"{club.Nation ?? "Nationality unavailable"} · {club.PlayerCount:N0} linked player identities · {club.DataSource}",
                11, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
            heroCopy.Children.Add(MonolithDesignSystem.DataText("RECORD ID  /  " + club.SourceId, 9,
                MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
            heroRow.Children.Add(heroCopy);
            heroStack.Children.Add(heroRow);
            content.Children.Add(hero);

            content.Children.Add(MonolithDesignSystem.MicroLabel("CLUB DATA", true));
            AddDetailRow(content, "Nation", club.Nation ?? "Not decoded");
            AddDetailRow(content, "Linked players", club.PlayerCount.ToString("N0", CultureInfo.InvariantCulture));
            AddDetailRow(content, "Source", club.DataSource);
            if (facts.Count == 0)
                content.Children.Add(MonolithDesignSystem.BodyText(
                    "No additional finance, facility, stadium or affiliation facts were decoded for this club. A club identity alone does not contain those details.",
                    11, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
            foreach (var group in facts.GroupBy(x => x.Group))
            {
                content.Children.Add(MonolithDesignSystem.MicroLabel(group.Key.Replace('_', ' ').ToUpperInvariant()));
                foreach (var fact in group.Take(80))
                    AddDetailRow(content, fact.Field.Replace('_', ' ').Replace('.', ' '), fact.Value);
            }

            content.Children.Add(MonolithDesignSystem.MicroLabel("CLUB STAFF", true));
            if (isLive)
                content.Children.Add(MonolithDesignSystem.BodyText(
                    "Live staff-to-club relationships are not validated by the current reader, so no staff are inferred here.",
                    10, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
            else if (clubStaff.Count == 0)
                content.Children.Add(MonolithDesignSystem.BodyText(
                    "No staff records with an exact matching club name were supplied by this save snapshot.",
                    10, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
            else
            {
                AddDetailRow(content, "Exact club-name matches", clubStaff.Count.ToString("N0", CultureInfo.InvariantCulture));
                var staffGrid = CreateGrid();
                staffGrid.Columns.Add(Column("NAME", "Name", 1.2));
                staffGrid.Columns.Add(Column("ROLE", "Job", 1));
                staffGrid.Columns.Add(Column("AGE", "Age", .4));
                staffGrid.ItemsSource = clubStaff;
                staffGrid.Height = Math.Min(280, Math.Max(110, clubStaff.Count * 32));
                staffGrid.MouseDoubleClick += async (_, _) =>
                {
                    if (staffGrid.SelectedItem is not AtlasStaffRecord person) return;
                    _pendingStaffSourceId = person.SourceId;
                    _pendingStaffSearchQuery = person.Name;
                    await NavigateAsync("Staff");
                };
                content.Children.Add(staffGrid);
            }

            content.Children.Add(MonolithDesignSystem.MicroLabel("CLUB SQUAD", true));
            if (squadRows.Count == 0)
                content.Children.Add(MonolithDesignSystem.BodyText(
                    "No player-to-club links were imported for this club. The identity reader can find club names without necessarily decoding complete squad relationships for every record.",
                    11, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
            else
            {
                var analysis = ClubSquadAnalysisService.Analyse(squadRows, squadWages);
                var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 7, 0, 10) };
                metrics.Children.Add(MetricCard("AVG AGE", analysis.AverageAge?.ToString("0.0") ?? "—",
                    $"{analysis.AgeKnownCount}/{analysis.PlayerCount} known", "◷"));
                metrics.Children.Add(MetricCard("AVG CA", analysis.AverageCurrentAbility?.ToString("0.0") ?? "—",
                    $"{analysis.CurrentAbilityKnownCount}/{analysis.PlayerCount} known", "↗"));
                metrics.Children.Add(MetricCard("AVG PA", analysis.AveragePotentialAbility?.ToString("0.0") ?? "—",
                    $"{analysis.PotentialAbilityKnownCount}/{analysis.PlayerCount} known", "⌁"));
                metrics.Children.Add(MetricCard("TOTAL VALUE", analysis.TotalValue?.ToString("N0", CultureInfo.InvariantCulture) ?? "—",
                    $"{analysis.ValueKnownCount}/{analysis.PlayerCount} · source units", "◈"));
                content.Children.Add(metrics);
                content.Children.Add(MonolithDesignSystem.MicroLabel("AGE PROFILE"));
                foreach (var band in analysis.AgeBands)
                    AddDetailRow(content, band.Label, band.Count.ToString("N0", CultureInfo.InvariantCulture));
                content.Children.Add(MonolithDesignSystem.MicroLabel("PRIMARY POSITION COVERAGE"));
                foreach (var position in analysis.PositionCounts.Take(12))
                    AddDetailRow(content, position.Position, position.Count.ToString("N0", CultureInfo.InvariantCulture));
                AddDetailRow(content, "Wages recorded", $"{analysis.WageKnownCount:N0}/{analysis.PlayerCount:N0}");
                AddDetailRow(content, "Wage total / mean", analysis.TotalWage.HasValue
                    ? $"{analysis.TotalWage.Value.ToString("N0", CultureInfo.InvariantCulture)} / {analysis.AverageWage?.ToString("N0", CultureInfo.InvariantCulture) ?? "—"} source units"
                    : "Unavailable");
                AddDetailRow(content, "Home-grown at club",
                    $"{analysis.HomeGrownClubYes} yes · {analysis.HomeGrownClubNo} no · {analysis.HomeGrownClubUnknown} unknown");
                AddDetailRow(content, "Home-grown in nation",
                    $"{analysis.HomeGrownNationYes} yes · {analysis.HomeGrownNationNo} no · {analysis.HomeGrownNationUnknown} unknown");
                AddDetailRow(content, "Other data coverage",
                    $"Age {analysis.AgeKnownCount}/{analysis.PlayerCount} · CA {analysis.CurrentAbilityKnownCount}/{analysis.PlayerCount} · PA {analysis.PotentialAbilityKnownCount}/{analysis.PlayerCount} · value {analysis.ValueKnownCount}/{analysis.PlayerCount}");
                content.Children.Add(MonolithDesignSystem.BodyText(
                    "Player values and wages retain imported source units. Currency and wage pay period are not inferred from a numeric field alone.",
                    10, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
                AddDetailRow(content, "Roster rows loaded", squadRows.Count.ToString("N0", CultureInfo.InvariantCulture));
                var squadGrid = CreateGrid();
                squadGrid.RowHeight = 36;
                squadGrid.Columns.Add(Column("PLAYER", "Name", 1.3));
                squadGrid.Columns.Add(Column("AGE", "Age", .4));
                squadGrid.Columns.Add(Column("POSITION", "Position", .7));
                squadGrid.Columns.Add(Column("CA", "DisplayAbility", .4));
                squadGrid.Columns.Add(Column("PA", "DisplayPotential", .4));
                squadGrid.ItemsSource = squadRows;
                squadGrid.Height = Math.Min(500, Math.Max(160, Math.Min(squadRows.Count, 14) * 38));
                squadGrid.MouseDoubleClick += async (_, _) =>
                {
                    if (squadGrid.SelectedItem is not PlayerRecord player) return;
                    _selectedPlayer = player;
                    await NavigateAsync("Players");
                    await ShowPlayerProfileAsync(player);
                };
                content.Children.Add(squadGrid);
            }
            content.Children.Add(MonolithDesignSystem.BodyText(
                "Finance rows may be historical snapshots. Confirm reporting dates and money units before relying on them.",
                10, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Club record tab data load failed.", ex);
            content.Children.Add(MonolithDesignSystem.MicroLabel("CLUB RECORD LOAD FAILED"));
            content.Children.Add(MonolithDesignSystem.BodyText(ex.Message, 11,
                MonolithDesignSystem.Solid(MonolithDesignSystem.Negative)));
        }
    }

    private ContextMenu BuildClubRecordTabContextMenuV9(TabItem tab)
    {
        var menu = new ContextMenu();
        var close = new MenuItem { Header = "Close club record" };
        close.Click += (_, _) => CloseRecordTabV9(tab);
        menu.Items.Add(close);
        var closeOthers = new MenuItem { Header = "Close other club records" };
        closeOthers.Click += (_, _) => CloseOtherRecordTabsV9(tab, closeToRight: false);
        menu.Items.Add(closeOthers);
        var closeRight = new MenuItem { Header = "Close club records to the right" };
        closeRight.Click += (_, _) => CloseOtherRecordTabsV9(tab, closeToRight: true);
        menu.Items.Add(closeRight);
        return menu;
    }

    private async Task RestoreOpenClubTabsV9Async(IReadOnlyList<AtlasClubRecord> clubs)
    {
        foreach (var club in clubs)
        {
            if (_activePage != "Clubs" || _profileTabs is null) return;
            try { await ShowClubRecordTabV9Async(club); }
            catch (Exception ex) { AtlasLog.Warning("Could not restore club record tab: " + ex.Message); }
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
    }
}
