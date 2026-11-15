using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FmAtlas.Data;
using FmAtlas.Models;

namespace FmAtlas;

public partial class MainWindow
{
    private sealed class EntitySearchUiRow
    {
        public ComboBox Field { get; init; } = null!;
        public TextBox Minimum { get; init; } = null!;
        public TextBox Maximum { get; init; } = null!;
        public TextBox TextValue { get; init; } = null!;
        public ComboBox BooleanValue { get; init; } = null!;
        public Border Container { get; init; } = null!;
    }

    private async Task<UIElement> AdvancedSearchWorkspaceAsync()
    {
        var root = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var top = new StackPanel();
        top.Children.Add(Txt("FMRTE SEARCH WORKSPACE", 9, true, Brush("#E9823B")));
        top.Children.Add(Txt("Advanced Search", 27, true, Brush("#F08A42")));
        top.Children.Add(Txt("Choose what you want to find. Each entity has its own field library, with filters discovered from the imported FM career and verified live data.", 11, false, Brush("#9CA1A6"), true));

        var selector = new Grid { Margin = new Thickness(0, 14, 0, 12) };
        selector.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        selector.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        selector.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var host = new ContentControl();

        Button MakeEntityButton(string label, string subtitle, string glyph, string mode, int column)
        {
            var inner = new StackPanel();
            inner.Children.Add(Txt(glyph, 18, true, Brush("#E9823B")));
            inner.Children.Add(Txt(label.ToUpperInvariant(), 12, true, Brush("#EEE9E1")));
            inner.Children.Add(Txt(subtitle, 9, false, Brush("#9CA1A6")));
            var button = new Button
            {
                Content = inner,
                Tag = mode,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 8, 0),
                Background = Brush("#111316"),
                Foreground = Brush("#EEE9E1"),
                BorderBrush = Brush("#292C30"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                MinHeight = 76,
                Template = RoundedButtonTemplate(0)
            };
            Grid.SetColumn(button, column);
            selector.Children.Add(button);
            return button;
        }

        var playerButton = MakeEntityButton("Player", "Attributes · positions · contracts", "01", "Player", 0);
        var staffButton = MakeEntityButton("Staff", "Coaching · scouting · management", "02", "Staff", 1);
        var clubButton = MakeEntityButton("Club", "Facilities · finance · stadium", "03", "Club", 2);

        void SetActiveButton(Button active)
        {
            foreach (var button in new[] { playerButton, staffButton, clubButton })
            {
                var selected = ReferenceEquals(button, active);
                button.Background = Brush(selected ? "#352319" : "#111316");
                button.BorderBrush = Brush(selected ? "#E9823B" : "#292C30");
                if (button.Content is StackPanel stack)
                    foreach (var child in stack.Children.OfType<TextBlock>())
                        child.Foreground = Brush(selected ? "#EEE9E1" : "#9CA1A6");
            }
        }

        async Task ShowModeAsync(string mode)
        {
            var active = mode switch { "Player" => playerButton, "Staff" => staffButton, _ => clubButton };
            SetActiveButton(active);
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                UIElement workspace = mode switch
                {
                    "Player" => await PlayerAdvancedSearchWorkspaceAsync(),
                    "Staff" => await BuildAdvancedStaffSearchWorkspaceAsync(),
                    _ => await BuildAdvancedClubSearchWorkspaceAsync()
                };
                host.Content = workspace;
            }
            catch (Exception ex)
            {
                AtlasLog.Error($"Could not open {mode} advanced search.", ex);
                host.Content = ErrorPanel("SEARCH WORKSPACE UNAVAILABLE", ex.Message);
            }
            finally { Mouse.OverrideCursor = null; }
        }

        playerButton.Click += async (_, _) => await ShowModeAsync("Player");
        staffButton.Click += async (_, _) => await ShowModeAsync("Staff");
        clubButton.Click += async (_, _) => await ShowModeAsync("Club");

