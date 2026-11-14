using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using FmAtlas.Data;
using FmAtlas.Models;
using FmAtlas.Services;

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

    private PlayerComparisonWindow(IReadOnlyList<PlayerRecord> players, IReadOnlyList<Dictionary<string, double>> attributes, IReadOnlyDictionary<string, IReadOnlyList<PositionSuitabilityEntry>> positionRatings, IReadOnlyList<List<AtlasFactRecord>> performanceFacts)
    {
        Title = "FM Atlas · Player Comparison";
        Width = 1320;
        Height = 820;
        MinWidth = 900;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(21, 23, 25));
        Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 244));

        var root = new DockPanel { Margin = new Thickness(18) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = "SCOUTING INTELLIGENCE", Foreground = new SolidColorBrush(Color.FromRgb(110, 174, 218)), FontSize = 10, FontWeight = FontWeights.Bold });
        header.Children.Add(new TextBlock { Text = "Player comparison", FontSize = 26, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 2) });
        header.Children.Add(new TextBlock
        {
            Text = $"Comparing {players.Count} players · attributes, 15 position ratings and imported performance facts · missing fields remain blank.",
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
        grid.ItemsSource = BuildRows(players, attributes, positionRatings);

        var radarData = PlayerComparisonRadarService.Build(
            players.Select(x => x.Name).ToList(), attributes);
        var radarPanel = BuildRadarPanel(radarData);
        DockPanel.SetDock(radarPanel, Dock.Top);
        root.Children.Add(radarPanel);

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
        var positionRatings = await database.GetImportedPositionSuitabilityAsync();
        var performanceFacts = await Task.WhenAll(selected.Select(x => database.GetPlayerPerformanceFactsAsync(x.SourceId)));
        var window = new PlayerComparisonWindow(selected, attributeSets, positionRatings, performanceFacts) { Owner = owner };
        window.ShowDialog();
    }

    private static List<ComparisonRow> BuildRows(IReadOnlyList<PlayerRecord> players, IReadOnlyList<Dictionary<string, double>> attributes, IReadOnlyDictionary<string, IReadOnlyList<PositionSuitabilityEntry>> positionRatings, IReadOnlyList<List<AtlasFactRecord>> performanceFacts)
    {
        var rows = new List<ComparisonRow>();
        void AddCore(string group, string field, Func<PlayerRecord, string> selector)
            => rows.Add(CreateRow(group, field, players.Select(selector).ToArray()));

        AddCore("Profile", "Age", x => x.Age?.ToString(CultureInfo.InvariantCulture) ?? "—");
        AddCore("Profile", "Club", x => x.Club ?? "—");
        AddCore("Profile", "League", x => x.League ?? "—");
        AddCore("Profile", "Nationality", x => x.Nation ?? "—");
        AddCore("Profile", "Home-grown at club", x => x.DisplayHomeGrownClub);
        AddCore("Profile", "Home-grown in nation", x => x.DisplayHomeGrownNation);
        AddCore("Profile", "Position", x => x.Position ?? "—");

        // Compare every canonical FM position side by side, not just the displayed
        // primary position. This exposes versatility and specialist suitability.
        foreach (var position in PositionSuitabilityService.PositionCodes)
        {
            var values = players.Select(player =>
            {
                if (!positionRatings.TryGetValue(player.SourceId, out var entries)) return "—";
                var rating = entries.FirstOrDefault(x => x.Code.Equals(position, StringComparison.OrdinalIgnoreCase))?.Rating;
                return rating.HasValue ? rating.Value.ToString("0.#", CultureInfo.InvariantCulture) + "/20" : "—";
            }).ToArray();
            if (values.Any(x => x != "—"))
                rows.Add(CreateRow("Position suitability · source 1–20", position, values));
        }
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

        // Imported season, match and FM View Exporter facts are shown alongside attributes.
        // These are original source values; no per-90 conversion or event metric is inferred here.
        var performanceKeys = performanceFacts.SelectMany(x => x)
            .Select(x => (x.Group, x.Field))
            .Distinct()
            .OrderBy(x => x.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Field, StringComparer.OrdinalIgnoreCase);
        foreach (var key in performanceKeys)
        {
            var values = performanceFacts.Select(facts =>
            {
                var fact = facts.FirstOrDefault(x =>
                    x.Group.Equals(key.Group, StringComparison.OrdinalIgnoreCase)
                    && x.Field.Equals(key.Field, StringComparison.OrdinalIgnoreCase));
                return fact?.Value ?? "—";
            }).ToArray();
            rows.Add(CreateRow("Performance · imported source values · " + key.Group, key.Field, values));
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

    private static UIElement BuildRadarPanel(PlayerComparisonRadarData data)
    {
        var layout = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        layout.Children.Add(new TextBlock
        {
            Text = "SHARED ATTRIBUTE RADAR",
            Foreground = new SolidColorBrush(Color.FromRgb(110, 174, 218)),
            FontSize = 10,
            FontWeight = FontWeights.Bold
        });
        layout.Children.Add(new TextBlock
        {
            Text = "Average source-backed 1–20 ratings by attribute group · only categories present for every compared player are plotted.",
            Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 7)
        });

        if (!data.CanRender)
        {
            layout.Children.Add(new TextBlock
            {
                Text = data.Message,
                Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 10)
            });
            return layout;
        }

        var canvas = new Canvas
        {
            Width = 800,
            Height = 355,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(19, 27, 36)),
            ClipToBounds = true
        };
        const double centerX = 400, centerY = 165, radius = 112;
        var gridBrush = new SolidColorBrush(Color.FromRgb(47, 61, 75));
        var axisBrush = new SolidColorBrush(Color.FromRgb(83, 107, 126));

        for (var ring = 1; ring <= 5; ring++)
        {
            var ringRadius = radius * ring / 5d;
            var points = new PointCollection();
            for (var axis = 0; axis < data.Categories.Count; axis++)
            {
                var angle = -Math.PI / 2 + axis * (2 * Math.PI / data.Categories.Count);
                points.Add(new Point(
                    centerX + Math.Cos(angle) * ringRadius,
                    centerY + Math.Sin(angle) * ringRadius));
            }
            canvas.Children.Add(new Polygon
            {
                Points = points,
                Stroke = gridBrush,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            });
        }

        for (var axis = 0; axis < data.Categories.Count; axis++)
        {
            var angle = -Math.PI / 2 + axis * (2 * Math.PI / data.Categories.Count);
            var x = centerX + Math.Cos(angle) * radius;
            var y = centerY + Math.Sin(angle) * radius;
            canvas.Children.Add(new Line
            {
                X1 = centerX, Y1 = centerY, X2 = x, Y2 = y,
                Stroke = axisBrush, StrokeThickness = 1
            });

            var label = new TextBlock
            {
                Text = data.Categories[axis],
                Width = 118,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 229, 237)),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(label, centerX + Math.Cos(angle) * (radius + 44) - 59);
            Canvas.SetTop(label, centerY + Math.Sin(angle) * (radius + 36) - 10);
            canvas.Children.Add(label);
        }

        var palette = new[]
        {
            Color.FromRgb(87, 183, 228),
            Color.FromRgb(238, 156, 89),
            Color.FromRgb(157, 201, 120),
            Color.FromRgb(194, 150, 221)
        };
        var legend = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 10)
        };
        for (var seriesIndex = 0; seriesIndex < data.Series.Count; seriesIndex++)
        {
            var series = data.Series[seriesIndex];
            var color = palette[seriesIndex % palette.Length];
            var brush = new SolidColorBrush(color);
            var points = new PointCollection();
            var dots = new List<Ellipse>();

            for (var axis = 0; axis < series.Values.Count; axis++)
            {
                var angle = -Math.PI / 2 + axis * (2 * Math.PI / series.Values.Count);
                var valueRadius = radius * Math.Clamp(series.Values[axis], 1, 20) / 20d;
                var point = new Point(
                    centerX + Math.Cos(angle) * valueRadius,
                    centerY + Math.Sin(angle) * valueRadius);
                points.Add(point);

                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = brush,
                    Stroke = Brushes.White,
                    StrokeThickness = 0.7,
                    ToolTip = $"{series.PlayerName} · {data.Categories[axis]}: {series.Values[axis]:0.0}/20 from {series.AttributeCounts[axis]} attributes"
                };
                Canvas.SetLeft(dot, point.X - 4);
                Canvas.SetTop(dot, point.Y - 4);
                dots.Add(dot);
            }

            canvas.Children.Add(new Polygon
            {
                Points = points,
                Stroke = brush,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B))
            });
            foreach (var dot in dots) canvas.Children.Add(dot);

            legend.Children.Add(new TextBlock
            {
                Text = "● " + series.PlayerName,
                Foreground = brush,
                FontSize = 10,
                Margin = new Thickness(8, 2, 8, 2),
                ToolTip = $"Shared categories: {series.Values.Count}"
            });
        }

        var shell = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(23, 31, 40)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(48, 57, 67)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10),
            Child = new StackPanel()
        };
        var panel = (StackPanel)shell.Child;
        panel.Children.Add(canvas);
        panel.Children.Add(legend);
        panel.Children.Add(new TextBlock
        {
            Text = "Each axis is the mean of available ratings within that group. Hidden attributes and personality are excluded. Categories without data for every player are left off the radar, not scored as zero.",
            Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)),
            FontSize = 9,
            TextWrapping = TextWrapping.Wrap
        });
        layout.Children.Add(shell);
        return layout;
    }
}
