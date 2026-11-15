using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private sealed class AdvancedSearchUiRow
    {
        public ComboBox CategoryPicker { get; init; } = null!;
        public ComboBox FieldPicker { get; init; } = null!;
        public TextBox Minimum { get; init; } = null!;
        public TextBox Maximum { get; init; } = null!;
        public TextBox TextValue { get; init; } = null!;
        public ComboBox PositionValue { get; init; } = null!;
        public ComboBox BooleanValue { get; init; } = null!;
        public StackPanel ValueHost { get; init; } = null!;
        public Border Container { get; init; } = null!;
    }

    private async Task<UIElement> AdvancedSearchWorkspaceAsync()
    {
        var fields = await RunDatabaseAsync(() => _database.GetAdvancedPlayerSearchFieldsAsync());
        var categories = new[] { "All categories" }
            .Concat(fields.Select(x => x.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var rows = new List<AdvancedSearchUiRow>();
        var results = new List<PlayerRecord>();
        const int pageSize = 250;
        var pageOffset = 0;
        var lastSuccessfulOffset = 0;
        var hasNextPage = false;
        CancellationTokenSource? activeSearchCancellation = null;
        var savedProfiles = AdvancedSearchProfileStore.LoadAll().ToList();
        var resultColumns = new Dictionary<string, DataGridColumn>(StringComparer.OrdinalIgnoreCase);
        var columnChecks = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
        string[] defaultVisibleColumns = { "Name", "Age", "Club", "Position", "League", "Nation", "DisplayHomeGrownClub", "DisplayHomeGrownNation", "DisplayAbility", "DisplayPotential", "DisplayValue", "DataOrigin" };
        StackPanel conditionPanel = null!;
        var positionInputs = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        CheckBox positionAny = null!;
        CheckBox positionAtLeast = null!;
        TextBlock status = null!;
        Button searchButton = null!;
        Button cancelSearchButton = null!;
        Button loadProfileButton = null!;
        Button deleteProfileButton = null!;
        Button previousPageButton = null!;
        Button nextPageButton = null!;
        TextBlock pageStatus = null!;
        DataGrid grid = null!;
        Button compareSelectedButton = null!;
        Button similarSelectedButton = null!;
        Button exportMatchesButton = null!;
        (string Label, string Key)[] sortOptions = Array.Empty<(string Label, string Key)>();
        ComboBox primarySortPicker = null!;
        ComboBox primarySortDirection = null!;
        ComboBox secondarySortPicker = null!;
        ComboBox secondarySortDirection = null!;

        var introduction = MonolithDesignSystem.Panel(new StackPanel(), 14, new Thickness(0, 0, 0, 12), new Thickness(16));
        var intro = (StackPanel)introduction.Child;
        intro.Children.Add(MonolithDesignSystem.MicroLabel("ATLAS SEARCH STUDIO", true));
        intro.Children.Add(MonolithDesignSystem.DisplayText("Advanced Search", 27));
        intro.Children.Add(MonolithDesignSystem.BodyText("Build one player search across core profile fields, imported attributes, personality, contract facts, positions and other available save facts. Choose whether ALL conditions or ANY condition must match. The field library is discovered from actual imported data; unsupported or missing fields are not invented.", 12, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        var summary = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        summary.Children.Add(StatusPill($"{fields.Count:N0} searchable fields", "Available core player fields plus fields actually present in the latest imported save and local Atlas facts."));
        summary.Children.Add(StatusPill($"{categories.Count - 1:N0} categories", "Categories are derived from fields present in the active data."));
        summary.Children.Add(StatusPill("60 conditions", "Maximum conditions in one query."));
        intro.Children.Add(summary);

        var builder = MonolithDesignSystem.Panel(new StackPanel(), 14, new Thickness(0, 0, 0, 10), new Thickness(14));
        var builderStack = (StackPanel)builder.Child;
        builderStack.Children.Add(MonolithDesignSystem.MicroLabel("PLAYER FILTER BUILDER", true));
        builderStack.Children.Add(MonolithDesignSystem.DisplayText("Combine general, contract, attributes, positions and save facts", 17));

        var searchText = Input("Optional player, club, nation or player ID search");
        searchText.Width = 310;
        var fieldSearch = Input("Find a field · Finishing, Pace, CA, PA…");
        fieldSearch.Width = 270;
        var logic = new ComboBox
        {
            Width = 170,
            Height = 35,
            Margin = new Thickness(0, 3, 8, 8),
            FontSize = 10,
            ToolTip = "ALL / AND requires every condition. ANY / OR requires at least one condition."
        };
        logic.Items.Add("All conditions (AND)");
        logic.Items.Add("Any condition (OR)");
        logic.SelectedIndex = 0;

        var profilePicker = new ComboBox
        {
            Width = 205,
            Height = 34,
            Margin = new Thickness(0, 3, 8, 8),
            FontSize = 10,
            DisplayMemberPath = nameof(AdvancedSearchProfile.Name),
            ItemsSource = savedProfiles,
            ToolTip = "Load a saved search profile from this PC."
        };
        if (savedProfiles.Count > 0) profilePicker.SelectedIndex = 0;
        var toolbar = new WrapPanel { Margin = new Thickness(0, 7, 0, 6) };
        toolbar.Children.Add(FilterControl("OPTIONAL PLAYER / CLUB / ID", searchText, 320));
        toolbar.Children.Add(FilterControl("SEARCH FIELD LIBRARY", fieldSearch, 280));
        toolbar.Children.Add(FilterControl("MATCH LOGIC", logic, 180));
        var addButton = SoftButton("＋ Add condition", (_, _) => AddCondition());
        searchButton = PrimaryButton("Search players", async (_, _) => await RunSearchAsync());
        exportMatchesButton = SoftButton("Export all matches CSV", async (_, _) => await ExportMatchingPlayersAsync());
        var shortlistSelectedButton = SoftButton("Add selected to shortlist", async (_, _) => await AddSelectedToShortlistAsync());
        compareSelectedButton = SoftButton("Compare selected (2–4)", async (_, _) => await CompareSelectedPlayersAsync());
        similarSelectedButton = SoftButton("Find similar players", async (_, _) => await FindSimilarForSelectedAsync());
        cancelSearchButton = SoftButton("Cancel search", (_, _) => activeSearchCancellation?.Cancel());
        cancelSearchButton.IsEnabled = false;
        var resetButton = SoftButton("Reset filters", (_, _) => ResetFilters());
        var saveProfileButton = SoftButton("Save profile", (_, _) => SaveCurrentProfile());
        loadProfileButton = SoftButton("Load profile", (_, _) => LoadSelectedProfile());
        deleteProfileButton = SoftButton("Delete profile", (_, _) => DeleteSelectedProfile());
        loadProfileButton.IsEnabled = savedProfiles.Count > 0;
        deleteProfileButton.IsEnabled = savedProfiles.Count > 0;
        toolbar.Children.Add(addButton);
        toolbar.Children.Add(searchButton);
        toolbar.Children.Add(exportMatchesButton);
        toolbar.Children.Add(shortlistSelectedButton);
        toolbar.Children.Add(compareSelectedButton);
        toolbar.Children.Add(similarSelectedButton);
        toolbar.Children.Add(cancelSearchButton);
        toolbar.Children.Add(resetButton);
        toolbar.Children.Add(saveProfileButton);
        toolbar.Children.Add(FilterControl("SAVED SEARCHES", profilePicker, 215));
        toolbar.Children.Add(loadProfileButton);
        toolbar.Children.Add(deleteProfileButton);
        toolbar.Children.Add(SoftButton("Performance Search", async (_, _) => await NavigateAsync("Stats Search")));
        toolbar.Children.Add(SoftButton("Multi-entity Search", (_, _) => ShowGlobalSearch()));
        builderStack.Children.Add(toolbar);

        sortOptions = new (string Label, string Key)[]
        {
            ("Player name", "player:Name"),
            ("Age", "player:Age"),
            ("Club", "player:Club"),
            ("Position", "player:Position"),
            ("League", "player:League"),
            ("Nationality", "player:Nation"),
            ("Current ability", "player:CurrentAbility"),
            ("Potential ability", "player:PotentialAbility"),
            ("Market value", "player:Value")
        };
        primarySortPicker = new ComboBox
        {
            Width = 175, Height = 32, FontSize = 10, Margin = new Thickness(0, 0, 8, 0),
            ItemsSource = sortOptions.Select(x => x.Label).ToList(),
            ToolTip = "Primary sort. Applied in SQLite before paging, not just to the visible page."
        };
        primarySortPicker.SelectedItem = "Player name";
        primarySortDirection = new ComboBox
        {
            Width = 112, Height = 32, FontSize = 10, Margin = new Thickness(0, 0, 8, 0),
            ItemsSource = new[] { "Ascending", "Descending" }
        };
        primarySortDirection.SelectedIndex = 0;
        secondarySortPicker = new ComboBox
        {
            Width = 175, Height = 32, FontSize = 10, Margin = new Thickness(0, 0, 8, 0),
            ItemsSource = new[] { "(None)" }.Concat(sortOptions.Select(x => x.Label)).ToList(),
            ToolTip = "Optional secondary sort for ties in the primary field."
        };
        secondarySortPicker.SelectedIndex = 0;
        secondarySortDirection = new ComboBox
        {
            Width = 112, Height = 32, FontSize = 10, Margin = new Thickness(0, 0, 8, 0),
            ItemsSource = new[] { "Ascending", "Descending" }
        };
        secondarySortDirection.SelectedIndex = 0;
        var sortToolbar = new WrapPanel { Margin = new Thickness(2, 2, 0, 8) };
        sortToolbar.Children.Add(FilterControl("SORT BY", primarySortPicker, 185));
        sortToolbar.Children.Add(FilterControl("DIRECTION", primarySortDirection, 122));
        sortToolbar.Children.Add(FilterControl("THEN BY", secondarySortPicker, 185));
        sortToolbar.Children.Add(FilterControl("DIRECTION", secondarySortDirection, 122));
        builderStack.Children.Add(sortToolbar);

        string? ResolveSortKey(string? label)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            foreach (var option in sortOptions)
                if (option.Label.Equals(label, StringComparison.OrdinalIgnoreCase))
                    return option.Key;
            return null;
        }

        string? ResolveSortLabel(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            foreach (var option in sortOptions)
                if (option.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return option.Label;
            return null;
        }

        List<AdvancedSearchSort> CaptureSorts()
        {
            var captured = new List<AdvancedSearchSort>(2);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var primaryKey = ResolveSortKey(primarySortPicker.SelectedItem?.ToString());
            if (!string.IsNullOrWhiteSpace(primaryKey) && seen.Add(primaryKey))
                captured.Add(new AdvancedSearchSort(primaryKey, primarySortDirection.SelectedIndex == 1));
            var secondaryKey = ResolveSortKey(secondarySortPicker.SelectedItem?.ToString());
            if (!string.IsNullOrWhiteSpace(secondaryKey) && seen.Add(secondaryKey))
                captured.Add(new AdvancedSearchSort(secondaryKey, secondarySortDirection.SelectedIndex == 1));
            return captured;
        }

        void ApplySorts(IReadOnlyList<AdvancedSearchSort>? savedSorts)
        {
            var primary = savedSorts?.FirstOrDefault();
            primarySortPicker.SelectedItem = ResolveSortLabel(primary?.FieldKey) ?? "Player name";
            primarySortDirection.SelectedItem = primary?.Descending == true ? "Descending" : "Ascending";
            var secondary = savedSorts?.Skip(1).FirstOrDefault();
            secondarySortPicker.SelectedItem = ResolveSortLabel(secondary?.FieldKey) ?? "(None)";
            secondarySortDirection.SelectedItem = secondary?.Descending == true ? "Descending" : "Ascending";
        }

        conditionPanel = new StackPanel();
        builderStack.Children.Add(Txt("CONDITIONS · CHOOSE A CATEGORY, FIELD AND VALUE", 8, true, Muted));
        builderStack.Children.Add(conditionPanel);

        // Independent FMRTE-style position group: zero is a no-op; ALL/ANY never
        // changes the existing global AND/OR logic.
        var positionCard = MonolithDesignSystem.Panel(new StackPanel(), 12, new Thickness(0, 10, 0, 8), new Thickness(12), raised: true);
        var positionStack = (StackPanel)positionCard.Child;
        positionStack.Children.Add(MonolithDesignSystem.MicroLabel("POSITION FAMILIARITY", true));
        positionStack.Children.Add(MonolithDesignSystem.DisplayText("Set a 0–20 rating for any of the 15 FM positions", 15));
        positionStack.Children.Add(Txt("Zero means no requirement. Exact matches are used by default; turn on At Least to treat each entered rating as a minimum. Save-reader position values may remain provisional until checked in FM27.", 10, false, Muted, wrap: true));
        var positionOptions = new WrapPanel { Margin = new Thickness(0, 8, 0, 5) };
        positionAny = new CheckBox { Content = "Any of these", Foreground = Ink, FontSize = 10, Margin = new Thickness(0, 3, 18, 5), ToolTip = "A player needs to match at least one entered position. Otherwise every entered position must match." };
        positionAtLeast = new CheckBox { Content = "At Least", Foreground = Ink, FontSize = 10, Margin = new Thickness(0, 3, 18, 5), ToolTip = "Treat each entered position rating as a minimum. Off means exact rating." };
        positionOptions.Children.Add(positionAny);
        positionOptions.Children.Add(positionAtLeast);
        positionStack.Children.Add(positionOptions);
        var positionTiles = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        var positionLabels = new (string Code, string Label)[]
        {
            ("GK", "Goalkeeper"), ("SW", "Sweeper"), ("DL", "Left defender"), ("DC", "Centre back"), ("DR", "Right defender"),
            ("WBL", "Left wing-back"), ("WBR", "Right wing-back"), ("DM", "Defensive midfield"), ("ML", "Left midfield"),
            ("MC", "Central midfield"), ("MR", "Right midfield"), ("AML", "Left attack"), ("AMC", "Central attack"),
            ("AMR", "Right attack"), ("STC", "Striker")
        };
        foreach (var position in positionLabels)
        {
            var tileStack = new StackPanel();
            tileStack.Children.Add(Txt(position.Code, 11, true, Ink));
            tileStack.Children.Add(Txt(position.Label, 8, false, Muted, wrap: true));
            var rating = new TextBox
            {
                Text = "0", Width = 54, Height = 30, Margin = new Thickness(0, 5, 0, 0),
                Padding = new Thickness(5, 3, 5, 3), FontSize = 11, FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceRaised), Foreground = MonolithDesignSystem.Solid(MonolithDesignSystem.Text), BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border), BorderThickness = new Thickness(1),
                ToolTip = position.Label + " familiarity · 0–20 · zero disables this requirement"
            };
            System.Windows.Automation.AutomationProperties.SetName(rating, position.Code + " position familiarity rating 0 to 20");
            positionInputs[position.Code] = rating;
            tileStack.Children.Add(rating);
            positionTiles.Children.Add(new Border
            {
                Width = 118, Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceRaised), BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9), Padding = new Thickness(9, 8, 9, 8),
                Margin = new Thickness(0, 0, 7, 7), Child = tileStack
            });
        }
        positionStack.Children.Add(positionTiles);
        builderStack.Children.Add(positionCard);

        status = Txt(fields.Count == 0 ? "No fields are available yet. Import a career save copy or add local scouting data." : "Add one or more conditions, then run the search.", 10, false, Muted, wrap: true);
        status.Margin = new Thickness(2, 8, 2, 0);
        builderStack.Children.Add(status);

        var resultCard = MonolithDesignSystem.Panel(new DockPanel(), 14, padding: new Thickness(14));
        var dock = (DockPanel)resultCard.Child;
        var resultHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        resultHeader.Children.Add(MonolithDesignSystem.MicroLabel("PLAYER MATCHES", true));
        resultHeader.Children.Add(Txt("Double-click a result to open its player profile. Source origin is shown so live identities, save records and local records stay distinguishable.", 10, false, Muted, wrap: true));
        var pager = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 0, 0) };
        previousPageButton = SoftButton("← Previous", async (_, _) =>
        {
            if (pageOffset <= 0) return;
            pageOffset = Math.Max(0, pageOffset - pageSize);
            await RunSearchAsync(false);
        });
        nextPageButton = SoftButton("Next →", async (_, _) =>
        {
            if (!hasNextPage) return;
            pageOffset += pageSize;
            await RunSearchAsync(false);
        });
        pageStatus = Txt("No search results yet", 9, false, Muted);
        pageStatus.Margin = new Thickness(8, 0, 8, 0);
        previousPageButton.IsEnabled = false;
        nextPageButton.IsEnabled = false;
        pager.Children.Add(previousPageButton);
        pager.Children.Add(pageStatus);
        pager.Children.Add(nextPageButton);
        resultHeader.Children.Add(pager);
        DockPanel.SetDock(resultHeader, Dock.Top);
        dock.Children.Add(resultHeader);

        grid = CreateGrid();
        grid.MinHeight = 310;
        grid.MaxHeight = 560;
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        grid.ToolTip = "Select multiple players with Ctrl or Shift. Double-click a player to open their profile.";
        void AddResultColumn(string header, string property, double weight)
        {
            var column = Column(header, property, weight);
            resultColumns[property] = column;
            grid.Columns.Add(column);
        }
        AddResultColumn("PLAYER", "Name", 1.2);
        AddResultColumn("AGE", "Age", .45);
        AddResultColumn("CLUB", "Club", 1.0);
        AddResultColumn("POSITION", "Position", .7);
        AddResultColumn("LEAGUE", "League", 1.0);
        AddResultColumn("NATION", "Nation", .8);
        AddResultColumn("HOME-GROWN · CLUB", "DisplayHomeGrownClub", .72);
        AddResultColumn("HOME-GROWN · NATION", "DisplayHomeGrownNation", .72);
        AddResultColumn("CA", "DisplayAbility", .45);
        AddResultColumn("PA", "DisplayPotential", .45);
        AddResultColumn("VALUE", "DisplayValue", .7);
        AddResultColumn("SOURCE", "DataOrigin", .9);

        var columnPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var columnNames = new (string Key, string Label)[]
        {
            ("Name", "Player"), ("Age", "Age"), ("Club", "Club"), ("Position", "Position"),
            ("League", "League"), ("Nation", "Nation"),
            ("DisplayHomeGrownClub", "Home-grown (club)"), ("DisplayHomeGrownNation", "Home-grown (nation)"),
            ("DisplayAbility", "CA"), ("DisplayPotential", "PA"), ("DisplayValue", "Value"), ("DataOrigin", "Source")
        };
        foreach (var item in columnNames)
        {
            var check = new CheckBox
            {
                Content = item.Label,
                IsChecked = defaultVisibleColumns.Contains(item.Key, StringComparer.OrdinalIgnoreCase),
                IsEnabled = !item.Key.Equals("Name", StringComparison.OrdinalIgnoreCase),
                Foreground = Ink,
                FontSize = 10,
                Margin = new Thickness(0, 2, 13, 5),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"Show or hide the {item.Label} result column."
            };
            check.Checked += (_, _) => SetColumnVisibility(item.Key, true);
            check.Unchecked += (_, _) => SetColumnVisibility(item.Key, false);
            columnChecks[item.Key] = check;
            columnPanel.Children.Add(check);
        }
        var columnCard = MonolithDesignSystem.Panel(new StackPanel(), 10, new Thickness(0, 0, 0, 8), new Thickness(10), raised: true);
        ((StackPanel)columnCard.Child).Children.Add(MonolithDesignSystem.MicroLabel("RESULT COLUMNS"));
        ((StackPanel)columnCard.Child).Children.Add(columnPanel);
        DockPanel.SetDock(columnCard, Dock.Top);
        dock.Children.Add(columnCard);
        void SetColumnVisibility(string key, bool visible)
        {
            if (key.Equals("Name", StringComparison.OrdinalIgnoreCase)) visible = true;
            if (resultColumns.TryGetValue(key, out var column))
                column.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
        void ApplyVisibleColumns(IEnumerable<string>? visible)
        {
            var selected = (visible ?? defaultVisibleColumns).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!selected.Contains("Name")) selected.Add("Name");
            foreach (var pair in resultColumns)
                pair.Value.Visibility = selected.Contains(pair.Key) ? Visibility.Visible : Visibility.Collapsed;
            foreach (var pair in columnChecks)
                pair.Value.IsChecked = selected.Contains(pair.Key);
        }
        grid.MouseDoubleClick += async (_, _) =>
        {
            if (grid.SelectedItem is not PlayerRecord player) return;
            await NavigateAsync("Players");
            _selectedPlayer = player;
            try { await ShowPlayerProfileAsync(player); }
            catch (Exception ex) { ClearProfile("The player profile could not be opened: " + ex.Message); }
        };
        dock.Children.Add(grid);


        List<PlayerRecord> CaptureSelectedPlayers(int minimum, int maximum)
            => grid.SelectedItems.OfType<PlayerRecord>()
                .Where(x => !string.IsNullOrWhiteSpace(x.SourceId))
                .GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .Take(maximum + 1)
                .ToList();

        async Task CompareSelectedPlayersAsync()
        {
            var selected = CaptureSelectedPlayers(2, 4);
            if (selected.Count < 2)
            {
                status.Text = "Select two to four players in the results grid to compare them side by side.";
                return;
            }
            if (selected.Count > 4)
            {
                status.Text = "Player comparison supports up to four players. Refine your selection and try again.";
                return;
            }
            await PlayerComparisonWindow.ShowAsync(this, _database, selected);
        }

        async Task FindSimilarForSelectedAsync()
        {
            var selected = CaptureSelectedPlayers(1, 1);
            if (selected.Count == 0)
            {
                status.Text = "Select one player in the results grid to find similar players.";
                return;
            }
            if (selected.Count > 1)
            {
                status.Text = "Select just one reference player for Similar Player Discovery.";
                return;
            }
            try
            {
                status.Text = $"Finding players with similar source-backed attributes to {selected[0].Name}…";
                similarSelectedButton.IsEnabled = false;
                var candidates = await RunDatabaseAsync(() => _database.FindSimilarPlayersAsync(
                    selected[0].SourceId, limit: 100, minimumCommonAttributes: 4, minimumAnchorCoverage: 0.35));
                var window = new SimilarPlayersWindow(_database, selected[0], candidates) { Owner = this };
                window.ShowDialog();
                status.Text = $"Similar Player Discovery completed · {candidates.Count:N0} candidates · shared attribute coverage is shown for each result.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Similar Player Discovery failed.", ex);
                status.Text = "Similar Player Discovery failed: " + ex.Message;
            }
            finally
            {
                similarSelectedButton.IsEnabled = true;
            }
        }

        async Task ExportMatchingPlayersAsync()
        {
            if (activeSearchCancellation is not null)
            {
                status.Text = "Finish or cancel the current search before starting an export.";
                return;
            }

            var criteria = CaptureCriteria(out var error);
            if (error.Length > 0) { status.Text = error; return; }
            var positionFilter = CapturePositionFilter(out var positionError);
            if (positionError.Length > 0) { status.Text = positionError; return; }
            if (criteria.Count == 0 && positionFilter is null)
            {
                status.Text = "Enter at least one general condition or position rating before exporting matches.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export all matching Advanced Search players",
                FileName = "FM-Atlas-advanced-search.csv",
                Filter = "CSV files (*.csv)|*.csv"
            };
            if (dialog.ShowDialog(this) != true) return;

            var query = searchText.Text;
            var matchAll = logic.SelectedIndex == 0;
            var sorts = CaptureSorts();
            using var cancellation = new CancellationTokenSource();
            activeSearchCancellation = cancellation;
            cancelSearchButton.IsEnabled = true;
            exportMatchesButton.IsEnabled = false;
            searchButton.IsEnabled = false;
            addButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;

            var destination = dialog.FileName;
            var temporaryFile = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            const int batchSize = 500;
            const int maximumRows = 50000;
            var exported = 0;
            var truncated = false;
            try
            {
                await using (var stream = new FileStream(
                    temporaryFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                await using (var writer = new StreamWriter(stream, new UTF8Encoding(true)))
                {
                    await writer.WriteLineAsync(CsvExportSafety.AdvancedPlayerHeader);

                    for (var offset = 0; offset < maximumRows; offset += batchSize)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        var batch = await RunDatabaseAsync(() => _database.SearchAdvancedPlayersAsync(
                            query, criteria, matchAll, batchSize, offset, cancellation.Token, sorts, positionFilter));
                        foreach (var player in batch)
                        {
                            cancellation.Token.ThrowIfCancellationRequested();
                            await writer.WriteLineAsync(CsvExportSafety.FormatAdvancedPlayerRow(player));
                            exported++;
                        }
                        status.Text = $"Exporting Advanced Search matches · {exported:N0} players written.";
                        if (batch.Count < batchSize) break;
                    }

                    if (exported == maximumRows)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        var extra = await RunDatabaseAsync(() => _database.SearchAdvancedPlayersAsync(
                            query, criteria, matchAll, 1, maximumRows, cancellation.Token, sorts, positionFilter));
                        truncated = extra.Count > 0;
                    }
                    await writer.FlushAsync();
                }

                cancellation.Token.ThrowIfCancellationRequested();
                File.Move(temporaryFile, destination, overwrite: true);
                status.Text = truncated
                    ? $"Exported the first {exported:N0} matching players. The 50,000-row safety cap was reached; narrow the search to export a smaller complete set."
                    : $"Exported all {exported:N0} matching players with source IDs and source-backed profile fields.";
                AtlasLog.Info($"Advanced Search CSV export completed: rows={exported}; truncated={truncated}; conditions={criteria.Count}; matchAll={matchAll}.");
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(temporaryFile)) File.Delete(temporaryFile); } catch { }
                status.Text = $"Advanced Search CSV export cancelled · {exported:N0} rows collected; no partial CSV was published.";
            }
            catch (Exception ex)
            {
                try { if (File.Exists(temporaryFile)) File.Delete(temporaryFile); } catch { }
                AtlasLog.Error("Advanced Search CSV export failed.", ex);
                status.Text = "Advanced Search export failed: " + ex.Message;
            }
            finally
            {
                activeSearchCancellation = null;
                cancelSearchButton.IsEnabled = false;
                exportMatchesButton.IsEnabled = true;
                searchButton.IsEnabled = true;
                addButton.IsEnabled = true;
                Mouse.OverrideCursor = null;
            }
        }

        async Task AddSelectedToShortlistAsync()
        {
            var selected = grid.SelectedItems.OfType<PlayerRecord>()
                .Where(x => !string.IsNullOrWhiteSpace(x.SourceId))
                .GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();
            if (selected.Count == 0)
            {
                status.Text = "Select one or more player rows first. Use Ctrl or Shift for multi-select.";
                return;
            }

            try
            {
                var names = await RunDatabaseAsync(() => _database.GetShortlistNamesAsync());
                var picker = new ComboBox
                {
                    IsEditable = true,
                    ItemsSource = names,
                    Text = names.FirstOrDefault() ?? "My Shortlist",
                    Height = 36,
                    Margin = new Thickness(0, 8, 0, 14),
                    FontSize = 12
                };
                var dialog = new Window
                {
                    Title = "Add selected players to shortlist",
                    Owner = this,
                    Width = 440,
                    Height = 205,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Background = Brush("#171F28"),
                    Foreground = Ink
                };
                var panel = new StackPanel { Margin = new Thickness(18) };
                panel.Children.Add(Txt($"Add {selected.Count:N0} selected players", 17, true, Ink));
                panel.Children.Add(Txt("Choose an existing shortlist or type a new name. The player records and career save are not modified.", 10, false, Muted, wrap: true));
                panel.Children.Add(picker);
                var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                actions.Children.Add(SoftButton("Cancel", (_, _) => dialog.DialogResult = false));
                actions.Children.Add(PrimaryButton("Add players", (_, _) => dialog.DialogResult = true));
                panel.Children.Add(actions);
                dialog.Content = panel;

                if (dialog.ShowDialog() != true) return;
                var shortlistName = picker.Text.Trim();
                if (shortlistName.Length == 0)
                {
                    status.Text = "Enter a shortlist name before adding players.";
                    return;
                }

                var added = await RunDatabaseAsync(() => _database.AddPlayersToShortlistAsync(selected, shortlistName));
                var alreadyPresent = selected.Count - added;
                status.Text = added == 0
                    ? $"All {alreadyPresent:N0} selected players were already in “{shortlistName}”."
                    : $"Added {added:N0} new players to “{shortlistName}” · {alreadyPresent:N0} were already there.";
                AtlasLog.Info($"Advanced Search shortlist batch: name={shortlistName}; selected={selected.Count}; added={added}; existing={alreadyPresent}.");
                grid.SelectedItems.Clear();
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Could not add selected Advanced Search players to a shortlist.", ex);
                status.Text = "Shortlist update failed: " + ex.Message;
            }
        }

        void UpdateValueControls(AdvancedSearchUiRow row)
        {
            row.ValueHost.Children.Clear();
            if (row.FieldPicker.SelectedItem is not AdvancedSearchField field) return;
            row.Container.ToolTip = $"Source: {field.Source} · Category: {field.Category}";
            switch (field.FieldType)
            {
                case AdvancedSearchFieldType.Number:
                    row.ValueHost.Children.Add(row.Minimum);
                    row.ValueHost.Children.Add(row.Maximum);
                    break;
                case AdvancedSearchFieldType.Text:
                    if (field.Key.Equals("player:Position", StringComparison.OrdinalIgnoreCase))
                        row.ValueHost.Children.Add(row.PositionValue);
                    else
                        row.ValueHost.Children.Add(row.TextValue);
                    break;
                case AdvancedSearchFieldType.Boolean:
                    row.ValueHost.Children.Add(row.BooleanValue);
                    break;
            }
        }

        void RefreshFieldChoices(AdvancedSearchUiRow row, string? preferredKey = null)
        {
            var category = row.CategoryPicker.SelectedItem?.ToString() ?? "All categories";
            IEnumerable<AdvancedSearchField> source = category.Equals("All categories", StringComparison.OrdinalIgnoreCase)
                ? fields
                : fields.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            var term = fieldSearch.Text.Trim();
            if (term.Length > 0)
                source = source.Where(x => x.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || x.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || x.Key.Contains(term, StringComparison.OrdinalIgnoreCase));
            var choices = source.ToList();
            row.FieldPicker.ItemsSource = choices;
            row.FieldPicker.SelectedItem = preferredKey is null ? null : choices.FirstOrDefault(x => x.Key.Equals(preferredKey, StringComparison.OrdinalIgnoreCase));
            if (row.FieldPicker.SelectedIndex < 0 && choices.Count > 0) row.FieldPicker.SelectedIndex = 0;
            UpdateValueControls(row);
        }

        fieldSearch.TextChanged += (_, _) =>
        {
            foreach (var row in rows.ToArray())
            {
                var selectedKey = (row.FieldPicker.SelectedItem as AdvancedSearchField)?.Key;
                RefreshFieldChoices(row, selectedKey);
            }
            status.Text = fieldSearch.Text.Trim().Length == 0
                ? "Showing the complete field library for the selected categories."
                : "Field library filtered by “" + fieldSearch.Text.Trim() + "”. Choose a result in each condition.";
        };

        void MoveCondition(AdvancedSearchUiRow row, int offset)
        {
            var current = rows.IndexOf(row);
            var target = current + offset;
            if (current < 0 || target < 0 || target >= rows.Count) return;

            // Keep the visual row order and the criteria-capture order identical.
            rows.RemoveAt(current);
            rows.Insert(target, row);
            conditionPanel.Children.Remove(row.Container);
            conditionPanel.Children.Insert(target, row.Container);
            status.Text = $"Condition {target + 1} of {rows.Count:N0} · order updated.";
        }

        AdvancedSearchUiRow? AddCondition(string? category = null, string? fieldKey = null)
        {
            if (rows.Count >= 60)
            {
                status.Text = "A universal player search supports up to 60 conditions.";
                return null;
            }
            if (fields.Count == 0) return null;

            var categoryPicker = new ComboBox
            {
                Width = 142,
                Height = 34,
                Margin = new Thickness(0, 0, 7, 0),
                FontSize = 10,
                ItemsSource = categories,
                ToolTip = "Filter the field library by category."
            };
            categoryPicker.SelectedItem = categories.FirstOrDefault(x => x.Equals(category, StringComparison.OrdinalIgnoreCase)) ?? "All categories";

            var fieldPicker = new ComboBox
            {
                Width = 245,
                Height = 34,
                Margin = new Thickness(0, 0, 7, 0),
                FontSize = 10,
                DisplayMemberPath = nameof(AdvancedSearchField.PickerLabel),
                IsTextSearchEnabled = true,
                ToolTip = "Choose a field that is present in the available Atlas data."
            };
            var minimum = Input("Minimum");
            minimum.Width = 82;
            minimum.Height = 34;
            minimum.Margin = new Thickness(0, 0, 6, 0);
            var maximum = Input("Maximum");
            maximum.Width = 82;
            maximum.Height = 34;
            maximum.Margin = new Thickness(0, 0, 6, 0);
            var textValue = Input("Contains text");
            textValue.Width = 150;
            textValue.Height = 34;
            textValue.Margin = new Thickness(0, 0, 6, 0);
            var positionValue = new ComboBox
            {
                Width = 132,
                Height = 34,
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 10,
                IsEditable = true,
                IsTextSearchEnabled = true,
                ItemsSource = PositionSuitabilityService.PositionCodes.Concat(new[] { "ST" }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                ToolTip = "Choose one of the 15 canonical FM positions. ST is retained as a legacy centre-forward alias; you can also type a source-specific position value."
            };
            var booleanValue = new ComboBox
            {
                Width = 105,
                Height = 34,
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 10,
                ToolTip = "Match Yes or No."
            };
            booleanValue.Items.Add("Choose…");
            booleanValue.Items.Add("Yes");
            booleanValue.Items.Add("No");
            booleanValue.SelectedIndex = 0;

            var valueHost = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Border container = null!;
            AdvancedSearchUiRow row = null!;
            var moveUp = SoftButton("↑", (_, _) => MoveCondition(row, -1));
            moveUp.Width = 32;
            moveUp.Height = 32;
            moveUp.Margin = new Thickness(2, 0, 2, 0);
            moveUp.ToolTip = "Move this condition earlier in the search.";
            System.Windows.Automation.AutomationProperties.SetName(moveUp, "Move condition up");
            var moveDown = SoftButton("↓", (_, _) => MoveCondition(row, 1));
            moveDown.Width = 32;
            moveDown.Height = 32;
            moveDown.Margin = new Thickness(2, 0, 2, 0);
            moveDown.ToolTip = "Move this condition later in the search.";
            System.Windows.Automation.AutomationProperties.SetName(moveDown, "Move condition down");
            var remove = SoftButton("×", (_, _) =>
            {
                conditionPanel.Children.Remove(container);
                rows.RemoveAll(x => x.Container == container);
                status.Text = $"{rows.Count:N0} condition(s) configured.";
            });
            remove.Width = 32;
            remove.Height = 32;
            remove.Margin = new Thickness(2, 0, 2, 0);
            remove.ToolTip = "Remove this condition.";
            System.Windows.Automation.AutomationProperties.SetName(remove, "Remove condition");

            // Wrap controls instead of forcing a single horizontal strip. This keeps
            // the filter builder usable at narrower window widths and higher DPI scales.
            var rowPanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            rowPanel.Children.Add(categoryPicker);
            rowPanel.Children.Add(fieldPicker);
            rowPanel.Children.Add(valueHost);
            rowPanel.Children.Add(moveUp);
            rowPanel.Children.Add(moveDown);
            rowPanel.Children.Add(remove);
            container = new Border
            {
                Background = Brush("#171F28"),
                BorderBrush = Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 5, 0, 5),
                Child = rowPanel
            };

            row = new AdvancedSearchUiRow
            {
                CategoryPicker = categoryPicker,
                FieldPicker = fieldPicker,
                Minimum = minimum,
                Maximum = maximum,
                TextValue = textValue,
                PositionValue = positionValue,
                BooleanValue = booleanValue,
                ValueHost = valueHost,
                Container = container
            };
            rows.Add(row);
            conditionPanel.Children.Add(container);

            categoryPicker.SelectionChanged += (_, _) => RefreshFieldChoices(row);
            fieldPicker.SelectionChanged += (_, _) => UpdateValueControls(row);
            RefreshFieldChoices(row, fieldKey);
            return row;
        }

        void ResetFilters()
        {
            activeSearchCancellation?.Cancel();
            rows.Clear();
            conditionPanel.Children.Clear();
            searchText.Clear();
            logic.SelectedIndex = 0;
            ApplyPositionFilter(null);
            ApplySorts(null);
            AddCondition();
            ApplyVisibleColumns(defaultVisibleColumns);
            pageOffset = 0;
            hasNextPage = false;
            previousPageButton.IsEnabled = false;
            nextPageButton.IsEnabled = false;
            pageStatus.Text = "No search results yet";
            status.Text = "Filters reset. Existing data and saved career snapshots are unchanged.";
            grid.ItemsSource = null;
            results.Clear();
        }

        void ApplyPositionFilter(AdvancedPositionFilter? saved)
        {
            foreach (var input in positionInputs.Values) input.Text = "0";
            positionAny.IsChecked = saved?.MatchAny == true;
            positionAtLeast.IsChecked = saved?.AtLeast == true;
            foreach (var item in saved?.Positions ?? Array.Empty<AdvancedPositionCriterion>())
                if (positionInputs.TryGetValue(item.PositionCode, out var input))
                    input.Text = item.Rating.ToString(CultureInfo.InvariantCulture);
        }

        AdvancedPositionFilter? CapturePositionFilter(out string error)
        {
            error = "";
            var selected = new List<AdvancedPositionCriterion>();
            foreach (var pair in positionInputs)
            {
                var raw = pair.Value.Text.Trim();
                if (raw.Length == 0) continue;
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating) || rating is < 0 or > 20)
                {
                    error = pair.Key + " familiarity must be a whole number from 0 to 20. Use 0 for no requirement.";
                    pair.Value.Focus();
                    return null;
                }
                if (rating > 0) selected.Add(new AdvancedPositionCriterion(pair.Key, rating));
            }
            return AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
                positionAny.IsChecked == true, positionAtLeast.IsChecked == true, selected));
        }

        List<AdvancedSearchCriterion> CaptureCriteria(out string error)
        {
            error = "";
            var criteria = new List<AdvancedSearchCriterion>();
            foreach (var row in rows)
            {
                if (row.FieldPicker.SelectedItem is not AdvancedSearchField field) continue;
                switch (field.FieldType)
                {
                    case AdvancedSearchFieldType.Number:
                    {
                        var hasMin = double.TryParse(row.Minimum.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var lower);
                        var hasMax = double.TryParse(row.Maximum.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var upper);
                        if (!hasMin && !hasMax) continue;
                        if (hasMin && hasMax && lower > upper)
                        {
                            error = $"Minimum cannot exceed maximum for {field.DisplayName}.";
                            return new();
                        }
                        if (field.Key.StartsWith("attribute:", StringComparison.OrdinalIgnoreCase)
                            && ((hasMin && (lower is < 1 or > 20 || !double.IsInteger(lower))) || (hasMax && (upper is < 1 or > 20 || !double.IsInteger(upper)))))
                        {
                            error = $"{field.DisplayName} must be between 1 and 20.";
                            return new();
                        }
                        if (field.Key.Equals("player:CurrentAbility", StringComparison.OrdinalIgnoreCase)
                            && ((hasMin && (lower is < 0 or > 200 || !double.IsInteger(lower))) || (hasMax && (upper is < 0 or > 200 || !double.IsInteger(upper)))))
                        {
                            error = "Current Ability must be between 0 and 200.";
                            return new();
                        }
                        if (field.Key.Equals("player:PotentialAbility", StringComparison.OrdinalIgnoreCase)
                            && ((hasMin && (lower is < -10 or > 200 || !double.IsInteger(lower))) || (hasMax && (upper is < -10 or > 200 || !double.IsInteger(upper)))))
                        {
                            error = "Potential Ability must be between -10 and 200, including negative random-potential values.";
                            return new();
                        }
                        criteria.Add(new AdvancedSearchCriterion(field.Key, hasMin ? lower : null, hasMax ? upper : null, null, null));
                        break;
                    }
                    case AdvancedSearchFieldType.Text:
                    {
                        var value = field.Key.Equals("player:Position", StringComparison.OrdinalIgnoreCase)
                            ? row.PositionValue.Text.Trim()
                            : row.TextValue.Text.Trim();
                        if (value.Length == 0) continue;
                        criteria.Add(new AdvancedSearchCriterion(field.Key, null, null, value, null));
                        break;
                    }
                    case AdvancedSearchFieldType.Boolean:
                    {
                        if (row.BooleanValue.SelectedIndex < 1) continue;
                        criteria.Add(new AdvancedSearchCriterion(field.Key, null, null, null, row.BooleanValue.SelectedIndex == 1));
                        break;
                    }
                }
            }
            return criteria;
        }

        string? AskProfileName(string initialValue = "")
        {
            var dialog = new Window
            {
                Title = "Save advanced search profile",
                Owner = this,
                Width = 420,
                Height = 188,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brush("#111820"),
                Foreground = Ink,
                ShowInTaskbar = false
            };
            var panel = new StackPanel { Margin = new Thickness(18) };
            panel.Children.Add(Txt("PROFILE NAME", 9, true, Teal));
            var nameBox = Input("e.g. U21 high-potential midfielders");
            nameBox.Text = initialValue;
            nameBox.Margin = new Thickness(0, 8, 0, 12);
            panel.Children.Add(nameBox);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var save = PrimaryButton("Save", (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(nameBox.Text)) dialog.DialogResult = true;
            });
            save.Width = 90;
            var cancel = SoftButton("Cancel", (_, _) => dialog.DialogResult = false);
            cancel.Width = 90;
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            panel.Children.Add(buttons);
            dialog.Content = panel;
            dialog.Loaded += (_, _) => { nameBox.Focus(); nameBox.SelectAll(); };
            return dialog.ShowDialog() == true ? nameBox.Text.Trim() : null;
        }

        void RefreshProfilePicker(string? selectId = null)
        {
            savedProfiles = AdvancedSearchProfileStore.LoadAll().ToList();
            profilePicker.ItemsSource = savedProfiles;
            profilePicker.SelectedItem = selectId is null
                ? savedProfiles.FirstOrDefault()
                : savedProfiles.FirstOrDefault(x => x.Id.Equals(selectId, StringComparison.OrdinalIgnoreCase));
            loadProfileButton.IsEnabled = savedProfiles.Count > 0;
            deleteProfileButton.IsEnabled = savedProfiles.Count > 0;
        }

        void SaveCurrentProfile()
        {
            var criteria = CaptureCriteria(out var error);
            if (error.Length > 0) { status.Text = error; return; }
            var positionFilter = CapturePositionFilter(out var positionError);
            if (positionError.Length > 0) { status.Text = positionError; return; }
            if (criteria.Count == 0 && positionFilter is null) { status.Text = "Add at least one general or position condition before saving a profile."; return; }
            var existing = profilePicker.SelectedItem as AdvancedSearchProfile;
            var name = AskProfileName(existing?.Name ?? "");
            if (string.IsNullOrWhiteSpace(name)) return;
            var sameName = savedProfiles.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (sameName is not null && !MessageBox.Show(this, $"A search profile named '{name}' already exists. Replace it?", "FM Atlas · Replace profile", MessageBoxButton.YesNo, MessageBoxImage.Question).Equals(MessageBoxResult.Yes))
                return;
            var profile = new AdvancedSearchProfile(
                sameName?.Id ?? Guid.NewGuid().ToString("N"),
                name,
                DateTime.UtcNow,
                searchText.Text.Trim(),
                logic.SelectedIndex == 0,
                criteria,
                resultColumns.Where(x => x.Value.Visibility == Visibility.Visible).Select(x => x.Key).ToList(),
                CaptureSorts(),
                positionFilter);
            try
            {
                AdvancedSearchProfileStore.Save(profile);
                RefreshProfilePicker(profile.Id);
                status.Text = $"Saved '{profile.Name}' locally on this PC · {criteria.Count:N0} condition(s) · {profile.VisibleColumns?.Count ?? 0:N0} visible columns.";
                AtlasLog.Info($"Advanced search profile saved: name={profile.Name}; conditions={criteria.Count}; columns={profile.VisibleColumns?.Count ?? 0}.");
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Advanced search profile save failed.", ex);
                status.Text = "Could not save the profile: " + ex.Message;
            }
        }

        void LoadSelectedProfile()
        {
            if (profilePicker.SelectedItem is not AdvancedSearchProfile profile) return;
            activeSearchCancellation?.Cancel();
            rows.Clear();
            conditionPanel.Children.Clear();
            // Clear the transient field-library query before restoring a saved profile,
            // so saved criteria cannot bind to the wrong first filtered field.
            fieldSearch.Clear();
            searchText.Text = profile.SearchText ?? "";
            logic.SelectedIndex = profile.MatchAll ? 0 : 1;
            ApplySorts(profile.Sorts);
            var missingFields = new List<string>();
            foreach (var criterion in profile.Criteria.Take(60))
            {
                var field = fields.FirstOrDefault(x => x.Key.Equals(criterion.FieldKey, StringComparison.OrdinalIgnoreCase));
                if (field is null) { missingFields.Add(criterion.FieldKey); continue; }
                var row = AddCondition(field.Category, field.Key);
                if (row is null) continue;
                switch (field.FieldType)
                {
                    case AdvancedSearchFieldType.Number:
                        row.Minimum.Text = criterion.Minimum?.ToString(CultureInfo.InvariantCulture) ?? "";
                        row.Maximum.Text = criterion.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "";
                        break;
                    case AdvancedSearchFieldType.Text:
                        if (field.Key.Equals("player:Position", StringComparison.OrdinalIgnoreCase))
                            row.PositionValue.Text = criterion.TextValue ?? "";
                        else
                            row.TextValue.Text = criterion.TextValue ?? "";
                        break;
                    case AdvancedSearchFieldType.Boolean:
                        row.BooleanValue.SelectedIndex = criterion.BooleanValue.HasValue ? (criterion.BooleanValue.Value ? 1 : 2) : 0;
                        break;
                }
            }
            ApplyPositionFilter(profile.PositionFilter);
            ApplyVisibleColumns(profile.VisibleColumns);
            grid.ItemsSource = null;
            results.Clear();
            pageOffset = 0;
            lastSuccessfulOffset = 0;
            hasNextPage = false;
            previousPageButton.IsEnabled = false;
            nextPageButton.IsEnabled = false;
            pageStatus.Text = "Profile loaded · run search";
            status.Text = missingFields.Count == 0
                ? $"Loaded '{profile.Name}' · {rows.Count:N0} condition(s). Run Search players to execute it."
                : $"Loaded '{profile.Name}' with {missingFields.Count:N0} unavailable field(s) skipped because this save does not provide them.";
        }

        void DeleteSelectedProfile()
        {
            if (profilePicker.SelectedItem is not AdvancedSearchProfile profile) return;
            var answer = MessageBox.Show(this, $"Delete the locally saved search profile '{profile.Name}'?", "FM Atlas · Delete profile", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            try
            {
                AdvancedSearchProfileStore.Delete(profile.Id);
                RefreshProfilePicker();
                status.Text = $"Deleted '{profile.Name}'. Career data and search results were not changed.";
            }
            catch (Exception ex)
            {
                status.Text = "Could not delete the profile: " + ex.Message;
                AtlasLog.Error("Advanced search profile delete failed.", ex);
            }
        }

        async Task RunSearchAsync(bool resetPage = true)
        {
            if (resetPage) pageOffset = 0;
            var criteria = CaptureCriteria(out var error);
            if (error.Length > 0) { status.Text = error; return; }
            var positionFilter = CapturePositionFilter(out var positionError);
            if (positionError.Length > 0) { status.Text = positionError; return; }
            if (criteria.Count == 0 && positionFilter is null)
            {
                status.Text = "Enter a value for at least one general condition or position rating. Blank conditions are ignored.";
                return;
            }

            var matchAll = logic.SelectedIndex == 0;
            using var searchCancellation = new CancellationTokenSource();
            activeSearchCancellation = searchCancellation;
            cancelSearchButton.IsEnabled = true;
            searchButton.IsEnabled = false;
            addButton.IsEnabled = false;
            previousPageButton.IsEnabled = false;
            nextPageButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            status.Text = $"Searching {criteria.Count:N0} general conditions and {positionFilter?.Positions?.Count ?? 0} position ratings…";
            try
            {
                var activeSorts = CaptureSorts();
                var pageRows = await RunDatabaseAsync(() => _database.SearchAdvancedPlayersAsync(
                    searchText.Text, criteria, matchAll, pageSize + 1, pageOffset, searchCancellation.Token, activeSorts, positionFilter));
                searchCancellation.Token.ThrowIfCancellationRequested();
                hasNextPage = pageRows.Count > pageSize;
                if (hasNextPage) pageRows.RemoveAt(pageRows.Count - 1);
                results = pageRows;
                lastSuccessfulOffset = pageOffset;
                grid.ItemsSource = results;
                previousPageButton.IsEnabled = pageOffset > 0;
                nextPageButton.IsEnabled = hasNextPage;
                pageStatus.Text = results.Count == 0
                    ? "No results on this page"
                    : $"Showing {pageOffset + 1:N0}–{pageOffset + results.Count:N0}" + (hasNextPage ? " · more available" : " · end of results");
                var sortSummary = string.Join(" → ", activeSorts.Select(x =>
                    $"{ResolveSortLabel(x.FieldKey)} {(x.Descending ? "↓" : "↑")}"));
                status.Text = $"{results.Count:N0} players on this page · {criteria.Count:N0} general conditions · {(matchAll ? "ALL / AND" : "ANY / OR")} · {positionFilter?.Positions?.Count ?? 0} position ratings · {(positionFilter is null ? "positions off" : positionFilter.MatchAny ? "position ANY" : "position ALL")}{(positionFilter?.AtLeast == true ? " · minimum ratings" : " · exact ratings")} · sort: {sortSummary} · only actual stored fields and values were tested.";
                AtlasLog.Info($"Universal advanced player search: conditions={criteria.Count}; matchAll={matchAll}; positionCount={positionFilter?.Positions?.Count ?? 0}; positionAny={positionFilter?.MatchAny}; atLeast={positionFilter?.AtLeast}; results={results.Count}.");
            }
            catch (OperationCanceledException)
            {
                pageOffset = lastSuccessfulOffset;
                status.Text = "Search cancelled. The last completed result page has been retained.";
                AtlasLog.Info("Universal advanced player search cancelled by user.");
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Universal advanced player search failed.", ex);
                status.Text = "Advanced search failed: " + ex.Message;
            }
            finally
            {
                activeSearchCancellation = null;
                cancelSearchButton.IsEnabled = false;
                searchButton.IsEnabled = true;
                addButton.IsEnabled = true;
                previousPageButton.IsEnabled = pageOffset > 0;
                nextPageButton.IsEnabled = hasNextPage;
                Mouse.OverrideCursor = null;
            }
        }

        if (fields.Count > 0) AddCondition();
        else
        {
            addButton.IsEnabled = false;
            searchButton.IsEnabled = false;
        }

        var specialist = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        specialist.Children.Add(SoftButton("Open Performance / Moneyball Search", async (_, _) => await NavigateAsync("Stats Search")));
        specialist.Children.Add(SoftButton("Open Global Search", (_, _) => ShowGlobalSearch()));
        specialist.Children.Add(SoftButton("Open Attribute Search", async (_, _) => await NavigateAsync("Attribute Search")));
        // FMRTE-style workspace split: the filter library/builder stays on the
        // left and the live result table stays visible on the right. Both panes
        // scroll independently and the divider can be resized by the user.
        var root = new Grid { Margin = new Thickness(2, 0, 2, 12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(introduction, 0);
        root.Children.Add(introduction);

        var workArea = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        workArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.96, GridUnitType.Star), MinWidth = 320 });
        workArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        workArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.12, GridUnitType.Star), MinWidth = 360 });

        var builderStackV9 = new StackPanel();
        builderStackV9.Children.Add(builder);
        builderStackV9.Children.Add(specialist);
        var builderScroll = new ScrollViewer
        {
            Content = builderStackV9,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(builderScroll, 0);
        workArea.Children.Add(builderScroll);

        var splitter = new GridSplitter
        {
            Width = 7,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            ShowsPreview = false,
            Focusable = true,
            ToolTip = "Drag to resize the filter builder and results panes."
        };
        Grid.SetColumn(splitter, 1);
        workArea.Children.Add(splitter);

        var resultScroll = new ScrollViewer
        {
            Content = resultCard,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(resultScroll, 2);
        workArea.Children.Add(resultScroll);

        Grid.SetRow(workArea, 1);
        root.Children.Add(workArea);
        return root;
    }

    private static Border StatusPill(string value, string tooltip)
    {
        var pill = MonolithDesignSystem.Panel(
            MonolithDesignSystem.DataText(value, 9, MonolithDesignSystem.Solid(MonolithDesignSystem.Text)),
            8, new Thickness(0, 0, 7, 0), new Thickness(9, 6, 9, 6), raised: true);
        pill.ToolTip = tooltip;
        ToolTipService.SetInitialShowDelay(pill, 350);
        return pill;
    }
}
