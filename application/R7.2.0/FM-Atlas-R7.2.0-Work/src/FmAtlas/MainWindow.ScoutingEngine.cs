using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.IO.Path;
using WpfCanvas = System.Windows.Controls.Canvas;
using Microsoft.Win32;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private sealed record ScoutingGridRow(
        string Player, string Club, string Position, string Competition, string Season,
        double Minutes, double MetricValue, string DisplayMetric, double? Percentile,
        string Source, string SourceType, string SourceId, ScoutingPlayerSeason Record);
    private sealed record MetricChoice(string Group, ScoutingMetricDefinition Definition)
    {
        public string Label => Group + " · " + Definition.Label;
        public override string ToString() => Label;
    }

    private static string ScoutingDatasetPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FmAtlas", "ScoutingDatasets", "player-seasons.json");

    private async Task<UIElement> ScoutingEnginePageAsync()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var dataset = await LoadScoutingDatasetAsync();
        var allRows = dataset.ToList();
        var results = new List<ScoutingGridRow>();

        var intro = new StackPanel();
        intro.Children.Add(Txt("RECRUITMENT INTELLIGENCE · EVENT DATA + FM SAVE DATA", 9, true, Teal));
        intro.Children.Add(Txt("Scouting analytics", 25, true, Ink));
        intro.Children.Add(Txt("A Wyscout / StatsBomb-inspired recruitment workspace. Import genuine event data or a user-owned Wyscout CSV, compare players within like-for-like cohorts, and inspect the source behind every metric. FM save statistics remain a separate source family.", 10, false, Muted, wrap: true));
        intro.Children.Add(Txt("StatsBomb Open Data attribution is required when publishing or sharing analysis from that dataset. Wyscout imports use your own authorised CSV exports.", 8, false, Muted, wrap: true));
        var importBar = new WrapPanel { Margin = new Thickness(0, 10, 0, 4) };
        TextBlock status = null!;
        CancellationTokenSource? activeScoutingImport = null;
        Button importStatsBomb = null!;
        Button importWyscout = null!;
        Button reload = null!;
        Button fmMatrix = null!;
        Button cancelScoutingImport = null!;
        Button find = null!;
        Button similar = null!;
        Button undervalued = null!;
        Button export = null!;
        Button eventLab = null!;
        intro.Children.Add(importBar);
        status = Txt($"{allRows.Count:N0} player-season records loaded · imports are stored locally on this PC.", 9, false, Muted, wrap: true);
        status.Margin = new Thickness(0, 4, 0, 2);
        intro.Children.Add(status);
        var introCard = Card(intro, 15, new Thickness(0, 0, 0, 10));
        Grid.SetRow(introCard, 0); root.Children.Add(introCard);

        var filterCard = Card(new StackPanel(), 13, new Thickness(0, 0, 0, 10));
        var filterStack = (StackPanel)filterCard.Child;
        var filters = new WrapPanel();
        var sourcePicker = new ComboBox { Width = 155, Height = 34, Margin = new Thickness(3), ToolTip = "Never compare percentiles across different provider definitions." };
        var competition = new ComboBox { Width = 180, Height = 34, Margin = new Thickness(3), IsEditable = true, ToolTip = "Competition cohort" };
        var season = new ComboBox { Width = 140, Height = 34, Margin = new Thickness(3), IsEditable = true, ToolTip = "Season cohort" };
        var position = new ComboBox { Width = 130, Height = 34, Margin = new Thickness(3), ItemsSource = new[] { "All positions", "Goalkeeper", "Defender", "Midfielder", "Attacker" }, SelectedIndex = 0 };
        var roleProfile = new ComboBox { Width = 190, Height = 34, Margin = new Thickness(3), IsTextSearchEnabled = true, DisplayMemberPath = nameof(ScoutingRoleProfile.Name) };
        roleProfile.Items.Add(new ScoutingRoleProfile("All roles", "", Array.Empty<string>()));
        foreach (var profile in ScoutingRecruitmentService.RoleProfiles) roleProfile.Items.Add(profile);
        roleProfile.SelectedIndex = 0;
        var metric = new ComboBox { Width = 270, Height = 34, Margin = new Thickness(3), IsTextSearchEnabled = true };
        var minimumMinutes = Input("Min minutes"); minimumMinutes.Text = "450"; minimumMinutes.Width = 95;
        var maxValue = Input("Max market value"); maxValue.Width = 125;
        var minAge = Input("Min age"); minAge.Text = "16"; minAge.Width = 75;
        var maxAge = Input("Max age"); maxAge.Text = "34"; maxAge.Width = 75;
        filters.Children.Add(FilterControl("DATA SOURCE", sourcePicker, 165));
        filters.Children.Add(FilterControl("ROLE PROFILE", roleProfile, 200));
        filters.Children.Add(FilterControl("COMPETITION", competition, 190));
        filters.Children.Add(FilterControl("SEASON", season, 150));
        filters.Children.Add(FilterControl("POSITION GROUP", position, 140));
        filters.Children.Add(FilterControl("PERFORMANCE METRIC", metric, 280));
        filters.Children.Add(FilterControl("MIN SAMPLE", minimumMinutes, 105));
        filters.Children.Add(FilterControl("MAX VALUE · SOURCE UNITS", maxValue, 135));
        filters.Children.Add(FilterControl("AGE RANGE", minAge, 80));
        filters.Children.Add(FilterControl("TO", maxAge, 80));
        filterStack.Children.Add(filters);
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        filterStack.Children.Add(actions);
        Grid.SetRow(filterCard, 1); root.Children.Add(filterCard);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.55, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var grid = CreateGrid();
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.Columns.Add(Column("PLAYER", nameof(ScoutingGridRow.Player), 1.15));
        grid.Columns.Add(Column("CLUB", nameof(ScoutingGridRow.Club), .9));
        grid.Columns.Add(Column("POS", nameof(ScoutingGridRow.Position), .55));
        grid.Columns.Add(Column("MIN", nameof(ScoutingGridRow.Minutes), .45));
        grid.Columns.Add(Column("METRIC", nameof(ScoutingGridRow.DisplayMetric), .65));
        grid.Columns.Add(Column("PERCENTILE", nameof(ScoutingGridRow.Percentile), .7));
        grid.Columns.Add(Column("SOURCE", nameof(ScoutingGridRow.SourceType), .7));
        var left = Card(grid, 12, new Thickness(0, 0, 7, 0));
        Grid.SetColumn(left, 0); body.Children.Add(left);

        var detailStack = new StackPanel();
        detailStack.Children.Add(Txt("PLAYER COMPARISON & EVIDENCE", 9, true, Teal));
        detailStack.Children.Add(Txt("Select one player for similar-player discovery, or two players to compare their source-backed metric profiles.", 10, false, Muted, wrap: true));
        var comparison = Txt("No players selected.", 10, false, Ink, wrap: true);
        comparison.Margin = new Thickness(0, 10, 0, 10);
        detailStack.Children.Add(comparison);
        var radar = new Canvas { Width = 260, Height = 230, Margin = new Thickness(0, 6, 0, 6) };
        detailStack.Children.Add(radar);
        var evidence = Txt("Metric definitions, sample rules and data provenance appear here.", 9, false, Muted, wrap: true);
        detailStack.Children.Add(evidence);
        var right = Card(new ScrollViewer { Content = detailStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 14, new Thickness(7, 0, 0, 0));
        Grid.SetColumn(right, 1); body.Children.Add(right);
        Grid.SetRow(body, 2); root.Children.Add(body);

        void RefreshChoices()
        {
            var oldComp = competition.Text; var oldSeason = season.Text; var oldMetric = (metric.SelectedItem as MetricChoice)?.Definition.Key; var oldSource = sourcePicker.SelectedItem?.ToString();
            sourcePicker.Items.Clear(); sourcePicker.Items.Add("All sources");
            foreach (var x in allRows.Select(x => x.SourceName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)) sourcePicker.Items.Add(x);
            sourcePicker.SelectedItem = oldSource is not null && sourcePicker.Items.Contains(oldSource) ? oldSource : "All sources";
            competition.Items.Clear(); competition.Items.Add("All competitions");
            foreach (var x in allRows.Select(x => x.Competition).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
                competition.Items.Add(x);
            competition.Text = string.IsNullOrWhiteSpace(oldComp) ? "All competitions" : oldComp;
            season.Items.Clear(); season.Items.Add("All seasons");
            foreach (var x in allRows.Select(x => x.Season).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x))
                season.Items.Add(x);
            season.Text = string.IsNullOrWhiteSpace(oldSeason) ? "All seasons" : oldSeason;

            var choices = new List<MetricChoice>();
            foreach (var group in ScoutingRecruitmentService.MetricGroups)
                foreach (var def in group.Metrics)
                    if (allRows.Any(p => p.Metrics.ContainsKey(def.Key))) choices.Add(new MetricChoice(group.Name, def));
            foreach (var key in allRows.SelectMany(p => p.Metrics.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            {
                if (choices.Any(x => x.Definition.Key.Equals(key, StringComparison.OrdinalIgnoreCase))) continue;
                if (new[] { "age", "market_value", "minutes_played" }.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;
                var label = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' '));
                var isAlreadyPer90 = key.EndsWith("_90", StringComparison.OrdinalIgnoreCase) || key.Contains("per_90", StringComparison.OrdinalIgnoreCase);
                choices.Add(new MetricChoice("Imported source metrics", new ScoutingMetricDefinition(key, label, isAlreadyPer90 ? "per 90" : "source units",
                    !isAlreadyPer90, true, 90, "Imported provider CSV")));
            }
            metric.ItemsSource = choices;
            metric.SelectedItem = choices.FirstOrDefault(x => x.Definition.Key.Equals(oldMetric, StringComparison.OrdinalIgnoreCase)) ?? choices.FirstOrDefault();
        }

        (string? Competition, string? Season, string? Position, string? Source) SelectedCohort()
        {
            var comp = competition.Text.Trim();
            var sea = season.Text.Trim();
            var selectedRole = roleProfile.SelectedItem as ScoutingRoleProfile;
            var pos = selectedRole is { Name: not "All roles" } ? selectedRole.PositionGroup : position.SelectedItem?.ToString();
            var source = sourcePicker.SelectedItem?.ToString();
            return (comp.Equals("All competitions", StringComparison.OrdinalIgnoreCase) || comp.Length == 0 ? null : comp,
                sea.Equals("All seasons", StringComparison.OrdinalIgnoreCase) || sea.Length == 0 ? null : sea,
                pos == "All positions" ? null : pos,
                source == "All sources" || string.IsNullOrWhiteSpace(source) ? null : source);
        }

        async Task RunRankAsync()
        {
            if (metric.SelectedItem is not MetricChoice choice) { status.Text = "Import a dataset containing performance metrics first."; return; }
            var cohort = SelectedCohort();
            if (!int.TryParse(minimumMinutes.Text, out var floor) || floor < 0) { status.Text = "Minimum minutes must be a non-negative whole number."; return; }
            var def = choice.Definition with { MinimumSampleMinutes = Math.Max(floor, choice.Definition.MinimumSampleMinutes) };
            var rows = allRows.Where(p => (cohort.Competition is null || p.Competition == cohort.Competition) &&
                (cohort.Season is null || p.Season == cohort.Season) &&
                (cohort.Position is null || p.PositionGroup == cohort.Position) &&
                (cohort.Source is null || p.SourceName == cohort.Source)).ToList();
            var ranked = rows.GroupBy(p => p.SourceName).SelectMany(group => ScoutingAnalyticsService.RankMetric(group, def, cohort.Competition, cohort.Season, cohort.Position, 5)).ToList();
            results = ranked.Select(r =>
            {
                var p = rows.First(x => x.SourceId == r.SourceId);
                var value = r.Per90Value ?? r.RawValue;
                return new ScoutingGridRow(r.PlayerName, r.Club ?? "—", r.PositionGroup ?? "—", r.Competition ?? "—", r.Season,
                    p.MinutesPlayed ?? 0, value, value.ToString("0.00", CultureInfo.InvariantCulture), r.Percentile,
                    r.SourceName, p.IsEventLevel ? "Event data" : "Season stats", r.SourceId, p);
            }).ToList();
            grid.ItemsSource = results;
            status.Text = $"{results.Count:N0} players ranked · {choice.Label} · {cohort.Source ?? "each source ranked separately"} · {cohort.Competition ?? "all competitions"} · {cohort.Season ?? "all seasons"}. Percentiles are withheld below a five-player cohort; source and minutes are retained.";
            evidence.Text = $"Metric: {choice.Definition.Label}\nDefinition: {choice.Definition.SourceFamily}; {(choice.Definition.IsRatePer90 ? "normalised per 90 using recorded minutes" : "reported in source units")}\nMinimum sample: {def.MinimumSampleMinutes:0} minutes\nCohort: {cohort.Competition ?? "all competitions"} / {cohort.Season ?? "all seasons"} / {cohort.Position ?? "all positions"}\nSource: each player's import provenance.";
            await Task.CompletedTask;
        }

        async Task RunSimilarAsync()
        {
            if (grid.SelectedItem is not ScoutingGridRow selected || metric.SelectedItem is not MetricChoice choice)
            { status.Text = "Select a player in the results first."; return; }
            var floor = int.TryParse(minimumMinutes.Text, out var parsedFloor) ? Math.Max(0, parsedFloor) : 450;
            var metrics = metric.Items.Cast<MetricChoice>().Select(x => x.Definition with
                { MinimumSampleMinutes = Math.Max(x.Definition.MinimumSampleMinutes, floor) })
                .Where(m => !new[] { "age", "market_value", "minutes_played" }.Contains(m.Key, StringComparer.OrdinalIgnoreCase))
                .ToList();
            var candidates = allRows.Where(p => p.SourceName == selected.Record.SourceName && p.Competition == selected.Record.Competition && p.Season == selected.Record.Season && p.PositionGroup == selected.Record.PositionGroup).ToList();
            var similarRows = ScoutingRecruitmentService.FindSimilar(selected.Record, candidates, metrics, 50, .35);
            results = similarRows.Select(x => new ScoutingGridRow(x.Player.PlayerName, x.Player.Club ?? "—",
                x.Player.PositionGroup ?? "—", x.Player.Competition ?? "—", x.Player.Season, x.Player.MinutesPlayed ?? 0,
                x.Similarity, x.Similarity.ToString("0.0") + "% similar", x.Similarity, x.Player.SourceName,
                x.Player.IsEventLevel ? "Event data" : "Season stats", x.Player.SourceId, x.Player)).ToList();
            grid.ItemsSource = results;
            status.Text = $"Similar players to {selected.Player}: {results.Count:N0} matches. Similarity is an Atlas distance score over shared normalised metrics, not a vendor rating.";
            evidence.Text = $"Reference: {selected.Player}\nMetrics compared: {metrics.Count}\nCohort: same competition, season and position group\nSimilarity: Euclidean distance over shared normalised metrics; candidates require at least 35% metric coverage.";
            await Task.CompletedTask;
        }

        async Task RunUndervaluedAsync()
        {
            if (metric.SelectedItem is not MetricChoice choice) { status.Text = "Choose a performance metric first."; return; }
            if (!double.TryParse(maxValue.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var limit)) limit = double.PositiveInfinity;
            if (!int.TryParse(minAge.Text, out var lowAge) || !int.TryParse(maxAge.Text, out var highAge) || lowAge > highAge)
            { status.Text = "Enter a valid age range."; return; }
            var cohort = SelectedCohort();
            var def = choice.Definition with { MinimumSampleMinutes = Math.Max(450, choice.Definition.MinimumSampleMinutes) };
            var sourceRows = allRows.Where(p => cohort.Source is null || p.SourceName == cohort.Source).ToList();
            var found = sourceRows.GroupBy(p => p.SourceName).SelectMany(group =>
                ScoutingRecruitmentService.FindUndervalued(group, def, cohort.Competition, cohort.Season,
                    cohort.Position, limit, Math.Max(450, int.TryParse(minimumMinutes.Text, out var floor) ? floor : 450), 5, lowAge, highAge)).ToList();
            results = found.Select(x => new ScoutingGridRow(x.Player.PlayerName, x.Player.Club ?? "—",
                x.Player.PositionGroup ?? "—", x.Player.Competition ?? "—", x.Player.Season, x.Player.MinutesPlayed ?? 0,
                x.CompositeScore, x.CompositeScore.ToString("0.0") + " / 100", x.PerformancePercentile,
                x.Player.SourceName, x.Player.IsEventLevel ? "Event data" : "Season stats", x.Player.SourceId, x.Player)).ToList();
            grid.ItemsSource = results;
            status.Text = $"{results.Count:N0} undervalued candidates. This is a transparent discovery score, not a predicted transfer fee or guarantee of availability.";
            evidence.Text = "Discovery score: performance percentile 65% + age score 20% + relative value score 15%. Requires age, market value, minutes and performance data in the same source. Market value remains in the provider's own units.";
            await Task.CompletedTask;
        }

        void UpdateRadar(ScoutingPlayerSeason a, ScoutingPlayerSeason b)
        {
            radar.Children.Clear();
            if (a.SourceName != b.SourceName || a.Competition != b.Competition || a.Season != b.Season || a.PositionGroup != b.PositionGroup)
            {
                radar.Children.Add(Txt("Radar requires the same provider, competition, season and position cohort.", 9, false, Muted, wrap: true));
                return;
            }
            var cohort = allRows.Where(p => p.SourceName == a.SourceName && p.Competition == a.Competition &&
                p.Season == a.Season && p.PositionGroup == a.PositionGroup).ToList();
            var rankedMetrics = metric.Items.Cast<MetricChoice>().Select(x => x.Definition)
                .Where(m => !new[] { "age", "market_value", "minutes_played" }.Contains(m.Key, StringComparer.OrdinalIgnoreCase))
                .Where(m => a.Metrics.ContainsKey(m.Key) && b.Metrics.ContainsKey(m.Key))
                .Select(m => m with { MinimumSampleMinutes = Math.Max(m.MinimumSampleMinutes, int.TryParse(minimumMinutes.Text, out var floor) ? floor : 450) })
                .Select(m => (Definition: m, Ranked: ScoutingAnalyticsService.RankMetric(cohort, m, a.Competition, a.Season, a.PositionGroup, 3)))
                .Where(x => x.Ranked.Count >= 3 && x.Ranked.Any(r => r.SourceId == a.SourceId && r.Percentile.HasValue) && x.Ranked.Any(r => r.SourceId == b.SourceId && r.Percentile.HasValue))
                .Take(6).ToList();
            if (rankedMetrics.Count < 3)
            {
                radar.Children.Add(Txt("Not enough shared, sample-qualified metrics for a cohort radar.", 9, false, Muted, wrap: true));
                return;
            }
            const double cx = 118, cy = 112, radius = 76;
            for (var ring = 1; ring <= 4; ring++)
            {
                var r = radius * ring / 4d;
                var points = new PointCollection();
                for (var i = 0; i < rankedMetrics.Count; i++)
                {
                    var angle = -Math.PI / 2 + 2 * Math.PI * i / rankedMetrics.Count;
                    points.Add(new Point(cx + Math.Cos(angle) * r, cy + Math.Sin(angle) * r));
                }
                radar.Children.Add(new Polygon { Points = points, Stroke = Brush("#3B4652"), StrokeThickness = 1, Fill = Brushes.Transparent });
            }
            PointCollection BuildPoints(ScoutingPlayerSeason player)
            {
                var points = new PointCollection();
                foreach (var item in rankedMetrics)
                {
                    var percentile = item.Ranked.FirstOrDefault(x => x.SourceId == player.SourceId)?.Percentile ?? 0;
                    var angle = -Math.PI / 2 + 2 * Math.PI * points.Count / rankedMetrics.Count;
                    points.Add(new Point(cx + Math.Cos(angle) * radius * percentile / 100d,
                        cy + Math.Sin(angle) * radius * percentile / 100d));
                }
                return points;
            }
            radar.Children.Add(new Polygon { Points = BuildPoints(a), Stroke = Brush("#F08A42"), StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(45, 240, 138, 66)) });
            radar.Children.Add(new Polygon { Points = BuildPoints(b), Stroke = Brush("#63A9E8"), StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(35, 99, 169, 232)) });
            for (var i = 0; i < rankedMetrics.Count; i++)
            {
                var angle = -Math.PI / 2 + 2 * Math.PI * i / rankedMetrics.Count;
                var label = Txt(rankedMetrics[i].Definition.Label.Replace(" / 90", ""), 8, false, Muted);
                WpfCanvas.SetLeft(label, cx + Math.Cos(angle) * (radius + 8) - 24);
                WpfCanvas.SetTop(label, cy + Math.Sin(angle) * (radius + 8) - 7);
                radar.Children.Add(label);
            }
        }

        void UpdateComparison()
        {
            var selected = grid.SelectedItems.Cast<ScoutingGridRow>().Take(2).ToList();
            if (selected.Count == 0) { radar.Children.Clear(); comparison.Text = "No players selected."; return; }
            if (selected.Count == 1)
            {
                radar.Children.Clear();
                var p = selected[0].Record;
                comparison.Text = $"{p.PlayerName}\n{p.Club} · {p.PositionGroup} · {p.Competition} · {p.Season}\nMinutes: {p.MinutesPlayed:0}\nSource: {p.SourceName}";
                evidence.Text = "Select a second player to compare shared metric values, or use Find similar to selected.";
                return;
            }
            var a = selected[0].Record; var b = selected[1].Record;
            UpdateRadar(a, b);
            var keys = a.Metrics.Keys.Intersect(b.Metrics.Keys, StringComparer.OrdinalIgnoreCase).Take(14).ToList();
            var lines = keys.Select(k => $"{k.Replace('_',' ')}: {a.Metrics[k]?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—"}  |  {b.Metrics[k]?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—"}");
            comparison.Text = $"{a.PlayerName}  vs  {b.PlayerName}\n" + string.Join("\n", lines);
            evidence.Text = $"Comparison shows {keys.Count} shared raw source metrics. Values are not silently converted between different providers or metric definitions. Select a like-for-like competition and season for percentile comparison.";
        }

        async Task ImportStatsBombAsync()
        {
            var dialog = new OpenFileDialog { Title = "Select StatsBomb Open Data competitions.json", Filter = "StatsBomb competitions.json|competitions.json|JSON files (*.json)|*.json", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                importStatsBomb.IsEnabled = false; importWyscout.IsEnabled = false; cancelScoutingImport.IsEnabled = true;
                using var cancellation = new CancellationTokenSource();
                activeScoutingImport = cancellation;
                status.Text = "Reading StatsBomb matches, lineups and event files…";
                var progress = new Progress<string>(x => status.Text = x);
                var imported = await Task.Run(() => StatsBombOpenDataImporter.ImportAsync(dialog.FileName, cancellation.Token, progress));
                allRows.RemoveAll(x => x.SourceName == "StatsBomb Open Data");
                allRows.AddRange(imported.Players);
                await SaveScoutingDatasetAsync(allRows);
                RefreshChoices();
                status.Text = $"StatsBomb import complete · {imported.Summary.MatchesImported:N0} matches · {imported.Summary.EventsRead:N0} events · {imported.Players.Count:N0} player/competition/season records. {string.Join(" ", imported.Summary.Warnings.Take(3))}";
            }
            catch (Exception ex) { AtlasLog.Error("StatsBomb Open Data import failed.", ex); status.Text = "StatsBomb import failed: " + ex.Message; }
            finally { activeScoutingImport = null; cancelScoutingImport.IsEnabled = false; importStatsBomb.IsEnabled = true; importWyscout.IsEnabled = true; }
        }

        async Task ImportWyscoutAsync()
        {
            var dialog = new OpenFileDialog { Title = "Select a Wyscout player statistics CSV export", Filter = "CSV files (*.csv)|*.csv", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                importStatsBomb.IsEnabled = false; importWyscout.IsEnabled = false; cancelScoutingImport.IsEnabled = true;
                using var cancellation = new CancellationTokenSource();
                activeScoutingImport = cancellation;
                status.Text = "Importing user-owned Wyscout season statistics…";
                var imported = await Task.Run(() => WyscoutCsvImporter.ImportAsync(dialog.FileName, cancellation.Token));
                allRows.RemoveAll(x => x.SourceName == "Wyscout user CSV");
                allRows.AddRange(imported);
                await SaveScoutingDatasetAsync(allRows);
                RefreshChoices();
                status.Text = $"Wyscout CSV import complete · {imported.Count:N0} player-season records. Provider fields and source units are preserved.";
            }
            catch (Exception ex) { AtlasLog.Error("Wyscout CSV import failed.", ex); status.Text = "Wyscout CSV import failed: " + ex.Message; }
            finally { activeScoutingImport = null; cancelScoutingImport.IsEnabled = false; importStatsBomb.IsEnabled = true; importWyscout.IsEnabled = true; }
        }


        async Task RunEventLabAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select StatsBomb Open Data competitions.json",
                Filter = "StatsBomb competitions.json|competitions.json|JSON files (*.json)|*.json",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                eventLab.IsEnabled = false; importStatsBomb.IsEnabled = false; importWyscout.IsEnabled = false;
                cancelScoutingImport.IsEnabled = true;
                using var cancellation = new CancellationTokenSource();
                activeScoutingImport = cancellation;
                status.Text = "Building unified actions and fitting the empirical xT grid…";
                var progress = new Progress<string>(message => status.Text = message);
                var actions = await Task.Run(() => ScoutingActionAnalyticsService.ImportStatsBombActionsAsync(
                    dialog.FileName, cancellation.Token, progress));
                cancellation.Token.ThrowIfCancellationRequested();
                if (actions.Count == 0) throw new InvalidDataException("No supported event actions were found in this StatsBomb dataset.");
                if (actions.Select(x => x.MatchId).Distinct().Count() < 1)
                    throw new InvalidDataException("No match-level training sample was available for xT.");
                var model = await Task.Run(() => ScoutingActionAnalyticsService.FitExpectedThreat(actions, 1, 100), cancellation.Token);
                var valued = ScoutingActionAnalyticsService.ValueActions(actions, model);
                var chains = ScoutingActionAnalyticsService.BuildPossessionChains(valued);
                var players = ScoutingActionAnalyticsService.AggregatePlayers(valued);
                status.Text = $"Event Lab ready · {model.TrainingMatches:N0} matches · {actions.Count:N0} unified actions · {chains.Count:N0} possession chains · empirical {model.Columns}×{model.Rows} xT grid.";
                var tabs = new TabControl { Margin = new Thickness(12) };
                var playerGrid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, CanUserAddRows = false, ItemsSource = players, FrozenColumnCount = 2 };
                var chainGrid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, CanUserAddRows = false, ItemsSource = chains, FrozenColumnCount = 2 };
                var actionGrid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, CanUserAddRows = false, ItemsSource = valued.Take(5000), FrozenColumnCount = 3 };
                tabs.Items.Add(new TabItem { Header = "Player impact", Content = playerGrid });
                tabs.Items.Add(new TabItem { Header = "Possession chains", Content = chainGrid });
                tabs.Items.Add(new TabItem { Header = "Unified actions (first 5,000)", Content = actionGrid });
                var panel = new DockPanel();
                var note = Txt($"ATLAS EMPIRICAL xT · {model.Columns} × {model.Rows} pitch grid\nTrained from {model.TrainingMatches:N0} matches and {model.TrainingActions:N0} event actions in this import. xT is the change in fitted zone threat for successful passes/carries; it does not value shots, failed actions or defensive actions. Model is dataset-specific and should be trained on a representative sample before cross-league comparisons.", 10, false, Muted, wrap: true);
                note.Margin = new Thickness(16, 12, 16, 4);
                DockPanel.SetDock(note, Dock.Top); panel.Children.Add(note); panel.Children.Add(tabs);
                var window = new Window
                {
                    Title = "FM Atlas · Event Lab",
                    Width = 1280, Height = 820, MinWidth = 850, MinHeight = 560,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this, Background = Panel, Foreground = Ink, Content = panel
                };
                window.ShowDialog();
            }
            catch (OperationCanceledException) { status.Text = "Event Lab import cancelled. Existing scouting datasets were not changed."; }
            catch (Exception ex) { AtlasLog.Error("Event Lab analysis failed.", ex); status.Text = "Event Lab failed: " + ex.Message; }
            finally
            {
                activeScoutingImport = null;
                cancelScoutingImport.IsEnabled = false;
                eventLab.IsEnabled = true; importStatsBomb.IsEnabled = true; importWyscout.IsEnabled = true;
            }
        }

        void ExportScoutingRows()
        {
            if (results.Count == 0) { MessageBox.Show(this, "Run a scouting search before exporting.", "FM Atlas · Scouting Analytics"); return; }
            var dialog = new SaveFileDialog { Title = "Export scouting analytics", FileName = "FM-Atlas-scouting-analytics.csv", Filter = "CSV files (*.csv)|*.csv" };
            if (dialog.ShowDialog(this) != true) return;
            static string Q(string? s) => CsvExportSafety.QuoteText(s);
            var lines = new List<string> { "Player,Club,Position,Competition,Season,Minutes,MetricValue,DisplayMetric,Percentile,Source,SourceType,SourceId" };
            lines.AddRange(results.Select(x => string.Join(",", new[]
            {
                Q(x.Player),Q(x.Club),Q(x.Position),Q(x.Competition),Q(x.Season),
                Q(x.Minutes.ToString(CultureInfo.InvariantCulture)),Q(x.MetricValue.ToString(CultureInfo.InvariantCulture)),
                Q(x.DisplayMetric),Q(x.Percentile?.ToString(CultureInfo.InvariantCulture)),Q(x.Source),Q(x.SourceType),Q(x.SourceId)
            })));
            File.WriteAllLines(dialog.FileName, lines, new System.Text.UTF8Encoding(true));
            status.Text = $"Exported {results.Count:N0} scouting results.";
        }

        importStatsBomb = PrimaryButton("Import StatsBomb Open Data", async (_, _) => await ImportStatsBombAsync());
        importWyscout = SoftButton("Import Wyscout CSV", async (_, _) => await ImportWyscoutAsync());
        reload = SoftButton("Reload saved datasets", async (_, _) => { allRows = (await LoadScoutingDatasetAsync()).ToList(); RefreshChoices(); status.Text = $"Loaded {allRows.Count:N0} player-season records from local datasets."; });
        fmMatrix = SoftButton("Open FM Scouting Matrix", async (_, _) => await NavigateAsync("Scouting Matrix"));
        cancelScoutingImport = SoftButton("Cancel import", (_, _) => activeScoutingImport?.Cancel());
        cancelScoutingImport.IsEnabled = false;
        eventLab = SoftButton("Open Event Lab · xT / Possessions", async (_, _) => await RunEventLabAsync());
        importBar.Children.Add(eventLab);
        importBar.Children.Add(importStatsBomb); importBar.Children.Add(importWyscout); importBar.Children.Add(reload);
        importBar.Children.Add(fmMatrix); importBar.Children.Add(cancelScoutingImport);

        find = PrimaryButton("Rank players", async (_, _) => await RunRankAsync());
        similar = SoftButton("Find similar to selected", async (_, _) => await RunSimilarAsync());
        undervalued = SoftButton("Find undervalued", async (_, _) => await RunUndervaluedAsync());
        export = SoftButton("Export results CSV", (_, _) => ExportScoutingRows());
        actions.Children.Add(find); actions.Children.Add(similar); actions.Children.Add(undervalued); actions.Children.Add(export);
        grid.SelectionChanged += (_, _) => UpdateComparison();

        roleProfile.SelectionChanged += (_, _) =>
        {
            if (roleProfile.SelectedItem is not ScoutingRoleProfile selectedRole || selectedRole.Name == "All roles") return;
            position.SelectedItem = selectedRole.PositionGroup;
            var preferred = selectedRole.PreferredMetricKeys
                .Select(key => metric.Items.Cast<MetricChoice>().FirstOrDefault(x => x.Definition.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(x => x is not null);
            if (preferred is not null) metric.SelectedItem = preferred;
        };
        RefreshChoices();
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static async Task<List<ScoutingPlayerSeason>> LoadScoutingDatasetAsync()
    {
        if (!File.Exists(ScoutingDatasetPath)) return new List<ScoutingPlayerSeason>();
        try
        {
            await using var stream = File.OpenRead(ScoutingDatasetPath);
            return await JsonSerializer.DeserializeAsync<List<ScoutingPlayerSeason>>(stream) ?? new List<ScoutingPlayerSeason>();
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Scouting dataset cache could not be read; source imports are left untouched.", ex);
            return new List<ScoutingPlayerSeason>();
        }
    }

    private static async Task SaveScoutingDatasetAsync(IEnumerable<ScoutingPlayerSeason> rows)
    {
        var path = ScoutingDatasetPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, rows.ToList(), new JsonSerializerOptions { WriteIndented = false });
        File.Move(temporary, path, true);
    }
}
