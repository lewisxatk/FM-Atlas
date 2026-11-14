using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using FmAtlas.Data;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas;

public sealed class PerformanceValueAnalysisWindow : Window
{
    private readonly ScoutDatabase _database;
    private readonly IReadOnlyList<StatMetricField> _metrics;
    private readonly ComboBox _metricPicker;
    private readonly CheckBox _per90;
    private readonly TextBox _minimumMinutes;
    private readonly TextBox _minimumSampleSize;
    private readonly ComboBox _positionFilter;
    private readonly ComboBox _leagueFilter;
    private readonly ComboBox _positionRatingCode;
    private readonly TextBox _minimumPositionRating;
    private readonly Canvas _chart;
    private readonly DataGrid _grid;
    private readonly TextBlock _status;
    private List<PerformanceValueCandidate> _allResults = new();
    private List<PerformanceValueCandidate> _results = new();
    private bool _updatingCohortFilters;
    private string _activeMetricName = "";
    private bool _activePer90;
    private int _activeMinimumSampleSize = 1;

    private PerformanceValueAnalysisWindow(ScoutDatabase database, IReadOnlyList<StatMetricField> metrics)
    {
        _database = database;
        _metrics = metrics;
        Title = "FM Atlas · Performance vs Value";
        Width = 1240;
        Height = 900;
        MinWidth = 980;
        MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(13, 17, 23));
        Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 244));

        var root = new DockPanel { Margin = new Thickness(18) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = "RECRUITMENT ANALYTICS", Foreground = new SolidColorBrush(Color.FromRgb(110, 174, 218)), FontSize = 10, FontWeight = FontWeights.Bold });
        header.Children.Add(new TextBlock { Text = "Performance vs Value", FontSize = 25, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 2) });
        header.Children.Add(new TextBlock
        {
            Text = "Compare a discovered career metric against transfer-value percentiles. Each player is ranked against the same eligible source-data pool; the gap is a screening signal, not a claim about asking price or player quality.",
            Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)), FontSize = 11, TextWrapping = TextWrapping.Wrap
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var controls = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        _metricPicker = new ComboBox { Width = 340, Height = 34, Margin = new Thickness(0, 0, 8, 6), ItemsSource = metrics, DisplayMemberPath = nameof(StatMetricField.PickerLabel), IsTextSearchEnabled = true };
        _metricPicker.SelectedIndex = 0;
        _per90 = new CheckBox { Content = "Per 90", Foreground = new SolidColorBrush(Color.FromRgb(220, 229, 237)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 6) };
        _minimumMinutes = new TextBox { Text = "450", Width = 90, Height = 34, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(8, 5, 8, 5) };
        _minimumSampleSize = new TextBox { Text = "1", Width = 65, Height = 34, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(8, 5, 8, 5), ToolTip = "Minimum number of source rows/periods used to calculate this player's metric." };
        _positionFilter = new ComboBox { Width = 155, Height = 34, Margin = new Thickness(0, 0, 8, 6) };
        _positionFilter.Items.Add("All positions");
        _positionFilter.SelectedIndex = 0;
        _leagueFilter = new ComboBox { Width = 190, Height = 34, Margin = new Thickness(0, 0, 8, 6) };
        _leagueFilter.Items.Add("All leagues");
        _leagueFilter.SelectedIndex = 0;
        _positionRatingCode = new ComboBox { Width = 105, Height = 34, Margin = new Thickness(0, 0, 6, 6), ToolTip = "Limit the comparison pool to players with this source FM position familiarity rating." };
        _positionRatingCode.Items.Add("Any FM pos");
        foreach (var code in PositionSuitabilityService.PositionCodes) _positionRatingCode.Items.Add(code);
        _positionRatingCode.SelectedIndex = 0;
        _minimumPositionRating = new TextBox { Text = "15", Width = 54, Height = 34, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(8, 5, 8, 5), ToolTip = "Minimum source familiarity from 1 to 20." };
        var clearCohorts = new Button { Content = "Clear cohort filters", Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 8, 6) };
        clearCohorts.Click += (_, _) =>
        {
            _positionFilter.SelectedIndex = 0;
            _leagueFilter.SelectedIndex = 0;
            _positionRatingCode.SelectedIndex = 0;
            _minimumPositionRating.Text = "15";
            ApplyCohortFilters();
        };
        var analyse = new Button { Content = "Analyse", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 6) };
        analyse.Click += async (_, _) => await AnalyseAsync();
        var export = new Button { Content = "Export CSV", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 6) };
        export.Click += (_, _) => ExportResults();
        controls.Children.Add(_metricPicker);
        controls.Children.Add(_per90);
        controls.Children.Add(new TextBlock { Text = "Minimum minutes", VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)), Margin = new Thickness(0, 0, 6, 6) });
        controls.Children.Add(_minimumMinutes);
        controls.Children.Add(new TextBlock { Text = "Min samples", VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)), Margin = new Thickness(0, 0, 6, 6) });
        controls.Children.Add(_minimumSampleSize);
        controls.Children.Add(_positionFilter);
        controls.Children.Add(_leagueFilter);
        controls.Children.Add(new TextBlock { Text = "FM position rating ≥", VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)), Margin = new Thickness(0, 0, 6, 6) });
        controls.Children.Add(_positionRatingCode);
        controls.Children.Add(_minimumPositionRating);
        controls.Children.Add(clearCohorts);
        controls.Children.Add(analyse);
        controls.Children.Add(export);
        DockPanel.SetDock(controls, Dock.Top);
        root.Children.Add(controls);

        _status = new TextBlock { Text = "Choose a metric and analyse the latest imported snapshot.", Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)), FontSize = 11, Margin = new Thickness(0, 0, 0, 9), TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(_status, Dock.Top);
        root.Children.Add(_status);

        _chart = new Canvas
        {
            Width = 930, Height = 365,
            Background = new SolidColorBrush(Color.FromRgb(19, 27, 36)),
            ClipToBounds = true
        };
        DrawEmptyChart();
        var chartScroll = new ScrollViewer { Content = _chart, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(chartScroll, Dock.Top);
        root.Children.Add(chartScroll);

        _grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            Background = new SolidColorBrush(Color.FromRgb(23, 31, 40)),
            Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 244)),
            RowBackground = new SolidColorBrush(Color.FromRgb(23, 31, 40)),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(32, 42, 53)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };
        AddColumn("PLAYER", "Player.Name", 1.25);
        AddColumn("CLUB", "Player.Club", .9);
        AddColumn("POSITION", "Player.Position", .7);
        AddColumn("PERFORMANCE", nameof(PerformanceValueCandidate.DisplayPerformanceValue), .75);
        AddColumn("PERF PCTL", nameof(PerformanceValueCandidate.DisplayPerformancePercentile), .6);
        AddColumn("VALUE PCTL", nameof(PerformanceValueCandidate.DisplayValuePercentile), .6);
        AddColumn("POS PCTL", nameof(PerformanceValueCandidate.DisplayPositionPercentile), .6);
        AddColumn("LEAGUE PCTL", nameof(PerformanceValueCandidate.DisplayLeaguePercentile), .7);
        AddColumn("VALUE GAP", nameof(PerformanceValueCandidate.DisplayValueGap), .65);
        AddColumn("MINUTES", nameof(PerformanceValueCandidate.DisplayMinutes), .7);
        AddColumn("SAMPLE", nameof(PerformanceValueCandidate.SampleSize), .5);
        AddColumn("LATEST PERIOD", nameof(PerformanceValueCandidate.LatestPeriod), .8);
        AddColumn("VALUE · SOURCE UNITS", "Player.Value", .85);
        root.Children.Add(_grid);
        Content = root;

        void AddColumn(string title, string path, double width)
            => _grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = new System.Windows.Data.Binding(path), Width = new DataGridLength(width, DataGridLengthUnitType.Star) });

        _metricPicker.SelectionChanged += (_, _) => UpdatePer90Availability();
        _positionFilter.SelectionChanged += (_, _) => { if (!_updatingCohortFilters) ApplyCohortFilters(); };
        _leagueFilter.SelectionChanged += (_, _) => { if (!_updatingCohortFilters) ApplyCohortFilters(); };
        _positionRatingCode.SelectionChanged += async (_, _) => { if (!_updatingCohortFilters && IsLoaded) await AnalyseAsync(); };
        _minimumPositionRating.LostFocus += async (_, _) => { if (IsLoaded && _positionRatingCode.SelectedIndex > 0) await AnalyseAsync(); };
        UpdatePer90Availability();
    }

    public static async Task ShowAsync(Window owner, ScoutDatabase database, IReadOnlyList<StatMetricField> metrics)
    {
        if (metrics.Count == 0)
        {
            MessageBox.Show(owner, "No statistical metric catalogue is available. Import a save containing performance rows first.", "FM Atlas · Performance vs Value", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var window = new PerformanceValueAnalysisWindow(database, metrics) { Owner = owner };
        window.Show();
        await window.AnalyseAsync();
    }

    private void UpdatePer90Availability()
    {
        var selected = _metricPicker.SelectedItem as StatMetricField;
        _per90.IsEnabled = selected?.SupportsPer90 == true;
        if (selected?.SupportsPer90 != true) _per90.IsChecked = false;
    }

    private async Task AnalyseAsync()
    {
        if (_metricPicker.SelectedItem is not StatMetricField metric) return;
        if (!int.TryParse(_minimumMinutes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) || minutes < 1 || minutes > 10000)
        {
            _status.Text = "Minimum minutes must be a whole number from 1 to 10,000.";
            return;
        }
        if (!int.TryParse(_minimumSampleSize.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minimumSampleSize)
            || minimumSampleSize < 1 || minimumSampleSize > 10000)
        {
            _status.Text = "Minimum samples must be a whole number from 1 to 10,000.";
            return;
        }
        if (_per90.IsChecked == true && !metric.SupportsPer90)
        {
            _status.Text = "This metric is not eligible for per-90 normalisation.";
            return;
        }

        _status.Text = "Calculating career-wide performance and value percentiles…";
        try
        {
            _activeMetricName = metric.DisplayName;
            _activePer90 = _per90.IsChecked == true;
            _activeMinimumSampleSize = minimumSampleSize;
            _allResults = await _database.GetPerformanceValueCandidatesAsync(
                metric.Key, _activePer90, minutes, 5000, minimumSampleSize: minimumSampleSize,
                positionRatingCode: _positionRatingCode.SelectedItem as string is { } selectedPosition && !selectedPosition.Equals("Any FM pos", StringComparison.OrdinalIgnoreCase) ? selectedPosition : null,
                minimumPositionRating: int.TryParse(_minimumPositionRating.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var positionFloor) ? Math.Clamp(positionFloor, 1, 20) : 15);
            RefreshCohortFilterOptions();
            ApplyCohortFilters();
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Performance vs Value analysis failed.", ex);
            _status.Text = "Analysis failed: " + ex.Message;
        }
    }

    private void RefreshCohortFilterOptions()
    {
        var previousPosition = _positionFilter.SelectedItem as string;
        var previousLeague = _leagueFilter.SelectedItem as string;
        _updatingCohortFilters = true;
        try
        {
            var positions = new List<string> { "All positions" };
            positions.AddRange(_allResults
                .Select(x => x.Player.Position?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)!);
            _positionFilter.ItemsSource = positions;
            _positionFilter.SelectedItem = positions.FirstOrDefault(x =>
                x.Equals(previousPosition, StringComparison.OrdinalIgnoreCase)) ?? "All positions";

            var leagues = new List<string> { "All leagues" };
            leagues.AddRange(_allResults
                .Select(x => x.Player.League?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)!);
            _leagueFilter.ItemsSource = leagues;
            _leagueFilter.SelectedItem = leagues.FirstOrDefault(x =>
                x.Equals(previousLeague, StringComparison.OrdinalIgnoreCase)) ?? "All leagues";
        }
        finally
        {
            _updatingCohortFilters = false;
        }
    }

    private void ApplyCohortFilters()
    {
        var selectedPosition = _positionFilter.SelectedItem as string;
        var selectedLeague = _leagueFilter.SelectedItem as string;
        var filterPosition = !string.IsNullOrWhiteSpace(selectedPosition)
            && !selectedPosition.Equals("All positions", StringComparison.OrdinalIgnoreCase);
        var filterLeague = !string.IsNullOrWhiteSpace(selectedLeague)
            && !selectedLeague.Equals("All leagues", StringComparison.OrdinalIgnoreCase);

        _results = _allResults.Where(x =>
            (!filterPosition || string.Equals(x.Player.Position?.Trim(), selectedPosition, StringComparison.OrdinalIgnoreCase))
            && (!filterLeague || string.Equals(x.Player.League?.Trim(), selectedLeague, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        _grid.ItemsSource = _results;
        if (_results.Count == 0)
        {
            DrawEmptyChart();
            _status.Text = _allResults.Count == 0
                ? "No players have both this metric and a positive source transfer value with the selected minutes floor."
                : $"No rows match the selected cohort filters · {_allResults.Count:N0} eligible players are available before filtering.";
            return;
        }

        DrawChart(_results, _activeMetricName, _activePer90);
        var cohortSummary = string.Join(" · ", new[]
        {
            filterPosition ? $"Position: {selectedPosition}" : "All positions",
            filterLeague ? $"League: {selectedLeague}" : "All leagues"
        });
        var positionRatingSummary = _positionRatingCode.SelectedItem is string code && !code.Equals("Any FM pos", StringComparison.OrdinalIgnoreCase)
            ? $"FM {code} familiarity ≥ {Math.Clamp(int.TryParse(_minimumPositionRating.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var floor) ? floor : 15, 1, 20)}/20"
            : "No FM familiarity floor";
        _status.Text = $"{_results.Count:N0} of {_allResults.Count:N0} eligible players shown · minimum {_activeMinimumSampleSize} source sample(s) · {_results[0].DisplayCoverage} coverage of the eligible valued-player pool · {positionRatingSummary} · {cohortSummary}. Percentiles are calculated within the qualifying familiarity pool, after minutes/sample floors; visible position/league filters are display filters only.";
    }

    private void DrawEmptyChart()
    {
        _chart.Children.Clear();
        AddText("Run an analysis to plot performance percentile against market-value percentile.", 18, 150, 14);
    }

    private void DrawChart(IReadOnlyList<PerformanceValueCandidate> rows, string metricName, bool per90)
    {
        _chart.Children.Clear();
        const double left = 62, top = 24, width = 820, height = 270;
        var axis = new SolidColorBrush(Color.FromRgb(126, 143, 158));
        var grid = new SolidColorBrush(Color.FromRgb(47, 61, 75));
        for (var tick = 0; tick <= 100; tick += 20)
        {
            var x = left + width * tick / 100d;
            var y = top + height - height * tick / 100d;
            _chart.Children.Add(new Line { X1 = x, Y1 = top, X2 = x, Y2 = top + height, Stroke = grid, StrokeThickness = 1 });
            _chart.Children.Add(new Line { X1 = left, Y1 = y, X2 = left + width, Y2 = y, Stroke = grid, StrokeThickness = 1 });
            AddText(tick.ToString(CultureInfo.InvariantCulture), x - 8, top + height + 8, 10);
            AddText(tick.ToString(CultureInfo.InvariantCulture), 22, y - 7, 10);
        }
        _chart.Children.Add(new Line { X1 = left, Y1 = top + height, X2 = left + width, Y2 = top + height, Stroke = axis, StrokeThickness = 1.4 });
        _chart.Children.Add(new Line { X1 = left, Y1 = top, X2 = left, Y2 = top + height, Stroke = axis, StrokeThickness = 1.4 });
        _chart.Children.Add(new Line { X1 = left, Y1 = top + height, X2 = left + width, Y2 = top, Stroke = new SolidColorBrush(Color.FromArgb(120, 228, 148, 83)), StrokeDashArray = new DoubleCollection { 4, 4 }, StrokeThickness = 1.2 });
        AddText("Market-value percentile →", left + width / 2 - 85, top + height + 31, 11);
        AddText("Performance percentile ↑", 8, 3, 11);
        AddText($"{metricName} {(per90 ? "per 90" : "source average")} · each dot is a player", left + 4, top + 4, 10);

        foreach (var item in rows)
        {
            var x = left + width * Math.Clamp(item.ValuePercentile, 0, 100) / 100d;
            var y = top + height - height * Math.Clamp(item.PerformancePercentile, 0, 100) / 100d;
            var dot = new Ellipse
            {
                Width = 8, Height = 8,
                Fill = new SolidColorBrush(Color.FromRgb(86, 176, 218)),
                Stroke = new SolidColorBrush(Color.FromRgb(220, 235, 245)),
                StrokeThickness = .6,
                ToolTip = $"{item.Player.Name}\n{metricName}: {item.PerformanceValue:0.00}\nPerformance percentile: {item.PerformancePercentile:0.0}\nValue percentile: {item.ValuePercentile:0.0}\nValue gap: {item.ValueGap:+0.0;-0.0;0.0}\nMinutes: {item.TotalMinutes:N0} · sample: {item.SampleSize}\nLatest period: {item.LatestPeriod}\nTransfer value: {item.Player.Value:N0} source units"
            };
            dot.MouseLeftButtonDown += (_, _) =>
            {
                _status.Text = $"{item.Player.Name} · performance P{item.PerformancePercentile:0.0} vs value P{item.ValuePercentile:0.0} · gap {item.ValueGap:+0.0;-0.0;0.0} · {item.TotalMinutes:N0} minutes across {item.SampleSize} record(s).";
            };
            Canvas.SetLeft(dot, x - 4);
            Canvas.SetTop(dot, y - 4);
            _chart.Children.Add(dot);
        }
    }

    private void AddText(string text, double left, double top, double size)
    {
        var block = new TextBlock { Text = text, FontSize = size, Foreground = new SolidColorBrush(Color.FromRgb(158, 173, 185)) };
        Canvas.SetLeft(block, left);
        Canvas.SetTop(block, top);
        _chart.Children.Add(block);
    }

    private void ExportResults()
    {
        if (_results.Count == 0)
        {
            MessageBox.Show(this, "Run a Performance vs Value analysis before exporting.", "FM Atlas · Performance vs Value");
            return;
        }
        var dialog = new SaveFileDialog { Title = "Export Performance vs Value", FileName = "FM-Atlas-performance-vs-value.csv", Filter = "CSV files (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true) return;
        static string Q(string? value) => CsvExportSafety.QuoteText(value);
        var lines = new List<string> { "Player,SourceId,Club,Position,League,Metric,Performance,PerformancePercentile,ValuePercentile,PositionPerformancePercentile,LeaguePerformancePercentile,ValueGap,Minutes,SampleSize,LatestPeriod,TransferValueSourceUnits" };
        lines.AddRange(_results.Select(x => string.Join(",", new[]
        {
            Q(x.Player.Name),Q(x.Player.SourceId),Q(x.Player.Club),Q(x.Player.Position),Q(x.Player.League),Q(x.MetricName),
            Q(x.PerformanceValue.ToString("0.0000",CultureInfo.InvariantCulture)),
            Q(x.PerformancePercentile.ToString("0.0",CultureInfo.InvariantCulture)),
            Q(x.ValuePercentile.ToString("0.0",CultureInfo.InvariantCulture)),
            Q(x.DisplayPositionPercentile),Q(x.DisplayLeaguePercentile),
            Q(x.ValueGap.ToString("0.0",CultureInfo.InvariantCulture)),
            Q(x.TotalMinutes.ToString("0",CultureInfo.InvariantCulture)),
            Q(x.SampleSize.ToString(CultureInfo.InvariantCulture)),Q(x.LatestPeriod),
            Q(x.Player.Value?.ToString(CultureInfo.InvariantCulture))
        })));
        File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
        _status.Text = $"Exported {_results.Count:N0} performance/value rows with metric, minutes, sample, period and source IDs.";
    }
}
