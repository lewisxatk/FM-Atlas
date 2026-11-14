using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private async Task<UIElement> RoleLabPageAsync()
    {
        var root = new Grid();
        Button find = null!;
        TextBlock status = null!;
        DataGrid grid = null!;
        List<RoleFitCandidate> results = new();
        CancellationTokenSource? activeSearchCancellation = null;
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 10));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("SCOUTING INTELLIGENCE", 9, true, Teal));
        hs.Children.Add(Txt("Role Fit Lab", 22, true, Ink));
        hs.Children.Add(Txt("Rank your imported players against transparent In Possession and Out of Possession role profiles. The score is an Atlas coaching estimate, not the game's hidden role formula or star rating. Missing attributes are excluded and coverage is shown beside every score.", 10, false, Muted, wrap: true));
        var controls = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        List<RoleFitProfile> customProfiles;
        try { customProfiles = RoleFitProfileStore.LoadAll().ToList(); }
        catch (Exception ex) { AtlasLog.Warning("Could not load custom Role Fit templates: " + ex.Message); customProfiles = new List<RoleFitProfile>(); }
        var roleProfiles = RoleFitService.GetProfiles().Concat(customProfiles).ToList();
        var role = new ComboBox { Width = 270, Height = 36, Margin = new Thickness(0, 0, 8, 8), DisplayMemberPath = nameof(RoleFitProfile.Name), ItemsSource = roleProfiles };
        role.SelectedIndex = 0;
        var minimumAge = Input("Min age"); minimumAge.Width = 76; minimumAge.Margin = new Thickness(0, 0, 6, 8);
        var maximumAge = Input("Max age"); maximumAge.Width = 76; maximumAge.Margin = new Thickness(0, 0, 6, 8);
        var minimumCa = Input("Min CA"); minimumCa.Width = 76; minimumCa.Margin = new Thickness(0, 0, 6, 8);
        var minimumPa = Input("Min PA"); minimumPa.Width = 76; minimumPa.Margin = new Thickness(0, 0, 6, 8);
        var positionFilter = Input("Position contains"); positionFilter.Width = 118; positionFilter.Margin = new Thickness(0, 0, 6, 8);
        var coverage = new ComboBox { Width = 132, Height = 36, Margin = new Thickness(0, 0, 8, 8), ToolTip = "Minimum percentage of the role's weighted attributes that must actually exist." };
        foreach (var value in new[] { "50% coverage", "60% coverage", "70% coverage", "80% coverage", "90% coverage", "100% coverage" }) coverage.Items.Add(value);
        coverage.SelectedIndex = 0;
        Button cancel = null!;
        find = PrimaryButton("Find best fits", async (_, _) => await SearchAsync()); find.Margin = new Thickness(0, 0, 8, 8);
        cancel = SoftButton("Cancel search", (_, _) => activeSearchCancellation?.Cancel()); cancel.Margin = new Thickness(0, 0, 8, 8); cancel.IsEnabled = false;
        var export = SoftButton("Export results CSV", (_, _) => ExportResults()); export.Margin = new Thickness(0, 0, 8, 8);
        var saveTemplate = SoftButton("Create / customise role template", (_, _) => EditRoleTemplate());
        saveTemplate.Margin = new Thickness(0, 0, 8, 8);
        var deleteTemplate = SoftButton("Delete custom template", (_, _) => DeleteSelectedTemplate());
        deleteTemplate.Margin = new Thickness(0, 0, 8, 8);
        deleteTemplate.IsEnabled = role.SelectedItem is RoleFitProfile { IsCustom: true };
        controls.Children.Add(role);
        controls.Children.Add(FilterControl("MIN AGE", minimumAge, 82));
        controls.Children.Add(FilterControl("MAX AGE", maximumAge, 82));
        controls.Children.Add(FilterControl("MIN CA", minimumCa, 82));
        controls.Children.Add(FilterControl("MIN PA", minimumPa, 82));
        controls.Children.Add(FilterControl("POSITION", positionFilter, 124));
        controls.Children.Add(FilterControl("DATA COVERAGE", coverage, 138));
        controls.Children.Add(find); controls.Children.Add(cancel); controls.Children.Add(export);
        controls.Children.Add(saveTemplate); controls.Children.Add(deleteTemplate);
        hs.Children.Add(controls);
        var description = Txt("", 9, false, Muted, wrap: true); hs.Children.Add(description);
        status = Txt("Choose a role and search the latest imported player snapshot.", 10, false, Muted); status.Margin = new Thickness(0, 8, 0, 0); hs.Children.Add(status);
        Grid.SetRow(header, 0); root.Children.Add(header);

        grid = CreateGrid();
        grid.Columns.Add(Column("PLAYER", nameof(RoleFitCandidate.Name), 1.35));
        grid.Columns.Add(Column("CLUB", nameof(RoleFitCandidate.Club), 1.15));
        grid.Columns.Add(Column("AGE", nameof(RoleFitCandidate.Age), .45));
        grid.Columns.Add(Column("POSITION", nameof(RoleFitCandidate.Position), .7));
        grid.Columns.Add(new DataGridTextColumn { Header = "ATLAS ROLE FIT / 20", Binding = new System.Windows.Data.Binding(nameof(RoleFitCandidate.Score)) { StringFormat = "0.00" }, Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "COVERAGE", Binding = new System.Windows.Data.Binding(nameof(RoleFitCandidate.Coverage)) { StringFormat = "P0" }, Width = new DataGridLength(.65, DataGridLengthUnitType.Star) });
        grid.Columns.Add(Column("ATTRIBUTE EVIDENCE", nameof(RoleFitCandidate.EvidenceSummary), 1.35));
        grid.Columns.Add(Column("ATTRS", nameof(RoleFitCandidate.AttributesAvailable), .45));
        grid.Columns.Add(Column("CA", nameof(RoleFitCandidate.CurrentAbility), .4));
        grid.Columns.Add(Column("PA", nameof(RoleFitCandidate.PotentialAbility), .4));
        var card = Card(grid, 14); Grid.SetRow(card, 1); root.Children.Add(card);

        results = new List<RoleFitCandidate>();
        void UpdateDescription()
        {
            if (role.SelectedItem is RoleFitProfile selected)
                description.Text = $"{(selected.IsCustom ? "CUSTOM TEMPLATE · " : "")}{selected.Phase} · {selected.PositionGroup} · {selected.Description}  |  Weight coverage: {selected.Weights.Count} requested attributes.";
        }
        void ApplySavedFilters(RoleFitSearchFilters? filters)
        {
            minimumAge.Text = filters?.MinimumAge?.ToString(CultureInfo.InvariantCulture) ?? "";
            maximumAge.Text = filters?.MaximumAge?.ToString(CultureInfo.InvariantCulture) ?? "";
            minimumCa.Text = filters?.MinimumCurrentAbility?.ToString(CultureInfo.InvariantCulture) ?? "";
            minimumPa.Text = filters?.MinimumPotentialAbility?.ToString(CultureInfo.InvariantCulture) ?? "";
            positionFilter.Text = filters?.PositionContains ?? "";
            coverage.SelectedIndex = filters is null ? 0 : Math.Clamp(
                (int)Math.Round((filters.MinimumCoverage - 0.5) * 10, MidpointRounding.AwayFromZero), 0, 5);
        }
        role.SelectionChanged += (_, _) =>
        {
            UpdateDescription();
            deleteTemplate.IsEnabled = role.SelectedItem is RoleFitProfile { IsCustom: true };
            if (role.SelectedItem is RoleFitProfile selected) ApplySavedFilters(selected.SavedFilters);
        };
        UpdateDescription();


        string NormalizedAttribute(string value)
            => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        void RefreshRoleTemplates(string? selectedName = null)
        {
            customProfiles = RoleFitProfileStore.LoadAll().ToList();
            roleProfiles = RoleFitService.GetProfiles().Concat(customProfiles).ToList();
            role.ItemsSource = roleProfiles;
            role.SelectedItem = selectedName is null
                ? roleProfiles.FirstOrDefault()
                : roleProfiles.FirstOrDefault(x => x.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
            if (role.SelectedIndex < 0) role.SelectedIndex = 0;
            deleteTemplate.IsEnabled = role.SelectedItem is RoleFitProfile { IsCustom: true };
            UpdateDescription();
        }

        void EditRoleTemplate()
        {
            var seed = role.SelectedItem as RoleFitProfile ?? RoleFitService.GetProfiles().First();
            var weights = seed.Weights.ToDictionary(x => NormalizedAttribute(x.Key), x => x.Value, StringComparer.OrdinalIgnoreCase);
            var fields = PlayerAttributeSchema.Fields
                .Where(x => !x.Category.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var editor = new Window
            {
                Title = "FM Atlas · Custom Role Template",
                Owner = this,
                Width = 760,
                Height = 780,
                MinWidth = 620,
                MinHeight = 580,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brush("#111820"),
                Foreground = Ink
            };
            var layout = new DockPanel { Margin = new Thickness(18) };
            var header = new StackPanel();
            header.Children.Add(Txt("REUSABLE SCOUTING PROFILE", 9, true, Teal));
            header.Children.Add(Txt("Custom role template", 22, true, Ink));
            header.Children.Add(Txt("Choose the attributes and weights that matter for this role. Only the source-backed visible 1–20 attributes are offered; the score remains an Atlas estimate.", 10, false, Muted, wrap: true));
            DockPanel.SetDock(header, Dock.Top);
            layout.Children.Add(header);

            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var feedback = Txt("Select at least two attributes and give each a positive weight.", 9, false, Muted, wrap: true);
            DockPanel.SetDock(footer, Dock.Bottom);
            var cancelEditor = SoftButton("Cancel", (_, _) => editor.DialogResult = false);
            var saveEditor = PrimaryButton("Save template", (_, _) => { });
            footer.Children.Add(cancelEditor);
            footer.Children.Add(saveEditor);
            DockPanel.SetDock(feedback, Dock.Bottom);
            layout.Children.Add(footer);
            layout.Children.Add(feedback);

            var form = new StackPanel { Margin = new Thickness(0, 12, 0, 8) };
            var nameBox = Input("Template name");
            nameBox.Text = seed.IsCustom ? seed.Name : seed.Name + " Custom";
            nameBox.Margin = new Thickness(0, 3, 0, 8);
            form.Children.Add(FilterControl("TEMPLATE NAME", nameBox, 320));

            var phasePicker = new ComboBox { Width = 190, Height = 34, Margin = new Thickness(0, 3, 8, 8) };
            foreach (var phase in new[] { "Both", "In Possession", "Out of Possession" }) phasePicker.Items.Add(phase);
            phasePicker.SelectedItem = phasePicker.Items.Cast<string>().FirstOrDefault(x => x.Equals(seed.Phase, StringComparison.OrdinalIgnoreCase)) ?? "Both";
            form.Children.Add(FilterControl("PHASE", phasePicker, 200));

            var positionBox = Input("Position group");
            positionBox.Text = seed.PositionGroup;
            positionBox.Width = 190;
            form.Children.Add(FilterControl("POSITION GROUP", positionBox, 200));

            var descriptionBox = new TextBox
            {
                Text = seed.Description,
                Height = 56,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 3, 0, 8),
                Background = Brush("#171F28"),
                Foreground = Ink,
                BorderBrush = Line,
                Padding = new Thickness(8)
            };
            form.Children.Add(FilterControl("DESCRIPTION", descriptionBox, 620));

            var fieldPanel = new StackPanel();
            var fieldRows = new List<(PlayerAttributeField Field, CheckBox Include, TextBox Weight)>();
            foreach (var group in fields.GroupBy(x => x.Category))
            {
                fieldPanel.Children.Add(Txt(group.Key.ToUpperInvariant(), 9, true, Teal) is var label ? Configure(label, new Thickness(0, 9, 0, 3)) : new TextBlock());
                foreach (var field in group)
                {
                    var isSelected = weights.TryGetValue(NormalizedAttribute(field.Label), out var weight);
                    var check = new CheckBox
                    {
                        Content = field.Label,
                        IsChecked = isSelected,
                        Width = 180,
                        Foreground = Ink,
                        FontSize = 10,
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = "Use this attribute in the role fit calculation."
                    };
                    var weightBox = Input("Weight");
                    weightBox.Text = (isSelected ? weight : 1d).ToString(CultureInfo.InvariantCulture);
                    weightBox.Width = 75;
                    weightBox.Height = 30;
                    weightBox.Margin = new Thickness(8, 0, 0, 0);
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 2, 2, 2) };
                    row.Children.Add(check);
                    row.Children.Add(weightBox);
                    fieldPanel.Children.Add(row);
                    fieldRows.Add((field, check, weightBox));
                }
            }
            var fieldScroll = new ScrollViewer
            {
                Content = fieldPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 390
            };
            form.Children.Add(Txt("ATTRIBUTE WEIGHTS · CHECK EACH ATTRIBUTE TO INCLUDE IT", 8, true, Muted));
            form.Children.Add(fieldScroll);
            layout.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            editor.Content = layout;

            saveEditor.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(positionBox.Text))
                {
                    feedback.Text = "Enter a template name and position group.";
                    editor.DialogResult = null;
                    return;
                }
                var selectedWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in fieldRows.Where(x => x.Include.IsChecked == true))
                {
                    if (!double.TryParse(row.Weight.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                        || !double.IsFinite(value) || value is <= 0 or > 20)
                    {
                        feedback.Text = $"{row.Field.Label} weight must be greater than 0 and no greater than 20.";
                        return;
                    }
                    selectedWeights[row.Field.Label] = value;
                }
                if (selectedWeights.Count < 2)
                {
                    feedback.Text = "Select at least two attributes.";
                    return;
                }
                var name = nameBox.Text.Trim();
                if (RoleFitService.GetProfiles().Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    feedback.Text = "That name is already used by a built-in role. Choose a different template name.";
                    return;
                }
                try
                {
                    var savedFilters = CaptureCurrentFilters();
                    var profile = new RoleFitProfile(name, phasePicker.SelectedItem?.ToString() ?? "Both",
                        positionBox.Text.Trim(), descriptionBox.Text.Trim(), selectedWeights, true, savedFilters);
                    RoleFitProfileStore.Save(profile);
                    editor.DialogResult = true;
                }
                catch (Exception ex)
                {
                    AtlasLog.Error("Custom Role Fit template could not be saved.", ex);
                    feedback.Text = "Could not save template: " + ex.Message;
                }
            };

            if (editor.ShowDialog() == true)
            {
                var savedName = nameBox.Text.Trim();
                RefreshRoleTemplates(savedName);
                status.Text = $"Saved custom role template '{savedName}' locally on this PC. It is available the next time Role Fit Lab opens.";
            }
        }

        void DeleteSelectedTemplate()
        {
            if (role.SelectedItem is not RoleFitProfile { IsCustom: true } selected)
            {
                status.Text = "Choose a custom template first. Built-in role profiles cannot be deleted.";
                return;
            }
            var answer = MessageBox.Show(this, $"Delete custom role template '{selected.Name}' from this PC?",
                "FM Atlas · Delete Role Template", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            try
            {
                RoleFitProfileStore.Delete(selected.Name);
                RefreshRoleTemplates();
                status.Text = $"Deleted custom role template '{selected.Name}'. Built-in roles and career data are unchanged.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Custom Role Fit template deletion failed.", ex);
                status.Text = "Could not delete template: " + ex.Message;
            }
        }

        int? ReadOptionalInt(TextBox box, string label, int maximum)
        {
            var raw = box.Text.Trim();
            if (raw.Length == 0) return null;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0 || value > maximum)
                throw new FormatException($"{label} must be a whole number from 0 to {maximum:N0}.");
            return value;
        }

        RoleFitSearchFilters CaptureCurrentFilters()
        {
            var minAge = ReadOptionalInt(minimumAge, "Minimum age", 120);
            var maxAge = ReadOptionalInt(maximumAge, "Maximum age", 120);
            if (minAge.HasValue && maxAge.HasValue && minAge > maxAge)
                throw new FormatException("Minimum age cannot exceed maximum age.");
            return new RoleFitSearchFilters(
                MinimumAge: minAge,
                MaximumAge: maxAge,
                MinimumCurrentAbility: ReadOptionalInt(minimumCa, "Minimum CA", 200),
                MinimumPotentialAbility: ReadOptionalInt(minimumPa, "Minimum PA", 200),
                PositionContains: positionFilter.Text.Trim(),
                MinimumCoverage: 0.5 + Math.Clamp(coverage.SelectedIndex, 0, 5) * 0.1,
                ResultLimit: 250);
        }

        async Task SearchAsync()
        {
            if (role.SelectedItem is not RoleFitProfile selected || activeSearchCancellation is not null) return;
            RoleFitSearchFilters filters;
            try
            {
                filters = CaptureCurrentFilters();
            }
            catch (FormatException ex)
            {
                status.Text = ex.Message;
                return;
            }

            using var cancellation = new CancellationTokenSource();
            activeSearchCancellation = cancellation;
            find.IsEnabled = false;
            cancel.IsEnabled = true;
            status.Text = "Scoring imported player attributes against the selected role…";
            try
            {
                results = await RunDatabaseAsync(() => _database.SearchRoleFitCandidatesAsync(selected, 50000, filters, cancellation.Token));
                cancellation.Token.ThrowIfCancellationRequested();
                grid.ItemsSource = results;
                if (results.Count == 0)
                    status.Text = "No players match these filters with enough source-backed attributes. Lower the coverage threshold or import a save copy with broader player attribute coverage.";
                else
                    status.Text = $"{results.Count:N0} candidates ranked · top Atlas estimate {results[0].Score:0.00}/20 · source coverage shown per player. This is not the game's role rating; position familiarity and tactical instructions are not yet included.";
                AtlasLog.Info($"Role Lab completed: {selected.Name} ({selected.Phase}), {results.Count} candidates; age={filters.MinimumAge}-{filters.MaximumAge}; minCA={filters.MinimumCurrentAbility}; minPA={filters.MinimumPotentialAbility}; position={filters.PositionContains}; coverage={filters.MinimumCoverage:P0}.");
            }
            catch (OperationCanceledException)
            {
                status.Text = "Role Fit search cancelled. The previous completed result set is retained.";
                AtlasLog.Info("Role Fit search cancelled by user.");
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Role Lab search failed.", ex);
                status.Text = "Role search failed: " + ex.Message;
            }
            finally
            {
                activeSearchCancellation = null;
                cancel.IsEnabled = false;
                find.IsEnabled = true;
            }
        }

        void ExportResults()
        {
            if (results.Count == 0) { MessageBox.Show(this, "Run a role search before exporting.", "Role Fit Lab"); return; }
            var dlg = new SaveFileDialog { Title = "Export Role Fit results", FileName = "FM-Atlas-role-fit.csv", Filter = "CSV files (*.csv)|*.csv" };
            if (dlg.ShowDialog(this) != true) return;

            static string Q(string? value, bool protectFormula = true)
            {
                var text = value ?? "";
                var trimmed = text.TrimStart();
                if (protectFormula && trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@')
                    text = "'" + text;
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }

            var sb = new StringBuilder("Player,Source ID,Club,Age,Position,Atlas role fit / 20,Coverage,Attribute evidence,Attributes available,Attributes expected,CA,PA\r\n");
            foreach (var item in results)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Q(item.Name), Q(item.SourceId), Q(item.Club),
                    Q(item.Age?.ToString(CultureInfo.InvariantCulture), false),
                    Q(item.Position),
                    Q(item.Score.ToString("0.00", CultureInfo.InvariantCulture), false),
                    Q(item.Coverage.ToString("P0", CultureInfo.InvariantCulture), false),
                    Q(item.EvidenceSummary),
                    Q(item.AttributesAvailable.ToString(CultureInfo.InvariantCulture), false),
                    Q(item.AttributesExpected.ToString(CultureInfo.InvariantCulture), false),
                    Q(item.CurrentAbility?.ToString(CultureInfo.InvariantCulture), false),
                    Q(item.PotentialAbility?.ToString(CultureInfo.InvariantCulture), false)
                }));
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            status.Text = $"Exported {results.Count:N0} role-fit rows with source IDs and attribute coverage.";
        }

        return root;
    }
}
