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
    private readonly TextBlock _status;

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

        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        _status = new TextBlock
        {
            Text = candidates.Count == 0 ? "No players met the shared-attribute coverage threshold." : $"{candidates.Count:N0} candidates ranked · similarity is not a guarantee of equivalent role, ability or transfer value.",
            VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)),
            Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap
        };
        footer.Children.Add(_status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var addToShortlist = new Button { Content = "Add selected to shortlist", Padding = new Thickness(12, 7, 12, 7), IsEnabled = candidates.Count > 0, Margin = new Thickness(0, 0, 8, 0) };
        addToShortlist.Click += async (_, _) => await AddSelectedToShortlistAsync();
        actions.Children.Add(addToShortlist);
        var compare = new Button { Content = "Compare with reference", Padding = new Thickness(12, 7, 12, 7), IsEnabled = candidates.Count > 0 };
        compare.Click += async (_, _) => await CompareSelectedAsync();
        actions.Children.Add(compare);
        footer.Children.Add(actions);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        _grid.MouseDoubleClick += async (_, _) => await CompareSelectedAsync();
        Content = root;
    }

    private string? PromptShortlistName()
    {
        var dialog = new Window
        {
            Title = "Add similar players to shortlist",
            Owner = this,
            Width = 440,
            Height = 205,
            MinWidth = 400,
            MinHeight = 180,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(17, 24, 32)),
            Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 244)),
            ShowInTaskbar = false
        };
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = "RECRUITMENT PIPELINE", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(110, 174, 218)) });
        panel.Children.Add(new TextBlock { Text = "Choose a shortlist name", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 3) });
        panel.Children.Add(new TextBlock { Text = "Use an existing name to add to that shortlist, or enter a new name to create it.", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)), TextWrapping = TextWrapping.Wrap });
        var name = new TextBox { Text = "My Shortlist", Margin = new Thickness(0, 10, 0, 12), Padding = new Thickness(8, 6, 8, 6), MaxLength = 80 };
        panel.Children.Add(name);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => dialog.DialogResult = false;
        var save = new Button { Content = "Add players", Padding = new Thickness(12, 6, 12, 6) };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || name.Text.Trim().Length > 80)
            {
                MessageBox.Show(dialog, "Enter a shortlist name between 1 and 80 characters.", "FM Atlas · Shortlists", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            dialog.DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.Loaded += (_, _) => { name.Focus(); name.SelectAll(); };
        return dialog.ShowDialog() == true ? name.Text.Trim() : null;
    }

    private async Task AddSelectedToShortlistAsync()
    {
        var selected = _grid.SelectedItems.OfType<SimilarPlayerCandidate>()
            .Select(x => x.Player)
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceId))
            .GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .Take(50)
            .ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Select one or more similar players first. Use Ctrl or Shift for multi-select.", "FM Atlas · Shortlists", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var shortlistName = PromptShortlistName();
        if (string.IsNullOrWhiteSpace(shortlistName)) return;
        try
        {
            var added = await _database.AddPlayersToShortlistAsync(selected, shortlistName);
            var alreadyPresent = selected.Count - added;
            _status.Text = $"'{shortlistName}': {added:N0} new player(s) added · {alreadyPresent:N0} already present · reference player was not added.";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not add the selected players to the shortlist: " + ex.Message;
        }
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
