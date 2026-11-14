using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FmAtlas.Data;
using FmAtlas.Models;

namespace FmAtlas;

public sealed class SimilarPlayersWindow : Window
{
    private readonly ScoutDatabase _database;
    private readonly PlayerRecord _reference;
    private readonly DataGrid _grid;

    public SimilarPlayersWindow(ScoutDatabase database, PlayerRecord reference, IReadOnlyList<SimilarPlayerCandidate> candidates)
    {
        _database = database;
        _reference = reference;
        Title = "FM Atlas · Similar Player Discovery";
        Width = 1160;
        Height = 720;
        MinWidth = 860;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(13, 17, 23));
        Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 244));

        var root = new DockPanel { Margin = new Thickness(18) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = "RECRUITMENT INTELLIGENCE", Foreground = new SolidColorBrush(Color.FromRgb(110, 174, 218)), FontSize = 10, FontWeight = FontWeights.Bold });
        header.Children.Add(new TextBlock { Text = "Similar players", FontSize = 25, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 2) });
        header.Children.Add(new TextBlock
        {
            Text = $"Reference: {reference.Name} · {reference.Club ?? "Club unavailable"} · {reference.Position ?? "Position unavailable"}. Similarity uses shared, source-backed 1–20 footballing attributes only; provisional hidden ratings and personality are excluded.",
            Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)), FontSize = 11, TextWrapping = TextWrapping.Wrap
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        _grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Extended, SelectionUnit = DataGridSelectionUnit.FullRow,
            Background = new SolidColorBrush(Color.FromRgb(23, 31, 40)),
            Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 244)),
            RowBackground = new SolidColorBrush(Color.FromRgb(23, 31, 40)),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(32, 42, 53)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "PLAYER", Binding = new Binding("Player.Name"), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "CLUB", Binding = new Binding("Player.Club"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "POSITION", Binding = new Binding("Player.Position"), Width = new DataGridLength(.7, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "SIMILARITY", Binding = new Binding(nameof(SimilarPlayerCandidate.DisplaySimilarity)), Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "COVERAGE", Binding = new Binding(nameof(SimilarPlayerCandidate.DisplayCoverage)), Width = new DataGridLength(.7, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "SHARED ATTRIBUTES", Binding = new Binding(nameof(SimilarPlayerCandidate.EvidenceSummary)), Width = new DataGridLength(1.1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "CA", Binding = new Binding("Player.CurrentAbility"), Width = new DataGridLength(.45, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "PA", Binding = new Binding("Player.PotentialAbility"), Width = new DataGridLength(.45, DataGridLengthUnitType.Star) });
        _grid.ItemsSource = candidates;
        DockPanel.SetDock(_grid, Dock.Top);
        root.Children.Add(_grid);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(new TextBlock
        {
            Text = candidates.Count == 0 ? "No players met the shared-attribute coverage threshold." : $"{candidates.Count:N0} candidates ranked · similarity is not a guarantee of equivalent role, ability or transfer value.",
            VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)), Margin = new Thickness(0, 0, 12, 0)
        });
        var compare = new Button { Content = "Compare with reference", Padding = new Thickness(12, 7, 12, 7), IsEnabled = candidates.Count > 0 };
        compare.Click += async (_, _) => await CompareSelectedAsync();
        footer.Children.Add(compare);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        _grid.MouseDoubleClick += async (_, _) => await CompareSelectedAsync();
        Content = root;
    }

    private async Task CompareSelectedAsync()
    {
        var selected = _grid.SelectedItems.OfType<SimilarPlayerCandidate>()
            .Select(x => x.Player)
            .Take(3)
            .ToList();
        if (selected.Count == 0 && _grid.SelectedItem is SimilarPlayerCandidate current)
            selected.Add(current.Player);
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Select one or more similar players first.", "FM Atlas · Similar Players", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await PlayerComparisonWindow.ShowAsync(this, _database, new[] { _reference }.Concat(selected).Take(4).ToList());
    }
}
