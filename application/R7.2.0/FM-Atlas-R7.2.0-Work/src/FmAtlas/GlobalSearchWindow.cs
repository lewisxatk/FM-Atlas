using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FmAtlas.Data;
using FmAtlas.Models;

namespace FmAtlas;

public sealed record GlobalSearchHit(string Type, string Name, string Details, object Record);

/// <summary>
/// A separate, database-backed search workspace. Results are bounded and the grid virtualizes rows;
/// no 40k-record visual tree is created when the window opens.
/// </summary>
public sealed class GlobalSearchWindow : Window
{
    private static readonly Brush Ink = BrushOf("#E8EEF4");
    private static readonly Brush Muted = BrushOf("#9EADB9");
    private static readonly Brush Teal = BrushOf("#F08A42");
    private static readonly Brush Line = BrushOf("#3A3E45");
    private readonly ScoutDatabase _database;
    private readonly Action<GlobalSearchHit> _openRecord;
    private readonly ObservableCollection<GlobalSearchHit> _results = new();
    private readonly ObservableCollection<GlobalSearchHit> _playerResults = new();
    private readonly ObservableCollection<GlobalSearchHit> _staffResults = new();
    private readonly ObservableCollection<GlobalSearchHit> _clubResults = new();
    private readonly TextBox _query = Input("Name, club, league, position, UID or nationality");
    private readonly ComboBox _entityScope = Combo(new[] { "All types", "Players", "Staff", "Clubs" });
    private readonly TextBox _minAge = Input("Min age");
    private readonly TextBox _maxAge = Input("Max age");
    private readonly TextBox _minCa = Input("Min CA");
    private readonly TextBox _maxCa = Input("Max CA");
    private readonly TextBox _minPa = Input("Min PA");
    private readonly TextBox _maxPa = Input("Max PA");
    private readonly TextBox _nation = Input("Nationality contains…");
    private readonly TextBox _league = Input("League contains…");
    private readonly TextBox _minLeftFoot = Input("Left foot min");
    private readonly TextBox _maxLeftFoot = Input("Left foot max");
    private readonly TextBox _minRightFoot = Input("Right foot min");
    private readonly TextBox _maxRightFoot = Input("Right foot max");
    private readonly TextBox _minAttribute = Input("Min 1–20");
    private readonly TextBox _maxAttribute = Input("Max 1–20");
    private readonly ComboBox _attribute = Combo(new[] { "Any attribute" });
    private readonly ComboBox _preferredFoot = Combo(new[] { "Any foot", "Right", "Left", "Both" });
    private readonly ComboBox _homeGrown = Combo(new[] { "Any home-grown status", "Club", "Nation" });
    private readonly ComboBox _position = Combo(new[] { "Any position", "Goalkeepers", "Defenders", "Midfielders", "Attackers", "GK", "DL", "DC", "DR", "WBL", "WBR", "DM", "ML", "MC", "MR", "AML", "AMC", "AMR", "ST" });
    private readonly DataGrid _grid = new();
    private readonly DataGrid _playerGrid = new();
    private readonly DataGrid _staffGrid = new();
    private readonly DataGrid _clubGrid = new();
    private readonly TabControl _resultTabs = new();
    private readonly TextBlock _status = new();
    private readonly Button _searchButton = new() { Content = "Search", MinWidth = 104, Padding = new Thickness(16, 9, 16, 9) };
    private readonly DispatcherTimer _queryDebounce = new() { Interval = TimeSpan.FromMilliseconds(420) };
    private bool _searchBusy;
    private bool _searchPending;
    private int _queryGeneration;

