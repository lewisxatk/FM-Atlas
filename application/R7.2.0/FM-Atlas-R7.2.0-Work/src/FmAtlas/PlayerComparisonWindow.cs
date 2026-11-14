using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FmAtlas.Data;
using FmAtlas.Models;

namespace FmAtlas;

public sealed class PlayerComparisonWindow : Window
{
    private sealed class ComparisonRow
    {
        public string Group { get; init; } = "";
        public string Field { get; init; } = "";
        public string Value0 { get; init; } = "—";
        public string Value1 { get; init; } = "—";
        public string Value2 { get; init; } = "—";
        public string Value3 { get; init; } = "—";
    }

    private PlayerComparisonWindow(IReadOnlyList<PlayerRecord> players, IReadOnlyList<Dictionary<string, double>> attributes)
    {
        Title = "FM Atlas · Player Comparison";
        Width = 1320;
        Height = 820;
        MinWidth = 900;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(13, 17, 23));
        Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 244));

        var root = new DockPanel { Margin = new Thickness(18) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = "SCOUTING INTELLIGENCE", Foreground = new SolidColorBrush(Color.FromRgb(110, 174, 218)), FontSize = 10, FontWeight = FontWeights.Bold });
        header.Children.Add(new TextBlock { Text = "Player comparison", FontSize = 26, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 2) });
        header.Children.Add(new TextBlock
        {
            Text = $"Comparing {players.Count} players · imported values only · missing fields remain blank rather than being treated as zero.",
            Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)), FontSize = 11
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            CanUserSortColumns = true, SelectionMode = DataGridSelectionMode.Single,
            Background = new SolidColorBrush(Color.FromRgb(23, 31, 40)),
            Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 244)),
            RowBackground = new SolidColorBrush(Color.FromRgb(23, 31, 40)),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(32, 42, 53)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "GROUP", Binding = new Binding(nameof(ComparisonRow.Group)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "ATTRIBUTE / FIELD", Binding = new Binding(nameof(ComparisonRow.Field)), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var label = player.Name + (string.IsNullOrWhiteSpace(player.Club) ? "" : "\n" + player.Club);
            var property = "Value" + i.ToString(CultureInfo.InvariantCulture);
            grid.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(property), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        }
        grid.ItemsSource = BuildRows(players, attributes);
        DockPanel.SetDock(grid, Dock.Bottom);
        root.Children.Add(grid);
        Content = root;
    }

    public static async Task ShowAsync(Window owner, ScoutDatabase database, IReadOnlyList<PlayerRecord> players)
    {
        var selected = players
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceId))
            .GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .Take(4)
            .ToList();
        if (selected.Count < 2) return;
        var attributeSets = await Task.WhenAll(selected.Select(x => database.GetPlayerAttributesAsync(x.SourceId)));
        var window = new PlayerComparisonWindow(selected, attributeSets) { Owner = owner };
        window.ShowDialog();
    }

    private static List<ComparisonRow> BuildRows(IReadOnlyList<PlayerRecord> players, IReadOnlyList<Dictionary<string, double>> attributes)
    {
        var rows = new List<ComparisonRow>();
        void AddCore(string group, string field, Func<PlayerRecord, string> selector)
            => rows.Add(CreateRow(group, field, players.Select(selector).ToArray()));

        AddCore("Profile", "Age", x => x.Age?.ToString(CultureInfo.InvariantCulture) ?? "—");
        AddCore("Profile", "Club", x => x.Club ?? "—");
        AddCore("Profile", "League", x => x.League ?? "—");
        AddCore("Profile", "Nationality", x => x.Nation ?? "—");
        AddCore("Profile", "Position", x => x.Position ?? "—");
        AddCore("Ability", "Current ability", x => x.CurrentAbility?.ToString(CultureInfo.InvariantCulture) ?? "—");
        AddCore("Ability", "Potential ability", x => x.PotentialAbility?.ToString(CultureInfo.InvariantCulture) ?? "—");
        AddCore("Value", "Market value · source units", x => x.Value?.ToString("N0", CultureInfo.InvariantCulture) ?? "—");
        AddCore("Source", "Data origin", x => x.DataOrigin);
        AddCore("Source", "Source ID", x => x.SourceId);

        var keys = attributes.SelectMany(x => x.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => CategoryFor(x), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => FieldFor(x), StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var values = attributes.Select(set =>
                set.TryGetValue(key, out var value) && double.IsFinite(value)
                    ? value.ToString("0.##", CultureInfo.InvariantCulture)
                    : "—").ToArray();
            rows.Add(CreateRow(CategoryFor(key), FieldFor(key), values));
        }
        return rows;
    }

    private static string CategoryFor(string key)
    {
        var separator = key.IndexOf(" · ", StringComparison.Ordinal);
        return separator > 0 ? key[..separator] : "Attributes";
    }

    private static string FieldFor(string key)
    {
        var separator = key.IndexOf(" · ", StringComparison.Ordinal);
        return separator > 0 ? key[(separator + 3)..] : key;
    }

    private static ComparisonRow CreateRow(string group, string field, IReadOnlyList<string> values)
        => new()
        {
            Group = group, Field = field,
            Value0 = values.ElementAtOrDefault(0) ?? "—",
            Value1 = values.ElementAtOrDefault(1) ?? "—",
            Value2 = values.ElementAtOrDefault(2) ?? "—",
            Value3 = values.ElementAtOrDefault(3) ?? "—"
        };
}
