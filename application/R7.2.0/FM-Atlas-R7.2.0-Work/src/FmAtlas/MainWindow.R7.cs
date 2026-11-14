using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using FmAtlas.Data;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private async Task SaveAtlasAsync()
    {
        try
        {
            await RunDatabaseAsync(() => _database.CheckpointAsync());
            if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Atlas database checkpoint completed · local edits are stored";
            MessageBox.Show(this, "Your FM Atlas database has been checkpointed. This saves Atlas-local edits and history; it does not save or modify the Football Manager career file.", "FM Atlas 27 · Save Atlas", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Atlas database checkpoint failed.", ex);
            MessageBox.Show(this, "Could not checkpoint the Atlas database. Your edits may still be in SQLite's write-ahead log.\n\n" + ex.Message, "Save Atlas failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task<UIElement> AnalyticsPageAsync()
    {
        var root = new Grid();
        Button run = null!;
        DataGrid grid = null!;
        TextBlock status = null!;
        List<AnalyticsMetricResult> displayedRows = new();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = Card(new StackPanel(), 14, new Thickness(0, 0, 0, 10));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("CAREER PERFORMANCE", 9, true, Teal));
        hs.Children.Add(Txt("Season analytics", 23, true, Ink));
        hs.Children.Add(Txt("Explore the numeric player-season and match metrics retained by the latest imported save. Per-90 options are calculated from season totals and playing minutes, with a configurable minimum sample (450 minutes by default). Fields only appear when the save reader supplies them.", 11, false, Muted, wrap: true));
        Grid.SetRow(header, 0); root.Children.Add(header);

        var filters = Card(new WrapPanel(), 14, new Thickness(0, 0, 0, 10));
        var wrap = (WrapPanel)filters.Child;
        var metric = new ComboBox { Width = 230, Height = 36, Margin = new Thickness(0, 0, 8, 8), IsEditable = true, IsTextSearchEnabled = true };
        var min = Input("Minimum value"); min.Width = 115; min.Margin = new Thickness(0, 0, 8, 8); min.Height = 36;
        var max = Input("Maximum value"); max.Width = 115; max.Margin = new Thickness(0, 0, 8, 8); max.Height = 36;
        var minMinutes = Input("Min minutes (per 90)"); minMinutes.Text = "450"; minMinutes.Width = 145; minMinutes.Margin = new Thickness(0, 0, 8, 8); minMinutes.Height = 36;
        run = PrimaryButton("Find players", (_, _) => _ = SearchMetricAsync()); run.Margin = new Thickness(0, 0, 8, 8);
        var export = SoftButton("Export results CSV", (_, _) => ExportAnalytics()); export.Margin = new Thickness(0, 0, 8, 8);
        wrap.Children.Add(FilterControl("SEASON / MATCH METRIC", metric, 245));
        wrap.Children.Add(FilterControl("MINIMUM", min, 130));
        wrap.Children.Add(FilterControl("MAXIMUM", max, 130));
        wrap.Children.Add(FilterControl("PER-90 SAMPLE", minMinutes, 120));
        wrap.Children.Add(run); wrap.Children.Add(export);
        Grid.SetRow(filters, 1); root.Children.Add(filters);

        grid = CreateGrid();
        grid.Columns.Add(Column("PLAYER", "Name", 1.4));
        grid.Columns.Add(Column("CLUB", "Club", 1.2));
        grid.Columns.Add(Column("SEASON", "Season", .9));
        grid.Columns.Add(new DataGridTextColumn { Header = "VALUE", Binding = new System.Windows.Data.Binding("Value") { StringFormat = "N2" }, Width = new DataGridLength(.7, DataGridLengthUnitType.Star) });
        var gridCard = Card(grid, 14); Grid.SetRow(gridCard, 2); root.Children.Add(gridCard);
        status = Txt("Loading available metrics…", 10, false, Muted); status.Margin = new Thickness(4, 8, 0, 0); Grid.SetRow(status, 3); root.Children.Add(status);
        try
        {
            var metrics = await RunDatabaseAsync(() => _database.GetSeasonMetricNamesAsync());
            var per90Metrics = await RunDatabaseAsync(() => _database.GetSeasonPer90MetricNamesAsync());
            var metricChoices = metrics.Concat(per90Metrics.Select(x => "Per 90 · " + x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            metric.ItemsSource = metricChoices;
            if (metricChoices.Count > 0) metric.SelectedIndex = 0;
            status.Text = metrics.Count == 0
                ? "No numeric season/match metrics were found in the latest imported snapshot. Import a save copy containing player_season_stats or player_match_stats."
                : $"{metrics.Count:N0} numeric metric fields found · {per90Metrics.Count:N0} season fields support a derived per-90 option · latest imported save snapshot.";
        }
        catch (Exception ex) { AtlasLog.Error("Analytics metric discovery failed.", ex); status.Text = "Metric discovery failed: " + ex.Message; }

        async Task SearchMetricAsync()
        {
            var selectedMetric = metric.SelectedItem?.ToString() ?? metric.Text.Trim();
            if (string.IsNullOrWhiteSpace(selectedMetric)) { status.Text = "Choose a metric first."; return; }
            var per90 = selectedMetric.StartsWith("Per 90 · ", StringComparison.OrdinalIgnoreCase);
            var key = per90 ? selectedMetric[9..].Trim() : selectedMetric;
            double? lo = double.TryParse(min.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var lv) ? lv : null;
            double? hi = double.TryParse(max.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var hv) ? hv : null;
            var sample = int.TryParse(minMinutes.Text, out var mins) ? Math.Clamp(mins, 1, 10000) : 450;
            if (lo.HasValue && hi.HasValue && lo > hi) { status.Text = "Minimum cannot exceed maximum."; return; }
            run.IsEnabled = false; status.Text = "Searching imported performance rows…";
            try
            {
                var rows = per90
                    ? await RunDatabaseAsync(() => _database.SearchSeasonMetricPer90Async(key, lo, hi, sample, 500))
                    : await RunDatabaseAsync(() => _database.SearchSeasonMetricAsync(key, lo, hi, 500));
                displayedRows = rows;
                grid.ItemsSource = rows;
                AtlasLog.Info($"Analytics search completed: metric={key}, per90={per90}, minutesFloor={(per90 ? sample : 0)}, rows={rows.Count}.");
                status.Text = $"{rows.Count:N0} matching rows · metric: {key}{(per90 ? $" per 90 · minimum {sample:N0} minutes" : "") } · only records with a linked player identity are displayed.";
            }
            catch (Exception ex) { AtlasLog.Error("Analytics search failed.", ex); status.Text = "Analytics search failed: " + ex.Message; }
            finally { run.IsEnabled = true; }
        }

        void ExportAnalytics()
        {
            if (displayedRows.Count == 0) { MessageBox.Show(this, "Run an analytics search before exporting.", "Analytics export"); return; }
            var dlg = new SaveFileDialog { Title = "Export analytics results", FileName = "FM-Atlas-analytics.csv", Filter = "CSV files (*.csv)|*.csv" };
            if (dlg.ShowDialog(this) != true) return;
            static string Q(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new StringBuilder("Player,Club,Season,Value\r\n");
            foreach (var row in displayedRows)
                sb.AppendLine(string.Join(",", new[] { Q(row.Name), Q(row.Club), Q(row.Season), Q(row.Value.ToString("0.####", CultureInfo.InvariantCulture)) }));
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            AtlasLog.Info($"Analytics CSV exported: {displayedRows.Count} rows.");
            status.Text = $"Exported {displayedRows.Count:N0} analytics rows.";
        }

        return root;
    }

    private async Task<UIElement> MassEditPageAsync()
    {
        var root = new StackPanel();
        if (!_editorEnabled)
        {
            var locked = Card(new StackPanel(), 18);
            var ls = (StackPanel)locked.Child;
            ls.Children.Add(Txt("EDITOR SAFETY LOCK", 9, true, Brush("#A06B24")));
            ls.Children.Add(Txt("Mass Edit is OFF", 23, true, Ink));
            ls.Children.Add(Txt("Atlas editing starts disabled every time the program launches. Enable Editor Studio for local edits and the narrow, separately confirmed live player-write path. Current live-write scope is CA, PA and 47 visible standard attributes only; five hidden attributes and all staff, club, contract and structural edits remain blocked. Back up your career and test on an offline copied save before relying on live edits.", 11, false, Muted, wrap: true));
            ls.Children.Add(PrimaryButton("Open Editor Studio", async (_, _) => await NavigateAsync("Live Editor")));
            root.Children.Add(locked);
            return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
        Button applyButton = null!;
        var header = Card(new StackPanel(), 14, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("BULK EDITOR", 9, true, Teal));
        hs.Children.Add(Txt("Mass edit", 23, true, Ink));
        hs.Children.Add(Txt("Apply one attribute value to a filtered set of local Atlas player records. Every changed row is written to Edit History. This does not write to the running FM game.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var form = Card(new StackPanel(), 14, new Thickness(0, 0, 0, 12));
        var body = (StackPanel)form.Child;
        var query = Input("Optional player / club / ID filter"); query.Height = 38;
        var attribute = new ComboBox { Height = 38, IsEditable = true, IsTextSearchEnabled = true, Margin = new Thickness(0, 7, 0, 0) };
        var category = new ComboBox { Height = 36, Margin = new Thickness(0, 7, 0, 0), ItemsSource = new[] { "Technical", "Mental", "Physical", "Goalkeeping", "Set Pieces", "Hidden", "Other" }, SelectedIndex = 0 };
        var operation = new ComboBox { Height = 36, Margin = new Thickness(0, 7, 0, 0), ItemsSource = new[] { "Set", "Add", "Scale" }, SelectedIndex = 0 };
        var value = Input("Value (Set 0–20 / Add points / Scale %)"); value.Height = 38; value.Margin = new Thickness(0, 7, 0, 0);
        var limit = Input("Maximum players (default 5000)"); limit.Text = "5000"; limit.Height = 38; limit.Margin = new Thickness(0, 7, 0, 0);
        body.Children.Add(Txt("FILTER PLAYERS", 9, true, Teal)); body.Children.Add(query);
        body.Children.Add(Txt("ATTRIBUTE", 9, true, Teal)); body.Children.Add(attribute);
        body.Children.Add(Txt("CATEGORY", 9, true, Teal)); body.Children.Add(category);
        body.Children.Add(Txt("OPERATION", 9, true, Teal)); body.Children.Add(operation);
        body.Children.Add(Txt("VALUE", 9, true, Teal)); body.Children.Add(value);
        body.Children.Add(Txt("SAFETY LIMIT", 9, true, Teal)); body.Children.Add(limit);
        var preview = Txt("Loading attribute catalogue…", 10, false, Muted); preview.Margin = new Thickness(0, 10, 0, 0); body.Children.Add(preview);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var previewButton = SoftButton("Preview target count", async (_, _) => await PreviewAsync());
        applyButton = PrimaryButton("Apply local mass edit", async (_, _) => await ApplyAsync());
        buttons.Children.Add(previewButton); buttons.Children.Add(applyButton); body.Children.Add(buttons);
        root.Children.Add(form);
        root.Children.Add(Txt("Safety: Set creates or replaces a value; Add adjusts existing values; Scale applies a percentage. Add/Scale skip players without an existing value. Every changed record is audited and can be undone only if it has not been changed again. Atlas database only.", 10, true, Brush("#94652A"), wrap: true));
        try
        {
            var names = await RunDatabaseAsync(() => _database.GetAttributeNamesAsync());
            attribute.ItemsSource = names;
            if (names.Count > 0) attribute.SelectedIndex = 0;
            preview.Text = $"{names.Count:N0} attribute names currently available in the local database.";
        }
        catch (Exception ex) { preview.Text = "Could not load attribute names: " + ex.Message; }

        int GetLimit() => int.TryParse(limit.Text, out var n) ? Math.Clamp(n, 1, 50000) : 5000;
        async Task PreviewAsync()
        {
            var count = await RunDatabaseAsync(() => _database.CountLocalPlayersAsync(query.Text));
            preview.Text = $"Preview: {count:N0} matching local player record(s). Commit will update at most {Math.Min(count, GetLimit()):N0}.";
        }
        async Task ApplyAsync()
        {
            var field = attribute.SelectedItem?.ToString() ?? attribute.Text.Trim();
            var mode = operation.SelectedItem?.ToString() ?? "Set";
            if (string.IsNullOrWhiteSpace(field) || !double.TryParse(value.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ||
                (mode == "Set" && (v < 0 || v > 20)) || (mode == "Add" && (v < -20 || v > 20)) || (mode == "Scale" && (v < -100 || v > 500)))
            { MessageBox.Show(this, "Set requires 0–20, Add requires -20 to +20, and Scale requires -100% to +500%.", "Mass edit validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var maxRows = GetLimit();
            var answer = MessageBox.Show(this, $"{mode} {v:0.##}{(mode == "Scale" ? "%" : "")} for {field} on up to {maxRows:N0} local player records matching '{query.Text}'?\n\nThis changes FM Atlas's local database only, not Football Manager.", "Confirm mass edit", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            applyButton.IsEnabled = false;
            try
            {
                var changed = await RunDatabaseAsync(() => _database.MassEditLocalAttributeAsync(query.Text, field, category.SelectedItem?.ToString() ?? "Other", v, maxRows, mode));
                AtlasLog.Info($"Mass edit completed: operation={mode}, field={field}, changed={changed}, limit={maxRows}.");
                preview.Text = $"Completed · {changed:N0} local records changed and individually logged. Records without an existing value were skipped for Add/Scale.";
                MessageBox.Show(this, $"Updated {changed:N0} local player records. See Edit History for the individual changes.", "Mass edit complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { AtlasLog.Error("Mass edit operation failed.", ex); preview.Text = "Mass edit failed: " + ex.Message; }
            finally { applyButton.IsEnabled = true; }
        }
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void AddPositionSuitabilityMap(Panel panel, List<AtlasFactRecord> saveFacts, List<(string FieldKey, double? NumericValue, string? TextValue, string ValidationStatus)> liveFacts)
    {
        var sourceValues = new List<KeyValuePair<string, double?>>();
        foreach (var fact in saveFacts)
        {
            if (double.TryParse(fact.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                sourceValues.Add(new KeyValuePair<string, double?>(fact.Field, value));
        }
        // The FM27 entity-array reader's position values are layout-matched but not yet
        // independently witnessed. Display them as provisional source reads, never as verified.
        var livePositions = liveFacts.Where(x => x.NumericValue.HasValue
            && x.FieldKey.StartsWith("positions.", StringComparison.OrdinalIgnoreCase)
            && (x.ValidationStatus.Contains("Verified", StringComparison.OrdinalIgnoreCase)
                || x.ValidationStatus.Contains("Provisional-ProfileMatched", StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var fact in livePositions)
            sourceValues.Add(new KeyValuePair<string, double?>(fact.FieldKey, fact.NumericValue));

        var positions = PositionSuitabilityService.MapSourceRatings(sourceValues);
        var card = Card(new StackPanel(), 13, new Thickness(0, 0, 0, 12));
        var stack = (StackPanel)card.Child;
        stack.Children.Add(Txt("POSITION SUITABILITY MAP", 9, true, Teal));
        stack.Children.Add(Txt("FM position ratings · source 1–20 scale", 17, true, Ink));
        stack.Children.Add(Txt("All 15 position slots are shown. Colours are Atlas display bands over source ratings; zero and missing values remain unrated.", 9, false, Muted, wrap: true));
        if (livePositions.Any(x => x.ValidationStatus.Contains("Provisional-ProfileMatched", StringComparison.OrdinalIgnoreCase)))
            stack.Children.Add(Txt("Includes read-only FM27 live-memory values · provisional layout match · confirm against the in-game Positions screen. These are not verified editor values.", 9, true, Brush("#A06B24"), wrap: true));

        // A pitch-shaped map is easier to scan than a flat 5x3 tile matrix. Position
        // coordinates are presentation-only; the rating and band always come from the
        // source-backed PositionSuitabilityService and never from the tile location.
        var pitch = new Canvas { Width = 640, Height = 340, ClipToBounds = true };
        var pitchBackground = new Border
        {
            CornerRadius = new CornerRadius(14),
            BorderBrush = Brush("#54776B"),
            BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush(Color.FromRgb(25, 65, 52), Color.FromRgb(16, 48, 41), 90),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = pitch
        };
        var fieldOutline = new System.Windows.Shapes.Rectangle
        {
            Width = 620, Height = 320, RadiusX = 8, RadiusY = 8,
            Stroke = Brush("#8BA99A"), StrokeThickness = 1.2,
            Fill = Brushes.Transparent
        };
        System.Windows.Controls.Canvas.SetLeft(fieldOutline, 10);
        System.Windows.Controls.Canvas.SetTop(fieldOutline, 10);
        pitch.Children.Add(fieldOutline);
        pitch.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 320, X2 = 320, Y1 = 10, Y2 = 330,
            Stroke = Brush("#78998B"), StrokeThickness = 1, Opacity = .8
        });
        pitch.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 104, Height = 104, Stroke = Brush("#78998B"),
            StrokeThickness = 1, Fill = Brushes.Transparent
        });
        System.Windows.Controls.Canvas.SetLeft(pitch.Children[^1], 268);
        System.Windows.Controls.Canvas.SetTop(pitch.Children[^1], 118);
        foreach (var (x, y, width, height) in new[]
        {
            (10d, 106d, 74d, 148d), (556d, 106d, 74d, 148d),
            (10d, 142d, 28d, 76d), (602d, 142d, 28d, 76d)
        })
        {
            pitch.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = width, Height = height, Stroke = Brush("#78998B"),
                StrokeThickness = 1, Fill = Brushes.Transparent
            });
            System.Windows.Controls.Canvas.SetLeft(pitch.Children[^1], x);
            System.Windows.Controls.Canvas.SetTop(pitch.Children[^1], y);
        }

        var positionPoints = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase)
        {
            ["GK"] = new Point(48, 170), ["SW"] = new Point(105, 170),
            ["DL"] = new Point(165, 88), ["DC"] = new Point(165, 170), ["DR"] = new Point(165, 252),
            ["WBL"] = new Point(245, 88), ["DM"] = new Point(265, 170), ["WBR"] = new Point(245, 252),
            ["ML"] = new Point(350, 88), ["MC"] = new Point(350, 170), ["MR"] = new Point(350, 252),
            ["AML"] = new Point(455, 88), ["AMC"] = new Point(455, 170), ["AMR"] = new Point(455, 252),
            ["STC"] = new Point(565, 170)
        };
        var pitchTiles = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in positions)
        {
            if (!positionPoints.TryGetValue(position.Code, out var point)) continue;
            var has = position.Rating.HasValue;
            var rating = position.Rating.GetValueOrDefault();
            var tile = new Border
            {
                Width = 62, Height = 48,
                Padding = new Thickness(3),
                CornerRadius = new CornerRadius(8),
                Background = has ? PositionRatingBrush(rating) : Brush("#263D35"),
                BorderBrush = Brush("#91AA9D"),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = true,
                ToolTip = has
                    ? $"{position.Label} · {rating:0.##}/20 · {position.BandLabel} (source rating)"
                    : $"{position.Label} · Unrated (no valid numeric source value)",
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        Txt(position.Code, 11, true, has && rating >= 15 ? Brushes.White : Brush("#F0F5F1")),
                        Txt(has ? $"{rating:0.##}" : "—", 10, true, has && rating >= 15 ? Brushes.White : Brush("#D6E2DA"))
                    }
                }
            };
            System.Windows.Automation.AutomationProperties.SetName(tile,
                has ? $"{position.Label}, rating {rating:0.##} out of 20, {position.BandLabel}" : $"{position.Label}, unrated");
            pitchTiles[position.Code] = tile;
            System.Windows.Controls.Canvas.SetLeft(tile, point.X - tile.Width / 2);
            System.Windows.Controls.Canvas.SetTop(tile, point.Y - tile.Height / 2);
            pitch.Children.Add(tile);
        }

        var selectedPositionDetail = Txt("Select any position to inspect its source rating and suitability band.", 9, false, Muted, wrap: true);
        void SelectPosition(PositionSuitabilityEntry position, Border tile)
        {
            foreach (var candidate in pitchTiles.Values)
            {
                candidate.BorderBrush = Brush("#91AA9D");
                candidate.BorderThickness = new Thickness(1);
            }
            tile.BorderBrush = Brush("#F0A04B");
            tile.BorderThickness = new Thickness(2);
            selectedPositionDetail.Text = position.Rating.HasValue
                ? $"{position.Label} · {position.Rating:0.##}/20 · {position.BandLabel} · source rating; no Atlas-generated score."
                : $"{position.Label} · Unrated · no valid numeric source value was supplied.";
        }

        foreach (var position in positions)
        {
            if (!pitchTiles.TryGetValue(position.Code, out var tile)) continue;
            tile.MouseLeftButtonUp += (_, _) => SelectPosition(position, tile);
            tile.KeyDown += (_, e) =>
            {
                if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space)
                {
                    e.Handled = true;
                    SelectPosition(position, tile);
                }
            };
        }
        var pitchView = new Viewbox { Stretch = Stretch.Uniform, MaxHeight = 340, Child = pitchBackground };
        stack.Children.Add(pitchView);
        stack.Children.Add(selectedPositionDetail);
        var legend = new WrapPanel();
        legend.Children.Add(Txt("1–4 Low · Red",9,true,Brush("#B94A48")));
        legend.Children.Add(Txt("   5–9 Limited · Orange",9,true,Brush("#C47B32")));
        legend.Children.Add(Txt("   10–14 Competent · Yellow",9,true,Brush("#9C8B28")));
        legend.Children.Add(Txt("   15–17 Strong · Green",9,true,Brush("#37875A")));
        legend.Children.Add(Txt("   18–20 Natural · Deep green",9,true,Brush("#176B46")));
        legend.Children.Add(Txt("   0 / missing · Unrated",9,true,Muted));
        stack.Children.Add(legend);
        if (positions.All(x => !x.Rating.HasValue))
            stack.Children.Add(Txt("No valid numeric position ratings were supplied by this player's imported positions object or read-only live scan. Missing values are not interpreted as zero.", 10, false, Muted, wrap: true));
        panel.Children.Add(card);
    }

    private static Brush PositionRatingBrush(double value) => value switch
    {
        < 5 => Brush("#B94A48"), < 10 => Brush("#D18A3A"), < 15 => Brush("#C9B84A"), < 18 => Brush("#4F9B70"), _ => Brush("#176B46")
    };

    private async void ExportDiagnostics()
    {
        try
        {
            var report = await CreateDiagnosticsReportAsync(false);
            var dlg = new SaveFileDialog { Title = "Save redacted FM Atlas 27 diagnostics", FileName = "FM-Atlas-27-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt", Filter = "Text files (*.txt)|*.txt" };
            if (dlg.ShowDialog(this) == true)
            {
                await File.WriteAllTextAsync(dlg.FileName, report, new UTF8Encoding(false));
                AtlasLog.Info("Redacted diagnostics report exported from the toolbar.");
                MessageBox.Show(this, "Redacted diagnostics exported. It excludes player names, save filename and local database path.", "Diagnostics ready", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex) { AtlasLog.Error("Diagnostics export failed.", ex); MessageBox.Show(this, "Diagnostics export failed: " + ex.Message, "FM Atlas 27", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
