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
        var role = new ComboBox { Width = 270, Height = 36, Margin = new Thickness(0, 0, 8, 8), DisplayMemberPath = nameof(RoleFitProfile.Name), ItemsSource = RoleFitService.GetProfiles() };
        role.SelectedIndex = 0;
        var minimumAge = Input("Min age"); minimumAge.Width = 76; minimumAge.Margin = new Thickness(0, 0, 6, 8);
        var maximumAge = Input("Max age"); maximumAge.Width = 76; maximumAge.Margin = new Thickness(0, 0, 6, 8);
        var minimumCa = Input("Min CA"); minimumCa.Width = 76; minimumCa.Margin = new Thickness(0, 0, 6, 8);
        var minimumPa = Input("Min PA"); minimumPa.Width = 76; minimumPa.Margin = new Thickness(0, 0, 6, 8);
        var positionFilter = Input("Position contains"); positionFilter.Width = 118; positionFilter.Margin = new Thickness(0, 0, 6, 8);
        var coverage = new ComboBox { Width = 132, Height = 36, Margin = new Thickness(0, 0, 8, 8), ToolTip = "Minimum percentage of the role's weighted attributes that must actually exist." };
        foreach (var value in new[] { "50% coverage", "60% coverage", "70% coverage", "80% coverage", "90% coverage", "100% coverage" }) coverage.Items.Add(value);
        coverage.SelectedIndex = 0;
        find = PrimaryButton("Find best fits", async (_, _) => await SearchAsync()); find.Margin = new Thickness(0, 0, 8, 8);
        var cancel = SoftButton("Cancel search", (_, _) => activeSearchCancellation?.Cancel()); cancel.Margin = new Thickness(0, 0, 8, 8); cancel.IsEnabled = false;
        var export = SoftButton("Export results CSV", (_, _) => ExportResults()); export.Margin = new Thickness(0, 0, 8, 8);
        controls.Children.Add(role);
        controls.Children.Add(FilterControl("MIN AGE", minimumAge, 82));
        controls.Children.Add(FilterControl("MAX AGE", maximumAge, 82));
        controls.Children.Add(FilterControl("MIN CA", minimumCa, 82));
        controls.Children.Add(FilterControl("MIN PA", minimumPa, 82));
        controls.Children.Add(FilterControl("POSITION", positionFilter, 124));
        controls.Children.Add(FilterControl("DATA COVERAGE", coverage, 138));
        controls.Children.Add(find); controls.Children.Add(cancel); controls.Children.Add(export);
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
        grid.Columns.Add(Column("ATTRS", nameof(RoleFitCandidate.AttributesAvailable), .45));
        grid.Columns.Add(Column("CA", nameof(RoleFitCandidate.CurrentAbility), .4));
        grid.Columns.Add(Column("PA", nameof(RoleFitCandidate.PotentialAbility), .4));
        var card = Card(grid, 14); Grid.SetRow(card, 1); root.Children.Add(card);

        results = new List<RoleFitCandidate>();
        void UpdateDescription()
        {
            if (role.SelectedItem is RoleFitProfile selected)
                description.Text = $"{selected.Phase} · {selected.PositionGroup} · {selected.Description}  |  Weight coverage: {selected.Weights.Count} requested attributes.";
        }
        role.SelectionChanged += (_, _) => UpdateDescription();
        UpdateDescription();

        int? ReadOptionalInt(TextBox box, string label, int maximum)
        {
            var raw = box.Text.Trim();
            if (raw.Length == 0) return null;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0 || value > maximum)
                throw new FormatException($"{label} must be a whole number from 0 to {maximum:N0}.");
            return value;
        }

        async Task SearchAsync()
        {
            if (role.SelectedItem is not RoleFitProfile selected || activeSearchCancellation is not null) return;
            RoleFitSearchFilters filters;
            try
            {
                var minAge = ReadOptionalInt(minimumAge, "Minimum age", 120);
                var maxAge = ReadOptionalInt(maximumAge, "Maximum age", 120);
                if (minAge.HasValue && maxAge.HasValue && minAge > maxAge)
                    throw new FormatException("Minimum age cannot exceed maximum age.");
                filters = new RoleFitSearchFilters(
                    MinimumAge: minAge,
                    MaximumAge: maxAge,
                    MinimumCurrentAbility: ReadOptionalInt(minimumCa, "Minimum CA", 200),
                    MinimumPotentialAbility: ReadOptionalInt(minimumPa, "Minimum PA", 200),
                    PositionContains: positionFilter.Text.Trim(),
                    MinimumCoverage: 0.5 + Math.Clamp(coverage.SelectedIndex, 0, 5) * 0.1,
                    ResultLimit: 250);
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
                AtlasLog.Info($"Role Lab completed: {selected.Name} ({selected.Phase}), {results.Count} candidates; age={minAge}-{maxAge}; minCA={filters.MinimumCurrentAbility}; minPA={filters.MinimumPotentialAbility}; position={filters.PositionContains}; coverage={filters.MinimumCoverage:P0}.");
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

            var sb = new StringBuilder("Player,Source ID,Club,Age,Position,Atlas role fit / 20,Coverage,Attributes available,Attributes expected,CA,PA\r\n");
            foreach (var item in results)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Q(item.Name), Q(item.SourceId), Q(item.Club),
                    Q(item.Age?.ToString(CultureInfo.InvariantCulture), false),
                    Q(item.Position),
                    Q(item.Score.ToString("0.00", CultureInfo.InvariantCulture), false),
                    Q(item.Coverage.ToString("P0", CultureInfo.InvariantCulture), false),
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