        Grid.SetRow(selector, 0);
        top.Children.Add(selector);
        root.Children.Add(top);

        var body = new Grid();
        Grid.SetRow(body, 1);
        body.Children.Add(host);
        root.Children.Add(body);
        await ShowModeAsync("Player");
        return root;
    }

    private UIElement ErrorPanel(string title, string message)
    {
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(Txt(title, 12, true, Brush("#E9823B")));
        panel.Children.Add(Txt(message, 11, false, Brush("#EEE9E1"), true));
        return Card(panel, 0);
    }

    private Task<UIElement> BuildAdvancedStaffSearchWorkspaceAsync()
        => BuildAdvancedEntitySearchWorkspaceAsync("Staff");

    private Task<UIElement> BuildAdvancedClubSearchWorkspaceAsync()
        => BuildAdvancedEntitySearchWorkspaceAsync("Club");

    private async Task<UIElement> BuildAdvancedEntitySearchWorkspaceAsync(string entity)
    {
        var isStaff = entity.Equals("Staff", StringComparison.OrdinalIgnoreCase);
        var fields = await RunDatabaseAsync(() => isStaff
            ? _database.GetAdvancedStaffSearchFieldsAsync()
            : _database.GetAdvancedClubSearchFieldsAsync());

        var rows = new List<EntitySearchUiRow>();
        var results = new List<object>();
        const int pageSize = 250;
        var pageOffset = 0;
        var hasNext = false;

        StackPanel conditionPanel = new() { Margin = new Thickness(0, 8, 0, 0) };
        TextBlock status = Txt("Add a condition or enter a search term, then run the search.", 10, false, Brush("#9CA1A6"));
        Button previous = new();
        Button next = new();
        TextBlock pageLabel = Txt("No results", 9, false, Brush("#9CA1A6"));
        DataGrid grid = new();

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var stack = new StackPanel();

        var intro = Card(new StackPanel(), 0, new Thickness(0, 0, 0, 10));
        var introStack = (StackPanel)intro.Child;
        introStack.Children.Add(Txt(isStaff ? "STAFF SEARCH" : "CLUB SEARCH", 9, true, Brush("#E9823B")));
        introStack.Children.Add(Txt(isStaff ? "Find staff by their FM profile" : "Find clubs by their FM data", 20, true, Brush("#F08A42")));
        introStack.Children.Add(Txt(isStaff
            ? "Combine job, age, nation, ability and every staff field present in the imported snapshot. Coaching, scouting and training fields appear when the source supplies them."
            : "Search club identity, squad size, finances, facilities, stadium and linked club facts. Only fields present in the imported FM data are offered.",
            10, false, Brush("#9CA1A6"), true));
        stack.Children.Add(intro);

        var searchBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var query = Input(isStaff ? "Name, club, job, nationality or imported value" : "Club, nation, facility, debt or finance field");
        query.Width = 380;
        query.Height = 38;
        var logic = new ComboBox
        {
            Width = 172, Height = 38, Margin = new Thickness(3, 3, 8, 8),
            Background = Brush("#17191C"), Foreground = Brush("#EEE9E1"), BorderBrush = Brush("#292C30"),
            ToolTip = "Choose how multiple conditions combine."
        };
        logic.Items.Add("ALL conditions");
        logic.Items.Add("ANY condition");
        logic.SelectedIndex = 0;
        searchBar.Children.Add(query);
        searchBar.Children.Add(logic);
        searchBar.Children.Add(PrimaryButton("SEARCH", async (_, _) => { pageOffset = 0; await RunSearchAsync(); }));
        searchBar.Children.Add(SoftButton("＋ Add condition", (_, _) => AddCondition()));
        searchBar.Children.Add(SoftButton("Clear", (_, _) => ResetSearch()));
        stack.Children.Add(searchBar);

        var filters = Card(new StackPanel(), 0, new Thickness(0, 0, 0, 10));
        var filterStack = (StackPanel)filters.Child;
        var filterHeader = new Grid();
        filterHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filterHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filterHeader.Children.Add(Txt("FIELD CONDITIONS", 9, true, Brush("#E9823B")));
        var fieldCount = Txt($"{fields.Count:N0} source-backed fields", 9, false, Brush("#9CA1A6"));
        Grid.SetColumn(fieldCount, 1);
        filterHeader.Children.Add(fieldCount);
        filterStack.Children.Add(filterHeader);

        var conditionScroll = new ScrollViewer
        {
            Content = conditionPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 230
        };
        filterStack.Children.Add(conditionScroll);
        stack.Children.Add(filters);

        var resultCard = Card(new DockPanel(), 0, new Thickness(0, 0, 0, 0));
        var dock = (DockPanel)resultCard.Child;
        var resultHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        resultHeader.Children.Add(Txt(isStaff ? "STAFF MATCHES" : "CLUB MATCHES", 9, true, Brush("#E9823B")));
        status = Txt("Add a condition or enter a search term, then run the search.", 10, false, Brush("#9CA1A6"));
        status.Margin = new Thickness(0, 4, 0, 0);
        resultHeader.Children.Add(status);

        var paging = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
        previous = SoftButton("← Previous", async (_, _) => { if (pageOffset >= pageSize) { pageOffset -= pageSize; await RunSearchAsync(); } });
        next = SoftButton("Next →", async (_, _) => { if (hasNext) { pageOffset += pageSize; await RunSearchAsync(); } });
        pageLabel = Txt("No results", 9, false, Brush("#9CA1A6"));
        previous.IsEnabled = false; next.IsEnabled = false;
        paging.Children.Add(previous); paging.Children.Add(pageLabel); paging.Children.Add(next);
        resultHeader.Children.Add(paging);
        DockPanel.SetDock(resultHeader, Dock.Top);
        dock.Children.Add(resultHeader);

        grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = true,
            CanUserResizeColumns = true,
            CanUserSortColumns = true,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            Background = Brush("#090A0C"),
            Foreground = Brush("#EEE9E1"),
            RowBackground = Brush("#111316"),
            AlternatingRowBackground = Brush("#17191C"),
            BorderBrush = Brush("#292C30"),
            BorderThickness = new Thickness(1),
            MinHeight = 270
        };

        if (isStaff)
        {
            AddTextColumn(grid, "STAFF", nameof(AtlasStaffRecord.Name), 1.3);
            AddTextColumn(grid, "JOB", nameof(AtlasStaffRecord.Job), 1.0);
            AddTextColumn(grid, "CLUB", nameof(AtlasStaffRecord.Club), 1.0);
            AddTextColumn(grid, "AGE", nameof(AtlasStaffRecord.Age), .45);
            AddTextColumn(grid, "NATION", nameof(AtlasStaffRecord.Nation), .85);
            AddTextColumn(grid, "CA", nameof(AtlasStaffRecord.CurrentAbility), .45);
            AddTextColumn(grid, "PA", nameof(AtlasStaffRecord.PotentialAbility), .45);
            AddTextColumn(grid, "SOURCE", nameof(AtlasStaffRecord.ValidationStatus), .9);
        }
        else
        {
            AddTextColumn(grid, "CLUB", nameof(AtlasClubRecord.Name), 1.3);
            AddTextColumn(grid, "NATION", nameof(AtlasClubRecord.Nation), .85);
            AddTextColumn(grid, "SQUAD", nameof(AtlasClubRecord.PlayerCount), .5);
            AddTextColumn(grid, "SOURCE", nameof(AtlasClubRecord.DataSource), 1.1);
        }
        grid.MouseDoubleClick += async (_, _) =>
        {
            if (isStaff && grid.SelectedItem is AtlasStaffRecord staff)
            {
                _pendingStaffSourceId = staff.SourceId;
                _pendingStaffSearchQuery = staff.Name;
                await NavigateAsync("Staff");
            }
            else if (!isStaff && grid.SelectedItem is AtlasClubRecord club)
            {
                _pendingClubSourceId = club.SourceId;
                _pendingClubSearchQuery = club.Name;
                await NavigateAsync("Clubs");
            }
        };
        dock.Children.Add(grid);

        Grid.SetRow(stack, 0);
        root.Children.Add(stack);
        Grid.SetRow(resultCard, 1);
        root.Children.Add(resultCard);

        void AddTextColumn(DataGrid target, string header, string property, double weight)
        {
            target.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(property),
                Width = new DataGridLength(weight, DataGridLengthUnitType.Star),
                ElementStyle = new Style(typeof(TextBlock))
                {
                    Setters = { new Setter(TextBlock.ForegroundProperty, Brush("#EEE9E1")), new Setter(TextBlock.PaddingProperty, new Thickness(6, 4, 6, 4)) }
                }
            });
        }

        void AddCondition()
        {
            if (rows.Count >= 60) { status.Text = "Maximum 60 conditions per search."; return; }
            var field = new ComboBox
            {
                Width = 310, Height = 36, Margin = new Thickness(3),
                ItemsSource = fields,
                DisplayMemberPath = nameof(AdvancedSearchField.PickerLabel),
                IsTextSearchEnabled = true,
                Background = Brush("#17191C"),
                Foreground = Brush("#EEE9E1"),
                BorderBrush = Brush("#292C30"),
                ToolTip = "Choose a field. Its category is shown before the field name."
            };
            if (fields.Count > 0) field.SelectedIndex = 0;
            var minimum = Input("Minimum"); minimum.Width = 95; minimum.Height = 36;
            var maximum = Input("Maximum"); maximum.Width = 95; maximum.Height = 36;
            var textValue = Input("Contains text"); textValue.Width = 185; textValue.Height = 36;
            var boolean = new ComboBox
            {
                Width = 125, Height = 36, Margin = new Thickness(3),
                ItemsSource = new[] { "Any", "Yes", "No" }, SelectedIndex = 0,
                Background = Brush("#17191C"), Foreground = Brush("#EEE9E1"), BorderBrush = Brush("#292C30")
            };
            var values = new WrapPanel();
            values.Children.Add(minimum); values.Children.Add(maximum); values.Children.Add(textValue); values.Children.Add(boolean);
            var rowLayout = new DockPanel();
            var remove = SoftButton("Remove", (_, _) =>
            {
                var item = rows.FirstOrDefault(x => ReferenceEquals(x.Field, field));
                if (item is not null) { rows.Remove(item); conditionPanel.Children.Remove(item.Container); }
            });
            DockPanel.SetDock(remove, Dock.Right);
            rowLayout.Children.Add(remove);
            rowLayout.Children.Add(field);
            rowLayout.Children.Add(values);
            var border = new Border
            {
                Background = Brush("#111316"),
                BorderBrush = Brush("#292C30"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(6),
                Margin = new Thickness(0, 4, 0, 4),
                Child = rowLayout
            };
            var row = new EntitySearchUiRow { Field = field, Minimum = minimum, Maximum = maximum, TextValue = textValue, BooleanValue = boolean, Container = border };
            rows.Add(row);
            conditionPanel.Children.Add(border);

            void UpdateEditor()
            {
                var type = (field.SelectedItem as AdvancedSearchField)?.FieldType ?? AdvancedSearchFieldType.Text;
                minimum.Visibility = maximum.Visibility = type == AdvancedSearchFieldType.Number ? Visibility.Visible : Visibility.Collapsed;
                textValue.Visibility = type == AdvancedSearchFieldType.Text ? Visibility.Visible : Visibility.Collapsed;
                boolean.Visibility = type == AdvancedSearchFieldType.Boolean ? Visibility.Visible : Visibility.Collapsed;
            }
            field.SelectionChanged += (_, _) => UpdateEditor();
            UpdateEditor();
        }

        List<AdvancedSearchCriterion> CaptureCriteria(out string error)
        {
            error = string.Empty;
            var criteria = new List<AdvancedSearchCriterion>();
            foreach (var row in rows)
            {
                if (row.Field.SelectedItem is not AdvancedSearchField field) continue;
                if (field.FieldType == AdvancedSearchFieldType.Number)
                {
                    var hasMin = double.TryParse(row.Minimum.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var min);
                    var hasMax = double.TryParse(row.Maximum.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var max);
                    if (!hasMin && !hasMax) continue;
                    if (hasMin && hasMax && min > max)
                    {
                        error = $"Minimum cannot exceed maximum for {field.DisplayName}.";
                        return new List<AdvancedSearchCriterion>();
                    }
                    criteria.Add(new AdvancedSearchCriterion(field.Key, hasMin ? min : null, hasMax ? max : null, null, null));
                }
                else if (field.FieldType == AdvancedSearchFieldType.Text)
                {
                    var value = row.TextValue.Text.Trim();
                    if (value.Length == 0) continue;
                    criteria.Add(new AdvancedSearchCriterion(field.Key, null, null, value, null));
                }
                else
                {
                    var value = row.BooleanValue.SelectedItem?.ToString();
                    if (value is "Yes" or "No")
                        criteria.Add(new AdvancedSearchCriterion(field.Key, null, null, null, value == "Yes"));
                }
            }
            return criteria;
        }

        async Task RunSearchAsync()
        {
            var criteria = CaptureCriteria(out var error);
            if (error.Length > 0) { status.Text = error; return; }
            if (criteria.Count == 0 && string.IsNullOrWhiteSpace(query.Text))
            {
                status.Text = "Enter a search term or add at least one populated condition.";
                return;
            }

            previous.IsEnabled = false;
            next.IsEnabled = false;
            var searchButton = searchBar.Children.OfType<Button>().FirstOrDefault(x => x.Content?.ToString() == "SEARCH");
            if (searchButton is not null) searchButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var matchAll = logic.SelectedIndex == 0;
                if (isStaff)
                {
                    var page = await RunDatabaseAsync(() => _database.SearchAdvancedStaffAsync(query.Text, criteria, matchAll, pageSize + 1, pageOffset));
                    hasNext = page.Count > pageSize;
                    results = page.Take(pageSize).Cast<object>().ToList();
                }
                else
                {
                    var page = await RunDatabaseAsync(() => _database.SearchAdvancedClubsAsync(query.Text, criteria, matchAll, pageSize + 1, pageOffset));
                    hasNext = page.Count > pageSize;
                    results = page.Take(pageSize).Cast<object>().ToList();
                }
                grid.ItemsSource = results;
                pageLabel.Text = results.Count == 0 ? "No matches" : $"Showing {pageOffset + 1:N0}–{pageOffset + results.Count:N0}";
                status.Text = $"{results.Count:N0} records on this page · {criteria.Count:N0} active conditions · {(matchAll ? "ALL" : "ANY")} logic · source-backed fields only.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error($"Advanced {entity.ToLowerInvariant()} search failed.", ex);
                status.Text = $"Search failed: {ex.Message}";
            }
            finally
            {
                previous.IsEnabled = pageOffset > 0;
                next.IsEnabled = hasNext;
                if (searchButton is not null) searchButton.IsEnabled = true;
                Mouse.OverrideCursor = null;
            }
        }

        void ResetSearch()
        {
            query.Clear();
            rows.Clear();
            conditionPanel.Children.Clear();
            AddCondition();
            pageOffset = 0;
            hasNext = false;
            grid.ItemsSource = null;
            results.Clear();
            status.Text = "Search cleared. Add a condition or enter a search term.";
            pageLabel.Text = "No results";
            previous.IsEnabled = false;
            next.IsEnabled = false;
        }

        AddCondition();
        Grid.SetRow(stack, 0);
        root.Children.Add(stack);
        return root;
    }
}
