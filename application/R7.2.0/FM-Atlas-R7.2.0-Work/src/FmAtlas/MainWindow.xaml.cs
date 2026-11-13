using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using FmAtlas.Data;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow : Window
{
    private static Brush Ink => MonolithTheme.GetBrush("TextPrimary");
    private static Brush Teal => MonolithTheme.GetBrush("Accent");
    private static Brush TealDark => MonolithTheme.GetBrush("AccentSoft");
    private static Brush Muted => MonolithTheme.GetBrush("TextMuted");
    private static Brush Line => MonolithTheme.GetBrush("Border");
    private static Brush Canvas => MonolithTheme.GetBrush("Canvas");
    private readonly ScoutDatabase _database = new();
    private readonly ObservableCollection<PlayerRecord> _players = new();
    private readonly ObservableCollection<AtlasClubRecord> _clubs = new();
    private readonly ObservableCollection<AtlasStaffRecord> _staff = new();
    private readonly ObservableCollection<ShortlistRecord> _shortlist = new();
    private Grid? _shell;
    private StackPanel? _navPanel;
    private ContentControl? _pageHost;
    private TextBlock? _pageTitle;
    private TextBlock? _pageSubTitle;
    private TextBlock? _connectionSummary;
    private Ellipse? _connectionDot;
    private Button? _liveLoadButton;
    private ProgressBar? _liveLoadProgress;
    private TextBlock? _liveLoadStatus;
    private TextBox? _globalSearchBox;
    private string _activePage = "Dashboard";
    private PlayerRecord? _selectedPlayer;
    private DataGrid? _playerResultsGrid;
    private Grid? _playerWorkspaceGrid;
    private ColumnDefinition? _playerResultsColumn;
    private ColumnDefinition? _playerProfileColumn;
    private bool _profileExpanded;
    private Button? _profileExpandButton;
    private TabControl? _profileTabs;
    private StackPanel? _profilePanel;
    private Button? _previousPlayerPageButton;
    private Button? _nextPlayerPageButton;
    private TextBlock? _playerPageStatus;
    private int _playerOffset;
    private const int PlayerPageSize = 250;
    private bool _navigationBusy;
    private bool _editorEnabled; // Local edits and guarded live editing are OFF by default on every launch.
    private string? _queuedNavigation;
    private string? _pendingClubSourceId;
    private string? _pendingStaffSourceId;
    private string? _pendingClubSearchQuery;
    private string? _pendingStaffSearchQuery;
    private int _playerSearchRequestId;
    private readonly Dictionary<string, PlayerRecord> _openPlayerRecords = new(StringComparer.OrdinalIgnoreCase);
    private TextBlock? _playerCountText;
    private TextBox? _playerSearch;
    private TextBox? _playerMinAge;
    private TextBox? _playerMaxAge;
    private TextBox? _playerMinCa;
    private TextBox? _playerMaxCa;
    private TextBox? _playerMinPa;
    private TextBox? _playerMaxPa;
    private TextBox? _attributeMinimum;
    private TextBox? _attributeMaximum;
    private TextBox? _leftFootMinimum;
    private TextBox? _rightFootMinimum;
    private ComboBox? _attributePicker;
    private ComboBox? _positionPicker;
    private ComboBox? _shortlistPicker;

    // Microsoft.Data.Sqlite's async ADO.NET methods may complete synchronously.
    // Run database work on a worker so long queries/imports never occupy the WPF dispatcher.
    private static Task<T> RunDatabaseAsync<T>(Func<Task<T>> operation) => Task.Run(operation);
    private static Task RunDatabaseAsync(Func<Task> operation) => Task.Run(operation);

    public MainWindow()
    {
        AtlasLog.Initialise();
        AtlasLog.Info("Main window construction started.");
        InitializeComponent();
        FontFamily = new FontFamily(MonolithTheme.FontFamilyName);
        Loaded += WindowLoaded;
        Closed += (_, _) => AtlasLog.Info("Main window closed.");
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                e.Handled = true;
                ShowGlobalSearch();
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.F)
            {
                e.Handled = true;
                ShowGlobalSearch();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W)
            {
                e.Handled = true;
                CloseCurrentProfileTab();
            }
        };
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunDatabaseAsync(() => _database.InitialiseAsync());
            BuildShell();
            RefreshConnectionStatus();
            await RefreshCareerStatusAsync();
            await NavigateAsync("Dashboard");
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Application startup/database initialisation failed.", ex);
            MessageBox.Show(this, "FM Atlas could not initialise its local database. Your existing database has not been deleted.\n\n" + ex.Message,
                "FM Atlas database error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BuildShell()
    {
        RootGrid.Children.Clear();
        _shell = new Grid();
        _shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(224) });
        _shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _shell.Background = Canvas;
        RootGrid.Children.Add(_shell);

        var sidebar = BuildSidebar();
        Grid.SetColumn(sidebar, 0); _shell.Children.Add(sidebar);

        var main = new Grid { Margin = new Thickness(22, 18, 22, 18) };
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(88) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(main, 1); _shell.Children.Add(main);

        var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _pageTitle = Txt("Dashboard", 27, true, Ink);
        _pageSubTitle = Txt("Career intelligence, recruitment and squad planning", 11, false, Muted);
        titleStack.Children.Add(_pageTitle); titleStack.Children.Add(_pageSubTitle);
        header.Children.Add(titleStack);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _globalSearchBox = Input("Search players, staff, clubs…");
        _globalSearchBox.Width = 250; _globalSearchBox.Height = 36; _globalSearchBox.Margin = new Thickness(0, 0, 7, 0);
        _globalSearchBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; ShowGlobalSearch(_globalSearchBox.Text); } };
        actions.Children.Add(_globalSearchBox);
        actions.Children.Add(SoftButton("Search", (_, _) => ShowGlobalSearch(_globalSearchBox.Text)));
        _liveLoadButton = PrimaryButton("Reload Game", async (_, _) => await LoadLiveCareerAsync());
        actions.Children.Add(_liveLoadButton);
        actions.Children.Add(PrimaryButton("Save Atlas", async (_, _) => await SaveAtlasAsync()));
        var saveGame = SoftButton("Save Game", (_, _) => MessageBox.Show(this,
            "Supported FM Atlas live edits are applied to the running game memory and should become visible after you press Continue in Football Manager.\n\nFM Atlas does not press Continue or save your career file for you. To keep the change, use Ctrl+S inside Football Manager after checking the result.\n\nOnly CA, PA and the currently supported visible player attributes can be written. Unsupported fields remain blocked. Save a separate career backup before testing.",
            "FM Atlas 27 · Save your career", MessageBoxButton.OK, MessageBoxImage.Information));
        saveGame.ToolTip = "Instructions for saving verified live edits in Football Manager.";
        actions.Children.Add(saveGame);
        actions.Children.Add(SoftButton("Import Save", async (_, _) => await ImportCareerSaveAsync()));
        actions.Children.Add(SoftButton("Diagnostics", (_, _) => ExportDiagnostics()));
        Grid.SetColumn(actions, 1); header.Children.Add(actions);
        Grid.SetRow(header, 0); main.Children.Add(header);

        var career = Card(new Grid(), 14);
        var careerGrid = (Grid)career.Child;
        careerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        careerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var careerStack = new StackPanel();
        careerStack.Children.Add(Txt("YOUR CAREER", 9, true, Teal));
        careerStack.Children.Add(Txt("Career identity awaiting verification", 16, true, Ink));
        _connectionSummary = Txt("Open Football Manager 27 and load a career, or import a local save copy.", 10, false, Muted);
        careerStack.Children.Add(_connectionSummary);
        _liveLoadStatus = Txt("Ready", 9, false, Muted); _liveLoadStatus.Margin = new Thickness(0, 5, 0, 0); careerStack.Children.Add(_liveLoadStatus);
        _liveLoadProgress = new ProgressBar { Height = 4, Minimum = 0, Maximum = 100, Value = 0, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 5, 0, 0), Foreground = Brush("#3988C8"), Background = Brush("#DCE8F2") }; careerStack.Children.Add(_liveLoadProgress);
        careerGrid.Children.Add(careerStack);
        var state = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _connectionDot = new Ellipse { Width = 9, Height = 9, Fill = Brush("#D9A44D"), Margin = new Thickness(0, 0, 7, 0) };
        state.Children.Add(_connectionDot); state.Children.Add(Txt("Not connected", 10, true, Muted));
        Grid.SetColumn(state, 1); careerGrid.Children.Add(state);
        Grid.SetRow(career, 1); main.Children.Add(career);

        _pageHost = new ContentControl { Margin = new Thickness(0, 15, 0, 0) };
        Grid.SetRow(_pageHost, 2); main.Children.Add(_pageHost);
    }

    private UIElement BuildSidebar()
    {
        var rail = new Border { Background = Brush("#111820"), BorderBrush = Brush("#303943"), BorderThickness = new Thickness(0, 0, 1, 0) };
        var dock = new DockPanel { Margin = new Thickness(15, 18, 15, 15) }; rail.Child = dock;
        var brand = new StackPanel { Margin = new Thickness(4, 3, 4, 24) };
        var logoLine = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var logoImage = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/FmAtlas;component/Assets/FM-Atlas-27-Icon.png", UriKind.Absolute)), Stretch = Stretch.UniformToFill };
        var logo = new Border { Width = 43, Height = 43, CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderBrush = Brush("#C9D9E8"), BorderThickness = new Thickness(1), Child = logoImage };
        logoLine.Children.Add(logo);
        var word = new StackPanel { Margin = new Thickness(10, 2, 0, 0) };
        word.Children.Add(Txt("FM ATLAS 27", 17, true, Ink)); word.Children.Add(Txt("FOOTBALL INTELLIGENCE", 8, true, Muted));
        logoLine.Children.Add(word); brand.Children.Add(logoLine); DockPanel.SetDock(brand, Dock.Top); dock.Children.Add(brand);

        var navScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _navPanel = new StackPanel(); navScroll.Content = _navPanel; dock.Children.Add(navScroll);
        AddNavGroup("WORKSPACE");
        AddNav("▦", "Dashboard"); AddNav("♟", "Players"); AddNav("⌕", "Recruitment"); AddNav("◆", "Value Finder"); AddNav("⌗", "Attribute Search"); AddNav("∑", "Stats Search"); AddNav("▥", "Squad Architect");
        AddNav("◇", "Clubs"); AddNav("♙", "Staff"); AddNav("⇄", "Transfers"); AddNav("▥", "Analytics"); AddNav("⚑", "Tactical Lab"); AddNav("◎", "Role Lab");
        AddNavGroup("MANAGEMENT"); AddNav("▤", "Shortlists"); AddNav("✎", "Live Editor"); AddNav("▦", "Mass Edit"); AddNav("◷", "Edit History");
        AddNavGroup("ATLAS TOOLS"); AddNav("✦", "Atlas Assistant"); AddNav("⚙", "Settings"); AddNav("◉", "Tester & Logs"); AddNav("?", "Help & Tutorial");

        var bottom = new Border { Background = Brush("#171F28"), BorderBrush = Brush("#303943"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(15), Padding = new Thickness(11), Margin = new Thickness(0, 14, 0, 0) };
        var bottomStack = new StackPanel(); bottomStack.Children.Add(Txt("GAME CONNECTION", 9, true, Teal));
        var stateRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 3) };
        stateRow.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = Brush("#D9A44D"), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        stateRow.Children.Add(Txt("Waiting for FM27", 10, true, Ink)); bottomStack.Children.Add(stateRow);
        bottomStack.Children.Add(Txt("Steam / Game Pass profile detection · guarded player edits", 8, false, Muted, wrap: true));
        DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);
        return rail;
    }

    private void AddNavGroup(string label)
    {
        if (_navPanel is null) return;
        _navPanel.Children.Add(Txt(label, 9, true, Teal) is var t ? Configure(t, new Thickness(7, 16, 0, 5)) : new TextBlock());
    }

    private static TextBlock Configure(TextBlock t, Thickness margin) { t.Margin = margin; return t; }

    private void AddNav(string icon, string label)
    {
        if (_navPanel is null) return;
        var button = new Button { Tag = label, Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { Txt(icon, 14, true, TealDark), Txt("  " + label, 12, true, Ink) } }, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 11, 10, 11), Margin = new Thickness(0, 2, 0, 2), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        button.Template = RoundedButtonTemplate(13);
        button.Click += async (_, _) => await NavigateAsync(label);
        _navPanel.Children.Add(button);
    }

    private async Task NavigateAsync(string page)
    {
        if (_pageHost is null) return;
        if (_navigationBusy)
        {
            _queuedNavigation = page;
            return;
        }

        _navigationBusy = true;
        foreach (var button in _navPanel?.Children.OfType<Button>() ?? Enumerable.Empty<Button>()) button.IsEnabled = false;
        try
        {
            _activePage = page;
            if (_pageTitle is not null) _pageTitle.Text = page;
            if (_pageSubTitle is not null) _pageSubTitle.Text = PageDescription(page);
            foreach (var button in _navPanel?.Children.OfType<Button>() ?? Enumerable.Empty<Button>())
            {
                var active = string.Equals(button.Tag?.ToString(), page, StringComparison.OrdinalIgnoreCase);
                button.Background = active ? Brush("#2A3427") : Brushes.Transparent;
                if (button.Content is StackPanel row)
                    foreach (var tb in row.Children.OfType<TextBlock>()) tb.Foreground = active ? Brush("#D8F5A2") : (tb.Text.Trim() == page ? Ink : Muted);
            }

            _pageHost.Content = NavigationLoadingPane(page);
            await Dispatcher.Yield(DispatcherPriority.Background);
            UIElement content = page switch
            {
                "Dashboard" => await DashboardPageAsync(),
                "Players" => await PlayerWorkspaceAsync(false),
                "Recruitment" => await PlayerWorkspaceAsync(true),
                "Clubs" => await ClubsPageAsync(),
                "Staff" => await StaffPageAsync(),
                "Squad Architect" => await SquadArchitectPageAsync(),
                "Transfers" => await TransfersPageAsync(),
                "Tactical Lab" => await TacticalLabPageAsync(),
                "Role Lab" => await RoleLabPageAsync(),
                "Shortlists" => await ShortlistsPageAsync(),
                "Live Editor" => await LiveEditorPageAsync(),
                "Edit History" => await EditHistoryPageAsync(),
                "Atlas Assistant" => AssistantPage(),
                "Settings" => await SettingsPageAsync(),
                "Help & Tutorial" => TutorialPage(),
                "Analytics" => await AnalyticsPageAsync(),
                "Stats Search" => await StatsSearchPageAsync(),
                "Attribute Search" => await AttributeSearchPageAsync(),
                "Value Finder" => await ValueFinderPageAsync(),
                "Mass Edit" => await MassEditPageAsync(),
                "Tester & Logs" => await TesterLogsPageAsync(),
                _ => DashboardPageFallback()
            };
            _pageHost.Content = content;
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Workspace navigation failed: " + page, ex);
            var panel = new StackPanel { Margin = new Thickness(26) };
            panel.Children.Add(Txt("WORKSPACE COULD NOT OPEN", 10, true, Brush("#A54E55")));
            panel.Children.Add(Txt(page, 24, true, Ink));
            panel.Children.Add(Txt(ex.Message, 12, false, Muted, wrap: true));
            panel.Children.Add(SoftButton("Return to Dashboard", async (_, _) => await NavigateAsync("Dashboard")));
            _pageHost.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
        finally
        {
            _navigationBusy = false;
            foreach (var button in _navPanel?.Children.OfType<Button>() ?? Enumerable.Empty<Button>()) button.IsEnabled = true;
            var queued = _queuedNavigation;
            _queuedNavigation = null;
            if (!string.IsNullOrWhiteSpace(queued) && !string.Equals(queued, page, StringComparison.OrdinalIgnoreCase))
                await NavigateAsync(queued);
        }
    }

    private static UIElement NavigationLoadingPane(string page)
    {
        var panel = new StackPanel { Margin = new Thickness(28), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Txt("OPENING WORKSPACE", 10, true, Brush("#C6F36B")));
        panel.Children.Add(Txt(page, 24, true, Brush("#E8EEF4")));
        panel.Children.Add(Txt("Preparing data and layout…", 12, false, Brush("#9EADB9")));
        return panel;
    }

    private void ShowGlobalSearch(string? initialQuery = null)
    {
        var search = new GlobalSearchWindow(_database, this, hit => _ = OpenGlobalSearchHitAsync(hit), initialQuery);
        search.ShowDialog();
    }

    private async Task OpenGlobalSearchHitAsync(GlobalSearchHit hit)
    {
        switch (hit.Record)
        {
            case PlayerRecord player:
                await NavigateAsync("Players");
                _selectedPlayer = player;
                try { await ShowPlayerProfileAsync(player); }
                catch (Exception ex)
                {
                    ClearProfile("The player record could not be opened: " + ex.Message);
                }
                break;
            case AtlasClubRecord club:
                _pendingClubSourceId = club.SourceId;
                _pendingClubSearchQuery = club.Name;
                await NavigateAsync("Clubs");
                break;
            case AtlasStaffRecord staff:
                _pendingStaffSourceId = staff.SourceId;
                _pendingStaffSearchQuery = staff.Name;
                await NavigateAsync("Staff");
                break;
        }
    }

    private static string PageDescription(string page) => page switch
    {
        "Dashboard" => "Your career at a glance · data coverage · import health",
        "Players" => "Player intelligence · search · profile · attributes",
        "Recruitment" => "Build an explainable shortlist from your imported career data",
        "Squad Architect" => "Inspect club squads, age distribution and positional coverage",
        "Clubs" => "Club directory · squad links · available save data",
        "Staff" => "Staff directory · coaching and staff fields where supported by the source",
        "Analytics" => "Season and match performance · metrics actually present in your save",
        "Stats Search" => "Multi-condition statistical discovery · grouped source metrics · per-90 sample controls",
        "Attribute Search" => "Build precise player profiles with up to 60 simultaneous 1–20 ratings",
        "Value Finder" => "Spot players whose ability and potential rank ahead of their market-value percentile",
        "Mass Edit" => "Bulk local database edits · preview and audit before applying",
        "Transfers" => "Transfer candidate discovery from available player and contract data",
        "Tactical Lab" => "Tactical and role analysis · only where the source provides enough verified inputs",
        "Role Lab" => "Transparent IP/OOP role-fit estimates from imported player attributes",
        "Tester & Logs" => "System checks · diagnostic export · application and build logs",
        "Shortlists" => "Manage your local recruitment lists",
        "Live Editor" => "Local database editor · live memory writes blocked pending field certification",
        "Edit History" => "Local FM Atlas data changes and audit trail",
        "Atlas Assistant" => "Search your local career snapshot using natural-language-style filters",
        "Settings" => "Data sources · save bridge · local storage · diagnostics",
        "Help & Tutorial" => "Quick start · player search · profiles · save imports · troubleshooting",
        _ => "FM Atlas 27"
    };

    private async Task<UIElement> DashboardPageAsync()
    {
        var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var hero = new Border { CornerRadius = new CornerRadius(23), Padding = new Thickness(24), Background = new LinearGradientBrush(Color.FromRgb(29, 39, 49), Color.FromRgb(18, 25, 33), 25), BorderBrush = Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 14) };
        var heroGrid = new Grid(); heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var heroLeft = new StackPanel(); heroLeft.Children.Add(Txt("WELCOME TO COACHPRO", 9, true, Brush("#B7E5E0")));
        heroLeft.Children.Add(Txt("A clearer view of your football world.", 25, true, Brushes.White));
        heroLeft.Children.Add(Txt("Load the active career for verified live identity data, or import a save copy to unlock the broader player and club intelligence layer.", 11, false, Brush("#DBF2EF"), wrap: true));
        heroGrid.Children.Add(heroLeft);
        var heroActions = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Width = 185 };
        heroActions.Children.Add(WhiteButton("Import career save", async (_, _) => await ImportCareerSaveAsync()));
        var liveButton = new Button { Content = "Load live game", Margin = new Thickness(3), Padding = new Thickness(12, 9, 12, 9), Foreground = Brush("#0D1117"), Background = Brush("#C6F36B"), BorderBrush = Brush("#4BA6A8"), Template = RoundedButtonTemplate(12), FontWeight = FontWeights.SemiBold };
        liveButton.Click += async (_, _) => await LoadLiveCareerAsync();
        heroActions.Children.Add(liveButton);
        Grid.SetColumn(heroActions, 1); heroGrid.Children.Add(heroActions); hero.Child = heroGrid; root.Children.Add(hero);

        var cards = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 14) };
        cards.Children.Add(MetricCard("PLAYER RECORDS", (metrics.SavePlayers + metrics.LivePlayers).ToString("N0"), metrics.SavePlayers > 0 ? $"{metrics.SavePlayers:N0} save snapshot · {metrics.LivePlayers:N0} live identities (sources may overlap)" : "Live identities only · not full attribute profiles", "♟"));
        cards.Children.Add(MetricCard("CLUB RECORDS", (metrics.SaveClubs + metrics.LiveClubs).ToString("N0"), metrics.SaveClubs > 0 ? $"{metrics.SaveClubs:N0} save snapshot · {metrics.LiveClubs:N0} live identities (sources may overlap)" : "Live club identities only · details depend on save import", "◇"));
        cards.Children.Add(MetricCard("STAFF", metrics.SaveStaff.ToString("N0"), metrics.SaveStaff > 0 ? "Save snapshot records" : "Not yet populated", "♙"));
        cards.Children.Add(MetricCard("ATTRIBUTE FACTS", metrics.LocalAttributeFacts.ToString("N0"), "Imported local values", "▤"));
        Grid.SetRow(cards, 1); root.Children.Add(cards);

        var lower = new Grid(); lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) }); lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        var coverage = Card(new StackPanel(), 17); var coverageStack = (StackPanel)coverage.Child;
        coverageStack.Children.Add(Txt("DATA COVERAGE", 9, true, Teal)); coverageStack.Children.Add(Txt("What Atlas currently knows", 19, true, Ink));
        AddCoverageRow(coverageStack, "Live player identities", metrics.LivePlayers, "FM27 in-memory read-only importer");
        AddCoverageRow(coverageStack, "Save snapshot players", metrics.SavePlayers, metrics.SavePlayers > 0 ? "FMSave local parser" : "Import a local career save copy");
        AddCoverageRow(coverageStack, "Save snapshot staff", metrics.SaveStaff, "Staff identity rows; attribute coverage depends on the parser");
        AddCoverageRow(coverageStack, "Imported attribute values", metrics.LocalAttributeFacts, "Only imported facts; no generated ratings");
        coverageStack.Children.Add(Txt("Live CA/PA and attribute reads remain build-specific. Candidate memory offsets are not promoted to verified fields automatically.", 10, false, Muted, wrap: true));
        Grid.SetColumn(coverage, 0); lower.Children.Add(coverage);

        var status = Card(new StackPanel(), 17); var statusStack = (StackPanel)status.Child;
        statusStack.Children.Add(Txt("CAREER SNAPSHOT", 9, true, Teal)); statusStack.Children.Add(Txt("Latest data import", 19, true, Ink));
        statusStack.Children.Add(Txt(metrics.LastSaveFileName ?? "No save snapshot imported yet", 12, true, Ink, wrap: true));
        if (!string.IsNullOrWhiteSpace(metrics.CurrentManager))
            statusStack.Children.Add(Txt($"Human manager: {metrics.CurrentManager}{(string.IsNullOrWhiteSpace(metrics.CurrentManagerClub) ? "" : " · " + metrics.CurrentManagerClub)}", 10, true, Teal, wrap: true));
        statusStack.Children.Add(Txt(metrics.LastSaveImportUtc is null ? "—" : "Imported " + FormatDate(metrics.LastSaveImportUtc), 10, false, Muted));
        if (!string.IsNullOrWhiteSpace(metrics.SaveBuild)) statusStack.Children.Add(Txt($"{metrics.SaveGame ?? "Football Manager"} · {metrics.SaveBuild} · In-game date {metrics.SaveGameDate ?? "unknown"}", 10, true, Teal, wrap: true));
        if (metrics.SaveBuildKnown == false) statusStack.Children.Add(Txt("UNVERIFIED BUILD LAYOUT — inspect reader report before relying on fields.", 9, true, Brush("#B45B2F"), wrap: true));
        if (string.Equals(metrics.SaveGame, "FM27", StringComparison.OrdinalIgnoreCase)) statusStack.Children.Add(Txt("FM27 field meanings remain unconfirmed by the parser. Compare important values with the game.", 9, false, Muted, wrap: true));
        statusStack.Children.Add(new Border { Height = 1, Background = Brush("#D9E5EF"), Margin = new Thickness(0, 12, 0, 10) });
        statusStack.Children.Add(Txt("Live connector", 11, true, Ink)); statusStack.Children.Add(Txt(metrics.LastLiveImportUtc is null ? "No successful live import recorded" : "Last completed " + FormatDate(metrics.LastLiveImportUtc), 10, false, Muted));
        statusStack.Children.Add(Txt("The local database at %LOCALAPPDATA%\\FmAtlas\\scouting.db is retained. Each save import is stored as a snapshot; a new import does not delete previous source records.", 10, false, Muted, wrap: true));
        Grid.SetColumn(status, 1); lower.Children.Add(status);
        Grid.SetRow(lower, 2); root.Children.Add(lower);
        return root;
    }

    private async Task<UIElement> PlayerWorkspaceAsync(bool recruitmentMode)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _playerWorkspaceGrid = root;
        _playerResultsColumn = new ColumnDefinition { Width = new GridLength(_profileExpanded ? 0 : .82, GridUnitType.Star) };
        _playerProfileColumn = new ColumnDefinition { Width = new GridLength(_profileExpanded ? 1 : 1.18, GridUnitType.Star) };
        root.ColumnDefinitions.Add(_playerResultsColumn);
        root.ColumnDefinitions.Add(_playerProfileColumn);

        var searchCard = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 10));
        var searchStack = (StackPanel)searchCard.Child;
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headline = new StackPanel();
        headline.Children.Add(Txt(recruitmentMode ? "RECRUITMENT WORKSPACE" : "PLAYER DATABASE", 9, true, Teal));
        _playerCountText = Txt("Search the career database", 18, true, Ink);
        headline.Children.Add(_playerCountText);
        headline.Children.Add(Txt("Search results are paged and virtualized. The 40K identity count is not a count of fully decoded player profiles or attribute sets.", 10, false, Muted, wrap: true));
        top.Children.Add(headline);
        var headButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        headButtons.Children.Add(SoftButton("⌕ Advanced Search", (_, _) => ShowGlobalSearch()));
        headButtons.Children.Add(SoftButton("Import report", async (_, _) => await ImportIntelligenceReportAsync()));
        headButtons.Children.Add(PrimaryButton("＋ Shortlist", async (_, _) => await AddSelectedToShortlistAsync()));
        Grid.SetColumn(headButtons, 1); top.Children.Add(headButtons);
        searchStack.Children.Add(top);

        var searchRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _playerSearch = Input("Search player, club, nation or UID");
        _playerSearch.Height = 40; _playerSearch.FontSize = 12; _playerSearch.Margin = new Thickness(0, 2, 8, 4);
        _playerSearch.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; _playerOffset = 0; await RefreshPlayerResultsAsync(); } };
        Grid.SetColumn(_playerSearch, 0); searchRow.Children.Add(_playerSearch);
        _positionPicker = new ComboBox { ItemsSource = new[] { "Any position", "ATTACKERS", "MIDFIELDERS", "DEFENDERS", "GOALKEEPERS", "GK", "DL", "DC", "DR", "WBL", "WBR", "DM", "ML", "MC", "MR", "AML", "AMC", "AMR", "ST" }, SelectedIndex = 0, Margin = new Thickness(0, 2, 8, 4), Padding = new Thickness(9), MinHeight = 40, FontSize = 11 };
        Grid.SetColumn(_positionPicker, 1); searchRow.Children.Add(_positionPicker);
        var searchButton = PrimaryButton("Search", async (_, _) => { _playerOffset = 0; await RefreshPlayerResultsAsync(); });
        searchButton.MinWidth = 88; searchButton.Height = 40; Grid.SetColumn(searchButton, 2); searchRow.Children.Add(searchButton);
        searchStack.Children.Add(searchRow);

        var positionRibbon = new WrapPanel { Margin = new Thickness(0, 7, 0, 1) };
        foreach (var category in new[] { "All", "Attackers", "Midfielders", "Defenders", "Goalkeepers" })
        {
            var chip = new Button { Content = category.ToUpperInvariant(), Tag = category, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 7, 0), Background = category == "All" ? Brush("#2D75B6") : Brush("#EAF1F8"), Foreground = category == "All" ? Brushes.White : Brush("#486477"), BorderBrush = Brushes.Transparent, FontWeight = FontWeights.SemiBold, FontSize = 9, Template = RoundedButtonTemplate(18), Cursor = Cursors.Hand };
            chip.Click += async (_, _) =>
            {
                _positionPicker!.SelectedItem = category switch { "Attackers" => "ATTACKERS", "Midfielders" => "MIDFIELDERS", "Defenders" => "DEFENDERS", "Goalkeepers" => "GOALKEEPERS", _ => "Any position" };
                foreach (var btn in positionRibbon.Children.OfType<Button>()) { var active = btn == chip; btn.Background = active ? Brush("#2D75B6") : Brush("#EAF1F8"); btn.Foreground = active ? Brush("#0D1117") : Brush("#9EADB9"); }
                _playerOffset = 0;
                await RefreshPlayerResultsAsync();
            };
            positionRibbon.Children.Add(chip);
        }
        searchStack.Children.Add(positionRibbon);
        Grid.SetRow(searchCard, 0); Grid.SetColumnSpan(searchCard, 2); root.Children.Add(searchCard);

        var listCard = Card(new DockPanel(), 16, new Thickness(0, 0, 9, 0));
        var listDock = (DockPanel)listCard.Child;
        var listHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        listHeader.Children.Add(Txt("PLAYER RESULTS", 9, true, Teal));
        listHeader.Children.Add(Txt("Select a row to open its profile tab · double-click to focus", 10, false, Muted));
        DockPanel.SetDock(listHeader, Dock.Top); listDock.Children.Add(listHeader);

        _playerResultsGrid = CreateGrid();
        _playerResultsGrid.FontSize = 11;
        _playerResultsGrid.RowHeight = 40;
        _playerResultsGrid.EnableRowVirtualization = true;
        _playerResultsGrid.EnableColumnVirtualization = true;
        VirtualizingPanel.SetIsVirtualizing(_playerResultsGrid, true);
        VirtualizingPanel.SetVirtualizationMode(_playerResultsGrid, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(_playerResultsGrid, true);
        _playerResultsGrid.Columns.Add(Column("PLAYER", "Name", 1.35));
        _playerResultsGrid.Columns.Add(Column("CLUB", "Club", 1.15));
        _playerResultsGrid.Columns.Add(Column("AGE", "Age", .45));
        _playerResultsGrid.Columns.Add(Column("POS", "Position", .55));
        _playerResultsGrid.Columns.Add(Column("CA", "DisplayAbility", .4));
        _playerResultsGrid.Columns.Add(Column("PA", "DisplayPotential", .4));
        _playerResultsGrid.Columns.Add(Column("NATION", "Nation", .8));
        _playerResultsGrid.ItemsSource = _players;
        _playerResultsGrid.SelectionChanged += async (_, _) =>
        {
            if (_playerResultsGrid.SelectedItem is PlayerRecord player)
            {
                _selectedPlayer = player;
                await ShowPlayerProfileAsync(player);
            }
        };
        listDock.Children.Add(_playerResultsGrid);

        var pageBar = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        _playerPageStatus = Txt("Search to begin", 9, false, Muted);
        DockPanel.SetDock(_playerPageStatus, Dock.Left); pageBar.Children.Add(_playerPageStatus);
        var pageButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _previousPlayerPageButton = SoftButton("← Previous", async (_, _) => { _playerOffset = Math.Max(0, _playerOffset - PlayerPageSize); await RefreshPlayerResultsAsync(); });
        _nextPlayerPageButton = SoftButton("Next →", async (_, _) => { _playerOffset += PlayerPageSize; await RefreshPlayerResultsAsync(); });
        _previousPlayerPageButton.IsEnabled = false; _nextPlayerPageButton.IsEnabled = false;
        pageButtons.Children.Add(_previousPlayerPageButton); pageButtons.Children.Add(_nextPlayerPageButton);
        DockPanel.SetDock(pageButtons, Dock.Right); pageBar.Children.Add(pageButtons);
        DockPanel.SetDock(pageBar, Dock.Bottom); listDock.Children.Add(pageBar);
        Grid.SetRow(listCard, 2); Grid.SetColumn(listCard, 0); root.Children.Add(listCard);

        var profileCard = Card(new TabControl { FontSize = 11, Background = Brush("#171F28") }, 16, new Thickness(0, 0, 0, 0));
        _profileTabs = (TabControl)profileCard.Child;
        _profileTabs.SelectionChanged += (_, _) =>
        {
            if (_profileTabs.SelectedItem is TabItem selected && selected.Content is ScrollViewer scroller && scroller.Content is StackPanel selectedPanel)
                _profilePanel = selectedPanel;
        };
        AddWelcomeProfileTab();
        Grid.SetRow(profileCard, 2); Grid.SetColumn(profileCard, 1); root.Children.Add(profileCard);
        foreach (var openedPlayer in _openPlayerRecords.Values.ToList())
            await ShowPlayerProfileAsync(openedPlayer);
        if (_profileExpanded) ToggleProfileExpanded(true);

        await Task.CompletedTask;
        return root;
    }

    private void AddWelcomeProfileTab()
    {
        if (_profileTabs is null) return;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(Txt("PLAYER CENTRE", 9, true, Teal));
        panel.Children.Add(Txt("Open a player profile", 23, true, Ink));
        panel.Children.Add(Txt("Use Global Search (Ctrl+F) or the Players search field to find a player. Each opened record gets its own closable tab; use Ctrl+W to close the selected profile. Your open profiles are kept while you move between workspaces.", 11, false, Muted, wrap: true));
        panel.Children.Add(Txt("The full profile follows the familiar Football Manager order: Technical, Set Pieces, Mental, Physical, Goalkeeping and Hidden. Atlas displays imported values only; it does not fabricate ratings.", 10, false, Muted, wrap: true));
        var tab = new TabItem { Header = "Welcome", Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Tag = "__welcome" };
        _profileTabs.Items.Add(tab); _profileTabs.SelectedItem = tab; _profilePanel = panel;
    }

    private static FrameworkElement FilterControl(string label, FrameworkElement control, double width)
    {
        var panel = new StackPanel { Width = width, Margin = new Thickness(0, 0, 8, 0) };
        panel.Children.Add(Txt(label, 8, true, Muted)); panel.Children.Add(control); return panel;
    }

    private async Task ImportIntelligenceReportAsync()
    {
        var open = new OpenFileDialog { Title = "Import a verified FM Atlas player intelligence report", Filter = "CSV reports (*.csv)|*.csv|All files (*.*)|*.*", CheckFileExists = true };
        if (open.ShowDialog(this) != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            AtlasLog.Info("Starting player intelligence CSV import.");
            var result = await RunDatabaseAsync(() => _database.ImportFullIntelligenceReportAsync(open.FileName));
            AtlasLog.Info($"Player intelligence CSV import completed: matched={result.MatchedPlayers}, facts={result.FactsImported}.");
            MessageBox.Show(this, $"Intelligence report imported.\n\nMatched players: {result.MatchedPlayers:N0}\nFacts imported: {result.FactsImported:N0}\n\nOnly fields present in the report were imported.", "FM Atlas · Intelligence report", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshPlayerResultsAsync();
        }
        catch (Exception ex) { MessageBox.Show(this, "The intelligence report was not imported.\n\n" + ex.Message, "FM Atlas · Report import", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { Mouse.OverrideCursor = null; }
    }

    private async Task RefreshPlayerResultsAsync()
    {
        if (_playerResultsGrid is null || _playerCountText is null) return;
        var term = _playerSearch?.Text.Trim() ?? string.Empty;
        if (term.Length < 2)
        {
            ++_playerSearchRequestId; // invalidate any search still completing in the background
            Mouse.OverrideCursor = null;
            _players.Clear();
            _playerCountText.Text = "Search the career database";
            _playerPageStatus!.Text = "Enter at least two characters · the full player database is not loaded into the screen";
            _previousPlayerPageButton!.IsEnabled = false;
            _nextPlayerPageButton!.IsEnabled = false;
            ClearProfile("Choose a player from search results. Your open profile tabs will remain available.");
            return;
        }

        var requestId = ++_playerSearchRequestId;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (_playerPageStatus is not null) _playerPageStatus.Text = "Searching…";
            var pos = _positionPicker?.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(pos) || pos == "Any position") pos = null;
            // Query enough from each source to construct one correctly ordered merged page.
            // Applying the same OFFSET independently to both sources creates skipped/duplicated
            // records whenever save and live rows overlap.
            var mergedFetchLimit = Math.Clamp(_playerOffset + PlayerPageSize + 1, PlayerPageSize + 1, 100000);
            var localTask = RunDatabaseAsync(() => _database.SearchAsync(term, null, null, pos, null, null, null, limit: mergedFetchLimit, offset: 0));
            var liveTask = RunDatabaseAsync(() => _database.SearchLivePlayersAsync(term, mergedFetchLimit, ageAsOf: DateTime.Today, offset: 0));
            var localCountTask = RunDatabaseAsync(() => _database.CountLocalPlayersAsync(term));
            var liveCountTask = RunDatabaseAsync(() => _database.CountLivePlayersAsync(term));
            await Task.WhenAll(localTask, liveTask, localCountTask, liveCountTask);

            var local = await localTask;
            var live = await liveTask;
            // Merge/deduplicate/sort on a worker too: deep pages can contain tens of thousands
            // of candidates even though only one page of rows is ultimately displayed.
            var (results, mergedHasMore) = await Task.Run(() =>
            {
                static string IdentityKey(PlayerRecord item) =>
                    item.Name.Trim().ToUpperInvariant() + "|" + (item.Club ?? string.Empty).Trim().ToUpperInvariant() + "|" + (item.Age?.ToString() ?? "?");
                static int Completeness(PlayerRecord item) =>
                    (item.Age.HasValue ? 1 : 0) + (item.Nation is not null ? 1 : 0) +
                    (item.League is not null ? 1 : 0) + (item.Position is not null ? 1 : 0) +
                    (item.CurrentAbility.HasValue ? 1 : 0) + (item.PotentialAbility.HasValue ? 1 : 0) +
                    (item.Value.HasValue ? 1 : 0) + (item.PreferredFoot is not null ? 1 : 0) +
                    (item.LeftFoot.HasValue ? 1 : 0) + (item.RightFoot.HasValue ? 1 : 0);
                // Prefer the richest source row when the same player appears in both indexes.
                var merged = local.Concat(live)
                    .GroupBy(IdentityKey, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.OrderByDescending(Completeness).First())
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Club, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var hasMore = merged.Count > _playerOffset + PlayerPageSize;
                var page = merged.Skip(_playerOffset).Take(PlayerPageSize).ToList();
                return (page, hasMore);
            });
            var countLocal = await localCountTask;
            var countLive = await liveCountTask;
            if (requestId != _playerSearchRequestId) return;
            // The counts describe source rows, not unique entities; they are displayed as such.
            var total = countLocal + countLive;

            _players.Clear();
            foreach (var item in results) _players.Add(item);
            _playerCountText.Text = $"{total:N0} source records · {countLive:N0} live identities · {countLocal:N0} save/local records";
            _playerPageStatus!.Text = results.Count == 0
                ? "No matches · adjust the search or filters"
                : $"Showing {_playerOffset + 1:N0}–{_playerOffset + results.Count:N0} · {countLive:N0} live identities · {countLocal:N0} save/local records";
            _previousPlayerPageButton!.IsEnabled = _playerOffset > 0;
            _nextPlayerPageButton!.IsEnabled = results.Count == PlayerPageSize && mergedHasMore;

            if (results.Count == 0) ClearProfile("No records matched. Check spelling, try a shorter term, or use Advanced Search.");
            else if (_playerResultsGrid.SelectedItem is null) _playerResultsGrid.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            if (requestId == _playerSearchRequestId) _playerPageStatus!.Text = "Search failed: " + ex.Message;
        }
        finally { if (requestId == _playerSearchRequestId) Mouse.OverrideCursor = null; }
    }

    private void RenderPlayerCards()
    {
        // Compatibility shim retained for existing callers. DataGrid owns its virtualized containers.
        if (_playerResultsGrid is not null) _playerResultsGrid.ItemsSource = _players;
    }

    private async Task ShowPlayerProfileAsync(PlayerRecord player)
    {
        if (_profileTabs is null) return;
        var tab = _profileTabs.Items.OfType<TabItem>().FirstOrDefault(x => string.Equals(x.Tag?.ToString(), player.SourceId, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
        {
            foreach (var welcome in _profileTabs.Items.OfType<TabItem>().Where(x => Equals(x.Tag, "__welcome")).ToList()) _profileTabs.Items.Remove(welcome);
            var panel = new StackPanel { Margin = new Thickness(16) };
            var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            tab = new TabItem { Tag = player.SourceId, Content = scroll };
            var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            header.Children.Add(new TextBlock { Text = player.Name, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush("#183B56"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 7, 0) });
            var close = new Button { Content = "×", Width = 20, Height = 20, Padding = new Thickness(0), Margin = new Thickness(0, 0, 1, 0), Background = Brush("#EAF1F5"), Foreground = Brush("#526B7D"), BorderBrush = Brushes.Transparent, FontSize = 13, Cursor = Cursors.Hand, ToolTip = "Close player tab" };
            close.Click += (_, _) =>
            {
                _openPlayerRecords.Remove(player.SourceId);
                _profileTabs.Items.Remove(tab);
                if (_profileTabs.Items.Count == 0) AddWelcomeProfileTab();
            };
            header.Children.Add(close);
            tab.Header = header;
            _profileTabs.Items.Add(tab);
            _openPlayerRecords[player.SourceId] = player;
        }
        else _openPlayerRecords[player.SourceId] = player;
        _profileTabs.SelectedItem = tab;
        var profilePanel = (StackPanel)((ScrollViewer)tab.Content).Content;
        _profilePanel = profilePanel;
        if (profilePanel.Children.Count > 0) return;
        profilePanel.Children.Clear();

        var attributesTask = RunDatabaseAsync(() => _database.GetPlayerAttributesAsync(player.SourceId));
        var generalTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "general"));
        var positionTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "positions"));
        var reputationTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "reputation"));
        var stateTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "state"));
        var contractTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "contract"));
        var traitsTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "traits"));
        var performanceTask = RunDatabaseAsync(() => _database.GetPlayerPerformanceFactsAsync(player.SourceId));
        Task<List<(string FieldKey, double? NumericValue, string? TextValue, string ValidationStatus)>>? liveFactsTask = null;
        if (player.SourceId.StartsWith("LIVE:", StringComparison.Ordinal) && long.TryParse(player.SourceId[5..], out var liveUid))
            liveFactsTask = RunDatabaseAsync(() => _database.GetLivePlayerFactsAsync(liveUid));
        await Task.WhenAll(new Task[] { attributesTask, generalTask, positionTask, reputationTask, stateTask, contractTask, traitsTask, performanceTask }.Concat(liveFactsTask is null ? Array.Empty<Task>() : new Task[] { liveFactsTask }));

        var attributes = await attributesTask;
        var generalFacts = await generalTask;
        var positionFacts = await positionTask;
        var reputationFacts = await reputationTask;
        var stateFacts = await stateTask;
        var contractFacts = await contractTask;
        var traits = await traitsTask;
        var performanceFacts = await performanceTask;
        var liveFacts = liveFactsTask is null ? new List<(string FieldKey, double? NumericValue, string? TextValue, string ValidationStatus)>() : await liveFactsTask;
        foreach (var fact in liveFacts.Where(f => f.NumericValue.HasValue && f.ValidationStatus.Contains("Verified", StringComparison.OrdinalIgnoreCase) && (f.FieldKey.Contains("Attribute", StringComparison.OrdinalIgnoreCase) || f.FieldKey.StartsWith("Ability.", StringComparison.OrdinalIgnoreCase))))
            attributes[fact.FieldKey] = fact.NumericValue!.Value;

        profilePanel.Children.Add(Txt("PLAYER PROFILE", 9, true, Teal));
        var hero = new Border { Background = new LinearGradientBrush(Color.FromRgb(29, 39, 49), Color.FromRgb(18, 25, 33), 25), BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(18), Margin = new Thickness(0, 8, 0, 13) };
        var heroGrid = new Grid();
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var graphicsUid = player.SourceId.StartsWith("FMSAVE:", StringComparison.OrdinalIgnoreCase) ? player.SourceId[7..]
            : player.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase) ? player.SourceId[5..] : null;
        var facePath = await Task.Run(() => GraphicsResolver.Shared.ResolveFace(graphicsUid));
        var avatarBorder = new Border { Width = 68, Height = 68, CornerRadius = new CornerRadius(14), Background = Brush("#202A35"), BorderBrush = Brush("#485562"), BorderThickness = new Thickness(1), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        if (!string.IsNullOrWhiteSpace(facePath) && File.Exists(facePath))
        {
            try
            {
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 160; bitmap.UriSource = new Uri(facePath, UriKind.Absolute); bitmap.EndInit(); bitmap.Freeze();
                avatarBorder.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
            }
            catch { avatarBorder.Child = Txt(Initials(player.Name), 22, true, Brush("#C6F36B")); }
        }
        else avatarBorder.Child = new TextBlock { Text = Initials(player.Name), FontSize = 21, FontWeight = FontWeights.Bold, Foreground = Brush("#C6F36B"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        heroGrid.Children.Add(avatarBorder);
        var heroStack = new StackPanel();
        heroStack.Children.Add(Txt(player.Name, 25, true, Ink));
        heroStack.Children.Add(Txt(player.Club ?? "Club unavailable", 12, true, Brush("#B9C5D0")));
        heroStack.Children.Add(Txt($"{player.Age?.ToString() ?? "—"} years  ·  {player.Nation ?? "Nationality unavailable"}  ·  {player.Position ?? "Position unavailable"}", 11, false, Brush("#B9C5D0"), wrap: true));
        heroStack.Children.Add(Txt($"{player.League ?? "League unavailable"}  ·  {player.DataOrigin}", 9, false, Brush("#9EADB9"), wrap: true));
        Grid.SetColumn(heroStack, 1); heroGrid.Children.Add(heroStack);
        var actions = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(WhiteButton("＋ Add to shortlist", async (_, _) => { _selectedPlayer = player; await AddSelectedToShortlistAsync(); }));
        _profileExpandButton = SoftButton(_profileExpanded ? "Show search results" : "Expand full profile", (_, _) => ToggleProfileExpanded(!_profileExpanded));
        actions.Children.Add(_profileExpandButton);
        actions.Children.Add(SoftButton("Open in Editor Studio", async (_, _) => { _selectedPlayer = player; await NavigateAsync("Live Editor"); }));
        Grid.SetColumn(actions, 2); heroGrid.Children.Add(actions); hero.Child = heroGrid; profilePanel.Children.Add(hero);

        var ability = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };
        var isLiveRecord = player.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase);
        ability.Children.Add(ProfileMetric("CURRENT ABILITY", player.DisplayAbility, player.CurrentAbility.HasValue ? (isLiveRecord ? "Live memory · provisional until UI witness" : "Imported save / local source") : "Not imported"));
        ability.Children.Add(ProfileMetric("POTENTIAL", player.DisplayPotential, player.PotentialAbility.HasValue ? (isLiveRecord ? "Live memory · provisional until UI witness" : "Imported save / local source") : "Not imported"));
        ability.Children.Add(ProfileMetric("VALUE", player.DisplayValue, "Stored source units"));
        ability.Children.Add(ProfileMetric("AGE", player.Age?.ToString() ?? "—", player.DateOfBirth?.ToString("dd MMM yyyy") ?? "Date of birth unavailable"));
        profilePanel.Children.Add(ability);

        var detailCard = new Border { Background = Brush("#171F28"), BorderBrush = Brush("#303943"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(13), Margin = new Thickness(0, 0, 0, 12) };
        var detailGrid = new UniformGrid { Columns = 2 };
        AddDetailRow(detailGrid, "Nationality", player.Nation ?? "Not decoded");
        AddDetailRow(detailGrid, "League", player.League ?? "Not decoded");
        AddDetailRow(detailGrid, "Natural positions", player.Position ?? "Not imported");
        AddDetailRow(detailGrid, "Preferred foot", player.PreferredFoot ?? "Not imported");
        AddDetailRow(detailGrid, "Left foot", player.LeftFoot?.ToString() ?? "—");
        AddDetailRow(detailGrid, "Right foot", player.RightFoot?.ToString() ?? "—");
        AddDetailRow(detailGrid, "Home-grown at club", player.HomeGrownClub.HasValue ? (player.HomeGrownClub.Value ? "Yes" : "No") : "Unknown");
        AddDetailRow(detailGrid, "Home-grown nation", player.HomeGrownNation.HasValue ? (player.HomeGrownNation.Value ? "Yes" : "No") : "Unknown");
        detailCard.Child = detailGrid;
        profilePanel.Children.Add(detailCard);

        if (player.SourceId.StartsWith("FMSAVE:", StringComparison.Ordinal))
            profilePanel.Children.Add(Txt("DATA QUALITY NOTE · CA/PA, attributes and several FM27 save fields are currently parser-derived and not independently verified for every game build. Values are shown only when the imported snapshot actually contains them; confirm critical edits in Football Manager.", 10, false, Brush("#8B622C"), wrap: true));
        var liveStatus = player.SourceId.StartsWith("LIVE:", StringComparison.Ordinal)
            ? (liveFacts.Count == 0
                ? "LIVE DATA COVERAGE · No live fact rows are stored for this player yet. Refresh the live read after loading a career."
                : $"LIVE DATA COVERAGE · {liveFacts.Count:N0} profile facts stored. CA, PA, 52 player attributes and position ratings are provisional layout reads until checked against the Football Manager UI; unverified facts are labelled and are not treated as independent proof.")
            : "";
        if (!string.IsNullOrWhiteSpace(liveStatus)) profilePanel.Children.Add(Txt(liveStatus, 10, true, Brush("#84612B"), wrap: true));

        AddOrderedAttributeSections(profilePanel, attributes);
        AddPositionSuitabilityMap(profilePanel, positionFacts, liveFacts);
        AddProfileFactsSection(profilePanel, "POSITION SUITABILITY · SOURCE VALUES", positionFacts, 36);
        AddProfileFactsSection(profilePanel, "PERSONAL & GENERAL INFORMATION", generalFacts, 30);
        AddProfileFactsSection(profilePanel, "CONTRACT & REGISTRATION", contractFacts, 30);
        AddProfileFactsSection(profilePanel, "CONDITION & STATUS", stateFacts, 30);
        AddProfileFactsSection(profilePanel, "PLAYER TRAITS", traits, 24);
        AddProfileFactsSection(profilePanel, "REPUTATION · FIELD MEANINGS UNCONFIRMED", reputationFacts, 30);
        if (performanceFacts.Count == 0)
            profilePanel.Children.Add(Txt("PERFORMANCE DATA · No player-season, career-history or match-stat rows for this player were supplied by the latest import. Re-import a save snapshot with those tables or add an FM View Exporter CSV.", 9, false, Muted, wrap: true));
        else
        {
            profilePanel.Children.Add(Txt($"PERFORMANCE INTELLIGENCE · {performanceFacts.Count:N0} numeric source facts retained", 9, true, Teal) is var performanceHeading ? Configure(performanceHeading, new Thickness(0, 18, 0, 6)) : new TextBlock());
            foreach (var group in performanceFacts.GroupBy(x => x.Group).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                profilePanel.Children.Add(Txt(group.Key.ToUpperInvariant(), 8, true, Teal) is var statsHeading ? Configure(statsHeading, new Thickness(0, 9, 0, 4)) : new TextBlock());
                var statsGrid = new UniformGrid { Columns = 2 };
                foreach (var fact in group) AddDetailRow(statsGrid, fact.Field, fact.Value ?? "—");
                profilePanel.Children.Add(statsGrid);
            }
        }

        profilePanel.Children.Add(Txt("DATA PROVENANCE", 9, true, Teal) is var sourceTitle ? Configure(sourceTitle, new Thickness(0, 18, 0, 5)) : new TextBlock());
        profilePanel.Children.Add(Txt($"Record: {player.SourceId}\nOrigin: {player.DataOrigin}\nGenerated-player status: {player.RegenStatus} · {player.RegenConfidence}", 9, false, Muted, wrap: true));
    }

    private static Border ProfileMetric(string label, string value, string note)
    {
        var panel = new StackPanel();
        panel.Children.Add(Txt(label, 8, true, Brush("#9EADB9")));
        panel.Children.Add(Txt(value, 22, true, Brush("#E8EEF4")));
        panel.Children.Add(Txt(note, 8, false, Brush("#9EADB9"), wrap: true));
        return new Border { Background = Brush("#171F28"), BorderBrush = Brush("#303943"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(11), Margin = new Thickness(0, 0, 8, 0), Child = panel };
    }

    private static void AddOrderedAttributeSections(Panel panel, Dictionary<string, double> values)
    {
        string Normalize(string value)
        {
            var last = value.Split(new[] { '·', '.', ':' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? value;
            return Regex.Replace(last, "[^A-Za-z0-9]", "").ToLowerInvariant();
        }
        var lookup = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in values)
        {
            var key = Normalize(item.Key);
            if (!lookup.ContainsKey(key)) lookup[key] = item.Value;
        }
        if (values.Count == 0)
        {
            panel.Children.Add(new Border
            {
                Background = Brush("#30291D"), BorderBrush = Brush("#66563A"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 10),
                Child = Txt("NO ATTRIBUTE VALUES IMPORTED · The sections below show the Football Manager attribute catalogue, but this source has not supplied ratings for the player. Blank values are not zero and are never guessed.", 10, true, Brush("#E6C477"), wrap: true)
            });
        }
        var fields = PlayerAttributeSchema.Fields;
        foreach (var group in fields.GroupBy(x => x.Category))
        {
            var section = new StackPanel { Margin = new Thickness(0, 13, 0, 2) };
            section.Children.Add(Txt(group.Key.ToUpperInvariant(), 10, true, Teal));
            var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 5, 0, 0) };
            foreach (var field in group)
            {
                var hasValue = lookup.TryGetValue(Normalize(field.Label), out var value);
                grid.Children.Add(AttributeCell(field.Label, hasValue ? value.ToString("0.#") : "—", hasValue ? value : (double?)null));
            }
            section.Children.Add(grid);
            if (group.Key.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                section.Children.Add(Txt("Hidden attributes are shown only when present in imported facts. Candidate memory offsets are not treated as proof of a decoded value.", 9, false, Muted, wrap: true));
            panel.Children.Add(section);
        }
        var known = new HashSet<string>(fields.Select(x => Normalize(x.Label)), StringComparer.OrdinalIgnoreCase);
        var extras = values.Where(x => !known.Contains(Normalize(x.Key)) && !x.Key.StartsWith("Ability.", StringComparison.OrdinalIgnoreCase)).Take(80).ToList();
        if (extras.Count > 0)
        {
            panel.Children.Add(Txt("OTHER IMPORTED ATTRIBUTE FACTS", 9, true, Teal) is var title ? Configure(title, new Thickness(0, 15, 0, 5)) : new TextBlock());
            var extraGrid = new UniformGrid { Columns = 2 };
            foreach (var item in extras) extraGrid.Children.Add(AttributeCell(item.Key, item.Value.ToString("0.#"), item.Value));
            panel.Children.Add(extraGrid);
        }
    }

    private static Border AttributeCell(string label, string value, double? numeric)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 7, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = Txt(label, 10, false, Brush("#C4CED8"));
        row.Children.Add(name);
        var valueBrush = !numeric.HasValue ? Brush("#9EADB9") : numeric.Value >= 15 ? Brush("#8BCFA6") : numeric.Value >= 11 ? Brush("#E6C477") : Brush("#F08C96");
        var pill = new Border { Background = !numeric.HasValue ? Brush("#202A35") : numeric.Value >= 15 ? Brush("#20342C") : numeric.Value >= 11 ? Brush("#30291D") : Brush("#35252A"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 3, 8, 3), MinWidth = 34, Child = Txt(value, 10, true, valueBrush) };
        Grid.SetColumn(pill, 1); row.Children.Add(pill);
        return new Border { BorderBrush = Brush("#303943"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(3, 5, 3, 5), Child = row };
    }

    private static void AddProfileFactsSection(Panel panel, string title, List<AtlasFactRecord> facts, int limit)
    {
        if (facts.Count == 0) return;
        panel.Children.Add(Txt(title, 9, true, Teal) is var heading ? Configure(heading, new Thickness(0, 16, 0, 5)) : new TextBlock());
        var grid = new UniformGrid { Columns = 2 };
        foreach (var fact in facts) AddDetailRow(grid, fact.Field.Replace('_', ' ').Replace('.', ' '), fact.Value);
        panel.Children.Add(grid);
    }

    private void ToggleProfileExpanded(bool? expanded = null)
    {
        _profileExpanded = expanded ?? !_profileExpanded;
        if (_playerResultsColumn is null || _playerProfileColumn is null) return;
        _playerResultsColumn.Width = new GridLength(_profileExpanded ? 0 : .82, GridUnitType.Star);
        _playerProfileColumn.Width = new GridLength(_profileExpanded ? 1 : 1.18, GridUnitType.Star);
        if (_profileExpandButton is not null) _profileExpandButton.Content = _profileExpanded ? "Show search results" : "Expand full profile";
    }

    private void CloseCurrentProfileTab()
    {
        if (_profileTabs?.SelectedItem is not TabItem tab || Equals(tab.Tag, "__welcome")) return;
        var sourceId = tab.Tag?.ToString();
        if (!string.IsNullOrWhiteSpace(sourceId)) _openPlayerRecords.Remove(sourceId);
        _profileTabs.Items.Remove(tab);
        if (_profileTabs.Items.Count == 0) AddWelcomeProfileTab();
    }

    private void ClearProfile(string message)
    {
        if (_profileTabs is null) return;
        if (_profileTabs.Items.Count == 0) { AddWelcomeProfileTab(); return; }
        if (_profileTabs.SelectedItem is not TabItem tab || tab.Tag?.ToString() != "__welcome")
        {
            var welcome = _profileTabs.Items.OfType<TabItem>().FirstOrDefault(x => Equals(x.Tag, "__welcome"));
            if (welcome is null) { AddWelcomeProfileTab(); welcome = _profileTabs.Items.OfType<TabItem>().FirstOrDefault(x => Equals(x.Tag, "__welcome")); }
            if (welcome is not null) _profileTabs.SelectedItem = welcome;
        }
        _profilePanel = _profileTabs.SelectedItem is TabItem selected && selected.Content is ScrollViewer viewer ? (StackPanel)viewer.Content : null;
        if (_profilePanel is null) return;
        _profilePanel.Children.Clear();
        _profilePanel.Children.Add(Txt("PLAYER CENTRE", 9, true, Teal));
        _profilePanel.Children.Add(Txt("Open a player profile", 21, true, Ink));
        _profilePanel.Children.Add(Txt(message, 10, false, Muted, wrap: true));
    }

    private async Task<UIElement> ClubsPageAsync()
    {
        const int pageSize = 200;
        var clubOffset = 0;
        var clubRequestVersion = 0;
        var clubProfileVersion = 0;
        Button previous = null!;
        Button next = null!;
        StackPanel stack = null!;
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.08, GridUnitType.Star) });
        var left = Card(new DockPanel(), 15, new Thickness(0, 0, 10, 0));
        var dock = (DockPanel)left.Child;
        var top = new StackPanel();
        top.Children.Add(Txt("CLUB INTELLIGENCE", 9, true, Teal));
        top.Children.Add(Txt("Club directory", 20, true, Ink));
        top.Children.Add(Txt("Search the complete club index. Only one page is loaded into the grid at a time.", 10, false, Muted, wrap: true));
        var search = Input("Search club name or ID");
        search.Margin = new Thickness(0, 8, 0, 5);
        if (!string.IsNullOrWhiteSpace(_pendingClubSearchQuery)) { search.Text = _pendingClubSearchQuery; _pendingClubSearchQuery = null; }
        top.Children.Add(search);
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);

        var dg = CreateGrid();
        dg.Columns.Add(Column("CLUB", "Name", 1.8));
        dg.Columns.Add(Column("NATION", "Nation", .8));
        dg.Columns.Add(Column("PLAYERS", "PlayerCount", .6));
        dg.Columns.Add(Column("SOURCE", "DataSource", 1.1));
        dock.Children.Add(dg);
        var footer = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var listStatus = Txt("Loading club index…", 9, false, Muted);
        DockPanel.SetDock(listStatus, Dock.Left); footer.Children.Add(listStatus);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        previous = SoftButton("← Previous", async (_, _) => { clubOffset = Math.Max(0, clubOffset - pageSize); await Refresh(); });
        next = SoftButton("Next →", async (_, _) => { clubOffset += pageSize; await Refresh(); });
        previous.IsEnabled = false; next.IsEnabled = false;
        buttons.Children.Add(previous); buttons.Children.Add(next);
        DockPanel.SetDock(buttons, Dock.Right); footer.Children.Add(buttons);
        DockPanel.SetDock(footer, Dock.Bottom); dock.Children.Add(footer);
        Grid.SetColumn(left, 0); root.Children.Add(left);

        var info = Card(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = new StackPanel() }, 15);
        stack = (StackPanel)((ScrollViewer)info.Child).Content;
        stack.Children.Add(Txt("CLUB PROFILE", 9, true, Teal));
        stack.Children.Add(Txt("Select a club", 20, true, Ink));
        stack.Children.Add(Txt("Club identity, squad links, staff, finances, facilities and stadium information appear when the imported save or live reader actually supplies them. Historical values are labelled as such.", 10, false, Muted, wrap: true));
        Grid.SetColumn(info, 1); root.Children.Add(info);

        async Task ShowClubSummaryAsync(AtlasClubRecord club)
        {
            var requestVersion = ++clubProfileVersion;
            var isLive = club.DataSource.Contains("live", StringComparison.OrdinalIgnoreCase);
            var (facts, squadRows) = await Task.Run(async () =>
            {
                var loadedFacts = isLive
                    ? await _database.GetLiveClubFactsAsync(club.SourceId).ConfigureAwait(false)
                    : await _database.GetFmsaveClubFactsAsync(club.SourceId).ConfigureAwait(false);
                var loadedSquad = await _database.GetPlayersForClubAsync(club.SourceId, isLive, 5000).ConfigureAwait(false);
                return (loadedFacts, loadedSquad);
            });
            if (requestVersion != clubProfileVersion) return;

            stack.Children.Clear();
            stack.Children.Add(Txt("CLUB PROFILE", 9, true, Teal));
            var clubUid = club.SourceId.StartsWith("FMSAVE:", StringComparison.OrdinalIgnoreCase) || club.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase)
                ? club.SourceId[(club.SourceId.IndexOf(':') + 1)..] : null;
            var clubLogoPath = await Task.Run(() => GraphicsResolver.Shared.ResolveClubLogo(clubUid));
            var clubHero = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 0, 8) };
            var clubLogo = new Border { Width = 58, Height = 58, Background = Brush("#202A35"), BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Margin = new Thickness(0, 0, 12, 0) };
            if (!string.IsNullOrWhiteSpace(clubLogoPath) && File.Exists(clubLogoPath))
            {
                try { var logo = new BitmapImage(); logo.BeginInit(); logo.CacheOption = BitmapCacheOption.OnLoad; logo.DecodePixelWidth = 128; logo.UriSource = new Uri(clubLogoPath, UriKind.Absolute); logo.EndInit(); logo.Freeze(); clubLogo.Child = new Image { Source = logo, Stretch = Stretch.Uniform }; }
                catch { clubLogo.Child = Txt("CLUB", 9, true, Teal); }
            }
            else clubLogo.Child = Txt("CLUB", 9, true, Teal);
            clubHero.Children.Add(clubLogo); clubHero.Children.Add(Txt(club.Name, 23, true, Ink, wrap: true));
            stack.Children.Add(clubHero);
            AddDetailRow(stack, "Nation", club.Nation ?? "Not decoded");
            AddDetailRow(stack, "Linked players", club.PlayerCount.ToString("N0"));
            AddDetailRow(stack, "Source", club.DataSource);
            AddDetailRow(stack, "Club record ID", club.SourceId);
            stack.Children.Add(Txt("CLUB DATA", 9, true, Teal) is var heading ? Configure(heading, new Thickness(0, 17, 0, 6)) : new TextBlock());

            if (facts.Count == 0)
                stack.Children.Add(Txt("No additional finance, facility, stadium or affiliation facts were decoded for this club. A club identity alone does not contain those details.", 10, false, Muted, wrap: true));
            foreach (var group in facts.GroupBy(x => x.Group))
            {
                stack.Children.Add(Txt(group.Key.Replace('_', ' ').ToUpperInvariant(), 8, true, Teal));
                foreach (var fact in group.Take(80)) AddDetailRow(stack, fact.Field.Replace('_', ' ').Replace('.', ' '), fact.Value);
            }

            stack.Children.Add(Txt("CLUB SQUAD", 9, true, Teal) is var squadTitle ? Configure(squadTitle, new Thickness(0, 18, 0, 6)) : new TextBlock());
            if (squadRows.Count == 0)
                stack.Children.Add(Txt("No player-to-club links were imported for this club. The identity reader can find club names without necessarily decoding the complete squad relationship for every record.", 10, false, Muted, wrap: true));
            else
            {
                AddDetailRow(stack, "Roster rows loaded", squadRows.Count.ToString("N0"));
                var squadGrid = CreateGrid(); squadGrid.RowHeight = 34;
                squadGrid.Columns.Add(Column("PLAYER", "Name", 1.3));
                squadGrid.Columns.Add(Column("AGE", "Age", .4));
                squadGrid.Columns.Add(Column("POSITION", "Position", .7));
                squadGrid.Columns.Add(Column("CA", "DisplayAbility", .4));
                squadGrid.Columns.Add(Column("PA", "DisplayPotential", .4));
                squadGrid.ItemsSource = squadRows;
                squadGrid.Height = Math.Min(430, Math.Max(145, Math.Min(squadRows.Count, 12) * 35));
                squadGrid.MouseDoubleClick += async (_, _) =>
                {
                    if (squadGrid.SelectedItem is PlayerRecord selectedPlayer)
                    {
                        _selectedPlayer = selectedPlayer;
                        await NavigateAsync("Players");
                        await ShowPlayerProfileAsync(selectedPlayer);
                    }
                };
                stack.Children.Add(squadGrid);
            }
            stack.Children.Add(Txt("Finance rows may be historical snapshots. Confirm reporting dates and money units before relying on them.", 9, false, Muted, wrap: true));
        }

        dg.SelectionChanged += async (_, _) =>
        {
            if (dg.SelectedItem is not AtlasClubRecord club) return;
            try { await ShowClubSummaryAsync(club); }
            catch (Exception ex)
            {
                stack.Children.Clear();
                stack.Children.Add(Txt("CLUB PROFILE COULD NOT LOAD", 10, true, Brush("#A54E55")));
                stack.Children.Add(Txt(ex.Message, 10, false, Muted, wrap: true));
            }
        };

        async Task Refresh()
        {
            var requestVersion = ++clubRequestVersion;
            var query = search.Text.Trim();
            listStatus.Text = "Loading clubs…";
            previous.IsEnabled = next.IsEnabled = false;
            try
            {
                var (rows, count) = await Task.Run(async () =>
                {
                    var page = await _database.SearchClubDirectoryAsync(query, pageSize, clubOffset).ConfigureAwait(false);
                    var total = await _database.CountClubDirectoryAsync(query).ConfigureAwait(false);
                    return (page, total);
                });
                if (requestVersion != clubRequestVersion) return;
                dg.ItemsSource = rows;
                listStatus.Text = $"{rows.Count:N0} shown · {count:N0} source records";
                previous.IsEnabled = clubOffset > 0;
                next.IsEnabled = clubOffset + rows.Count < count && rows.Count == pageSize;
                if (!string.IsNullOrWhiteSpace(_pendingClubSourceId))
                {
                    var target = rows.FirstOrDefault(x => x.SourceId == _pendingClubSourceId);
                    if (target is not null) { dg.SelectedItem = target; dg.ScrollIntoView(target); }
                    _pendingClubSourceId = null;
                }
                else if (rows.Count == 0) { stack.Children.Clear(); stack.Children.Add(Txt("No clubs found", 20, true, Ink)); }
                else if (dg.SelectedItem is null)
                {
                    stack.Children.Clear();
                    stack.Children.Add(Txt("CLUB PROFILE", 9, true, Teal));
                    stack.Children.Add(Txt("Select a club", 20, true, Ink));
                    stack.Children.Add(Txt("Choose a row to load its club facts and linked squad. FM Atlas does not fetch thousands of squad rows until you open a club.", 10, false, Muted, wrap: true));
                }
            }
            catch (Exception ex)
            {
                if (requestVersion != clubRequestVersion) return;
                listStatus.Text = "Club search failed: " + ex.Message;
                stack.Children.Clear();
                stack.Children.Add(Txt("CLUB DIRECTORY COULD NOT LOAD", 10, true, Brush("#A54E55")));
                stack.Children.Add(Txt(ex.Message, 10, false, Muted, wrap: true));
            }
        }

        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        debounce.Tick += async (_, _) => { debounce.Stop(); clubOffset = 0; await Refresh(); };
        search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
        await Refresh();
        return root;
    }

    private async Task<UIElement> StaffPageAsync()
    {
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = Card(new StackPanel(), 14, new Thickness(0, 0, 0, 10)); var s = (StackPanel)header.Child; s.Children.Add(Txt("STAFF INTELLIGENCE", 9, true, Teal)); s.Children.Add(Txt("Staff directory", 20, true, Ink));
        s.Children.Add(Txt("Staff identity, job, club, contract and preferences are shown where the save reader exposes them. The published reader currently locates only one rated staff attribute (adaptability); it does not provide the complete staff attribute panel shown in FM.", 10, false, Muted, wrap: true));
        Grid.SetRow(header, 0); root.Children.Add(header);
        var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.85, GridUnitType.Star) });
        var panel = Card(new DockPanel(), 14, new Thickness(0, 0, 9, 0)); var dock = (DockPanel)panel.Child; var search = Input("Search staff, club or ID");
        if (!string.IsNullOrWhiteSpace(_pendingStaffSearchQuery)) { search.Text = _pendingStaffSearchQuery; _pendingStaffSearchQuery = null; }
        DockPanel.SetDock(search, Dock.Top); dock.Children.Add(search);
        var staffStatus = Txt("Enter at least two characters to search the imported staff index.", 9, false, Muted, wrap: true); DockPanel.SetDock(staffStatus, Dock.Bottom); dock.Children.Add(staffStatus);
        var dg = CreateGrid(); dg.Columns.Add(Column("NAME", "Name", 1.3)); dg.Columns.Add(Column("ROLE", "Job", 1)); dg.Columns.Add(Column("CLUB", "Club", 1.2)); dg.Columns.Add(Column("AGE", "Age", .5)); dg.ItemsSource = _staff; dock.Children.Add(dg); Grid.SetColumn(panel, 0); body.Children.Add(panel);
        var profile = Card(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel() }, 14); var profileStack = (StackPanel)((ScrollViewer)profile.Child).Content;
        profileStack.Children.Add(Txt("STAFF PROFILE", 9, true, Teal)); profileStack.Children.Add(Txt("Select a staff member", 19, true, Ink)); profileStack.Children.Add(Txt("Staff profiles are limited to fields actually present in the imported save.", 10, false, Muted, wrap: true));
        Grid.SetColumn(profile, 1); body.Children.Add(profile); Grid.SetRow(body, 1); root.Children.Add(body);
        var staffProfileVersion = 0;
        async Task ShowStaffAsync(AtlasStaffRecord person)
        {
            var requestVersion = ++staffProfileVersion;
            var facts = await RunDatabaseAsync(() => _database.GetFmsaveStaffFactsAsync(person.SourceId));
            if (requestVersion != staffProfileVersion) return;
            profileStack.Children.Clear(); profileStack.Children.Add(Txt("STAFF PROFILE", 9, true, Teal)); profileStack.Children.Add(Txt(person.Name, 22, true, Ink));
            AddDetailRow(profileStack, "Role", person.Job); AddDetailRow(profileStack, "Club", person.Club); AddDetailRow(profileStack, "Age", person.Age?.ToString()); AddDetailRow(profileStack, "Nation ID (save-local)", person.Nation); AddDetailRow(profileStack, "Record ID", person.SourceId);
            profileStack.Children.Add(Txt("IMPORTED STAFF FACTS", 9, true, Teal) is var h ? Configure(h, new Thickness(0, 17, 0, 6)) : new TextBlock());
            if (facts.Count == 0) profileStack.Children.Add(Txt("No additional staff facts were imported.", 10, false, Muted));
            foreach (var group in facts.GroupBy(x => x.Group))
            {
                profileStack.Children.Add(Txt(group.Key.ToUpperInvariant(), 8, true, Teal));
                foreach (var fact in group.Take(50)) AddDetailRow(profileStack, fact.Field.Replace('_', ' ').Replace('.', ' '), fact.Value);
            }
        }

        dg.SelectionChanged += async (_, _) =>
        {
            if (dg.SelectedItem is not AtlasStaffRecord person) return;
            try { await ShowStaffAsync(person); }
            catch (Exception ex)
            {
                profileStack.Children.Clear();
                profileStack.Children.Add(Txt("STAFF PROFILE COULD NOT LOAD", 10, true, Brush("#A54E55")));
                profileStack.Children.Add(Txt(ex.Message, 10, false, Muted, wrap: true));
            }
        };
        var staffRequestVersion = 0;
        async Task Refresh()
        {
            var requestVersion = ++staffRequestVersion;
            var query = search.Text.Trim();
            if (query.Length < 2 && string.IsNullOrWhiteSpace(_pendingStaffSourceId))
            {
                _staff.Clear(); dg.ItemsSource = _staff;
                staffStatus.Text = "Type two or more characters. Staff records are not bulk-loaded on first open.";
                profileStack.Children.Clear(); profileStack.Children.Add(Txt("STAFF PROFILE", 9, true, Teal)); profileStack.Children.Add(Txt("Search for a staff member", 19, true, Ink));
                profileStack.Children.Add(Txt("This reader currently imports staff only from supported career-save snapshots. The live identity probe has not populated staff records.", 10, false, Muted, wrap: true));
                return;
            }
            staffStatus.Text = "Searching staff…";
            try
            {
                var rows = await RunDatabaseAsync(() => _database.SearchFmsaveStaffAsync(query));
                if (requestVersion != staffRequestVersion) return;
                _staff.Clear(); foreach (var r in rows) _staff.Add(r); dg.ItemsSource = _staff;
                staffStatus.Text = rows.Count == 0 ? "No staff records found in the imported sources. Import a supported career save." : $"{rows.Count:N0} staff records · bounded search results";
                if (!string.IsNullOrWhiteSpace(_pendingStaffSourceId)) { var target = rows.FirstOrDefault(x => x.SourceId == _pendingStaffSourceId); if (target is not null) { dg.SelectedItem = target; dg.ScrollIntoView(target); } _pendingStaffSourceId = null; }
            }
            catch (Exception ex)
            {
                if (requestVersion != staffRequestVersion) return;
                staffStatus.Text = "Staff search failed: " + ex.Message;
                profileStack.Children.Clear();
                profileStack.Children.Add(Txt("STAFF DIRECTORY COULD NOT LOAD", 10, true, Brush("#A54E55")));
                profileStack.Children.Add(Txt(ex.Message, 10, false, Muted, wrap: true));
            }
        }
        var staffSearchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        staffSearchDebounce.Tick += async (_, _) => { staffSearchDebounce.Stop(); await Refresh(); };
        search.TextChanged += (_, _) => { staffSearchDebounce.Stop(); staffSearchDebounce.Start(); };
        await Refresh();
        return root;
    }

    private async Task<UIElement> SquadArchitectPageAsync()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var (allPlayers, sourceLabel) = await Task.Run(async () =>
        {
            // Merge both sources: a partial save snapshot must not hide the much larger
            // live identity index (the earlier R5 implementation did exactly that).
            const int sourceLimit = 50000;
            var localRowsTask = _database.SearchAsync("", null, null, null, null, null, null, limit: sourceLimit);
            var liveRowsTask = _database.SearchLivePlayersAsync("", sourceLimit);
            await Task.WhenAll(localRowsTask, liveRowsTask).ConfigureAwait(false);
            var localRows = await localRowsTask.ConfigureAwait(false);
            var liveRows = await liveRowsTask.ConfigureAwait(false);
            // Do not merge people by name + club + age: homonyms and same-age players
            // can be silently collapsed. LIVE UIDs and FMSAVE persistent IDs are different
            // namespaces until a separately validated crosswalk exists. Deduplicate only
            // exact source IDs; retain cross-provider rows as distinct records for now.
            static int Completeness(PlayerRecord p) =>
                (p.Age.HasValue ? 1 : 0) + (p.Nation is not null ? 1 : 0) + (p.Position is not null ? 1 : 0) +
                (p.CurrentAbility.HasValue ? 1 : 0) + (p.PotentialAbility.HasValue ? 1 : 0) + (p.Value.HasValue ? 1 : 0) +
                (p.League is not null ? 1 : 0) + (p.PreferredFoot is not null ? 1 : 0);
            var merged = localRows.Concat(liveRows)
                .GroupBy(p => p.SourceId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(Completeness).First())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var label = $"Source-preserving view · {liveRows.Count:N0} live identities + {localRows.Count:N0} save/local records · cross-provider identity matching not assumed";
            return (merged, label);
        });

        var header = Card(new Grid(), 15, new Thickness(0, 0, 0, 10));
        var headerGrid = (Grid)header.Child;
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(Txt("SQUAD ARCHITECT", 9, true, Teal));
        title.Children.Add(Txt("Squad structure & succession", 21, true, Ink));
        title.Children.Add(Txt("Inspect imported club records, age profile and positional cover. Provider-specific identities are kept separate unless Atlas has a verified cross-provider identity link. Team slot IDs are retained, but senior/reserve/youth labels are not guessed.", 10, false, Muted, wrap: true));
        headerGrid.Children.Add(title);
        var filter = new StackPanel { Width = 245, VerticalAlignment = VerticalAlignment.Center };
        filter.Children.Add(Txt("CLUB", 8, true, Teal));
        var clubPicker = new ComboBox { Padding = new Thickness(9), Margin = new Thickness(0, 4, 0, 0), MinHeight = 38, IsTextSearchEnabled = true, MaxDropDownHeight = 480 };
        VirtualizingPanel.SetIsVirtualizing(clubPicker, true);
        VirtualizingPanel.SetVirtualizationMode(clubPicker, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(clubPicker, true);
        var clubs = allPlayers.Select(x => x.Club).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        clubPicker.ItemsSource = new[] { "All clubs" }.Concat(clubs).ToList(); clubPicker.SelectedIndex = 0;
        filter.Children.Add(clubPicker); Grid.SetColumn(filter, 1); headerGrid.Children.Add(filter);
        root.Children.Add(header);

        var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(metrics, 1); root.Children.Add(metrics);
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.8, GridUnitType.Star) });
        var rosterCard = Card(new DockPanel(), 15, new Thickness(0, 0, 8, 0));
        var rosterDock = (DockPanel)rosterCard.Child;
        var rosterTitle = new StackPanel(); rosterTitle.Children.Add(Txt("SQUAD ROSTER", 9, true, Teal)); rosterTitle.Children.Add(Txt("Imported player records", 17, true, Ink));
        DockPanel.SetDock(rosterTitle, Dock.Top); rosterDock.Children.Add(rosterTitle);
        var roster = CreateGrid();
        roster.Columns.Add(Column("PLAYER", "Name", 1.3)); roster.Columns.Add(Column("POSITION", "Position", .8)); roster.Columns.Add(Column("AGE", "Age", .45)); roster.Columns.Add(Column("CA", "DisplayAbility", .45)); roster.Columns.Add(Column("PA", "DisplayPotential", .45));
        rosterDock.Children.Add(roster); Grid.SetColumn(rosterCard, 0); Grid.SetRow(rosterCard, 0); body.Children.Add(rosterCard);

        var insightCard = Card(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel() }, 15);
        var insight = (StackPanel)((ScrollViewer)insightCard.Child).Content;
        Grid.SetColumn(insightCard, 1); Grid.SetRow(insightCard, 0); body.Children.Add(insightCard);
        root.Children.Add(body); Grid.SetRow(body, 2);

        async Task RefreshSquad()
        {
            var selectedClub = clubPicker.SelectedItem?.ToString();
            // Filtering, sorting, grouping and aggregate calculations can touch 40K rows.
            // Do all of that away from the dispatcher; only the compact summary is rendered here.
            var snapshot = await Task.Run(() =>
            {
                var squad = string.IsNullOrWhiteSpace(selectedClub) || selectedClub == "All clubs"
                    ? allPlayers
                    : allPlayers.Where(x => string.Equals(x.Club, selectedClub, StringComparison.OrdinalIgnoreCase)).ToList();
                var ordered = squad.OrderBy(x => x.Position).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var ages = squad.Where(x => x.Age.HasValue).Select(x => (double)x.Age!.Value).ToList();
                var cas = squad.Where(x => x.CurrentAbility.HasValue).Select(x => (double)x.CurrentAbility!.Value).ToList();
                var pas = squad.Where(x => x.PotentialAbility.HasValue).Select(x => (double)x.PotentialAbility!.Value).ToList();
                var positions = squad.GroupBy(x => string.IsNullOrWhiteSpace(x.Position) ? "Unspecified" : x.Position!)
                    .Select(g => (Position: g.Key, Count: g.Count())).OrderByDescending(x => x.Count).ThenBy(x => x.Position).ToList();
                var bands = new[]
                {
                    (Label: "U21", Min: 0, Max: 20), (Label: "21–24", Min: 21, Max: 24),
                    (Label: "25–29", Min: 25, Max: 29), (Label: "30+", Min: 30, Max: 100)
                }.Select(b => (b.Label, Count: squad.Count(x => x.Age.HasValue && x.Age.Value >= b.Min && x.Age.Value <= b.Max))).ToList();
                return (Squad: ordered, AverageAge: ages.Count == 0 ? (double?)null : ages.Average(),
                    AverageCa: cas.Count == 0 ? (double?)null : cas.Average(), AveragePa: pas.Count == 0 ? (double?)null : pas.Average(),
                    Positions: positions, AgeBands: bands);
            });
            roster.ItemsSource = snapshot.Squad;
            metrics.Children.Clear();
            metrics.Children.Add(MetricCard("PLAYERS", snapshot.Squad.Count.ToString("N0"), sourceLabel, "♟"));
            metrics.Children.Add(MetricCard("AVERAGE AGE", snapshot.AverageAge?.ToString("0.0") ?? "—", "Where available", "◷"));
            metrics.Children.Add(MetricCard("AVERAGE CA", snapshot.AverageCa?.ToString("0.0") ?? "—", "Parser values · unconfirmed", "↗"));
            metrics.Children.Add(MetricCard("AVERAGE PA", snapshot.AveragePa?.ToString("0.0") ?? "—", "Parser values · unconfirmed", "⌁"));
            insight.Children.Clear();
            insight.Children.Add(Txt("POSITIONAL COVER", 9, true, Teal)); insight.Children.Add(Txt("Imported primary-position counts", 18, true, Ink));
            foreach (var position in snapshot.Positions) AddDetailRow(insight, position.Position, position.Count.ToString("N0") + " players");
            insight.Children.Add(Txt("AGE PROFILE", 9, true, Teal) is var ageTitle ? Configure(ageTitle, new Thickness(0, 16, 0, 5)) : new TextBlock());
            foreach (var band in snapshot.AgeBands) AddDetailRow(insight, band.Label, band.Count.ToString("N0"));
            insight.Children.Add(Txt("SOURCE & LIMITS", 9, true, Teal) is var limits ? Configure(limits, new Thickness(0, 16, 0, 5)) : new TextBlock());
            insight.Children.Add(Txt("This view merges the live identity index and save/local player rows. Live-only records can lack positions, abilities, contracts and other fields. Team slot labels (Senior, B-team, Reserve or U21) are not guessed unless the source mapping is validated.", 9, false, Muted, wrap: true));
            if (snapshot.Squad.Count == 0) insight.Children.Add(Txt("No players are linked to this club in the current imported sources.", 10, false, Muted));
        }
        clubPicker.SelectionChanged += async (_, _) => await RefreshSquad();
        await RefreshSquad();
        return root;
    }

    private async Task<UIElement> TransfersPageAsync()
    {
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var head = Card(new StackPanel(), 14); var stack = (StackPanel)head.Child; stack.Children.Add(Txt("TRANSFER INTELLIGENCE", 9, true, Teal)); stack.Children.Add(Txt("Player market", 20, true, Ink));
        stack.Children.Add(Txt("This view ranks imported players by available value and ability. Transfer-list flags, asking prices and contract expiry should only be shown once imported from the save and mapped to the player; this build does not infer availability.", 10, false, Muted, wrap: true));
        root.Children.Add(head);
        var body = Card(new DockPanel(), 14); var grid = CreateGrid(); grid.Columns.Add(Column("PLAYER", "Name", 1.4)); grid.Columns.Add(Column("CLUB", "Club", 1.2)); grid.Columns.Add(Column("AGE", "Age", .4)); grid.Columns.Add(Column("CA", "DisplayAbility", .4)); grid.Columns.Add(Column("PA", "DisplayPotential", .4)); grid.Columns.Add(Column("VALUE", "DisplayValue", .8)); body.Child = grid; Grid.SetRow(body, 1); root.Children.Add(body);
        var ranked = await Task.Run(async () =>
        {
            var players = await _database.SearchAsync("", null, null, null, null, null, null, limit: 5000).ConfigureAwait(false);
            if (players.Count == 0) players = await _database.SearchLivePlayersAsync("", 5000).ConfigureAwait(false);
            return players.OrderByDescending(p => p.Value ?? 0).Take(1000).ToList();
        });
        grid.ItemsSource = ranked;
        return root;
    }

    private async Task<UIElement> TacticalLabPageAsync()
    {
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var tactics = await RunDatabaseAsync(() => _database.GetFmsaveExtraTableCountsAsync());
        var left = Card(new StackPanel(), 16); var s = (StackPanel)left.Child; s.Children.Add(Txt("TACTICAL INTELLIGENCE", 9, true, Teal)); s.Children.Add(Txt("Formation & role workspace", 21, true, Ink));
        s.Children.Add(Txt("The local snapshot retains tactical, selection and training tables when exposed by the save reader. This version does not yet decode every tactic field into a trustworthy formation or role-rating model.", 10, false, Muted, wrap: true));
        foreach (var t in tactics.Where(x => new[] { "tactics", "training", "selections", "set_pieces" }.Contains(x.TableName))) AddDetailRow(s, t.TableName.Replace('_', ' '), t.RowCount.ToString("N0") + " rows");
        Grid.SetColumn(left, 0); root.Children.Add(left);
        var right = Card(new StackPanel(), 16); var r = (StackPanel)right.Child; r.Children.Add(Txt("ROLE ANALYSIS", 9, true, Teal)); r.Children.Add(Txt("Explainable ratings only", 21, true, Ink));
        r.Children.Add(Txt("Role fit should be calculated from official role attribute weights, exact position familiarity and relevant duties. Until the FM27 mappings are validated, FM Atlas will not present its own rough weighted score as the game's internal role rating.", 10, false, Muted, wrap: true));
        AddDetailRow(r, "Inputs required", "Position ratings · attribute values · FM role weights"); AddDetailRow(r, "Current status", "Waiting for validated role schema"); Grid.SetColumn(right, 1); root.Children.Add(right);
        return root;
    }

    private async Task<UIElement> ShortlistsPageAsync()
    {
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });
        var left = Card(new DockPanel(), 14); var dock = (DockPanel)left.Child;
        var top = new StackPanel(); top.Children.Add(Txt("RECRUITMENT", 9, true, Teal)); top.Children.Add(Txt("My shortlists", 20, true, Ink));
        var controls = new StackPanel { Orientation = Orientation.Horizontal }; _shortlistPicker = new ComboBox { Width = 210, Margin = new Thickness(0, 5, 5, 9), Padding = new Thickness(8) }; controls.Children.Add(_shortlistPicker); controls.Children.Add(SoftButton("＋ New", async (_, _) => await CreateNewShortlistAsync())); top.Children.Add(controls); DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
        var dg = CreateGrid(); dg.Columns.Add(Column("PLAYER", "PlayerName", 1.2)); dg.Columns.Add(Column("CLUB", "DisplayClub", 1)); dg.Columns.Add(Column("POSITION", "DisplayPosition", .7)); dg.Columns.Add(Column("ADDED", "AddedUtc", 1)); dg.ItemsSource = _shortlist; dock.Children.Add(dg);
        var right = Card(new StackPanel(), 14); var s = (StackPanel)right.Child; s.Children.Add(Txt("SHORTLIST ACTIONS", 9, true, Teal)); s.Children.Add(Txt("Recruitment workflow", 19, true, Ink)); s.Children.Add(Txt("Add players from the player profile. Notes and groups are stored locally and remain available between application updates.", 10, false, Muted, wrap: true));
        s.Children.Add(SoftButton("Remove selected", async (_, _) => { if (dg.SelectedItem is ShortlistRecord item && _shortlistPicker.SelectedItem is string name) { await RunDatabaseAsync(() => _database.RemoveFromShortlistAsync(item.SourceId, name)); await RefreshShortlistRowsAsync(dg); } }));
        s.Children.Add(SoftButton("Export shortlist CSV", (_, _) => ExportShortlistCsv()));
        Grid.SetColumn(left, 0); root.Children.Add(left); Grid.SetColumn(right, 1); root.Children.Add(right);
        var names = await RunDatabaseAsync(() => _database.GetShortlistNamesAsync()); _shortlistPicker.ItemsSource = names; _shortlistPicker.SelectedIndex = 0;
        _shortlistPicker.SelectionChanged += async (_, _) => await RefreshShortlistRowsAsync(dg);
        await RefreshShortlistRowsAsync(dg);
        return root;
    }

    private async Task<UIElement> LiveEditorPageAsync()
    {
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 10)); var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("EDITOR WORKSPACE", 9, true, Teal)); hs.Children.Add(Txt("Editor Studio", 23, true, Ink));
        hs.Children.Add(Txt(_editorEnabled ? "Atlas Local Editor · ON for this session" : "Atlas Local Editor · OFF by default", 11, true, _editorEnabled ? Brush("#2D8A63") : Brush("#94652A")));
        hs.Children.Add(_editorEnabled
            ? SoftButton("Turn Editor OFF", async (_, _) => { _editorEnabled = false; AtlasLog.Info("FM Atlas editor disabled by user."); await NavigateAsync("Live Editor"); })
            : PrimaryButton("Enable Editor", async (_, _) => { _editorEnabled = true; AtlasLog.Info("FM Atlas editor enabled by user for this session; live writes remain individually gated."); await NavigateAsync("Live Editor"); }));
        var alert = new Border { Background = Brush("#342C1C"), BorderBrush = Brush("#66502C"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 6) };
        alert.Child = Txt("LOCAL ATLAS EDITS ONLY. Live Football Manager memory Apply and Undo are disabled by the build-wide certification gate. An executable/module profile match is not proof that individual field offsets and encodings are correct. Read-only inspection and preview work can continue; live writes will not be enabled until field-level witnesses and disposable offline career Apply/read-back/Undo tests have passed.", 10, true, Brush("#E6C477"), wrap: true);
        hs.Children.Add(alert);
        if (!_editorEnabled)
        {
            hs.Children.Add(Txt("The editor is locked. Enable it above to access local Atlas database edits. This switch does not enable or unlock writes to the running Football Manager game.", 11, false, Muted, wrap: true));
            Grid.SetRow(header, 0); root.Children.Add(header); return root;
        }
        if (_selectedPlayer is null)
        {
            hs.Children.Add(Txt("Choose a player in Players before opening the local editor.", 11, false, Muted));
            hs.Children.Add(SoftButton("Open Players", async (_, _) => await NavigateAsync("Players")));
            Grid.SetRow(header, 0); root.Children.Add(header); return root;
        }
        hs.Children.Add(Txt($"Selected player · {_selectedPlayer.Name} · {_selectedPlayer.Club ?? "Club unavailable"}", 11, true, Ink));
        Grid.SetRow(header, 0); root.Children.Add(header);

        var editor = Card(new DockPanel(), 16); var dock = (DockPanel)editor.Child;
        var controls = new StackPanel(); controls.Children.Add(Txt("ABILITY VALUES", 9, true, Teal));
        var abilityRow = new WrapPanel { Margin = new Thickness(0, 7, 0, 9) };
        var caInput = Input("Current Ability"); caInput.Width = 110; caInput.Text = _selectedPlayer.CurrentAbility?.ToString() ?? "";
        var paInput = Input("Potential Ability"); paInput.Width = 110; paInput.Text = _selectedPlayer.PotentialAbility?.ToString() ?? "";
        abilityRow.Children.Add(FieldLabel("CA")); abilityRow.Children.Add(caInput); abilityRow.Children.Add(FieldLabel("PA")); abilityRow.Children.Add(paInput);
        abilityRow.Children.Add(PrimaryButton("Save CA / PA locally", async (_, _) =>
        {
            if (!int.TryParse(caInput.Text, out var ca) || !int.TryParse(paInput.Text, out var pa) || ca < 0 || ca > 200 || pa < 0 || pa > 200)
            { MessageBox.Show(this, "Enter both CA and PA as whole numbers from 0 to 200.", "Local editor"); return; }
            try
            {
                await RunDatabaseAsync(() => _database.SaveLocalAbilitiesAsync(_selectedPlayer.SourceId, _selectedPlayer.Name, ca, pa));
                AtlasLog.Info("Local CA/PA edit saved for the selected player.");
                await RefreshPlayerResultsAsync();
                _selectedPlayer = _players.FirstOrDefault(x => x.SourceId == _selectedPlayer.SourceId) ?? _selectedPlayer;
                await ShowPlayerProfileAsync(_selectedPlayer);
                MessageBox.Show(this, "CA and PA saved to the FM Atlas database only. The Football Manager career has not been changed.", "Local edit saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { AtlasLog.Error("Local CA/PA edit failed.", ex); MessageBox.Show(this, ex.Message, "Local editor", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }));
        if (LiveWriteCertification.IsCertified && _selectedPlayer.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase))
        {
            abilityRow.Children.Add(SoftButton("Apply CA / PA to live FM", async (_, _) =>
            {
                if (!uint.TryParse(_selectedPlayer.SourceId[5..], out var uid) || !int.TryParse(caInput.Text, out var ca) || !int.TryParse(paInput.Text, out var pa) || ca is < 1 or > 200 || pa is < 1 or > 200)
                { MessageBox.Show(this, "Live CA and PA edits need a live FM player UID and whole-number values from 1 to 200.", "Live editor"); return; }
                if (!_selectedPlayer.CurrentAbility.HasValue || !_selectedPlayer.PotentialAbility.HasValue)
                { MessageBox.Show(this, "Atlas has no current CA/PA baseline for this player. Refresh the live import before editing.", "Live editor"); return; }
                LiveMemoryEditService.BatchReceipt? receipt = null;
                try
                {
                    var pid = await Task.Run(LiveMemoryEditService.FindSupportedProcessId);
                    var plan = await Task.Run(() => LiveMemoryEditService.StageBatch(pid, uid, _selectedPlayer.Name, new[]
                    {
                        new LiveMemoryEditService.Change("ability.current_ability", _selectedPlayer.CurrentAbility.Value, ca),
                        new LiveMemoryEditService.Change("ability.potential_ability", _selectedPlayer.PotentialAbility.Value, pa)
                    }));
                    var confirm = MessageBox.Show(this,
                        $"LIVE FM27 MEMORY WRITE PREVIEW\n\n{_selectedPlayer.Name} · {plan.ProfileId}\nCurrent Ability: {_selectedPlayer.CurrentAbility} → {ca}\nPotential Ability: {_selectedPlayer.PotentialAbility} → {pa}\n\nBefore applying, confirm these original CA/PA values match an independent editor such as FMRTE or FMLibreEditor. Save a new backup copy of your career, and use an offline single-player save. Atlas will re-check the game build, player UID/name and both old values immediately before writing, then read both values back.\n\nApply these changes to the running game now?",
                        "FM Atlas · Live editor safety check", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes) return;
                    receipt = await Task.Run(() => LiveMemoryEditService.ApplyBatch(plan));
                    try
                    {
                        await RunDatabaseAsync(() => _database.SaveLocalAbilitiesAsync(_selectedPlayer.SourceId, _selectedPlayer.Name, ca, pa, "FM27 live memory · read-back verified"));
                        await RunDatabaseAsync(() => _database.UpdateLivePlayerFactAsync(uid, "ability.current_ability", ca));
                        await RunDatabaseAsync(() => _database.UpdateLivePlayerFactAsync(uid, "ability.potential_ability", pa));
                    }
                    catch { await Task.Run(() => LiveMemoryEditService.UndoBatch(receipt)); throw; }
                    AtlasLog.Info($"Live FM27 CA/PA edit read-back verified: uid={uid}; profile={plan.ProfileId}; fields=2.");
                    await RefreshPlayerResultsAsync();
                    _selectedPlayer = _players.FirstOrDefault(x => x.SourceId == _selectedPlayer.SourceId) ?? _selectedPlayer;
                    await ShowPlayerProfileAsync(_selectedPlayer);
                    MessageBox.Show(this, "FM27 accepted both live CA/PA writes and Atlas read them back successfully. Press Continue in Football Manager to let the game refresh derived displays, then save your career to persist the changes.", "Live edit verified", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { AtlasLog.Error("Live CA/PA edit failed or was blocked.", ex); MessageBox.Show(this, ex.Message, "FM Atlas · Live edit stopped", MessageBoxButton.OK, MessageBoxImage.Warning); }
            }));
        }
        controls.Children.Add(abilityRow);
        controls.Children.Add(Txt("PLAYER ATTRIBUTES", 9, true, Teal));
        controls.Children.Add(Txt("Edit imported values in the FM Atlas database only. Saving here does not change the running Football Manager career. Live memory writes are disabled pending independent field certification.", 9, false, Muted));
        DockPanel.SetDock(controls, Dock.Top); dock.Children.Add(controls);

        var rows = await RunDatabaseAsync(() => _database.GetPlayerAttributeRowsAsync(_selectedPlayer.SourceId));
        var viewRows = new ObservableCollection<EditableAttributeView>(rows.Select(x => new EditableAttributeView { AttributeName=x.Name, Category=x.Category, OriginalValue=x.Value, Value=x.Value }));
        var grid = CreateGrid(); grid.IsReadOnly = false; grid.CanUserAddRows = false;
        var attributeNameColumn = Column("ATTRIBUTE", "AttributeName", 1.4); attributeNameColumn.IsReadOnly = true; grid.Columns.Add(attributeNameColumn);
        var categoryColumn = Column("GROUP", "Category", 1.1); categoryColumn.IsReadOnly = true; grid.Columns.Add(categoryColumn);
        grid.Columns.Add(new DataGridTextColumn { Header = "VALUE / 20", Binding = new Binding(nameof(EditableAttributeView.Value)) { StringFormat = "0.#", UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(.6, DataGridLengthUnitType.Star) });
        grid.ItemsSource = viewRows; dock.Children.Add(grid);
        var saveAttributes = PrimaryButton("Save changed attributes locally", async (_, _) =>
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
            var changed = viewRows.Where(x => Math.Abs(x.Value - x.OriginalValue) > 0.0001).ToList();
            if (changed.Count == 0) { MessageBox.Show(this, "No attribute changes to save.", "Local editor"); return; }
            if (changed.Any(x => x.Value < 1 || x.Value > 20)) { MessageBox.Show(this, "Player attributes must be between 1 and 20.", "Local editor"); return; }
            try
            {
                foreach (var item in changed) await RunDatabaseAsync(() => _database.SavePlayerAttributeAsync(_selectedPlayer.SourceId, _selectedPlayer.Name, item.AttributeName, item.Category, item.Value));
                AtlasLog.Info($"Local player attribute edit completed: fields={changed.Count}.");
                foreach (var item in changed) item.OriginalValue = item.Value;
                await ShowPlayerProfileAsync(_selectedPlayer);
                MessageBox.Show(this, $"Saved {changed.Count:N0} local attribute changes. No Football Manager values were written.", "Local edits saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { AtlasLog.Error("Local player attribute edit failed.", ex); MessageBox.Show(this, ex.Message, "Local editor", MessageBoxButton.OK, MessageBoxImage.Warning); }
        });
        saveAttributes.Margin = new Thickness(0, 9, 0, 0); DockPanel.SetDock(saveAttributes, Dock.Bottom); dock.Children.Add(saveAttributes);
        if (LiveWriteCertification.IsCertified && _selectedPlayer.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase))
        {
            var liveSaveAttributes = SoftButton("Apply changed attributes to live FM", async (_, _) =>
            {
                grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
                var changed = viewRows.Where(x => Math.Abs(x.Value - x.OriginalValue) > 0.0001).ToList();
                if (changed.Count == 0) { MessageBox.Show(this, "No changed attributes to apply.", "Live editor"); return; }
                if (changed.Any(x => Math.Abs(x.Value - Math.Round(x.Value)) > 0.0001 || Math.Abs(x.OriginalValue - Math.Round(x.OriginalValue)) > 0.0001 || x.Value is < 1 or > 20 || x.OriginalValue is < 1 or > 20))
                { MessageBox.Show(this, "Live attribute writes require whole-number current and target values from 1 to 20.", "Live editor"); return; }
                if (!uint.TryParse(_selectedPlayer.SourceId[5..], out var uid)) { MessageBox.Show(this, "The LIVE player identity could not be decoded.", "Live editor"); return; }
                LiveMemoryEditService.BatchReceipt? receipt = null;
                try
                {
                    var changes = changed.Select(x => new LiveMemoryEditService.Change(x.AttributeName, (int)Math.Round(x.OriginalValue), (int)Math.Round(x.Value))).ToArray();
                    var pid = await Task.Run(LiveMemoryEditService.FindSupportedProcessId);
                    var plan = await Task.Run(() => LiveMemoryEditService.StageBatch(pid, uid, _selectedPlayer.Name, changes));
                    var preview = string.Join("\n", plan.Changes.Select(x => $"{x.FieldKey}: {x.ExpectedCurrentValue} → {x.TargetValue}"));
                    var confirm = MessageBox.Show(this,
                        $"LIVE FM27 ATTRIBUTE WRITE PREVIEW\n\n{_selectedPlayer.Name} · {plan.ProfileId}\n{preview}\n\nConfirm that every original value shown above matches this player's current Attribute screen in Football Manager. Save a new backup copy of your career first and use offline single-player only. Atlas will re-check the exact build, player UID/name and each original byte before writing, then read every field back.\n\nApply {plan.Changes.Count} field(s) to the running FM game now?",
                        "FM Atlas · Live attribute safety check", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes) return;
                    receipt = await Task.Run(() => LiveMemoryEditService.ApplyBatch(plan));
                    try
                    {
                        await RunDatabaseAsync(() => _database.SavePlayerAttributeBatchAsync(
                            _selectedPlayer.SourceId,
                            _selectedPlayer.Name,
                            changed.Select(item => (item.AttributeName, item.Category, item.Value)).ToArray(),
                            "FM27 live memory · read-back verified"));
                        foreach (var item in changed)
                            await RunDatabaseAsync(() => _database.UpdateLivePlayerFactAsync(uid, "attributes." + item.AttributeName, item.Value));
                    }
                    catch { await Task.Run(() => LiveMemoryEditService.UndoBatch(receipt)); throw; }
                    foreach (var item in changed) item.OriginalValue = item.Value;
                    AtlasLog.Info($"Live FM27 attribute batch read-back verified: uid={uid}; profile={plan.ProfileId}; fields={plan.Changes.Count}.");
                    await ShowPlayerProfileAsync(_selectedPlayer);
                    MessageBox.Show(this, $"FM27 accepted {plan.Changes.Count:N0} live attribute writes and Atlas read each one back successfully. Press Continue in Football Manager to refresh derived displays, then save your career to persist them.", "Live attributes verified", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { AtlasLog.Error("Live player attribute write failed or was blocked.", ex); MessageBox.Show(this, ex.Message, "FM Atlas · Live edit stopped", MessageBoxButton.OK, MessageBoxImage.Warning); }
            });
            liveSaveAttributes.Margin = new Thickness(0, 8, 0, 0); DockPanel.SetDock(liveSaveAttributes, Dock.Bottom); dock.Children.Add(liveSaveAttributes);
        }
        Grid.SetRow(editor, 1); root.Children.Add(editor); return root;
    }

    private sealed class EditableAttributeView
    {
        public string AttributeName { get; set; } = "";
        public string Category { get; set; } = "";
        public double OriginalValue { get; set; }
        public double Value { get; set; }
    }

    private async Task<UIElement> EditHistoryPageAsync()
    {
        var panel = Card(new DockPanel(), 15); var dock = (DockPanel)panel.Child;
        var top = new StackPanel();
        top.Children.Add(Txt("AUDIT TRAIL", 9, true, Teal));
        top.Children.Add(Txt("Edit history & guarded undo", 20, true, Ink));
        top.Children.Add(Txt("Local Atlas edits can be undone in the local database. Any legacy live-memory history entries remain blocked from inverse writes until the build-wide certification gate is formally passed.", 10, false, Muted, wrap: true));
        var dg = CreateGrid(); dg.SelectionMode = DataGridSelectionMode.Single;
        dg.Columns.Add(Column("ID", "Id", .35)); dg.Columns.Add(Column("PLAYER", "PlayerName", 1.1)); dg.Columns.Add(Column("FIELD", "FieldName", 1)); dg.Columns.Add(Column("OLD", "OldValue", .6)); dg.Columns.Add(Column("NEW", "NewValue", .6)); dg.Columns.Add(Column("CHANGED", "ChangedUtc", 1)); dg.Columns.Add(Column("SOURCE", "ChangeSource", 1));
        dg.ItemsSource = await RunDatabaseAsync(() => _database.GetRecentEditHistoryAsync());
        top.Children.Add(SoftButton("Undo selected change", async (_, _) =>
        {
            if (dg.SelectedItem is not AtlasHistoryRecord selected) { MessageBox.Show(this, "Select an edit history row first.", "Undo edit"); return; }
            var liveChange = selected.ChangeSource.Contains("FM27 live memory", StringComparison.OrdinalIgnoreCase);
            if (!liveChange)
            {
                var confirmLocal = MessageBox.Show(this, $"Undo local change #{selected.Id} for {selected.PlayerName}?\n\nThis changes the FM Atlas database only, not the live game.", "Confirm local undo", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirmLocal != MessageBoxResult.Yes) return;
                try { await RunDatabaseAsync(() => _database.UndoLocalEditAsync(selected.Id)); AtlasLog.Info($"Local edit history item undone: id={selected.Id}."); }
                catch (Exception ex) { AtlasLog.Error($"Local edit undo failed for history item {selected.Id}.", ex); MessageBox.Show(this, ex.Message, "Undo failed", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            }
            else
            {
                if (!selected.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase) || !uint.TryParse(selected.SourceId[5..], out var uid))
                { MessageBox.Show(this, "This history row is marked as a live edit but does not contain a valid LIVE:UID identity. Atlas will not guess which game player to modify.", "Live undo blocked", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (!double.TryParse(selected.NewValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var expected) || !double.TryParse(selected.OldValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var restore))
                { MessageBox.Show(this, "The old and new values for this live edit are not both numeric. Atlas cannot safely create an inverse write.", "Live undo blocked", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (Math.Abs(expected - Math.Round(expected)) > 0.0001 || Math.Abs(restore - Math.Round(restore)) > 0.0001 || expected is < 1 or > 200 || restore is < 1 or > 200)
                { MessageBox.Show(this, "This live undo value is outside the currently supported whole-number ability/attribute range.", "Live undo blocked", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                var fieldKey = selected.FieldName switch
                {
                    "CurrentAbility" => "ability.current_ability",
                    "PotentialAbility" => "ability.potential_ability",
                    _ => selected.FieldName
                };
                LiveMemoryEditService.BatchReceipt? receipt = null;
                try
                {
                    var pid = await Task.Run(LiveMemoryEditService.FindSupportedProcessId);
                    var plan = await Task.Run(() => LiveMemoryEditService.StageBatch(pid, uid, selected.PlayerName,
                        new[] { new LiveMemoryEditService.Change(fieldKey, (int)Math.Round(expected), (int)Math.Round(restore)) }));
                    var confirmLive = MessageBox.Show(this,
                        $"LIVE FM27 UNDO PREVIEW\n\n{selected.PlayerName} · {plan.ProfileId}\n{selected.FieldName}: {expected:0.##} → {restore:0.##}\n\nAtlas has re-resolved the player UID and confirmed that FM currently contains the value recorded as NEW. It will also check that the same career has stayed loaded, then write and read back the old value. Back up your career and use offline single-player only.\n\nApply this inverse write to Football Manager?",
                        "FM Atlas · Guarded live undo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirmLive != MessageBoxResult.Yes) return;
                    receipt = await Task.Run(() => LiveMemoryEditService.ApplyBatch(plan));
                    try
                    {
                        await RunDatabaseAsync(() => _database.UndoLocalEditAsync(selected.Id));
                        var factKey = selected.FieldName switch
                        {
                            "CurrentAbility" => "ability.current_ability",
                            "PotentialAbility" => "ability.potential_ability",
                            _ => "attributes." + selected.FieldName
                        };
                        await RunDatabaseAsync(() => _database.UpdateLivePlayerFactAsync(uid, factKey, restore));
                    }
                    catch
                    {
                        await Task.Run(() => LiveMemoryEditService.UndoBatch(receipt));
                        throw;
                    }
                    AtlasLog.Info($"Guarded live FM27 undo read-back verified: historyId={selected.Id}; uid={uid}; field={fieldKey}; profile={plan.ProfileId}.");
                    MessageBox.Show(this, "Football Manager accepted the inverse write and Atlas read the restored value back. Press Continue in FM to refresh derived displays, then save the career if you wish to keep the restoration.", "Live undo verified", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { AtlasLog.Error($"Guarded live undo failed for history item {selected.Id}.", ex); MessageBox.Show(this, ex.Message, "Live undo failed or blocked", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            }
            dg.ItemsSource = await RunDatabaseAsync(() => _database.GetRecentEditHistoryAsync());
            await RefreshPlayerResultsAsync();
            if (_selectedPlayer is not null) await ShowPlayerProfileAsync(_selectedPlayer);
        }));
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top); dock.Children.Add(dg); return panel;
    }

    private UIElement AssistantPage()
    {
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) }); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = Card(new StackPanel(), 16); var s = (StackPanel)left.Child; s.Children.Add(Txt("ATLAS ASSISTANT", 9, true, Teal)); s.Children.Add(Txt("Ask your career", 22, true, Ink));
        s.Children.Add(Txt("The current assistant composes a local, explainable database search. It does not invent player facts or call an external AI service.", 10, false, Muted, wrap: true));
        var prompt = new TextBox { Height = 120, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 10, 0, 8), Padding = new Thickness(10), Text = "Find players under 23 with high finishing" };
        s.Children.Add(prompt); var run = PrimaryButton("Search career database", async (_, _) => await RunAssistantSearchAsync(prompt.Text)); s.Children.Add(run);
        s.Children.Add(SoftButton("Open advanced Global Search", (_, _) => ShowGlobalSearch()));
        var right = Card(new StackPanel(), 16); var resultStack = (StackPanel)right.Child; resultStack.Children.Add(Txt("SEARCH RESULTS", 9, true, Teal)); resultStack.Children.Add(Txt("Your results will appear here", 19, true, Ink)); resultStack.Children.Add(Txt("Supported filters depend on the fields imported in your save snapshot. Unsupported parts of a request are identified rather than silently ignored.", 10, false, Muted, wrap: true));
        Grid.SetColumn(left, 0); root.Children.Add(left); Grid.SetColumn(right, 1); root.Children.Add(right);
        _assistantResultStack = resultStack;
        return root;
    }
    private StackPanel? _assistantResultStack;

    private async Task RunAssistantSearchAsync(string query)
    {
        if (_assistantResultStack is null) return;
        var lower = query.ToLowerInvariant();
        var age = Regex.Match(lower, @"(?:under|below|younger than)\s+(\d+)");
        var maxAge = age.Success && int.TryParse(age.Groups[1].Value, out var a) ? a - 1 : (int?)null;
        var attrNames = await RunDatabaseAsync(() => _database.GetAttributeNamesAsync());
        var matchedAttribute = attrNames.FirstOrDefault(x => lower.Contains(x.Replace('_', ' ').ToLowerInvariant()));
        double? minimum = Regex.Match(lower, @"(?:at least|minimum|min)\s+(\d+)") is Match n && n.Success && double.TryParse(n.Groups[1].Value, out var val) ? val : null;
        var position = new[] { "striker", "centre back", "center back", "winger", "midfielder", "goalkeeper" }.FirstOrDefault(x => lower.Contains(x));
        var positionCode = position switch { "striker" => "ST", "centre back" or "center back" => "DC", "winger" => "AMR", "midfielder" => "MC", "goalkeeper" => "GK", _ => null };
        var results = await RunDatabaseAsync(() => _database.SearchAsync("", null, maxAge, positionCode, null, null, null, null, matchedAttribute, minimum, null));
        if (results.Count == 0) results = await RunDatabaseAsync(() => _database.SearchLivePlayersAsync("", 5000, matchedAttribute, minimum, null, null, maxAge));
        _assistantResultStack.Children.Clear(); _assistantResultStack.Children.Add(Txt("SEARCH RESULTS", 9, true, Teal));
        _assistantResultStack.Children.Add(Txt($"{results.Count:N0} matching records", 20, true, Ink));
        AddDetailRow(_assistantResultStack, "Age filter", maxAge.HasValue ? $"Under {maxAge + 1}" : "Not applied");
        AddDetailRow(_assistantResultStack, "Attribute", matchedAttribute ?? "Not applied");
        AddDetailRow(_assistantResultStack, "Minimum rating", minimum?.ToString() ?? "Not applied");
        AddDetailRow(_assistantResultStack, "Position", positionCode ?? "Not applied");
        _assistantResultStack.Children.Add(Txt("This is a rule-based query composer, not a generative football analyst. Footedness, tactical system, budget, transfer availability and replacement context are not applied unless their fields are present and mapped in the database.", 10, false, Muted, wrap: true));
        var dg = CreateGrid(); dg.Height = 420; dg.Columns.Add(Column("PLAYER", "Name", 1.2)); dg.Columns.Add(Column("AGE", "Age", .4)); dg.Columns.Add(Column("CLUB", "Club", 1)); dg.Columns.Add(Column("CA", "DisplayAbility", .4)); dg.Columns.Add(Column("PA", "DisplayPotential", .4)); dg.ItemsSource = results.Take(100).ToList(); _assistantResultStack.Children.Add(dg);
    }

    private async Task<UIElement> SettingsPageAsync()
    {
        var root = new StackPanel();

        var themeCard = Card(new StackPanel(), 16);
        var themeStack = (StackPanel)themeCard.Child;
        themeStack.Children.Add(Txt("MONOLITH DESIGN SYSTEM", 9, true, Teal));
        themeStack.Children.Add(Txt("Appearance", 20, true, Ink));
        themeStack.Children.Add(Txt("Choose the Obsidian dark workspace or Warm Light. Both use the same Atlas orange accent, typography and shared layout tokens.", 10, false, Muted, wrap: true));
        var themePicker = new ComboBox
        {
            Width = 220,
            Height = 34,
            Margin = new Thickness(0, 8, 0, 4),
            Padding = new Thickness(8, 5, 8, 5),
            Background = Brush("#F8FCFA"),
            Foreground = Ink,
            BorderBrush = Line,
            ItemsSource = new[] { "Obsidian", "Warm Light" },
            SelectedItem = MonolithTheme.CurrentMode == AtlasThemeMode.WarmLight ? "Warm Light" : "Obsidian"
        };
        themePicker.SelectionChanged += async (_, _) =>
        {
            if (themePicker.SelectedItem is not string selected) return;
            MonolithTheme.SetMode(selected == "Warm Light" ? AtlasThemeMode.WarmLight : AtlasThemeMode.Obsidian);
            await NavigateAsync(_activePage);
        };
        themeStack.Children.Add(themePicker);
        root.Children.Add(themeCard);

        var bridge = Card(new StackPanel(), 16); var b = (StackPanel)bridge.Child; b.Children.Add(Txt("SAVE READER", 9, true, Teal)); b.Children.Add(Txt("FM27 save snapshot bridge", 20, true, Ink));
        var available = await FmsaveBridgeService.IsAvailableAsync();
        b.Children.Add(Txt(available ? "fmsave is available under Python 3.12." : "fmsave is not currently available. Install Python 3.12+ and the local fmsave package to enable save-file imports.", 11, false, available ? Brush("#237C62") : Muted, wrap: true));
        b.Children.Add(SoftButton("Run save bridge setup", (_, _) => RunSaveBridgeSetup()));
        b.Children.Add(Txt("Current published fmsave documentation lists FM27 build 27.0.0+2486695 as a registered layout. Other FM27 builds may borrow layouts where section schemas match. A registered build does not certify field meanings: FM27 fields remain unconfirmed. Each import records a reader validation report.", 10, false, Muted, wrap: true));
        if (!string.IsNullOrWhiteSpace((await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync())).SaveValidationJson)) b.Children.Add(Txt("A validation report is stored with the latest snapshot. Reader failures and coverage should be reviewed before relying on the corresponding data.", 10, false, Teal, wrap: true));
        root.Children.Add(bridge);
        var db = Card(new StackPanel(), 16); var d = (StackPanel)db.Child; d.Children.Add(Txt("LOCAL DATA", 9, true, Teal)); d.Children.Add(Txt("Database & safety", 20, true, Ink));
        d.Children.Add(Txt("Database: %LOCALAPPDATA%\\FmAtlas\\scouting.db", 10, true, Ink)); d.Children.Add(Txt("FM Atlas 27 keeps your existing database. Save imports are stored as versioned snapshots; player attributes are normalized into the local attribute table. The live reader is read-only during discovery. The separately gated live editor currently supports guarded player CA, PA and standard attribute writes only; unsupported fields stay blocked.", 10, false, Muted, wrap: true));
        d.Children.Add(SoftButton("Open FM Atlas data folder", (_, _) => { var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas"); Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true }); })); root.Children.Add(db);
        var design = Card(new StackPanel(), 16); var ds = (StackPanel)design.Child; ds.Children.Add(Txt("OFFICIAL BRANDING", 9, true, Teal));
        ds.Children.Add(new Image { Source = new BitmapImage(new Uri("pack://application:,,,/FmAtlas;component/Assets/FM-Atlas-27-Logo.png", UriKind.Absolute)), Height = 150, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 12) });
        ds.Children.Add(Txt("MONOLITH · Swiss Sport editorial", 20, true, Ink)); ds.Children.Add(Txt("Obsidian / Warm Light surfaces, Atlas orange focus states, Inter typography where installed, restrained motion, consistent borders and dense football-intelligence layouts.", 10, false, Muted, wrap: true)); root.Children.Add(design);
        var graphics = Card(new StackPanel(), 16); var gs = (StackPanel)graphics.Child;
        gs.Children.Add(Txt("INSTALLED GRAPHICS", 9, true, Teal));
        gs.Children.Add(Txt("Reuse the faces, club logos, competition badges and flags already installed in your Football Manager 27 graphics folder. Atlas reads config.xml mappings and does not bundle or duplicate graphics packs.", 10, false, Muted, wrap: true));
        var graphicsScan = await Task.Run(() => GraphicsResolver.Shared.Scan());
        gs.Children.Add(Txt($"Root: {graphicsScan.GraphicsRoot ?? "Not found"}", 9, true, Ink, wrap: true));
        gs.Children.Add(Txt($"Faces {graphicsScan.FaceMappings:N0} · Clubs {graphicsScan.ClubMappings:N0} · Competitions {graphicsScan.CompetitionMappings:N0} · Flags {graphicsScan.FlagMappings:N0} · Configs {graphicsScan.ConfigFiles:N0}", 9, false, Muted, wrap: true));
        gs.Children.Add(Txt(graphicsScan.Status, 9, false, Muted, wrap: true));
        gs.Children.Add(SoftButton("Choose a graphics config.xml", async (_, _) =>
        {
            var open = new OpenFileDialog { Title = "Choose a config.xml inside Football Manager 27\\graphics", Filter = "Graphics config (config.xml)|config.xml", FileName = "config.xml", CheckFileExists = true };
            if (open.ShowDialog(this) != true) return;
            try
            {
                GraphicsResolver.Shared.SetRootFromConfigFile(open.FileName);
                await Task.Run(() => GraphicsResolver.Shared.Scan(true));
                await NavigateAsync("Settings");
            }
            catch (Exception ex) { AtlasLog.Error("Graphics root selection failed.", ex); MessageBox.Show(this, ex.Message, "Graphics folder not set", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }));
        gs.Children.Add(SoftButton("Rescan installed graphics", async (_, _) => { await Task.Run(() => GraphicsResolver.Shared.Scan(true)); await NavigateAsync("Settings"); }));
        root.Children.Add(graphics);
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
    }

    private UIElement TutorialPage()
    {
        var root = new StackPanel { Margin = new Thickness(4) };
        var hero = Card(new StackPanel(), 18, new Thickness(0, 0, 0, 12));
        var h = (StackPanel)hero.Child;
        h.Children.Add(Txt("FM ATLAS 27 · QUICK START", 10, true, Teal));
        h.Children.Add(Txt("Your career, organised.", 25, true, Ink));
        h.Children.Add(Txt("A practical guide to FM Atlas 27. The app keeps your local scouting data and save snapshots in place; live editing is available only for the exact supported Windows FM27 build profiles and a narrow set of player fields. It is OFF by default; use a copied offline career and review the Tester & Logs checks before applying changes.", 11, false, Muted, wrap: true));
        h.Children.Add(SoftButton("Open Global Search  ·  Ctrl + F", (_, _) => ShowGlobalSearch()));
        root.Children.Add(hero);

        void Section(string title, params string[] steps)
        {
            var card = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 11));
            var stack = (StackPanel)card.Child;
            stack.Children.Add(Txt(title, 15, true, Ink));
            for (var i = 0; i < steps.Length; i++)
                stack.Children.Add(Txt($"{i + 1}.  {steps[i]}", 11, false, Brush("#3D586E"), wrap: true) is var line ? Configure(line, new Thickness(0, 5, 0, 5)) : new TextBlock());
            root.Children.Add(card);
        }

        Section("1 · Connect to a career", "Start Football Manager 27 and load the career you want to inspect.", "Press Load Game to capture the supported live identity information. The live reader imports supported player/club identities, selected CA/PA and 1–20 attribute candidates. Values retain build/source provenance; they are not all independently verified and must be compared with the FM UI.", "For deeper player, staff and club information, choose Import Career Save and select a COPY of your save file. Keep your original save untouched.");
        Section("2 · Find players", "Press Global Search or Ctrl + F. Choose Players, Staff, Clubs or All records.", "Search by name, club or ID. Use position, age, CA/PA and nationality filters when those values are available.", "Double-click a result or select Open selected record. Player profiles open in their own closable tabs.", "Use Previous and Next in the player directory to browse pages. FM Atlas does not draw tens of thousands of player cards at once.");
        Section("3 · Read a player profile", "The profile displays attributes in Technical, Set Pieces, Mental, Physical, Goalkeeping and Hidden groups.", "A dash means the source did not supply a value. It is not zero and is never guessed.", "CA/PA and other parsed FM27 fields can be build-dependent and are marked as source-derived. Cross-check values in the game before making decisions.");
        Section("4 · Clubs and staff", "Open Clubs, search for a club and select it to inspect available club facts and its linked squad.", "Open Staff to browse staff included in the latest imported career snapshot. If no staff records were included, re-export and import a save snapshot.", "Finances, facilities, stadium and contract values are shown only when retained by the parser. Historical money fields must not be treated as current balances without checking their date and unit.");
        Section("5 · Atlas Assistant", "Use the Assistant for straightforward local queries such as players under a specified age or with an attribute threshold.", "The current assistant is a rule-based search composer, not an external generative AI model. It reports only filters it actually applied.", "For more detailed criteria, use Global Search. Keep the Assistant available as a quick way into the database.");
        Section("6 · Your data and safety", "The local database is stored at %LOCALAPPDATA%\\FmAtlas\\scouting.db.", "Save imports are versioned snapshots. The importer adds a new snapshot; it does not delete earlier source records.", "Atlas local edits are separate from Football Manager. A separate early live-write path supports CA, PA and 47 visible standard player attributes only for exact supported Windows FM27 profiles; it is OFF by default and requires preview, explicit Apply, session/identity/current-value checks and read-back. Hidden attributes and broader entity editing remain blocked.", "Back up the FmAtlas data folder before major changes or moving the application to another PC.");
        Section("7 · Analytics and Mass Edit", "Analytics discovers numeric season/match metrics retained by the latest imported save snapshot. Choose a metric, set minimum/maximum values and search.", "If xG, xA or tackles per 90 are not in the imported source, they will not appear. Atlas does not calculate or invent missing match statistics.", "Mass Edit changes filtered local Atlas player records only. Every changed player is recorded separately in Edit History. Save Atlas checkpoints the local database; it does not save the FM career.");
        Section("8 · Diagnostics", "Use Diagnostics in the top toolbar to export a support report. Check it before sharing because it may include player names, file paths and recent edit history.");
        Section("Troubleshooting", "If a page appears busy, wait for its progress state to finish. Navigation is locked during a page build to prevent overlapping loads.", "If CA/PA or attributes are blank after Load Game, use Import Career Save from a copy. Identity counts are not the same as complete attribute counts.", "If a club has no squad, verify that a save snapshot was imported and that the club-to-player links are present in the source export.", "After updating, run Build-FM-Atlas-27.bat. The build window must report 0 errors before launching the new executable.");
        root.Children.Add(SoftButton("Open FM Atlas data folder", (_, _) => { var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas"); Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true }); }));
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
    }

    private async Task LoadLiveCareerAsync()
    {
        if (_liveLoadButton is not null) { _liveLoadButton.IsEnabled = false; _liveLoadButton.Content = "Loading…"; }
        if (_liveLoadProgress is not null) { _liveLoadProgress.Visibility = Visibility.Visible; _liveLoadProgress.IsIndeterminate = true; }
        if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Connecting to Football Manager · validating supported build…";
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Reading career identities and relationships…";
            AtlasLog.Info("Starting read-only live identity scan.");
            var payload = await Task.Run(LiveDataImporter.Extract);
            if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Writing validated snapshot to local database…";
            var result = await RunDatabaseAsync(() => _database.ImportLiveSnapshotAsync(payload));
            AtlasLog.Info($"Live profile import completed: slots={result.PlayersSeen}, named={result.PlayersImported}, clubs={result.ClubsImported}, attributeFacts={result.AttributeRowsImported}, unreadable={result.UnnamedPlayers}, unresolvedLinks={result.UnlinkedPlayers}.");
            RefreshConnectionStatus();
            await RefreshCareerStatusAsync();
            MessageBox.Show(this, $"FM27 live profile snapshot imported.\n\nPlayer slots scanned: {result.PlayersSeen:N0}\nNamed players: {result.PlayersImported:N0}\nClubs: {result.ClubsImported:N0}\nRange-valid attribute/personality rows stored: {result.AttributeRowsImported:N0}\nUnreadable player slots: {result.UnnamedPlayers:N0}\nUnresolved club links: {result.UnlinkedPlayers:N0}\n\nImportant: the module profile matched exactly, but CA/PA and attribute offsets are still provisional field mappings. They are visible in Atlas for comparison with the in-game UI, not certified as safe live-edit targets. Staff, contract, match-stat and club-finance live extraction remain separate work.", "FM Atlas · Live career import", MessageBoxButton.OK, MessageBoxImage.Information);
            await NavigateAsync("Dashboard");
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Live identity import failed; no import committed.", ex);
            MessageBox.Show(this, "No live import was committed.\n\n" + ex.Message, "FM Atlas · Live import stopped safely", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (_liveLoadButton is not null) { _liveLoadButton.IsEnabled = true; _liveLoadButton.Content = "Reload Game"; }
            if (_liveLoadProgress is not null) { _liveLoadProgress.IsIndeterminate = false; _liveLoadProgress.Visibility = Visibility.Collapsed; }
            if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Ready · profile-matched live identity + provisional player field snapshot";
        }
    }

    private async Task ImportCareerSaveAsync()
    {
        var open = new OpenFileDialog { Title = "Select a COPY of your Football Manager career save", Filter = "Football Manager saves (*.fm;*.fmf;*.dat)|*.fm;*.fmf;*.dat|All files (*.*)|*.*", CheckFileExists = true };
        if (open.ShowDialog(this) != true) return;
        if (!await FmsaveBridgeService.IsAvailableAsync())
        {
            var answer = MessageBox.Show(this, "The local save reader is not installed. Run Setup-FMAtlas-Save-Bridge.ps1 from the extracted FM Atlas folder, then restart the app.\n\nWould you like to open the setup script's folder?", "Save bridge not installed", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer == MessageBoxResult.Yes) Process.Start(new ProcessStartInfo("explorer.exe", AppContext.BaseDirectory) { UseShellExecute = true });
            return;
        }
        var confirmation = MessageBox.Show(this, "For safety, select a COPY of your career save, not your only save file. The parser is read-only, but the game may continue saving while you are browsing.\n\nProceed with a local, offline snapshot?", "Import career snapshot", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            AtlasLog.Info("Starting read-only FM career save export/import.");
            var result = await FmsaveBridgeService.ExportAsync(open.FileName);
            var imported = await RunDatabaseAsync(() => _database.ImportFmsaveSnapshotAsync(result.OutputJsonPath));
            AtlasLog.Info($"Career snapshot import completed: players={imported.PlayersImported}, staff={imported.StaffImported}, clubs={imported.ClubsImported}, extraRows={imported.ExtraRowsImported}, warnings={imported.Warnings.Count}.");
            var warnings = imported.Warnings.Count == 0 ? "No adapter warnings were reported." : string.Join("\n", imported.Warnings.Take(8));
            MessageBox.Show(this, $"Career save snapshot imported.\n\nPlayers: {imported.PlayersImported:N0}\nStaff: {imported.StaffImported:N0}\nClubs: {imported.ClubsImported:N0}\nAdditional table rows retained: {imported.ExtraRowsImported:N0}\n\n{warnings}\n\nThe source save was read-only; no game process or save was modified.", "FM Atlas · Save snapshot complete", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshConnectionStatus(); await RefreshCareerStatusAsync(); await NavigateAsync("Dashboard");
        }
        catch (Exception ex)
        {
            AtlasLog.Error("FM career save import failed.", ex);
            MessageBox.Show(this, "The save snapshot was not imported.\n\n" + ex.Message, "FM Atlas · Save import error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private void RunSaveBridgeSetup()
    {
        var script = System.IO.Path.Combine(AppContext.BaseDirectory, "Setup-FMAtlas-Save-Bridge.ps1");
        if (!File.Exists(script)) { MessageBox.Show(this, "Setup script was not found next to the application. Extract the complete FM Atlas release folder first."); return; }
        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"") { UseShellExecute = true });
    }

    private void RefreshConnectionStatus()
    {
        var status = GameProcessDetector.Detect();
        if (_connectionDot is not null) _connectionDot.Fill = Brush(status.MemoryReadVerified ? "#2BA578" : status.IsRunning ? "#D8A34A" : "#C7797B");
        if (_connectionSummary is not null) _connectionSummary.Text = status.Detail + (status.MemoryReadVerified ? " · memory probe only; full career import remains separately gated" : "");
    }


    private async Task RefreshCareerStatusAsync()
    {
        if (_connectionSummary is null) return;
        var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
        if (!string.IsNullOrWhiteSpace(metrics.SaveBuild))
        {
            var buildStatus = metrics.SaveBuildKnown == true ? "registered parser layout" : metrics.SaveBuildKnown == false ? "unregistered parser layout" : "parser layout status unknown";
            _connectionSummary.Text = $"Local career snapshot loaded · {metrics.SaveGame ?? "Football Manager"} {metrics.SaveBuild} · {metrics.SaveGameDate ?? "date unknown"} · {buildStatus}. FM27 field meanings remain unconfirmed.";
        }
    }

    private async Task AddSelectedToShortlistAsync()
    {
        if (_selectedPlayer is null) { MessageBox.Show(this, "Select a player first.", "Shortlist"); return; }
        var names = await RunDatabaseAsync(() => _database.GetShortlistNamesAsync());
        var name = names.FirstOrDefault() ?? "My Shortlist";
        if (_shortlistPicker?.SelectedItem is string selected) name = selected;
        await RunDatabaseAsync(() => _database.AddToShortlistAsync(_selectedPlayer, name));
        MessageBox.Show(this, $"{_selectedPlayer.Name} added to {name}.", "Shortlist updated", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task CreateNewShortlistAsync()
    {
        var dialog = new Window { Title = "New shortlist", Owner = this, Width = 390, Height = 190, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush("#171F28") };
        var panel = new StackPanel { Margin = new Thickness(18) }; panel.Children.Add(Txt("Shortlist name", 16, true, Ink)); var input = Input("e.g. Summer 2027 targets"); panel.Children.Add(input);
        var save = PrimaryButton("Create shortlist", (_, _) => dialog.DialogResult = true); panel.Children.Add(save); dialog.Content = panel;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(input.Text))
        {
            await RunDatabaseAsync(() => _database.CreateShortlistAsync(input.Text.Trim())); if (_shortlistPicker is not null) { _shortlistPicker.ItemsSource = await RunDatabaseAsync(() => _database.GetShortlistNamesAsync()); _shortlistPicker.SelectedItem = input.Text.Trim(); }
        }
    }

    private async Task RefreshShortlistRowsAsync(DataGrid grid)
    {
        if (_shortlistPicker?.SelectedItem is not string name) return;
        var rows = await RunDatabaseAsync(() => _database.GetShortlistAsync(name)); _shortlist.Clear(); foreach (var r in rows) _shortlist.Add(r); grid.ItemsSource = _shortlist;
    }

    private async void ExportShortlistCsv()
    {
        if (_shortlistPicker?.SelectedItem is not string name) return;
        var rows = await RunDatabaseAsync(() => _database.GetShortlistAsync(name));
        var save = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"{SanitizeFileName(name)}.csv" };
        if (save.ShowDialog(this) != true) return;
        static string Escape(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        var lines = new[] { "Player,Club,Position,SourceId,Notes" }.Concat(rows.Select(x => string.Join(',', Escape(x.PlayerName), Escape(x.DisplayClub), Escape(x.DisplayPosition), Escape(x.SourceId), Escape(x.Notes))));
        await File.WriteAllLinesAsync(save.FileName, lines);
        MessageBox.Show(this, $"Exported {rows.Count:N0} shortlist entries.", "Export complete");
    }

    private UIElement DashboardPageFallback() => Card(Txt("No page is available.", 15, true, Ink), 20);

    private static Border MetricCard(string title, string value, string subtitle, string symbol)
    {
        var stack = new StackPanel(); var top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(Txt(title, 9, true, Teal)); var icon = Txt(symbol, 16, true, TealDark); Grid.SetColumn(icon, 1); top.Children.Add(icon);
        stack.Children.Add(top); stack.Children.Add(Txt(value, 25, true, Ink)); stack.Children.Add(Txt(subtitle, 9, false, Muted));
        return Card(stack, 14, new Thickness(0, 0, 10, 0));
    }

    private static void AddCoverageRow(Panel panel, string label, int count, string description)
    {
        var row = new Grid { Margin = new Thickness(0, 8, 0, 8) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var s = new StackPanel(); s.Children.Add(Txt(label, 11, true, Ink)); s.Children.Add(Txt(description, 9, false, Muted)); row.Children.Add(s); var v = Txt(count.ToString("N0"), 17, true, TealDark); Grid.SetColumn(v, 1); row.Children.Add(v); panel.Children.Add(row);
    }

    private static void AddAbilityTile(Grid grid, int column, string label, string value)
    {
        var border = new Border { Background = Brush("#EAF2F9"), CornerRadius = new CornerRadius(11), Padding = new Thickness(10), Margin = new Thickness(0, 7, 6, 7) };
        var stack = new StackPanel(); stack.Children.Add(Txt(label, 8, true, Muted)); stack.Children.Add(Txt(value, 20, true, Ink)); border.Child = stack; Grid.SetColumn(border, column); grid.Children.Add(border);
    }

    private static void AddDetailRow(Panel panel, string label, string? value)
    {
        var row = new Grid { Margin = new Thickness(0, 4, 0, 4) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(Txt(label, 9, false, Muted)); var val = Txt(string.IsNullOrWhiteSpace(value) ? "—" : value, 10, true, Ink, wrap: true); Grid.SetColumn(val, 1); row.Children.Add(val); panel.Children.Add(row);
    }

    private static Border AttributeBar(string name, double value)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.Children.Add(Txt(name.Replace('_', ' ').Replace('.', ' '), 10, true, Ink)); var val = Txt(value.ToString("0.#"), 11, true, Ink); Grid.SetColumn(val, 1); row.Children.Add(val); stack.Children.Add(row);
        var track = new Border { Background = Brush("#D9E7F1"), Height = 6, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 5, 0, 0), Child = new Border { Background = Brush(value >= 15 ? "#277BB9" : value >= 11 ? "#E5A65D" : "#D77B7E"), HorizontalAlignment = HorizontalAlignment.Left, Width = Math.Clamp(value, 0, 20) * 12.0, CornerRadius = new CornerRadius(4) } };
        stack.Children.Add(track); return new Border { Child = stack };
    }

    private static DataGrid CreateGrid()
    {
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserSortColumns = true, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = Brush("#E1E9EF"), Background = Brush("#171F28"), RowBackground = Brush("#171F28"), AlternatingRowBackground = Brush("#202A35"), BorderThickness = new Thickness(0), RowHeight = 38, FontSize = 11, Foreground = Brush("#25435E"), SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow, EnableRowVirtualization = true, EnableColumnVirtualization = true, FontFamily = new FontFamily(MonolithTheme.FontFamilyName) };
        VirtualizingPanel.SetIsVirtualizing(grid, true); VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling); ScrollViewer.SetCanContentScroll(grid, true);
        grid.ColumnHeaderHeight = 36; grid.Margin = new Thickness(0, 2, 0, 0);
        ScrollViewer.SetIsDeferredScrollingEnabled(grid, true);
        return grid;
    }

    private static DataGridTextColumn Column(string header, string property, double weight)
        => new() { Header = header, Binding = new Binding(property), Width = new DataGridLength(weight, DataGridLengthUnitType.Star), ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center), new Setter(TextBlock.MarginProperty, new Thickness(7, 0, 5, 0)), new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis) } } };

    private static Border Card(UIElement child, double radius, Thickness? margin = null)
        => new() { Background = Brush("#F8FCFA"), BorderBrush = Brush("#D4E1EC"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(radius), Padding = new Thickness(15), Margin = margin ?? new Thickness(0), Child = child };

    private static TextBlock Txt(string text, double size, bool bold, Brush foreground, bool wrap = false)
        => new() { Text = text, FontSize = Math.Max(10, size), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = foreground, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily(MonolithTheme.FontFamilyName) };

    private static TextBox Input(string placeholder)
        => new() { Text = "", Tag = placeholder, ToolTip = placeholder, Height = 35, Padding = new Thickness(9, 7, 9, 7), Margin = new Thickness(0, 3, 0, 8), Background = Brush("#FBFDFF"), Foreground = Brush("#1A3851"), BorderBrush = Brush("#D4E1EC"), BorderThickness = new Thickness(1), FontSize = 12 };

    private static TextBlock FieldLabel(string label) => Txt(label, 9, true, Muted);

    private static Button SoftButton(string content, RoutedEventHandler click)
    {
        var button = new Button { Content = content, Padding = new Thickness(13, 8, 13, 8), Margin = new Thickness(3), Background = Brush("#EFF5FA"), Foreground = Brush("#365F83"), BorderBrush = Brush("#D4E1EC"), BorderThickness = new Thickness(1), FontSize = 11, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, Template = RoundedButtonTemplate(11) };
        button.Click += click; return button;
    }

    private static Button PrimaryButton(string content, RoutedEventHandler click)
    {
        var button = new Button { Content = content, Padding = new Thickness(15, 9, 15, 9), Margin = new Thickness(3), Background = MonolithTheme.GetBrush("Accent"), Foreground = new SolidColorBrush(Color.FromRgb(16, 18, 22)), BorderBrush = MonolithTheme.GetBrush("Accent"), FontSize = 11, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, Template = RoundedButtonTemplate(11) };
        button.Click += click; return button;
    }

    private static Button WhiteButton(string content, RoutedEventHandler click)
    {
        var button = new Button { Content = content, Padding = new Thickness(13, 9, 13, 9), Margin = new Thickness(3), Background = Brush("#F6FCFA"), Foreground = Brush("#205F91"), BorderBrush = Brush("#D3E9E4"), FontSize = 11, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, Template = RoundedButtonTemplate(11) };
        button.Click += click; return button;
    }

    private static ControlTemplate RoundedButtonTemplate(double radius)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty)); border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty)); border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty)); border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(presenter); template.VisualTree = border; return template;
    }

    private static Brush Brush(string hex) => MonolithTheme.ResolveLegacy(hex);

    private static string Initials(string name)
    {
        var bits = name.Split(' ', StringSplitOptions.RemoveEmptyEntries); return bits.Length switch { 0 => "?", 1 => bits[0][..Math.Min(2, bits[0].Length)].ToUpperInvariant(), _ => (bits[0][0].ToString() + bits[^1][0]).ToUpperInvariant() };
    }
    private static string FormatDate(string value) => DateTime.TryParse(value, out var dt) ? dt.ToLocalTime().ToString("dd MMM yyyy · HH:mm") : value;
    private static string SanitizeFileName(string value) { foreach (var c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_'); return value; }
}
