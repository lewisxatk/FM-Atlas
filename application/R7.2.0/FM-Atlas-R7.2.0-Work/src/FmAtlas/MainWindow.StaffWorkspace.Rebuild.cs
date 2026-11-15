using FmAtlas.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FmAtlas.Models;

namespace FmAtlas;

public partial class MainWindow
{
    private readonly Dictionary<string, AtlasStaffRecord> _openStaffRecordsV9 =
        new(StringComparer.OrdinalIgnoreCase);
    // Shared record-tab closing supports Clubs; the full Club tab workspace is the next migration.
    private readonly Dictionary<string, AtlasClubRecord> _openClubRecordsV9 =
        new(StringComparer.OrdinalIgnoreCase);
    private string _staffPoolSearchTextV9 = "";

    private async Task<UIElement> StaffWorkspaceV9Async()
    {
        // Search text is held separately and survives workspace navigation.
        if (_staffPoolSearchTextV9.Length == 0)
            _staffPoolSearchTextV9 = _pendingStaffSearchQuery ?? "";

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
            Header = "STAFF DIRECTORY",
            Tag = "__staff-pool",
            Content = BuildStaffDirectoryPageV9()
        };
        tabs.Items.Add(pool);
        tabs.SelectedItem = pool;

        var restore = _openStaffRecordsV9.Values.ToList();
        if (restore.Count > 0)
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,
                new Action(() => _ = RestoreOpenStaffTabsV9Async(restore)));

        await RefreshStaffPoolV9Async();
        return tabs;
    }

    private UIElement BuildStaffDirectoryPageV9()
    {
        var root = new Grid { Margin = new Thickness(8, 8, 8, 4) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var heading = new Grid { Margin = new Thickness(8, 3, 8, 14) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var copy = new StackPanel();
        copy.Children.Add(MonolithDesignSystem.MicroLabel("PEOPLE INTELLIGENCE / STAFF", true));
        copy.Children.Add(MonolithDesignSystem.DisplayText("Staff directory", 25));
        copy.Children.Add(MonolithDesignSystem.BodyText(
            "Find staff and the human manager in the loaded career or imported save. Select a result to open the full staff record in a separate tab.",
            12, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        heading.Children.Add(copy);
        var action = MonolithDesignSystem.ActionButton("OPEN MY MANAGER",
            async (_, _) => await OpenMyManagerAsync());
        action.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(action, 1);
        heading.Children.Add(action);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var searchCard = MonolithDesignSystem.Panel(new Grid(), 12,
            new Thickness(0, 0, 0, 12), new Thickness(10), raised: true);
        var searchGrid = (Grid)searchCard.Child;
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var search = MonolithDesignSystem.TextInput("Search staff, club or record ID", 13);
        search.Text = _staffPoolSearchTextV9;
        search.MinHeight = 42;
        search.Margin = new Thickness(0, 0, 10, 0);
        search.TextChanged += (_, _) => _staffPoolSearchTextV9 = search.Text;
        search.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await RefreshStaffPoolV9Async();
            }
        };
        searchGrid.Children.Add(search);
        var searchButton = MonolithDesignSystem.ActionButton("SEARCH",
            async (_, _) => await RefreshStaffPoolV9Async(), primary: true);
        searchButton.MinWidth = 100;
        Grid.SetColumn(searchButton, 1);
        searchGrid.Children.Add(searchButton);
        Grid.SetRow(searchCard, 1);
        root.Children.Add(searchCard);

        var resultsCard = MonolithDesignSystem.Panel(new DockPanel(), 12, padding: new Thickness(0));
        var dock = (DockPanel)resultsCard.Child;
        var resultHeader = new StackPanel { Margin = new Thickness(12, 10, 12, 8) };
        resultHeader.Children.Add(MonolithDesignSystem.MicroLabel("STAFF RESULTS", true));
        resultHeader.Children.Add(MonolithDesignSystem.BodyText(
            "Open a staff record by selecting a row. CA/PA and source confidence are kept visible.",
            11, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        DockPanel.SetDock(resultHeader, Dock.Top);
        dock.Children.Add(resultHeader);

        var grid = CreateGrid();
        grid.FontSize = 11;
        grid.RowHeight = 42;
        grid.EnableRowVirtualization = true;
        grid.EnableColumnVirtualization = true;
        VirtualizingPanel.SetIsVirtualizing(grid, true);
        VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(grid, true);
        grid.Columns.Add(Column("STAFF MEMBER", "Name", 1.3));
        grid.Columns.Add(Column("ROLE", "Job", .9));
        grid.Columns.Add(Column("CLUB", "Club", 1.0));
        grid.Columns.Add(Column("AGE", "Age", .45));
        grid.Columns.Add(Column("CA", "CurrentAbility", .4));
        grid.Columns.Add(Column("PA", "PotentialAbility", .4));
        grid.Columns.Add(Column("NATION", "Nation", .8));
        grid.Columns.Add(Column("SOURCE / CONFIDENCE", "ValidationStatus", 1.0));
        grid.ItemsSource = _staff;
        grid.SelectionChanged += async (_, _) =>
        {
            if (grid.SelectedItem is not AtlasStaffRecord staff) return;
            try { await ShowStaffRecordTabV9Async(staff); }
            catch (Exception ex)
            {
                AtlasLog.Error("Could not open staff record tab.", ex);
                if (tabs.Items.OfType<TabItem>().FirstOrDefault(x => Equals(x.Tag, "__staff-pool")) is { } poolTab)
                    tabs.SelectedItem = poolTab;
                if (_staffPoolStatusV9 is not null)
                    _staffPoolStatusV9.Text = "Staff profile failed to load: " + ex.Message;
            }
        };
        dock.Children.Add(grid);

        var footer = new DockPanel { Margin = new Thickness(12, 8, 12, 10) };
        _staffPoolStatusV9 = MonolithDesignSystem.DataText(
            "Enter at least two characters to search the available staff index.",
            10, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        DockPanel.SetDock(_staffPoolStatusV9, Dock.Left);
        footer.Children.Add(_staffPoolStatusV9);
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(footer);

        Grid.SetRow(resultsCard, 2);
        root.Children.Add(resultsCard);
        _staffGridV9 = grid;
        return root;
    }

    private TextBlock? _staffPoolStatusV9;
    private DataGrid? _staffGridV9;
    private int _staffPoolRequestVersionV9;

    private async Task RefreshStaffPoolV9Async()
    {
        if (_staffGridV9 is null || _staffPoolStatusV9 is null) return;
        var query = _staffPoolSearchTextV9.Trim();
        var request = ++_staffPoolRequestVersionV9;
        if (query.Length < 2 && string.IsNullOrWhiteSpace(_pendingStaffSourceId))
        {
            _staff.Clear();
            _staffPoolStatusV9.Text = "Type two or more characters. Staff records are not bulk-loaded into the workspace.";
            return;
        }

        _staffPoolStatusV9.Text = "Searching staff…";
        try
        {
            var rows = await RunDatabaseAsync(() => _database.SearchFmsaveStaffAsync(query));
            if (request != _staffPoolRequestVersionV9) return;
            _staff.Clear();
            foreach (var row in rows) _staff.Add(row);
            _staffGridV9.ItemsSource = _staff;
            _staffPoolStatusV9.Text = rows.Count == 0
                ? "No staff records found in the imported sources. Import a supported career save."
                : $"{rows.Count:N0} matching staff records · source confidence is shown per row.";

            if (!string.IsNullOrWhiteSpace(_pendingStaffSourceId))
            {
                var target = rows.FirstOrDefault(x => x.SourceId == _pendingStaffSourceId);
                if (target is not null)
                {
                    _staffGridV9.SelectedItem = target;
                    _staffGridV9.ScrollIntoView(target);
                }
                _pendingStaffSourceId = null;
                _pendingStaffSearchQuery = null;
            }
        }
        catch (Exception ex)
        {
            if (request != _staffPoolRequestVersionV9) return;
            _staffPoolStatusV9.Text = "Staff search failed: " + ex.Message;
            AtlasLog.Error("V9 staff directory search failed.", ex);
        }
    }

    private async Task ShowStaffRecordTabV9Async(AtlasStaffRecord person)
    {
        if (_profileTabs is null) return;
        var tag = "STAFF|" + person.SourceId;
        var existing = _profileTabs.Items.OfType<TabItem>()
            .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _profileTabs.SelectedItem = existing;
            _openStaffRecordsV9[person.SourceId] = person;
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
        header.Children.Add(MonolithDesignSystem.BodyText(person.Name, 11,
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
            ToolTip = "Close staff record"
        };
        close.Click += (_, _) => CloseRecordTabV9(tab);
        header.Children.Add(close);
        tab.Header = header;
        tab.ContextMenu = BuildStaffRecordTabContextMenuV9(tab);
        tab.ContextMenuOpening += (_, _) => _profileTabs.SelectedItem = tab;
        _profileTabs.Items.Add(tab);
        _profileTabs.SelectedItem = tab;
        _openStaffRecordsV9[person.SourceId] = person;
        _profilePanel = content;

        var facts = await RunDatabaseAsync(() => _database.GetFmsaveStaffFactsAsync(person.SourceId));
        content.Children.Add(MonolithDesignSystem.MicroLabel("STAFF RECORD", true));
        content.Children.Add(MonolithDesignSystem.DisplayText(person.Name, 26));
        content.Children.Add(MonolithDesignSystem.BodyText(
            "Staff information is limited to source-backed values available in this career snapshot.",
            12, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        var overview = MonolithDesignSystem.Panel(new StackPanel(), 12,
            new Thickness(0, 12, 0, 12), new Thickness(14), raised: true);
        var overviewStack = (StackPanel)overview.Child;
        AddDetailRow(overviewStack, "Role", person.Job);
        AddDetailRow(overviewStack, "Club", person.Club);
        AddDetailRow(overviewStack, "Age", person.Age?.ToString());
        AddDetailRow(overviewStack, "Nation", person.Nation);
        AddDetailRow(overviewStack, "Current ability", person.CurrentAbility?.ToString() ?? "—");
        AddDetailRow(overviewStack, "Potential ability", person.PotentialAbility?.ToString() ?? "—");
        AddDetailRow(overviewStack, "Human manager", person.IsHumanManager ? "Yes" : "No / not identified");
        AddDetailRow(overviewStack, "Source / confidence", person.ValidationStatus);
        AddDetailRow(overviewStack, "Record ID", person.SourceId);
        content.Children.Add(overview);

        var preferenceFacts = facts.Where(x => x.Field.StartsWith("preference.", StringComparison.OrdinalIgnoreCase)).ToList();
        if (preferenceFacts.Count > 0)
        {
            content.Children.Add(MonolithDesignSystem.MicroLabel("PREFERRED JOB MATCH · PROVISIONAL", true));
            var topJob = preferenceFacts.FirstOrDefault(x => x.Field.Equals("preference.top_job", StringComparison.OrdinalIgnoreCase));
            var preferenceSummary = preferenceFacts.FirstOrDefault(x => x.Field.Equals("preference.summary", StringComparison.OrdinalIgnoreCase));
            var highest = preferenceFacts.FirstOrDefault(x => x.Field.Equals("preference.highest_rating", StringComparison.OrdinalIgnoreCase));
            AddDetailRow(content, "Preferred job", topJob?.Value ?? preferenceSummary?.Value ?? "No clear preferred job recorded");
            if (highest?.Value is not null) AddDetailRow(content, "Highest preference rating", highest.Value + " / 20");
            foreach (var fact in preferenceFacts
                .Where(x => !x.Field.Equals("preference.top_job", StringComparison.OrdinalIgnoreCase)
                    && !x.Field.Equals("preference.summary", StringComparison.OrdinalIgnoreCase)
                    && !x.Field.Equals("preference.highest_rating", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => double.TryParse(x.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var score) ? score : -1)
                .Take(15))
                AddDetailRow(content, fact.Field["preference.".Length..].Replace('_', ' '), fact.Value + " / 20");
            content.Children.Add(MonolithDesignSystem.BodyText(
                "FM27 stores broad role groups; analyst preference combines recruitment and performance analysis, and one group remains unresolved. Scores are not a guarantee of job suitability.",
                10, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        }

        var coachingFacts = facts.Where(x => x.Field.StartsWith("staff_attributes.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Field, StringComparer.OrdinalIgnoreCase).ToList();
        if (coachingFacts.Count > 0)
        {
            content.Children.Add(MonolithDesignSystem.MicroLabel("COACHING ATTRIBUTES · PROVISIONAL", true));
            foreach (var fact in coachingFacts)
                AddDetailRow(content, fact.Field["staff_attributes.".Length..].Replace('_', ' '),
                    fact.Value is null ? "—" : fact.Value + " / 20");
        }

        var personalityFacts = facts.Where(x => x.Field.StartsWith("staff_personality.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Field, StringComparer.OrdinalIgnoreCase).ToList();
        if (personalityFacts.Count > 0)
        {
            content.Children.Add(MonolithDesignSystem.MicroLabel("PERSONALITY · PROVISIONAL", true));
            foreach (var fact in personalityFacts)
                AddDetailRow(content, fact.Field["staff_personality.".Length..].Replace('_', ' '),
                    fact.Value is null ? "—" : fact.Value + " / 20");
        }

        var contractFacts = facts.Where(x => x.Field.StartsWith("contract.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Field, StringComparer.OrdinalIgnoreCase).ToList();
        if (contractFacts.Count > 0)
        {
            content.Children.Add(MonolithDesignSystem.MicroLabel("CONTRACT DETAILS · SOURCE VALUES", true));
            foreach (var fact in contractFacts)
            {
                var field = fact.Field["contract.".Length..].Replace('_', ' ');
                var value = fact.Field.Equals("contract.wage_per_week_gbp", StringComparison.OrdinalIgnoreCase)
                    && decimal.TryParse(fact.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var wage)
                        ? "£" + wage.ToString("N0", CultureInfo.InvariantCulture) + " / week"
                        : fact.Value;
                AddDetailRow(content, field, value);
            }
        }

        var remainingFacts = facts.Where(x =>
            !x.Field.StartsWith("preference.", StringComparison.OrdinalIgnoreCase)
            && !x.Field.StartsWith("staff_attributes.", StringComparison.OrdinalIgnoreCase)
            && !x.Field.StartsWith("staff_personality.", StringComparison.OrdinalIgnoreCase)
            && !x.Field.StartsWith("contract.", StringComparison.OrdinalIgnoreCase)).ToList();
        content.Children.Add(MonolithDesignSystem.MicroLabel("IMPORTED STAFF FACTS", true));
        if (remainingFacts.Count == 0 && coachingFacts.Count == 0 && personalityFacts.Count == 0
            && contractFacts.Count == 0 && preferenceFacts.Count == 0)
            content.Children.Add(MonolithDesignSystem.BodyText("No additional staff facts were imported.", 11,
                MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        foreach (var group in remainingFacts.GroupBy(x => x.Group))
        {
            content.Children.Add(MonolithDesignSystem.MicroLabel(group.Key.ToUpperInvariant()));
            foreach (var fact in group.Take(50))
                AddDetailRow(content, fact.Field.Replace('_', ' ').Replace('.', ' '), fact.Value);
        }
    }

    private ContextMenu BuildStaffRecordTabContextMenuV9(TabItem tab)
    {
        var menu = new ContextMenu();
        var close = new MenuItem { Header = "Close staff record" };
        close.Click += (_, _) => CloseRecordTabV9(tab);
        menu.Items.Add(close);
        var closeOthers = new MenuItem { Header = "Close other staff records" };
        closeOthers.Click += (_, _) => CloseOtherRecordTabsV9(tab, closeToRight: false);
        menu.Items.Add(closeOthers);
        var closeRight = new MenuItem { Header = "Close staff records to the right" };
        closeRight.Click += (_, _) => CloseOtherRecordTabsV9(tab, closeToRight: true);
        menu.Items.Add(closeRight);
        return menu;
    }

    private async Task RestoreOpenStaffTabsV9Async(IReadOnlyList<AtlasStaffRecord> people)
    {
        foreach (var person in people)
        {
            if (_activePage != "Staff" || _profileTabs is null) return;
            try { await ShowStaffRecordTabV9Async(person); }
            catch (Exception ex) { AtlasLog.Warning("Could not restore staff record tab: " + ex.Message); }
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
    }
}
