using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using FmAtlas.Data;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private sealed record StatFilterUiRow(ComboBox Field, TextBox Minimum, TextBox Maximum, Border Container);
    private sealed record AttributeFilterUiRow(ComboBox Field, TextBox Minimum, TextBox Maximum, Border Container);

    private async Task<UIElement> StatsSearchPageAsync()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var stack = new StackPanel();
        var title = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 12));
        var titleStack = (StackPanel)title.Child;
        titleStack.Children.Add(Txt("PERFORMANCE INTELLIGENCE", 9, true, Teal));
        titleStack.Children.Add(Txt("Statistical Search Lab", 25, true, Ink));
        titleStack.Children.Add(Txt("Build multi-condition searches across every numeric field discovered in the imported FM career snapshot. Grouped by role-relevant area, with optional per-90 normalisation and minutes thresholds. Fields only appear when the reader actually supplied them.", 11, false, Muted, wrap: true));
        stack.Children.Add(title);

        var metricFields = await RunDatabaseAsync(() => _database.GetSeasonMetricCatalogAsync());
        var filterRows = new List<StatFilterUiRow>();
        var resultRows = new List<StatSearchResult>();
        var profileStore = new SavedSearchProfileStore();
        var savedProfiles = new List<SavedSearchProfile>();
        var status = Txt("Discovering statistical fields in the latest imported save…", 10, false, Muted, wrap: true);
        status.Margin = new Thickness(2, 8, 2, 0);
        var filterPanel = new StackPanel();
        var groupedMetrics = new ListCollectionView(metricFields);
        groupedMetrics.GroupDescriptions.Add(new PropertyGroupDescription(nameof(StatMetricField.Category)));

        var controls = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 10));
        var controlStack = (StackPanel)controls.Child;
        var options = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var per90 = new CheckBox { Content = "Calculate per 90 minutes", Foreground = Brush("#DCE5ED"), FontSize = 11, Margin = new Thickness(0, 5, 18, 0), VerticalAlignment = VerticalAlignment.Center };
        var minMinutes = Input("Minimum minutes"); minMinutes.Text = "450"; minMinutes.Width = 135; minMinutes.Margin = new Thickness(0, 0, 8, 0);
        var matchMode = new ComboBox
        {
            Width = 170, Height = 34, Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(8, 4, 8, 4),
            ItemsSource = new[] { "Match ALL (AND)", "Match ANY (OR)" },
            SelectedIndex = 0
        };
        options.Children.Add(matchMode);
        options.Children.Add(per90);
        options.Children.Add(FilterControl("PER-90 SAMPLE FLOOR", minMinutes, 150));
        Button findButton = null!;
        Button importViewButton = null!;
        DataGrid grid = null!;
        var addButton = SoftButton("＋ Add metric condition", (_, _) => AddFilterRow());
        findButton = PrimaryButton("Search statistics", async (_, _) => await RunSearchAsync());
        var resetButton = SoftButton("Reset", (_, _) => { filterRows.Clear(); filterPanel.Children.Clear(); AddFilterRow(); status.Text = "Conditions cleared."; });
        var exportButton = SoftButton("Export CSV", (_, _) => ExportResults());
        importViewButton = SoftButton("Import FM View CSV", async (_, _) => await ImportViewCsvAsync());
        options.Children.Add(addButton); options.Children.Add(findButton); options.Children.Add(resetButton); options.Children.Add(exportButton); options.Children.Add(importViewButton);
        controlStack.Children.Add(options);

        var profileRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var profileName = Input("Profile name");
        profileName.Width = 160; profileName.Margin = new Thickness(0, 0, 7, 0);
        var profilesPicker = new ComboBox
        {
            Width = 210, Height = 34, Margin = new Thickness(0, 0, 7, 0),
            Padding = new Thickness(8, 4, 8, 4), DisplayMemberPath = nameof(SavedSearchProfile.Name),
            ItemsSource = savedProfiles
        };
        profileRow.Children.Add(profilesPicker);
        profileRow.Children.Add(profileName);
        profileRow.Children.Add(SoftButton("New profile", (_, _) =>
        {
            profilesPicker.SelectedItem = null;
            profileName.Text = string.Empty;
            status.Text = "New statistical profile. Set its name and conditions, then save.";
        }));
        profileRow.Children.Add(SoftButton("Save profile", (_, _) => SaveCurrentProfile()));
        profileRow.Children.Add(SoftButton("Load profile", (_, _) => LoadSelectedProfile()));
        profileRow.Children.Add(SoftButton("Delete profile", (_, _) => DeleteSelectedProfile()));
        controlStack.Children.Add(profileRow);
        controlStack.Children.Add(Txt("CONDITIONS · CHOOSE WHETHER ALL OR ANY CONDITION MUST MATCH THE SAME PLAYER AND PERIOD", 8, true, Muted));
        controlStack.Children.Add(Txt("The catalogue combines every numeric metric found in the latest imported save plus any FM View Exporter CSV you import. To capture more columns, add them to an in-game player view before exporting; FM's own view export has a practical column ceiling.", 9, false, Muted, wrap: true));
        controlStack.Children.Add(filterPanel);
        controlStack.Children.Add(status);
        stack.Children.Add(controls);
        Grid.SetRow(stack, 0); root.Children.Add(stack);

        var resultCard = Card(new DockPanel(), 16);
        var dock = (DockPanel)resultCard.Child;
        var resultHeader = new StackPanel();
        resultHeader.Children.Add(Txt("MATCHING PLAYERS", 9, true, Teal));
        resultHeader.Children.Add(Txt("Combine goals, expected metrics, passing, defensive actions, goalkeeping and more in one search.", 10, false, Muted, wrap: true));
        DockPanel.SetDock(resultHeader, Dock.Top); dock.Children.Add(resultHeader);
        grid = CreateGrid();
        grid.Columns.Add(Column("PLAYER", "Name", 1.2));
        grid.Columns.Add(Column("CLUB", "Club", 1.0));
        grid.Columns.Add(Column("SEASON / MATCH", "Period", .8));
        grid.Columns.Add(Column("MINUTES", "Minutes", .6));
        grid.Columns.Add(Column("MATCHED METRICS", "ValuesSummary", 2.0));
        dock.Children.Add(grid);
        Grid.SetRow(resultCard, 1); root.Children.Add(resultCard);

        void AddFilterRow(string? key = null, double? minValue = null, double? maxValue = null)
        {
            if (filterRows.Count >= 60) { status.Text = "A single search supports up to 60 metric conditions."; return; }
            if (metricFields.Count == 0) return;
            var field = new ComboBox { Width = 285, Height = 34, Margin = new Thickness(0, 0, 8, 0), ItemsSource = groupedMetrics, DisplayMemberPath = "PickerLabel", FontSize = 10, Background = Brush("#151B22"), Foreground = Brush("#E8EEF4"), IsTextSearchEnabled = true };
            var groupHeader = new FrameworkElementFactory(typeof(TextBlock));
            groupHeader.SetBinding(TextBlock.TextProperty, new Binding(nameof(CollectionViewGroup.Name)));
            groupHeader.SetValue(TextBlock.ForegroundProperty, Teal);
            groupHeader.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            groupHeader.SetValue(TextBlock.FontSizeProperty, 10d);
            groupHeader.SetValue(TextBlock.MarginProperty, new Thickness(7, 6, 4, 3));
            field.GroupStyle.Add(new GroupStyle { HeaderTemplate = new DataTemplate { VisualTree = groupHeader } });
            var min = Input("Min"); min.Width = 90; min.Height = 34; min.Margin = new Thickness(0, 0, 7, 0);
            var max = Input("Max"); max.Width = 90; max.Height = 34; max.Margin = new Thickness(0, 0, 7, 0);
            if (key is not null && !metricFields.Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                status.Text = $"Saved profile field '{key}' is not present in the current metric catalogue; it was not substituted.";
                return;
            }
            if (key is not null) field.SelectedItem = metricFields.First(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            else field.SelectedIndex = 0;
            if (minValue.HasValue) min.Text = minValue.Value.ToString(CultureInfo.InvariantCulture);
            if (maxValue.HasValue) max.Text = maxValue.Value.ToString(CultureInfo.InvariantCulture);
            var container = new Border { Background = Brush("#1C242E"), BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 5) };
            var remove = SoftButton("×", (_, _) => { filterPanel.Children.Remove(container); filterRows.RemoveAll(x => x.Container == container); });
            remove.Width = 35; remove.Height = 34; remove.Margin = new Thickness(0);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 0) };
            row.Children.Add(field); row.Children.Add(min); row.Children.Add(max); row.Children.Add(remove);
            container.Child = row;
            filterRows.Add(new StatFilterUiRow(field, min, max, container));
            filterPanel.Children.Add(container);
        }

        void RefreshSavedProfiles(string? selectedId = null)
        {
            try
            {
                savedProfiles = profileStore.Load()
                    .Where(x => x.Kind == SavedSearchKind.Statistics)
                    .ToList();
                profilesPicker.ItemsSource = null;
                profilesPicker.ItemsSource = savedProfiles;
                profilesPicker.SelectedItem = selectedId is null
                    ? null
                    : savedProfiles.FirstOrDefault(x => x.Id == selectedId);
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Could not load saved statistical profiles.", ex);
                status.Text = "Saved profiles could not be loaded. The profile file was preserved; see Diagnostics for details.";
            }
        }

        void SaveCurrentProfile()
        {
            var name = profileName.Text.Trim();
            if (name.Length == 0)
            {
                status.Text = "Enter a name for this statistical search profile.";
                return;
            }

            var conditions = new List<SavedSearchCondition>();
            foreach (var row in filterRows)
            {
                if (row.Field.SelectedItem is not StatMetricField field) continue;
                var hasMin = double.TryParse(row.Minimum.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var lower);
                var hasMax = double.TryParse(row.Maximum.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var upper);
                if (!hasMin && !hasMax) continue;
                if ((hasMin && !double.IsFinite(lower)) || (hasMax && !double.IsFinite(upper)))
                {
                    status.Text = $"Enter finite numeric boundaries for {field.DisplayName}.";
                    return;
                }
                if (hasMin && hasMax && lower > upper)
                {
                    status.Text = $"Minimum cannot exceed maximum for {field.DisplayName}.";
                    return;
                }
                if (per90.IsChecked == true && !field.SupportsPer90)
                {
                    status.Text = $"{field.DisplayName} cannot be converted per 90 because it is already a rate, percentage, rating or playing-time field.";
                    return;
                }
                conditions.Add(new SavedSearchCondition(field.Key, field.DisplayName, hasMin ? lower : null, hasMax ? upper : null));
            }

            if (conditions.Count == 0)
            {
                status.Text = "Add at least one metric condition with a minimum or maximum before saving.";
                return;
            }
            if (per90.IsChecked == true && (!int.TryParse(minMinutes.Text, out var sample) || sample < 1))
            {
                status.Text = "Enter a valid minimum minutes sample before saving a per-90 profile.";
                return;
            }

            var selected = profilesPicker.SelectedItem as SavedSearchProfile;
            var now = DateTime.UtcNow;
            var profile = new SavedSearchProfile(
                selected?.Id ?? Guid.NewGuid().ToString("N"),
                name,
                SavedSearchKind.Statistics,
                null,
                matchMode.SelectedIndex == 1,
                per90.IsChecked == true,
                int.TryParse(minMinutes.Text, out var minimum) ? Math.Clamp(minimum, 1, 10000) : 450,
                conditions,
                selected?.CreatedUtc ?? now,
                now);

            try
            {
                var saved = profileStore.Save(profile);
                RefreshSavedProfiles(saved.Id);
                status.Text = $"Saved statistical profile '{saved.Name}' locally. Only conditions and search settings were stored.";
                AtlasLog.Info($"Saved statistical search profile: id={saved.Id}; conditions={conditions.Count}.");
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Could not save statistical search profile.", ex);
                status.Text = "Profile could not be saved: " + ex.Message;
            }
        }

        void LoadSelectedProfile()
        {
            if (profilesPicker.SelectedItem is not SavedSearchProfile profile)
            {
                status.Text = "Choose a saved statistical profile to load.";
                return;
            }

            filterRows.Clear();
            filterPanel.Children.Clear();
            foreach (var condition in profile.Conditions)
                AddFilterRow(condition.Key, condition.Minimum, condition.Maximum);

            matchMode.SelectedIndex = profile.MatchAny ? 1 : 0;
            per90.IsChecked = profile.Per90;
            minMinutes.Text = profile.MinimumMinutes.ToString(CultureInfo.InvariantCulture);
            profileName.Text = profile.Name;

            var loaded = filterRows.Count;
            status.Text = loaded == profile.Conditions.Count
                ? $"Loaded '{profile.Name}' · {loaded} condition(s). Run the search to refresh results."
                : $"Loaded {loaded} of {profile.Conditions.Count} conditions. Missing metric fields were left out rather than substituted; review the profile before searching.";
        }

        void DeleteSelectedProfile()
        {
            if (profilesPicker.SelectedItem is not SavedSearchProfile profile)
            {
                status.Text = "Choose a saved statistical profile to delete.";
                return;
            }
            if (MessageBox.Show(this, $"Delete saved profile '{profile.Name}'?", "FM Atlas · Delete search profile",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                profileStore.Delete(profile.Id);
                profileName.Text = string.Empty;
                RefreshSavedProfiles();
                status.Text = $"Deleted saved profile '{profile.Name}'.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Could not delete statistical search profile.", ex);
                status.Text = "Profile could not be deleted: " + ex.Message;
            }
        }

        async Task RunSearchAsync()
        {
            var criteria = new List<StatFilterCriterion>();
            foreach (var row in filterRows)
            {
                if (row.Field.SelectedItem is not StatMetricField field) continue;
                var hasMin = double.TryParse(row.Minimum.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var lower);
                var hasMax = double.TryParse(row.Maximum.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var upper);
                if (!hasMin && !hasMax) continue;
                if ((hasMin && !double.IsFinite(lower)) || (hasMax && !double.IsFinite(upper)))
                {
                    status.Text = $"Enter finite numeric boundaries for {field.DisplayName}.";
                    return;
                }
                if (hasMin && hasMax && lower > upper) { status.Text = $"Minimum cannot exceed maximum for {field.DisplayName}."; return; }
                if (per90.IsChecked == true && !field.SupportsPer90) { status.Text = $"{field.DisplayName} is already a rate, percentage, rating or playing-time field and cannot be normalised per 90 again."; return; }
                criteria.Add(new StatFilterCriterion(field.Key, field.DisplayName, hasMin ? lower : null, hasMax ? upper : null));
            }
            if (criteria.Count == 0) { status.Text = "Add at least one metric condition with a minimum or maximum."; return; }
            if (per90.IsChecked == true && (!int.TryParse(minMinutes.Text, out var sample) || sample < 1)) { status.Text = "Enter a valid minimum minutes sample."; return; }
            var minSample = int.TryParse(minMinutes.Text, out var value) ? Math.Clamp(value, 1, 10000) : 450;
            findButton.IsEnabled = false; Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            status.Text = "Searching imported player-season and player-match records…";
            try
            {
                var matchAny = matchMode.SelectedIndex == 1;
                resultRows = await RunDatabaseAsync(() => _database.SearchSeasonStatCriteriaAsync(criteria, per90.IsChecked == true, minSample, 5000, matchAny));
                grid.ItemsSource = resultRows;
                status.Text = $"{resultRows.Count:N0} matching player-period rows · {criteria.Count} condition(s) · {(matchAny ? "ANY condition (OR)" : "ALL conditions (AND)")} · {(per90.IsChecked == true ? $"per 90, minimum {minSample:N0} minutes" : "raw source values")} · source fields retained with their original metric keys.";
                AtlasLog.Info($"Advanced stats search completed: fields={criteria.Count}; matchAny={matchAny}; per90={per90.IsChecked == true}; results={resultRows.Count}.");
            }
            catch (Exception ex) { AtlasLog.Error("Advanced stats search failed.", ex); status.Text = "Statistics search failed: " + ex.Message; }
            finally { findButton.IsEnabled = true; Mouse.OverrideCursor = null; }
        }

        async Task ImportViewCsvAsync()
        {
            var dialog = new OpenFileDialog { Title = "Import a CSV exported from FM View Exporter", Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                status.Text = "Importing FM View Exporter rows and matching player IDs…";
                importViewButton.IsEnabled = false;
                var imported = await RunDatabaseAsync(() => _database.ImportFmViewExporterCsvAsync(dialog.FileName));
                AtlasLog.Info($"FM View Exporter CSV imported: import={imported.ImportId}; rows={imported.RowsImported}; players={imported.PlayersCreated}; {imported.Note}.");
                await NavigateAsync("Stats Search");
            }
            catch (Exception ex)
            {
                AtlasLog.Error("FM View Exporter CSV import failed.", ex);
                status.Text = "CSV import failed: " + ex.Message;
                MessageBox.Show(this, ex.Message, "FM Atlas · FM View CSV import", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { importViewButton.IsEnabled = true; }
        }

        void ExportResults()
        {
            if (resultRows.Count == 0) { MessageBox.Show(this, "Run a statistical search before exporting.", "FM Atlas · Stats Search"); return; }
            var dialog = new SaveFileDialog { Title = "Export statistical search", FileName = "FM-Atlas-stat-search.csv", Filter = "CSV files (*.csv)|*.csv" };
            if (dialog.ShowDialog(this) != true) return;
            static string Q(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
            var lines = new List<string> { "Player,Club,Period,Minutes,Matched Metrics" };
            lines.AddRange(resultRows.Select(x => string.Join(",", Q(x.Name), Q(x.Club), Q(x.Period), Q(x.Minutes?.ToString("0.##", CultureInfo.InvariantCulture)), Q(x.ValuesSummary))));
            File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
            status.Text = $"Exported {resultRows.Count:N0} statistical search results.";
        }

        if (metricFields.Count == 0)
        {
            status.Text = "No numeric performance fields are present yet. Import an FM27 save copy with player_season_stats / player_match_stats available, then reopen this workspace.";
        }
        else
        {
            AddFilterRow();
            status.Text = $"{metricFields.Count:N0} numeric metric paths discovered · {metricFields.Select(x => x.Category).Distinct().Count():N0} groups · includes {string.Join(", ", metricFields.SelectMany(x => x.SourceTables.Split(',', StringSplitOptions.RemoveEmptyEntries)).Distinct().OrderBy(x => x))}.";
        }
        RefreshSavedProfiles();
        return root;
    }

    private async Task<UIElement> AttributeSearchPageAsync()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var pageStack = new StackPanel();
        var header = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 12));
        var heading = (StackPanel)header.Child;
        heading.Children.Add(Txt("PLAYER FINDER", 9, true, Teal));
        heading.Children.Add(Txt("Attribute Search", 25, true, Ink));
        heading.Children.Add(Txt("Build your own player profile from any number of 1–20 attribute and personality requirements. Every selected attribute is applied together, so you can define your exact striker, midfielder, defender or goalkeeper rather than choosing one attribute at a time.", 11, false, Muted, wrap: true));
        pageStack.Children.Add(header);

        var fields = PlayerAttributeSchema.Fields.Concat(PlayerAttributeSchema.PersonalityFields)
            .Select(f => new AttributePickerField(f.Key, f.Label, f.Category))
            .ToList();
        var filterRows = new List<AttributeFilterUiRow>();
        List<PlayerRecord> resultRows = new();
        var profileStore = new SavedSearchProfileStore();
        var savedProfiles = new List<SavedSearchProfile>();
        var filterPanel = new StackPanel();
        var status = Txt("Loading attribute catalogue…", 10, false, Muted, wrap: true);
        status.Margin = new Thickness(2, 8, 2, 0);

        var builder = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 10));
        var builderStack = (StackPanel)builder.Child;
        var search = Input("Optional player, club, nation or UID search"); search.Width = 280; search.Margin = new Thickness(0, 0, 8, 0);
        var top = new WrapPanel(); top.Children.Add(FilterControl("OPTIONAL NAME / CLUB", search, 290));
        top.Children.Add(SoftButton("＋ Add attribute", (_, _) => AddFilterRow()));
        Button findButton = null!;
        DataGrid grid = null!;
        findButton = PrimaryButton("Find players", async (_, _) => await RunSearchAsync());
        top.Children.Add(findButton);
        top.Children.Add(SoftButton("Reset", (_, _) => { filterRows.Clear(); filterPanel.Children.Clear(); AddFilterRow(); status.Text = "Attribute conditions reset."; }));
        top.Children.Add(SoftButton("Export CSV", (_, _) => ExportResults()));
        builderStack.Children.Add(top);

        var profileRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 7) };
        var profileName = Input("Profile name");
        profileName.Width = 160; profileName.Margin = new Thickness(0, 0, 7, 0);
        var profilesPicker = new ComboBox
        {
            Width = 210, Height = 34, Margin = new Thickness(0, 0, 7, 0),
            Padding = new Thickness(8, 4, 8, 4), DisplayMemberPath = nameof(SavedSearchProfile.Name),
            ItemsSource = savedProfiles
        };
        profileRow.Children.Add(profilesPicker);
        profileRow.Children.Add(profileName);
        profileRow.Children.Add(SoftButton("New profile", (_, _) =>
        {
            profilesPicker.SelectedItem = null;
            profileName.Text = string.Empty;
            status.Text = "New attribute profile. Set its name and conditions, then save.";
        }));
        profileRow.Children.Add(SoftButton("Save profile", (_, _) => SaveCurrentProfile()));
        profileRow.Children.Add(SoftButton("Load profile", (_, _) => LoadSelectedProfile()));
        profileRow.Children.Add(SoftButton("Delete profile", (_, _) => DeleteSelectedProfile()));
        builderStack.Children.Add(profileRow);

        var presets = new WrapPanel { Margin = new Thickness(0, 5, 0, 5) };
        presets.Children.Add(Txt("QUICK PRESETS", 8, true, Muted));
        presets.Children.Add(SoftButton("Complete Striker", (_, _) => ApplyPreset(new[] { ("finishing", 15d), ("off_the_ball", 14d), ("composure", 13d), ("acceleration", 14d), ("pace", 14d) })));
        presets.Children.Add(SoftButton("Ball Winner", (_, _) => ApplyPreset(new[] { ("tackling", 15d), ("work_rate", 14d), ("stamina", 14d), ("aggression", 12d), ("anticipation", 13d) })));
        presets.Children.Add(SoftButton("Elite Goalkeeper", (_, _) => ApplyPreset(new[] { ("reflexes", 15d), ("handling", 14d), ("one_on_ones", 14d), ("aerial_reach", 14d), ("command_of_area", 13d) })));
        builderStack.Children.Add(presets);
        builderStack.Children.Add(Txt("ATTRIBUTE CONDITIONS · RATE EACH REQUIREMENT FROM 1 TO 20 · ALL CONDITIONS MATCH", 8, true, Muted));
        builderStack.Children.Add(filterPanel);
        builderStack.Children.Add(status);
        pageStack.Children.Add(builder);
        Grid.SetRow(pageStack, 0); root.Children.Add(pageStack);

        var resultCard = Card(new DockPanel(), 16);
        var dock = (DockPanel)resultCard.Child;
        var resultHeader = new StackPanel(); resultHeader.Children.Add(Txt("PLAYER MATCHES", 9, true, Teal)); resultHeader.Children.Add(Txt("Only imported players with actual stored values are included. Missing ratings are never treated as zero.", 10, false, Muted));
        DockPanel.SetDock(resultHeader, Dock.Top); dock.Children.Add(resultHeader);
        grid = CreateGrid();
        grid.Columns.Add(Column("PLAYER", "Name", 1.25)); grid.Columns.Add(Column("CLUB", "Club", 1.0)); grid.Columns.Add(Column("AGE", "Age", .45));
        grid.Columns.Add(Column("POSITION", "Position", .65)); grid.Columns.Add(Column("CA", "DisplayAbility", .45)); grid.Columns.Add(Column("PA", "DisplayPotential", .45));
        grid.Columns.Add(Column("NATION", "Nation", .75)); dock.Children.Add(grid);
        Grid.SetRow(resultCard, 1); root.Children.Add(resultCard);

        void AddFilterRow(string? key = null, double? minimum = 14, double? maximum = null)
        {
            if (filterRows.Count >= 60) { status.Text = "A search supports up to 60 attribute and personality conditions."; return; }
            var field = new ComboBox { Width = 255, Height = 34, Margin = new Thickness(0, 0, 8, 0), ItemsSource = fields, DisplayMemberPath = "PickerLabel", FontSize = 10, Background = Brush("#151B22"), Foreground = Brush("#E8EEF4") };
            if (key is not null && !fields.Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                status.Text = $"Saved profile field '{key}' is not available in this app version; it was not substituted.";
                return;
            }
            if (key is not null) field.SelectedItem = fields.First(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            else field.SelectedIndex = 0;
            var min = Input("Min 1–20"); min.Width = 88; min.Height = 34; min.Text = minimum?.ToString(CultureInfo.InvariantCulture) ?? ""; min.Margin = new Thickness(0, 0, 7, 0);
            var max = Input("Max 1–20"); max.Width = 88; max.Height = 34; max.Text = maximum?.ToString(CultureInfo.InvariantCulture) ?? ""; max.Margin = new Thickness(0, 0, 7, 0);
            var container = new Border { Background = Brush("#1C242E"), BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 5) };
            var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(field); row.Children.Add(min); row.Children.Add(max);
            var remove = SoftButton("×", (_, _) => { filterPanel.Children.Remove(container); filterRows.RemoveAll(x => x.Container == container); }); remove.Width = 35; remove.Height = 34; remove.Margin = new Thickness(0); row.Children.Add(remove); container.Child = row;
            filterRows.Add(new AttributeFilterUiRow(field, min, max, container)); filterPanel.Children.Add(container);
        }

        void ApplyPreset(IEnumerable<(string Key, double Minimum)> preset)
        {
            filterRows.Clear(); filterPanel.Children.Clear();
            foreach (var item in preset) AddFilterRow(item.Key, item.Minimum, null);
            status.Text = "Preset loaded. Adjust any 1–20 minimum/maximum before searching.";
        }

        void RefreshSavedProfiles(string? selectedId = null)
        {
            try
            {
                savedProfiles = profileStore.Load()
                    .Where(x => x.Kind == SavedSearchKind.Attributes)
                    .ToList();
                profilesPicker.ItemsSource = null;
                profilesPicker.ItemsSource = savedProfiles;
                profilesPicker.SelectedItem = selectedId is null
                    ? null
                    : savedProfiles.FirstOrDefault(x => x.Id == selectedId);
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Could not load saved attribute profiles.", ex);
                status.Text = "Saved profiles could not be loaded. The profile file was preserved; see Diagnostics for details.";
            }
        }

        void SaveCurrentProfile()
        {
            var name = profileName.Text.Trim();
            if (name.Length == 0)
            {
                status.Text = "Enter a name for this attribute-search profile.";
                return;
            }

            var conditions = new List<SavedSearchCondition>();
            foreach (var row in filterRows)
            {
                if (row.Field.SelectedItem is not AttributePickerField field) continue;
                var hasMin = int.TryParse(row.Minimum.Text, out var lower);
                var hasMax = int.TryParse(row.Maximum.Text, out var upper);
                if (!hasMin && !hasMax) continue;
                if ((hasMin && lower is < 1 or > 20) || (hasMax && upper is < 1 or > 20))
                {
                    status.Text = "Attribute values must be whole numbers from 1 to 20.";
                    return;
                }
                if (hasMin && hasMax && lower > upper)
                {
                    status.Text = $"Minimum cannot exceed maximum for {field.Label}.";
                    return;
                }
                conditions.Add(new SavedSearchCondition(field.Key, field.Label, hasMin ? lower : null, hasMax ? upper : null));
            }

            if (conditions.Count == 0)
            {
                status.Text = "Add at least one attribute condition before saving.";
                return;
            }

            var selected = profilesPicker.SelectedItem as SavedSearchProfile;
            var now = DateTime.UtcNow;
            var profile = new SavedSearchProfile(
                selected?.Id ?? Guid.NewGuid().ToString("N"),
                name,
                SavedSearchKind.Attributes,
                string.IsNullOrWhiteSpace(search.Text) ? null : search.Text.Trim(),
                false,
                false,
                0,
                conditions,
                selected?.CreatedUtc ?? now,
                now);

            try
            {
                var saved = profileStore.Save(profile);
                RefreshSavedProfiles(saved.Id);
                status.Text = $"Saved attribute profile '{saved.Name}' locally. Only conditions and the optional text filter were stored.";
                AtlasLog.Info($"Saved attribute search profile: id={saved.Id}; conditions={conditions.Count}.");
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Could not save attribute search profile.", ex);
                status.Text = "Profile could not be saved: " + ex.Message;
            }
        }

        void LoadSelectedProfile()
        {
            if (profilesPicker.SelectedItem is not SavedSearchProfile profile)
            {
                status.Text = "Choose a saved attribute profile to load.";
                return;
            }

            filterRows.Clear();
            filterPanel.Children.Clear();
            foreach (var condition in profile.Conditions)
                AddFilterRow(condition.Key, condition.Minimum, condition.Maximum);

            search.Text = profile.TextFilter ?? string.Empty;
            profileName.Text = profile.Name;

            var loaded = filterRows.Count;
            status.Text = loaded == profile.Conditions.Count
                ? $"Loaded '{profile.Name}' · {loaded} condition(s). Run the search to refresh results."
                : $"Loaded {loaded} of {profile.Conditions.Count} conditions. Missing fields were left out rather than substituted; review the profile before searching.";
        }

        void DeleteSelectedProfile()
        {
            if (profilesPicker.SelectedItem is not SavedSearchProfile profile)
            {
                status.Text = "Choose a saved attribute profile to delete.";
                return;
            }
            if (MessageBox.Show(this, $"Delete saved profile '{profile.Name}'?", "FM Atlas · Delete search profile",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                profileStore.Delete(profile.Id);
                profileName.Text = string.Empty;
                RefreshSavedProfiles();
                status.Text = $"Deleted saved profile '{profile.Name}'.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Could not delete attribute search profile.", ex);
                status.Text = "Profile could not be deleted: " + ex.Message;
            }
        }

        async Task RunSearchAsync()
        {
            var criteria = new List<AttributeSearchCriterion>();
            foreach (var row in filterRows)
            {
                if (row.Field.SelectedItem is not AttributePickerField field) continue;
                var hasMin = int.TryParse(row.Minimum.Text, out var lower); var hasMax = int.TryParse(row.Maximum.Text, out var upper);
                if (!hasMin && !hasMax) continue;
                if ((hasMin && lower is < 1 or > 20) || (hasMax && upper is < 1 or > 20)) { status.Text = "Attribute values must be whole numbers from 1 to 20."; return; }
                if (hasMin && hasMax && lower > upper) { status.Text = $"Minimum cannot exceed maximum for {field.Label}."; return; }
                criteria.Add(new AttributeSearchCriterion(field.Key, field.Label, hasMin ? lower : null, hasMax ? upper : null));
            }
            if (criteria.Count == 0) { status.Text = "Add at least one attribute condition and set its minimum or maximum."; return; }
            findButton.IsEnabled = false; Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            status.Text = $"Searching players for {criteria.Count} simultaneous attribute conditions…";
            try
            {
                resultRows = await RunDatabaseAsync(() => _database.SearchPlayerAttributeCriteriaAsync(search.Text, criteria, 5000));
                grid.ItemsSource = resultRows;
                status.Text = $"{resultRows.Count:N0} player matches · {criteria.Count} conditions · source: latest imported FM save / local scouting facts.";
                AtlasLog.Info($"Multi-attribute search completed: conditions={criteria.Count}; results={resultRows.Count}.");
            }
            catch (Exception ex) { AtlasLog.Error("Multi-attribute search failed.", ex); status.Text = "Attribute search failed: " + ex.Message; }
            finally { findButton.IsEnabled = true; Mouse.OverrideCursor = null; }
        }

        void ExportResults()
        {
            if (resultRows.Count == 0) { MessageBox.Show(this, "Run an attribute search before exporting.", "FM Atlas · Attribute Search"); return; }
            var dialog = new SaveFileDialog { Title = "Export attribute search", FileName = "FM-Atlas-attribute-search.csv", Filter = "CSV files (*.csv)|*.csv" };
            if (dialog.ShowDialog(this) != true) return;
            static string Q(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
            var lines = new List<string> { "Player,Club,Age,Position,CA,PA,Nation,SourceId" };
            lines.AddRange(resultRows.Select(x => string.Join(",", Q(x.Name), Q(x.Club), Q(x.Age?.ToString()), Q(x.Position), Q(x.DisplayAbility), Q(x.DisplayPotential), Q(x.Nation), Q(x.SourceId))));
            File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
            status.Text = $"Exported {resultRows.Count:N0} player matches.";
        }

        AddFilterRow();
        status.Text = $"{fields.Count:N0} attribute and personality fields available in the builder · values are filtered only when present in the imported player records.";
        RefreshSavedProfiles();
        return root;
    }

    private sealed record AttributePickerField(string Key, string Label, string Category)
    {
        public string PickerLabel => $"{Category}  ·  {Label}";
    }
}

internal static class PanelChildExtensions
{
    internal static void RemoveWhereChild(this Panel panel, Border child)
        => panel.Children.Remove(child);
}
