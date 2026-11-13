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
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 10));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("SCOUTING INTELLIGENCE", 9, true, Teal));
        hs.Children.Add(Txt("Role Fit Lab", 22, true, Ink));
        hs.Children.Add(Txt("Rank your imported players against transparent In Possession and Out of Possession role profiles. The score is an Atlas coaching estimate, not the game's hidden role formula or star rating. Missing attributes are excluded and coverage is shown beside every score.", 10, false, Muted, wrap: true));
        var controls = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        var role = new ComboBox { Width = 270, Height = 36, Margin = new Thickness(0, 0, 8, 8), DisplayMemberPath = nameof(RoleFitProfile.Name), ItemsSource = RoleFitService.GetProfiles() };
        role.SelectedIndex = 0;
        Button find = null!;
        TextBlock status = null!;
        DataGrid grid = null!;
        var results = new List<RoleFitCandidate>();
        find = PrimaryButton("Find best fits", async (_, _) => await SearchAsync()); find.Margin = new Thickness(0, 0, 8, 8);
        var export = SoftButton("Export results CSV", (_, _) => ExportResults()); export.Margin = new Thickness(0, 0, 8, 8);
        controls.Children.Add(role); controls.Children.Add(find); controls.Children.Add(export);
        hs.Children.Add(controls);
        var description = Txt("", 9, false, Muted, wrap: true); hs.Children.Add(description);
        var weightHeading = Txt("WEIGHTED ATTRIBUTES", 8, true, Teal);
        weightHeading.Margin = new Thickness(0, 10, 0, 3);
        hs.Children.Add(weightHeading);
        var weightsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
        hs.Children.Add(weightsPanel);
        var methodNote = Txt(
            "Method: weighted mean of available FM attributes (valid range 1–20). At least 50% of the role's requested attributes must be present; missing values are excluded, not treated as zero. This is an Atlas estimate, not FM's internal role score.",
            9, false, Muted, wrap: true);
        hs.Children.Add(methodNote);
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

        var contributionCard = Card(new StackPanel(), 12, new Thickness(0, 10, 0, 0));
        var contributionStack = (StackPanel)contributionCard.Child;
        contributionStack.Children.Add(Txt("SELECTED PLAYER CONTRIBUTIONS", 8, true, Teal));
        var contributionStatus = Txt("Select a player row to inspect how the available attribute inputs add up to the Atlas role-fit score.", 9, false, Muted, wrap: true);
        contributionStatus.Margin = new Thickness(0, 4, 0, 5);
        contributionStack.Children.Add(contributionStatus);
        var contributionPanel = new WrapPanel();
        contributionStack.Children.Add(contributionPanel);
        Grid.SetRow(contributionCard, 2); root.Children.Add(contributionCard);

        void UpdateDescription()
        {
            if (role.SelectedItem is not RoleFitProfile selected) return;

            description.Text = $"{selected.Phase} · {selected.PositionGroup} · {selected.Description}";
            weightsPanel.Children.Clear();

            foreach (var weight in selected.Weights.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var chipText = Txt($"{weight.Key}  ×  {weight.Value:0.##}", 9, true, Ink);
                var chip = new Border
                {
                    Background = TealDark,
                    BorderBrush = Line,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 5, 5),
                    Child = chipText,
                    ToolTip = $"{weight.Key}: Atlas weighting factor {weight.Value:0.##}. This is a transparent model input, not an FM hidden-role weight."
                };
                weightsPanel.Children.Add(chip);
            }
        }
        void UpdateContributionDetails(RoleFitCandidate? candidate)
        {
            contributionPanel.Children.Clear();
            if (candidate is null || candidate.Contributions.Count == 0)
            {
                contributionStatus.Text = "Select a player row to inspect the available attribute inputs and their additive score contributions.";
                return;
            }

            var contributionSum = candidate.Contributions.Sum(x => x.ScoreContribution);
            contributionStatus.Text =
                $"{candidate.Name} · {candidate.Score:0.00}/20 · {candidate.AttributesAvailable}/{candidate.AttributesExpected} inputs ({candidate.Coverage:P0} coverage). " +
                $"Displayed input contributions sum to {contributionSum:0.00}; missing or invalid attributes are not included.";

            foreach (var contribution in candidate.Contributions)
            {
                var label = Txt(
                    $"{contribution.Attribute}  {contribution.Value:0.#} × {contribution.Weight:0.##}  =  {contribution.ScoreContribution:0.00} pts",
                    9, true, Ink);
                var chip = new Border
                {
                    Background = TealDark,
                    BorderBrush = Line,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(8, 5, 8, 5),
                    Margin = new Thickness(0, 0, 5, 5),
                    Child = label,
                    ToolTip = $"{contribution.Attribute}: value {contribution.Value:0.##} × Atlas weight {contribution.Weight:0.##} contributes {contribution.ScoreContribution:0.000} points to the displayed weighted mean."
                };
                contributionPanel.Children.Add(chip);
            }
        }

        role.SelectionChanged += (_, _) =>
        {
            UpdateDescription();
            results.Clear();
            grid.ItemsSource = null;
            UpdateContributionDetails(null);
            status.Text = "Role changed. Run the search again to score this role's inputs.";
        };
        grid.SelectionChanged += (_, _) => UpdateContributionDetails(grid.SelectedItem as RoleFitCandidate);
        UpdateDescription();

        async Task SearchAsync()
        {
            if (role.SelectedItem is not RoleFitProfile selected) return;
            find.IsEnabled = false; status.Text = "Scoring imported player attributes…";
            try
            {
                results = await RunDatabaseAsync(() => _database.SearchRoleFitCandidatesAsync(selected, 50000));
                grid.ItemsSource = results;
                grid.SelectedItem = results.FirstOrDefault();
                UpdateContributionDetails(grid.SelectedItem as RoleFitCandidate);
                if (results.Count == 0)
                    status.Text = "No reliable role scores could be calculated. Import an FM27 save copy with player attributes; candidates need at least 50% of the role's requested attributes.";
                else
                    status.Text = $"{results.Count:N0} candidates ranked · top score {results[0].Score:0.00}/20 · select a player to inspect every available weighted input. Position familiarity and tactical instructions are not yet part of this estimate.";
                AtlasLog.Info($"Role Lab completed: {selected.Name} ({selected.Phase}), {results.Count} candidates.");
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Role Lab search failed.", ex);
                status.Text = "Role search failed: " + ex.Message;
            }
            finally { find.IsEnabled = true; }
        }

        void ExportResults()
        {
            if (results.Count == 0) { MessageBox.Show(this, "Run a role search before exporting.", "Role Fit Lab"); return; }
            var dlg = new SaveFileDialog { Title = "Export Role Fit results", FileName = "FM-Atlas-role-fit.csv", Filter = "CSV files (*.csv)|*.csv" };
            if (dlg.ShowDialog(this) != true) return;
            static string Q(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new StringBuilder("Player,Club,Age,Position,Atlas role fit / 20,Coverage,Attributes available,Attributes expected,CA,PA,Score contributions\r\n");
            foreach (var item in results)
            {
                var contributions = string.Join(" | ", item.Contributions.Select(x =>
                    $"{x.Attribute}={x.Value.ToString("0.##", CultureInfo.InvariantCulture)} x {x.Weight.ToString("0.##", CultureInfo.InvariantCulture)} -> {x.ScoreContribution.ToString("0.000", CultureInfo.InvariantCulture)} pts"));
                sb.AppendLine(string.Join(",", new[] { Q(item.Name), Q(item.Club), Q(item.Age?.ToString(CultureInfo.InvariantCulture)), Q(item.Position), Q(item.Score.ToString("0.00", CultureInfo.InvariantCulture)), Q(item.Coverage.ToString("P0", CultureInfo.InvariantCulture)), Q(item.AttributesAvailable.ToString(CultureInfo.InvariantCulture)), Q(item.AttributesExpected.ToString(CultureInfo.InvariantCulture)), Q(item.CurrentAbility?.ToString(CultureInfo.InvariantCulture)), Q(item.PotentialAbility?.ToString(CultureInfo.InvariantCulture)), Q(contributions) }));
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            status.Text = $"Exported {results.Count:N0} role-fit rows.";
        }

        return root;
    }
}
