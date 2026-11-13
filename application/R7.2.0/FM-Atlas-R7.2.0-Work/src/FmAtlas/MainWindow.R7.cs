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
            ls.Children.Add(Txt("Atlas editing starts disabled every time the program launches. Enable Editor Studio for local FM Atlas database edits only. Live Football Manager Apply and Undo are disabled by the build-wide certification gate; no in-game values can be written in this build. The source contains experimental guarded CA/PA and standard-attribute code, but it is not certified or available.", 11, false, Muted, wrap: true));
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
        var sourceValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var fact in saveFacts)
        {
            if (double.TryParse(fact.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                && double.IsFinite(value) && value >= 0 && value <= 20)
                sourceValues[fact.Field] = value;
        }

        // Live values are shown only when the field itself has a canonical UI-witnessed status.
        foreach (var fact in liveFacts.Where(x => x.NumericValue.HasValue && FieldEvidencePolicy.CanUseForDecision(x.ValidationStatus)))
        {
            var key = fact.FieldKey.Split('.').Last().Trim();
            var value = fact.NumericValue!.Value;
            if (double.IsFinite(value) && value >= 0 && value <= 20)
                sourceValues[key] = value;
        }

        var positions = PositionSuitabilityService.Build(sourceValues);
        var card = Card(new StackPanel(), 13, new Thickness(0, 0, 0, 12));
        var stack = (StackPanel)card.Child;
        stack.Children.Add(Txt("POSITION SUITABILITY MAP", 9, true, Teal));
        stack.Children.Add(Txt("15 FM position ratings · 0–20", 17, true, Ink));
        var compact = new UniformGrid { Columns = 5, Rows = 3, Margin = new Thickness(0, 5, 0, 5) };
        foreach (var position in positions)
        {
            var has = position.Rating.HasValue;
            var rating = position.Rating ?? 0;
            var label = has ? $"{position.Key.ToUpperInvariant()}\\n{rating:0}" : $"{position.Key.ToUpperInvariant()}\\n—";
            var tile = new Border
            {
                Margin = new Thickness(3),
                Padding = new Thickness(5),
                CornerRadius = new CornerRadius(7),
                Background = has ? PositionRatingBrush(rating) : Brush("#E8EEF2"),
                BorderBrush = Brush("#D5E0E7"),
                BorderThickness = new Thickness(1),
                ToolTip = has
                    ? $"{position.Label}: source rating {rating:0.##}/20 · {position.BandLabel}"
                    : $"{position.Label}: no position rating supplied by this source",
                Child = Txt(label, 11, true, has && rating >= 15 ? Brushes.White : Ink)
            };
            compact.Children.Add(tile);
        }
        stack.Children.Add(compact);

        var legend = new WrapPanel();
        legend.Children.Add(Txt("0–4 Low", 9, true, Brush("#B94A48")));
        legend.Children.Add(Txt("   5–9 Limited", 9, true, Brush("#C47B32")));
        legend.Children.Add(Txt("   10–14 Developing", 9, true, Brush("#9C8B28")));
        legend.Children.Add(Txt("   15–17 Accomplished", 9, true, Brush("#37875A")));
        legend.Children.Add(Txt("   18–20 Natural", 9, true, Brush("#176B46")));
        stack.Children.Add(legend);
        stack.Children.Add(Txt(
            "Data note: the 15 values are the source's position ratings, not position-mask bits. Natural (18+) and Accomplished (15–17) follow fmsave's documented classifier. The lower three colour bands are Atlas visual groupings, not Football Manager labels. Missing ratings remain blank.",
            9, false, Muted, wrap: true));

        if (positions.All(x => !x.Rating.HasValue))
            stack.Children.Add(Txt(
                "No usable position ratings were found. The profile will not infer familiarity from the player's listed position or attribute scores.",
                10, false, Muted, wrap: true));

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