    public GlobalSearchWindow(ScoutDatabase database, Window owner, Action<GlobalSearchHit> openRecord, string? initialQuery = null)
    {
        _database = database;
        _openRecord = openRecord;
        _query.Text = initialQuery ?? string.Empty;
        Owner = owner;
        Title = "FM Atlas 27 · Global Search";
        Width = 1120;
        Height = 760;
        MinWidth = 880;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushOf("#151719");
        Foreground = Ink;
        FontFamily = new FontFamily("Segoe UI");
        Build();
        Loaded += async (_, _) =>
        {
            try
            {
                var names = await Task.Run(() => _database.GetAttributeNamesAsync());
                _attribute.ItemsSource = new[] { "Any attribute" }.Concat(names).ToList();
                _attribute.SelectedIndex = 0;
            }
            catch { /* Search remains usable without an attribute-name lookup. */ }
            _query.Focus();
            _query.CaretIndex = _query.Text.Length;
            if (!string.IsNullOrWhiteSpace(_query.Text)) await RunSearchAsync();
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); else if (e.Key == Key.Enter) { e.Handled = true; _ = RunSearchAsync(); } };
    }

    private void Build()
    {
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        title.Children.Add(Text("ATLAS SEARCH", 10, true, Teal));
        title.Children.Add(Text("Find anyone or anything in your career", 25, true, Ink));
        title.Children.Add(Text("Search players, staff and clubs without loading the full database into the workspace.", 12, false, Muted));
        root.Children.Add(title);

        var filters = new Border { Background = BrushOf("#1B1D22"), BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(15), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 12) };
        var filterGrid = new Grid();
        filterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        filterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        filterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
        filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(_query, 0); Grid.SetColumn(_query, 0); _query.Height = 42; _query.Margin = new Thickness(0, 0, 8, 8); filterGrid.Children.Add(_query);
        _entityScope.SelectedIndex = 0;
        _entityScope.Height = 42;
        _entityScope.MinWidth = 150;
        _entityScope.Margin = new Thickness(0, 0, 8, 8);
        _entityScope.ToolTip = "Choose the record type to search: Players, Staff or Clubs. All types searches all three.";
        Grid.SetRow(_entityScope, 0); Grid.SetColumn(_entityScope, 1); filterGrid.Children.Add(_entityScope);
        _searchButton.Background = Teal; _searchButton.Foreground = BrushOf("#151719"); _searchButton.BorderBrush = Teal; _searchButton.FontWeight = FontWeights.SemiBold; _searchButton.Cursor = Cursors.Hand;
        Grid.SetRow(_searchButton, 0); Grid.SetColumn(_searchButton, 2); filterGrid.Children.Add(_searchButton);

        var advanced = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        advanced.Children.Add(Filter("POSITION", _position, 180));
        advanced.Children.Add(Filter("AGE FROM", _minAge, 95));
        advanced.Children.Add(Filter("AGE TO", _maxAge, 95));
        advanced.Children.Add(Filter("MIN CA", _minCa, 95));
        advanced.Children.Add(Filter("MAX CA", _maxCa, 95));
        advanced.Children.Add(Filter("MIN PA", _minPa, 95));
        advanced.Children.Add(Filter("MAX PA", _maxPa, 95));
        advanced.Children.Add(Filter("NATIONALITY", _nation, 190));
        advanced.Children.Add(Filter("LEAGUE", _league, 190));
        advanced.Children.Add(Filter("LEFT FOOT MIN", _minLeftFoot, 105));
        advanced.Children.Add(Filter("LEFT FOOT MAX", _maxLeftFoot, 105));
        advanced.Children.Add(Filter("RIGHT FOOT MIN", _minRightFoot, 105));
        advanced.Children.Add(Filter("RIGHT FOOT MAX", _maxRightFoot, 105));
        advanced.Children.Add(Filter("ATTRIBUTE", _attribute, 190));
        advanced.Children.Add(Filter("ATTRIBUTE MIN", _minAttribute, 95));
        advanced.Children.Add(Filter("ATTRIBUTE MAX", _maxAttribute, 95));
        advanced.Children.Add(Filter("PREFERRED FOOT", _preferredFoot, 130));
        advanced.Children.Add(Filter("HOME-GROWN", _homeGrown, 150));
        Grid.SetRow(advanced, 1); Grid.SetColumnSpan(advanced, 3); filterGrid.Children.Add(advanced);
        void ApplyEntityScope()
        {
            var scope = _entityScope.SelectedItem?.ToString() ?? "All types";
            var playerFiltersEnabled = scope is "All types" or "Players";
            advanced.IsEnabled = playerFiltersEnabled;
            advanced.Opacity = playerFiltersEnabled ? 1.0 : 0.42;
            _resultTabs.SelectedIndex = scope switch { "Players" => 1, "Staff" => 2, "Clubs" => 3, _ => 0 };
        }
        _entityScope.SelectionChanged += (_, _) =>
        {
            ApplyEntityScope();
            if (_query.Text.Trim().Length >= 2)
            {
                _queryGeneration++;
                _queryDebounce.Stop();
                _queryDebounce.Start();
            }
        };
        ApplyEntityScope();

        var note = Text("Tip: enter part of a name to search. Choose Players, Staff or Clubs above to focus the result set. Detailed numeric filters apply to player records only; staff and club searches use the source-backed fields currently available.", 10, false, Muted);
        note.Margin = new Thickness(0, 8, 0, 0); Grid.SetRow(note, 2); Grid.SetColumnSpan(note, 3); filterGrid.Children.Add(note);
        filters.Child = filterGrid; Grid.SetRow(filters, 1); root.Children.Add(filters);

        ConfigureResultsGrid(_grid, _results);
        ConfigureResultsGrid(_playerGrid, _playerResults);
        ConfigureResultsGrid(_staffGrid, _staffResults);
        ConfigureResultsGrid(_clubGrid, _clubResults);
        _resultTabs.Items.Add(new TabItem { Header = "All", Tag = "All", Content = _grid });
        _resultTabs.Items.Add(new TabItem { Header = "Players", Tag = "Players", Content = _playerGrid });
        _resultTabs.Items.Add(new TabItem { Header = "Staff", Tag = "Staff", Content = _staffGrid });
        _resultTabs.Items.Add(new TabItem { Header = "Clubs", Tag = "Clubs", Content = _clubGrid });
        _resultTabs.SelectedIndex = 0;
        Grid.SetRow(_resultTabs, 2); root.Children.Add(_resultTabs);

        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        _status.Text = "Ready · enter a search term to begin"; _status.Foreground = Muted; _status.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(_status, Dock.Left); footer.Children.Add(_status);
        var close = new Button { Content = "Close", Padding = new Thickness(16, 8, 16, 8), Background = BrushOf("#1B1D22"), Foreground = Ink, BorderBrush = Line, Cursor = Cursors.Hand };
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Right); footer.Children.Add(close);
        var open = new Button { Content = "Open selected record", Padding = new Thickness(16, 8, 16, 8), Background = Teal, Foreground = BrushOf("#151719"), BorderBrush = Teal, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
        open.Click += (_, _) => OpenSelected(); DockPanel.SetDock(open, Dock.Right); footer.Children.Add(open);
        Grid.SetRow(footer, 3); root.Children.Add(footer);

        _searchButton.Click += async (_, _) => await RunSearchAsync();
        _queryDebounce.Tick += async (_, _) => { _queryDebounce.Stop(); await RunSearchAsync(); };
        _query.TextChanged += (_, _) =>
        {
            _queryGeneration++;
            _queryDebounce.Stop();
            if (_query.Text.Trim().Length < 2)
            {
                _results.Clear(); _playerResults.Clear(); _staffResults.Clear(); _clubResults.Clear();
                _status.Text = "Type at least two characters to search players, staff and clubs.";
                return;
            }
            if (_query.Text.Trim().Length >= 2) _queryDebounce.Start();
        };
        Content = root;
    }

    private async Task RunSearchAsync()
    {
        if (_searchBusy) { _searchPending = true; return; }
        _searchBusy = true;
        var requestGeneration = _queryGeneration;
        var query = _query.Text.Trim();
        if (query.Length < 2)
        {
            _status.Text = "Enter at least two characters to search all record types.";
            _searchBusy = false;
            return;
        }
        var scope = _entityScope.SelectedItem?.ToString() ?? "All types";
        var playerScope = scope is "All types" or "Players";
        int? ReadInt(TextBox box) => int.TryParse(box.Text, out var value) ? value : null;
        var minAge = playerScope ? ReadInt(_minAge) : null; var maxAge = playerScope ? ReadInt(_maxAge) : null;
        var minCa = playerScope ? ReadInt(_minCa) : null; var maxCa = playerScope ? ReadInt(_maxCa) : null;
        var minPa = playerScope ? ReadInt(_minPa) : null; var maxPa = playerScope ? ReadInt(_maxPa) : null;
        var minLeftFoot = playerScope ? ReadInt(_minLeftFoot) : null; var maxLeftFoot = playerScope ? ReadInt(_maxLeftFoot) : null;
        var minRightFoot = playerScope ? ReadInt(_minRightFoot) : null; var maxRightFoot = playerScope ? ReadInt(_maxRightFoot) : null;
        double? ReadDouble(TextBox box) => double.TryParse(box.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
        var minAttribute = playerScope ? ReadDouble(_minAttribute) : null; var maxAttribute = playerScope ? ReadDouble(_maxAttribute) : null;
        if (new[] { minAge, maxAge, minCa, maxCa, minPa, maxPa }.Any(x => x.HasValue && x < 0))
        { _status.Text = "Age, CA and PA filters must be zero or higher."; _searchBusy = false; return; }
        if ((minAge.HasValue && maxAge.HasValue && minAge > maxAge) ||
            (minCa.HasValue && maxCa.HasValue && minCa > maxCa) ||
            (minPa.HasValue && maxPa.HasValue && minPa > maxPa) ||
            (minLeftFoot.HasValue && maxLeftFoot.HasValue && minLeftFoot > maxLeftFoot) ||
            (minRightFoot.HasValue && maxRightFoot.HasValue && minRightFoot > maxRightFoot))
        { _status.Text = "A minimum cannot be greater than its maximum."; _searchBusy = false; return; }
        if (new[] { minLeftFoot, maxLeftFoot, minRightFoot, maxRightFoot }.Any(x => x.HasValue && (x < 1 || x > 20)))
        { _status.Text = "Foot ratings must be between 1 and 20."; _searchBusy = false; return; }
        if (new[] { minAttribute, maxAttribute }.Any(x => x.HasValue && (x < 1 || x > 20)))
        { _status.Text = "Attributes are rated from 1 to 20. Correct the attribute range."; _searchBusy = false; return; }
        var attributeName = playerScope ? _attribute.SelectedItem?.ToString() : null;
        if (attributeName == "Any attribute") attributeName = null;
        if ((minAttribute.HasValue || maxAttribute.HasValue) && attributeName is null)
        { _status.Text = "Choose an attribute before applying an attribute range."; _searchBusy = false; return; }

        _searchButton.IsEnabled = false;
        _searchButton.Content = "Searching…";
        _results.Clear(); _playerResults.Clear(); _staffResults.Clear(); _clubResults.Clear();
        _status.Text = "Searching players, staff and clubs…";
        try
        {
            // Scope selection keeps the same bounded, source-backed search paths while allowing
            // FMRTE-style direct entry into Players, Staff or Clubs.
            var loadPlayers = scope is "All types" or "Players";
            var loadStaff = scope is "All types" or "Staff";
            var loadClubs = scope is "All types" or "Clubs";
            var hits = new List<GlobalSearchHit>();

            if (loadPlayers)
            {
                var position = _position.SelectedItem?.ToString() ?? "Any position";
                position = position switch { "Any position" => null!, "Goalkeepers" => "GOALKEEPERS", "Defenders" => "DEFENDERS", "Midfielders" => "MIDFIELDERS", "Attackers" => "ATTACKERS", _ => position };
                var nation = _nation.Text.Trim();
                var league = _league.Text.Trim();
                var preferredFoot = _preferredFoot.SelectedItem?.ToString(); if (preferredFoot == "Any foot") preferredFoot = null;
                var homeGrown = _homeGrown.SelectedItem?.ToString(); if (homeGrown == "Any home-grown status") homeGrown = null;
                var local = await Task.Run(() => _database.SearchAsync(query, minAge, maxAge, position,
                    string.IsNullOrWhiteSpace(league) ? null : league,
                    string.IsNullOrWhiteSpace(nation) ? null : nation, homeGrown, preferredFoot,
                    attributeName, minAttribute, maxAttribute,
                    minLeftFoot: minLeftFoot, maxLeftFoot: maxLeftFoot,
                    minRightFoot: minRightFoot, maxRightFoot: maxRightFoot,
                    minCa: minCa, maxCa: maxCa, minPa: minPa, maxPa: maxPa, limit: 350, offset: 0));
                var live = await Task.Run(() => _database.SearchLivePlayersAsync(query, 350, attributeName, minAttribute, maxAttribute, minAge, maxAge, DateTime.Today,
                    minCa: minCa, maxCa: maxCa, minPa: minPa, maxPa: maxPa));
                static string IdentityKey(PlayerRecord person) =>
                    $"{person.Name.Trim().ToUpperInvariant()}|{(person.Club ?? string.Empty).Trim().ToUpperInvariant()}|{person.Age?.ToString() ?? "?"}";
                static int Completeness(PlayerRecord person) =>
                    (person.Age.HasValue ? 1 : 0) + (person.Nation is not null ? 1 : 0) +
                    (person.League is not null ? 1 : 0) + (person.Position is not null ? 1 : 0) +
                    (person.CurrentAbility.HasValue ? 1 : 0) + (person.PotentialAbility.HasValue ? 1 : 0) +
                    (person.Value.HasValue ? 1 : 0) + (person.PreferredFoot is not null ? 1 : 0) +
                    (person.LeftFoot.HasValue ? 1 : 0) + (person.RightFoot.HasValue ? 1 : 0);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var person in local.Concat(live).GroupBy(IdentityKey, StringComparer.OrdinalIgnoreCase)
                             .Select(group => group.OrderByDescending(Completeness).First()))
                {
                    if (minCa.HasValue && (!person.CurrentAbility.HasValue || person.CurrentAbility < minCa)) continue;
                    if (maxCa.HasValue && (!person.CurrentAbility.HasValue || person.CurrentAbility > maxCa)) continue;
                    if (minPa.HasValue && (!person.PotentialAbility.HasValue || person.PotentialAbility < minPa)) continue;
                    if (maxPa.HasValue && (!person.PotentialAbility.HasValue || person.PotentialAbility > maxPa)) continue;
                    if (!string.IsNullOrWhiteSpace(nation) && (string.IsNullOrWhiteSpace(person.Nation) || !person.Nation.Contains(nation, StringComparison.OrdinalIgnoreCase))) continue;
                    if (!string.IsNullOrWhiteSpace(league) && (string.IsNullOrWhiteSpace(person.League) || !person.League.Contains(league, StringComparison.OrdinalIgnoreCase))) continue;
                    if (minLeftFoot.HasValue && (!person.LeftFoot.HasValue || person.LeftFoot < minLeftFoot)) continue;
                    if (maxLeftFoot.HasValue && (!person.LeftFoot.HasValue || person.LeftFoot > maxLeftFoot)) continue;
                    if (minRightFoot.HasValue && (!person.RightFoot.HasValue || person.RightFoot < minRightFoot)) continue;
                    if (maxRightFoot.HasValue && (!person.RightFoot.HasValue || person.RightFoot > maxRightFoot)) continue;
                    if (!string.IsNullOrWhiteSpace(preferredFoot) && !string.Equals(person.PreferredFoot, preferredFoot, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrWhiteSpace(homeGrown) && !(homeGrown == "Club" ? person.HomeGrownClub == true : person.HomeGrownNation == true)) continue;
                    if (position is not null && !MatchesPositionFilter(person.Position, position)) continue;
                    var personKey = $"{person.Name.Trim().ToUpperInvariant()}|{(person.Club ?? string.Empty).Trim().ToUpperInvariant()}|{person.Age?.ToString() ?? "?"}";
                    if (!seen.Add(personKey)) continue;
                    hits.Add(new GlobalSearchHit("Player", person.Name,
                        $"{person.Club ?? "Club unavailable"} · {person.Age?.ToString() ?? "Age —"} · {person.Position ?? "Position —"} · CA {person.DisplayAbility} / PA {person.DisplayPotential}", person));
                }
            }
            if (loadStaff)
            {
                var staff = await Task.Run(() => _database.SearchFmsaveStaffAsync(query));
                hits.AddRange(staff.Take(250).Select(person => new GlobalSearchHit("Staff", person.Name,
                    $"{person.Job ?? "Role unavailable"} · {person.Club ?? "Club unavailable"} · {person.Age?.ToString() ?? "Age —"}", person)));
            }
            if (loadClubs)
            {
                var clubs = await Task.Run(() => _database.SearchClubDirectoryAsync(query));
                hits.AddRange(clubs.Take(250).Select(club => new GlobalSearchHit("Club", club.Name,
                    $"{club.Nation ?? "Nation unavailable"} · {club.PlayerCount:N0} linked players · {club.DataSource}", club)));
            }

            if (requestGeneration != _queryGeneration) return;
            var ordered = hits.OrderByDescending(x => x.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .ThenBy(x => x.Type == "Player" ? 0 : x.Type == "Staff" ? 1 : 2)
                .Take(750).ToList();
            foreach (var hit in ordered)
            {
                _results.Add(hit);
                if (hit.Type == "Player") _playerResults.Add(hit);
                else if (hit.Type == "Staff") _staffResults.Add(hit);
                else if (hit.Type == "Club") _clubResults.Add(hit);
            }
            if (_resultTabs.Items.Count == 4)
            {
                ((TabItem)_resultTabs.Items[0]).Header = $"All ({_results.Count})";
                ((TabItem)_resultTabs.Items[1]).Header = $"Players ({_playerResults.Count})";
                ((TabItem)_resultTabs.Items[2]).Header = $"Staff ({_staffResults.Count})";
                ((TabItem)_resultTabs.Items[3]).Header = $"Clubs ({_clubResults.Count})";
            }
            _status.Text = $"{ordered.Count:N0} results · {_playerResults.Count:N0} players · {_staffResults.Count:N0} staff · {_clubResults.Count:N0} clubs · double-click to open";
        }
        catch (Exception ex)
        {
            if (requestGeneration == _queryGeneration) _status.Text = "Search failed: " + ex.Message;
        }
        finally
        {
            _searchBusy = false;
            _searchButton.IsEnabled = true;
            _searchButton.Content = "Search";
            if (_searchPending || requestGeneration != _queryGeneration)
            {
                _searchPending = false;
                await RunSearchAsync();
            }
        }
    }

    private static bool MatchesPositionFilter(string? rawPosition, string filter)
    {
        if (string.IsNullOrWhiteSpace(rawPosition)) return false;
        var positions = rawPosition.Split(new[] { ',', '/', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var groups = filter switch
        {
            "ATTACKERS" => new[] { "ST", "STC", "AML", "AMR" },
            "MIDFIELDERS" => new[] { "DM", "ML", "MC", "MR", "AML", "AMC", "AMR" },
            "DEFENDERS" => new[] { "SW", "DL", "DC", "DR", "WBL", "WBR" },
            "GOALKEEPERS" => new[] { "GK" },
            _ => new[] { filter }
        };
        return positions.Any(position => groups.Contains(position, StringComparer.OrdinalIgnoreCase));
    }

    private void OpenSelected()
    {
        var activeGrid = (_resultTabs.SelectedItem as TabItem)?.Content as DataGrid ?? _grid;
        if (activeGrid.SelectedItem is not GlobalSearchHit hit) return;
        _openRecord(hit);
        Close();
    }

    private static void ConfigureResultsGrid(DataGrid grid, ObservableCollection<GlobalSearchHit> source)
    {
        grid.AutoGenerateColumns = false; grid.IsReadOnly = true; grid.CanUserAddRows = false;
        grid.SelectionMode = DataGridSelectionMode.Single; grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        grid.HeadersVisibility = DataGridHeadersVisibility.Column; grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        grid.RowHeight = 38; grid.FontSize = 12; grid.Foreground = Ink; grid.Background = BrushOf("#1B1D22");
        grid.RowBackground = BrushOf("#1B1D22"); grid.AlternatingRowBackground = BrushOf("#24272D"); grid.BorderBrush = Line;
        grid.EnableRowVirtualization = true; grid.EnableColumnVirtualization = true;
        VirtualizingPanel.SetIsVirtualizing(grid, true); VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(grid, true);
        grid.Columns.Add(Column("TYPE", "Type", .65)); grid.Columns.Add(Column("NAME", "Name", 1.5)); grid.Columns.Add(Column("DETAILS", "Details", 2.4));
        grid.ItemsSource = source;
    }

    private static FrameworkElement Filter(string label, FrameworkElement control, double width)
    {
        var panel = new StackPanel { Width = width, Margin = new Thickness(0, 0, 9, 5) };
        panel.Children.Add(Text(label, 9, true, Muted)); panel.Children.Add(control); return panel;
    }

    private static DataGridTextColumn Column(string header, string property, double weight)
        => new() { Header = header, Binding = new System.Windows.Data.Binding(property), Width = new DataGridLength(weight, DataGridLengthUnitType.Star) };

    private static TextBox Input(string hint) => new()
    {
        Tag = hint, ToolTip = hint, Background = BrushOf("#111820"), Foreground = Ink,
        BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(9, 7, 9, 7),
        Margin = new Thickness(0, 3, 0, 8), FontSize = 12, Height = 34
    };

    private static ComboBox Combo(IEnumerable<string> values)
    {
        var box = new ComboBox { ItemsSource = values, SelectedIndex = 0, Padding = new Thickness(8), Height = 38, Background = BrushOf("#1B1D22"), Foreground = Ink, BorderBrush = Line, FontSize = 12 };
        return box;
    }

    private static TextBlock Text(string value, double size, bool bold, Brush foreground) => new()
    {
        Text = value, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = foreground, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 3)
    };

    private static SolidColorBrush BrushOf(string hex)
    {
        var mapped = hex.ToUpperInvariant() switch
        {
            "#EEF4F8" => "#151719", "#FFFFFF" => "#1B1D22", "#F5F9FC" => "#24272D",
            "#182F45" => "#E8EEF4", "#63778A" => "#9EADB9", "#087F91" => "#F08A42", "#D7E3EA" => "#3A3E45",
            _ => hex
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(mapped));
    }
}
