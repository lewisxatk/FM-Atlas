using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private async Task<UIElement> ValueFinderPageAsync()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var stack = new StackPanel();
        var header = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 12));
        var intro = (StackPanel)header.Child;
        intro.Children.Add(Txt("RECRUITMENT INTELLIGENCE", 9, true, Teal));
        intro.Children.Add(Txt("Value & Upside Finder", 25, true, Ink));
        intro.Children.Add(Txt("Find players whose ability and potential rank better than their transfer-value rank inside this imported career. This is a transparent relative screening index, not Football Manager's hidden valuation formula or a claim that the displayed number is a real asking price.", 11, false, Muted, wrap: true));
        stack.Children.Add(header);

        var filters = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 10));
        var filterStack = (StackPanel)filters.Child;
        var age = Input("Max age"); age.Width = 110; age.Text = "25";
        var value = Input("Max market value (source units)"); value.Width = 210;
        var ca = Input("Min CA"); ca.Width = 100;
        var upside = Input("Min PA − CA"); upside.Width = 110; upside.Text = "8";
        var position = Input("Position contains (optional)"); position.Width = 190;
        var row = new WrapPanel();
        row.Children.Add(FilterControl("MAX AGE", age, 125));
        row.Children.Add(FilterControl("MAX VALUE · SOURCE UNITS", value, 225));
        row.Children.Add(FilterControl("MIN CA", ca, 115));
        row.Children.Add(FilterControl("MIN POTENTIAL UPSIDE", upside, 125));
        row.Children.Add(FilterControl("POSITION", position, 205));
        var status = Txt("Waiting for search…", 10, false, Muted, wrap: true);
        status.Margin = new Thickness(2, 8, 2, 0);
        var results = new List<ValueFinderCandidate>();
        var grid = CreateGrid();
        grid.Columns.Add(Column("VALUE INDEX", "DisplayValueIndex", .65));
        grid.Columns.Add(Column("PLAYER", "Player.Name", 1.25));
        grid.Columns.Add(Column("CLUB", "Player.Club", 1.0));
        grid.Columns.Add(Column("AGE", "Player.Age", .42));
        grid.Columns.Add(Column("POSITION", "Player.Position", .65));
        grid.Columns.Add(Column("CA", "Player.CurrentAbility", .42));
        grid.Columns.Add(Column("PA", "Player.PotentialAbility", .42));
        grid.Columns.Add(Column("UPSIDE", "DisplayPotentialUpside", .5));
        grid.Columns.Add(Column("VALUE · SOURCE UNITS", "Player.Value", .85));
        grid.Columns.Add(Column("ABILITY PCTL", "DisplayAbilityPercentile", .6));
        grid.Columns.Add(Column("VALUE PCTL", "DisplayMarketValuePercentile", .6));

        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        Button search = null!;
        search = PrimaryButton("Find undervalued players", async (_, _) => await RunSearchAsync());
        var export = SoftButton("Export CSV", (_, _) => ExportRows());
        var reset = SoftButton("Reset", (_, _) => { age.Text = "25"; value.Clear(); ca.Clear(); upside.Text = "8"; position.Clear(); grid.ItemsSource = null; results.Clear(); status.Text = "Filters cleared."; });
        buttons.Children.Add(search); buttons.Children.Add(export); buttons.Children.Add(reset);
        filterStack.Children.Add(row); filterStack.Children.Add(buttons); filterStack.Children.Add(status);
        stack.Children.Add(filters);
        var body = Card(new DockPanel(), 16);
        var dock = (DockPanel)body.Child;
        var resultHeader = new StackPanel();
        resultHeader.Children.Add(Txt("VALUE GAPS IN THIS CAREER", 9, true, Teal));
        resultHeader.Children.Add(Txt("Value Index = 50 + weighted CA/PA percentile − market-value percentile. Higher scores mean football ability/potential rank above market-value rank. Percentiles are calculated across the full valid player pool before your filters are applied.", 10, false, Muted, wrap: true));
        DockPanel.SetDock(resultHeader, Dock.Top); dock.Children.Add(resultHeader);
        DockPanel.SetDock(grid, Dock.Bottom); dock.Children.Add(grid);
        Grid.SetRow(stack, 0); root.Children.Add(stack);
        Grid.SetRow(body, 1); root.Children.Add(body);
        grid.MouseDoubleClick += async (_, _) => { if (grid.SelectedItem is ValueFinderCandidate item) await ShowPlayerProfileAsync(item.Player); };

        async Task RunSearchAsync()
        {
            int? ReadInt(TextBox box) => int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
            double? maxValue = double.TryParse(value.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var mv) ? mv : null;
            var maxAge = ReadInt(age); var minCa = ReadInt(ca); var minUpside = ReadInt(upside) ?? 0;
            if ((maxAge.HasValue && maxAge < 15) || (maxAge.HasValue && maxAge > 60) || (minCa.HasValue && minCa is < 1 or > 200) || minUpside is < 0 or > 100 || (maxValue.HasValue && maxValue <= 0))
            { status.Text = "Check filters: age 15–60, CA 1–200, potential gap 0–100 and value above zero."; return; }
            search.IsEnabled = false; Mouse.OverrideCursor = Cursors.Wait;
            status.Text = "Ranking players by ability/potential against transfer-value percentiles…";
            try
            {
                var positionFilter = position.Text.Trim();
                results = await RunDatabaseAsync(() => _database.GetValueFinderCandidatesAsync(maxAge, maxValue, minCa, minUpside, positionFilter, 5000));
                grid.ItemsSource = results;
                status.Text = results.Count == 0
                    ? "No players match. Import a save snapshot with CA, PA and transfer_value fields, or relax the filters."
                    : $"{results.Count:N0} candidates · ranked using the latest save snapshot · source value units retained without currency conversion. Double-click a row to open the player profile.";
                AtlasLog.Info($"Value Finder completed: results={results.Count}; age<={maxAge?.ToString() ?? "any"}; minCA={minCa?.ToString() ?? "any"}; minUpside={minUpside}; maxValue={maxValue?.ToString(CultureInfo.InvariantCulture) ?? "any"}.");
            }
            catch (Exception ex) { AtlasLog.Error("Value Finder failed.", ex); status.Text = "Value Finder failed: " + ex.Message; }
            finally { search.IsEnabled = true; Mouse.OverrideCursor = null; }
        }

        void ExportRows()
        {
            if (results.Count == 0) { MessageBox.Show(this, "Run Value Finder before exporting.", "FM Atlas · Value Finder"); return; }
            var dialog = new SaveFileDialog { Title = "Export value finder", FileName = "FM-Atlas-value-finder.csv", Filter = "CSV files (*.csv)|*.csv" };
            if (dialog.ShowDialog(this) != true) return;
            static string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
            var lines = new List<string> { "ValueIndex,AbilityPercentile,MarketValuePercentile,PotentialUpside,Player,Club,Age,Position,CA,PA,TransferValueSourceUnits,Nation,SourceId" };
            lines.AddRange(results.Select(x => string.Join(",", x.ValueIndex.ToString("0.0", CultureInfo.InvariantCulture), x.AbilityPercentile.ToString("0.0", CultureInfo.InvariantCulture), x.MarketValuePercentile.ToString("0.0", CultureInfo.InvariantCulture), x.PotentialUpside, Q(x.Player.Name), Q(x.Player.Club), x.Player.Age?.ToString(CultureInfo.InvariantCulture) ?? "", Q(x.Player.Position), x.Player.CurrentAbility?.ToString() ?? "", x.Player.PotentialAbility?.ToString() ?? "", x.Player.Value?.ToString(CultureInfo.InvariantCulture) ?? "", Q(x.Player.Nation), Q(x.Player.SourceId))));
            File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
            status.Text = $"Exported {results.Count:N0} value candidates.";
        }

        return root;
    }
}
