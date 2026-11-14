using System.Globalization;
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
        var hasNextPage = false;
        var savedProfiles = AdvancedSearchProfileStore.LoadAll().ToList();
        var resultColumns = new Dictionary<string, DataGridColumn>(StringComparer.OrdinalIgnoreCase);
        var columnChecks = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
        string[] defaultVisibleColumns = { "Name", "Age", "Club", "Position", "League", "Nation", "DisplayAbility", "DisplayPotential", "DisplayValue", "DataOrigin" };
        StackPanel conditionPanel = null!;
        TextBlock status = null!;
        Button searchButton = null!;
        Button previousPageButton = null!;
        Button nextPageButton = null!;
        TextBlock pageStatus = null!;
        DataGrid grid = null!;

        var content = new StackPanel { Margin = new Thickness(2, 0, 2, 18) };
        var introduction = Card(new StackPanel(), 18, new Thickness(0, 0, 0, 12));
        var intro = (StackPanel)introduction.Child;
        intro.Children.Add(Txt("ATLAS SEARCH STUDIO", 9, true, Teal));
        intro.Children.Add(Txt("Advanced Search", 27, true, Ink));
        intro.Children.Add(Txt("Build one player search across core profile fields, imported attributes, personality, contract facts, positions and other available save facts. Choose whether ALL conditions or ANY condition must match. The field library is discovered from actual imported data; unsupported or missing fields are not invented.", 11, false, Muted, wrap: true));
        var summary = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        summary.Children.Add(StatusPill($"{fields.Count:N0} searchable fields", "Available core player fields plus fields actually present in the latest imported save and local Atlas facts."));
        summary.Children.Add(StatusPill($"{categories.Count - 1:N0} categories", "Categories are derived from fields present in the active data."));
        summary.Children.Add(StatusPill("60 conditions", "Maximum conditions in one query."));
        intro.Children.Add(summary);
        content.Children.Add(introduction);

        var builder = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 12));
        var builderStack = (StackPanel)builder.Child;
        builderStack.Children.Add(Txt("PLAYER FILTER BUILDER", 9, true, Teal));
        builderStack.Children.Add(Txt("Combine general, contract, attributes, positions and save facts", 17, true, Ink));

        var searchText = Input("Optional player, club, nation or player ID search");
        searchText.Width = 310;
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
        toolbar.Children.Add(FilterControl("MATCH LOGIC", logic, 180));
        var addButton = SoftButton("＋ Add condition", (_, _) => AddCondition());
        searchButton = PrimaryButton("Search players", async (_, _) => await RunSearchAsync());
        var resetButton = SoftButton("Reset filters", (_, _) => ResetFilters());
        var saveProfileButton = SoftButton("Save profile", (_, _) => SaveCurrentProfile());
        var loadProfileButton = SoftButton("Load profile", (_, _) => LoadSelectedProfile());
        var deleteProfileButton = SoftButton("Delete profile", (_, _) => DeleteSelectedProfile());
        loadProfileButton.IsEnabled = savedProfiles.Count > 0;
        deleteProfileButton.IsEnabled = savedProfiles.Count > 0;
        toolbar.Children.Add(addButton);
        toolbar.Children.Add(searchButton);
        toolbar.Children.Add(resetButton);
        toolbar.Children.Add(saveProfileButton);
        toolbar.Children.Add(FilterControl("SAVED SEARCHES", profilePicker, 215));
        toolbar.Children.Add(loadProfileButton);
        toolbar.Children.Add(deleteProfileButton);
        toolbar.Children.Add(SoftButton("Performance Search", async (_, _) => await NavigateAsync("Stats Search")));
        toolbar.Children.Add(SoftButton("Multi-entity Search", (_, _) => ShowGlobalSearch()));
        builderStack.Children.Add(toolbar);

        conditionPanel = new StackPanel();
        builderStack.Children.Add(Txt("CONDITIONS · CHOOSE A CATEGORY, FIELD AND VALUE", 8, true, Muted));
        builderStack.Children.Add(conditionPanel);
        status = Txt(fields.Count == 0 ? "No fields are available yet. Import a career save copy or add local scouting data." : "Add one or more conditions, then run the search.", 10, false, Muted, wrap: true);
        status.Margin = new Thickness(2, 8, 2, 0);
        builderStack.Children.Add(status);
        content.Children.Add(builder);

        var resultCard = Card(new DockPanel(), 16);
        var dock = (DockPanel)resultCard.Child;
        var resultHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        resultHeader.Children.Add(Txt("PLAYER MATCHES", 9, true, Teal));
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
        AddResultColumn("CA", "DisplayAbility", .45);
        AddResultColumn("PA", "DisplayPotential", .45);
        AddResultColumn("VALUE", "DisplayValue", .7);
        AddResultColumn("SOURCE", "DataOrigin", .9);

        var columnPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var columnNames = new (string Key, string Label)[]
        {
            ("Name", "Player"), ("Age", "Age"), ("Club", "Club"), ("Position", "Position"),
            ("League", "League"), ("Nation", "Nation"), ("DisplayAbility", "CA"),
            ("DisplayPotential", "PA"), ("DisplayValue", "Value"), ("DataOrigin", "Source")
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
        var columnCard = Card(new StackPanel(), 12, new Thickness(0, 0, 0, 8));
        ((StackPanel)columnCard.Child).Children.Add(Txt("RESULT COLUMNS", 8, true, Muted));
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
        content.Children.Add(resultCard);

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
            var source = category.Equals("All categories", StringComparison.OrdinalIgnoreCase)
                ? fields
                : fields.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            row.FieldPicker.ItemsSource = source;
            row.FieldPicker.SelectedItem = preferredKey is null ? null : source.FirstOrDefault(x => x.Key.Equals(preferredKey, StringComparison.OrdinalIgnoreCase));
            if (row.FieldPicker.SelectedIndex < 0 && source.Count > 0) row.FieldPicker.SelectedIndex = 0;
            UpdateValueControls(row);
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
            var remove = SoftButton("×", (_, _) =>
            {
                conditionPanel.Children.Remove(container);
                rows.RemoveAll(x => x.Container == container);
                status.Text = $"{rows.Count:N0} condition(s) configured.";
            });
            remove.Width = 34;
            remove.Height = 34;
            remove.Margin = new Thickness(0);

            var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            rowPanel.Children.Add(categoryPicker);
            rowPanel.Children.Add(fieldPicker);
            rowPanel.Children.Add(valueHost);
            rowPanel.Children.Add(remove);
            container = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(28, 130, 170, 215)),
                BorderBrush = Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 4, 0, 4),
                Child = rowPanel
            };

            var row = new AdvancedSearchUiRow
            {
                CategoryPicker = categoryPicker,
                FieldPicker = fieldPicker,
                Minimum = minimum,
                Maximum = maximum,
                TextValue = textValue,
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
            rows.Clear();
            conditionPanel.Children.Clear();
            searchText.Clear();
            logic.SelectedIndex = 0;
            AddCondition();
            pageOffset = 0;
            hasNextPage = false;
            previousPageButton.IsEnabled = false;
            nextPageButton.IsEnabled = false;
            pageStatus.Text = "No search results yet";
            status.Text = "Filters reset. Existing data and saved career snapshots are unchanged.";
            grid.ItemsSource = null;
            results.Clear();
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
                            && ((hasMin && lower is < 1 or > 20) || (hasMax && upper is < 1 or > 20)))
                        {
                            error = $"{field.DisplayName} must be between 1 and 20.";
                            return new();
                        }
                        criteria.Add(new AdvancedSearchCriterion(field.Key, hasMin ? lower : null, hasMax ? upper : null, null, null));
                        break;
                    }
                    case AdvancedSearchFieldType.Text:
                    {
                        var value = row.TextValue.Text.Trim();
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
            if (criteria.Count == 0) { status.Text = "Add at least one complete condition before saving a profile."; return; }
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
                resultColumns.Where(x => x.Value.Visibility == Visibility.Visible).Select(x => x.Key).ToList());
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
            rows.Clear();
            conditionPanel.Children.Clear();
            searchText.Text = profile.SearchText ?? "";
            logic.SelectedIndex = profile.MatchAll ? 0 : 1;
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
                        row.TextValue.Text = criterion.TextValue ?? "";
                        break;
                    case AdvancedSearchFieldType.Boolean:
                        row.BooleanValue.SelectedIndex = criterion.BooleanValue.HasValue ? (criterion.BooleanValue.Value ? 1 : 2) : 0;
                        break;
                }
            }
            ApplyVisibleColumns(profile.VisibleColumns);
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
            if (rows.Count == 0)
            {
                status.Text = "Add at least one condition before searching.";
                return;
            }
            var criteria = CaptureCriteria(out var error);
            if (error.Length > 0) { status.Text = error; return; }
            if (criteria.Count == 0)
            {
                status.Text = "Enter a value for at least one condition. Blank conditions are ignored.";
                return;
            }

            var matchAll = logic.SelectedIndex == 0;
            searchButton.IsEnabled = false;
            addButton.IsEnabled = false;
            previousPageButton.IsEnabled = false;
            nextPageButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            status.Text = $"Searching {criteria.Count:N0} conditions across {(matchAll ? "ALL / AND" : "ANY / OR")} of the available fields…";
            try
            {
                var pageRows = await RunDatabaseAsync(() => _database.SearchAdvancedPlayersAsync(searchText.Text, criteria, matchAll, pageSize + 1, pageOffset));
                hasNextPage = pageRows.Count > pageSize;
                if (hasNextPage) pageRows.RemoveAt(pageRows.Count - 1);
                results = pageRows;
                grid.ItemsSource = results;
                previousPageButton.IsEnabled = pageOffset > 0;
                nextPageButton.IsEnabled = hasNextPage;
                pageStatus.Text = results.Count == 0
                    ? "No results on this page"
                    : $"Showing {pageOffset + 1:N0}–{pageOffset + results.Count:N0}" + (hasNextPage ? " · more available" : " · end of results");
                status.Text = $"{results.Count:N0} players on this page · {criteria.Count:N0} conditions · {(matchAll ? "ALL / AND" : "ANY / OR")} · only actual stored fields and values were tested.";
                AtlasLog.Info($"Universal advanced player search: conditions={criteria.Count}; matchAll={matchAll}; results={results.Count}.");
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Universal advanced player search failed.", ex);
                status.Text = "Advanced search failed: " + ex.Message;
            }
            finally
            {
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
        content.Children.Add(specialist);

        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        return scroll;
    }

    private static Border StatusPill(string value, string tooltip)
    {
        var pill = new Border
        {
            Background = Brush("#1C242E"),
            BorderBrush = Brush("#303943"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 7, 0),
            Child = Txt(value, 9, true, Ink),
            ToolTip = tooltip
        };
        ToolTipService.SetInitialShowDelay(pill, 350);
        return pill;
    }
}
