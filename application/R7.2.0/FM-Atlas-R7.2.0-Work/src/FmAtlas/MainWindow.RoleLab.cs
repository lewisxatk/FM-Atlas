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
        find = PrimaryButton("Find best fits", async (_, _) => await SearchAsync()); find.Margin = new Thickness(0, 0, 8, 8);
        var export = SoftButton("Export results CSV", (_, _) => ExportResults()); export.Margin = new Thickness(0, 0, 8, 8);
        controls.Children.Add(role); controls.Children.Add(find); controls.Children.Add(export);
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

        async Task SearchAsync()
        {
            if (role.SelectedItem is not RoleFitProfile selected) return;
            find.IsEnabled = false; status.Text = "Scoring imported player attributes…";
            try
            {
                results = await RunDatabaseAsync(() => _database.SearchRoleFitCandidatesAsync(selected, 50000));
                grid.ItemsSource = results;
                if (results.Count == 0)
                    status.Text = "No reliable role scores could be calculated. Import an FM27 save copy with player attributes; candidates need at least 50% of the role's requested attributes.";
                else
                    status.Text = $"{results.Count:N0} candidates ranked · top score {results[0].Score:0.00}/20 · available input coverage shown per player. Position familiarity and tactical instructions are not yet part of this estimate.";
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
            var sb = new StringBuilder("Player,Club,Age,Position,Atlas role fit / 20,Coverage,Attributes available,Attributes expected,CA,PA\r\n");
            foreach (var item in results)
                sb.AppendLine(string.Join(",", new[] { Q(item.Name), Q(item.Club), Q(item.Age?.ToString(CultureInfo.InvariantCulture)), Q(item.Position), Q(item.Score.ToString("0.00", CultureInfo.InvariantCulture)), Q(item.Coverage.ToString("P0", CultureInfo.InvariantCulture)), Q(item.AttributesAvailable.ToString(CultureInfo.InvariantCulture)), Q(item.AttributesExpected.ToString(CultureInfo.InvariantCulture)), Q(item.CurrentAbility?.ToString(CultureInfo.InvariantCulture)), Q(item.PotentialAbility?.ToString(CultureInfo.InvariantCulture)) }));
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            status.Text = $"Exported {results.Count:N0} role-fit rows.";
        }

        return root;
    }
}
