using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using FmAtlas.Data;
using FmAtlas.Models;
using FmAtlas.Services;
using Microsoft.Win32;

namespace FmAtlas;

public partial class MainWindow
{
    private sealed record DevelopmentCandidate(
        string SourceId, string Name, string? Club, string? Position, int Age,
        int CurrentAbility, int PotentialAbility, int Headroom, decimal? Value,
        string Evidence);

    private async Task<UIElement> PlayerDevelopmentPageAsync()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 10));
        var intro = (StackPanel)header.Child;
        intro.Children.Add(Txt("PLAYER INTELLIGENCE · DEVELOPMENT", 9, true, Teal));
        intro.Children.Add(Txt("Development opportunity finder", 22, true, Ink));
        intro.Children.Add(Txt("Find players with a source-backed CA-to-PA gap, filter by age and position, and rank the remaining headroom. This is a snapshot-based screening tool: it does not claim historical attribute growth or guarantee that a player will reach their potential.", 10, false, Muted, wrap: true));

        var controls = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        var minAge = Input("Min age"); minAge.Width = 76;
        var maxAge = Input("Max age"); maxAge.Width = 76;
        var minGap = Input("Min CA–PA gap"); minGap.Width = 105; minGap.Text = "10";
        var minCa = Input("Min CA"); minCa.Width = 76;
        var maxValue = Input("Max value · source units"); maxValue.Width = 145;
        var position = new ComboBox { Width = 145, Height = 36, Margin = new Thickness(0, 0, 7, 8), IsEditable = true, IsTextSearchEnabled = true, ToolTip = "Optional position text filter; uses the imported position label." };
        position.Items.Add("Any position"); position.SelectedIndex = 0;
        var run = PrimaryButton("Find development targets", async (_, _) => await RunSearchAsync());
        var export = SoftButton("Export CSV", (_, _) => ExportRows());
        controls.Children.Add(FilterControl("MIN AGE", minAge, 82));
        controls.Children.Add(FilterControl("MAX AGE", maxAge, 82));
        controls.Children.Add(FilterControl("MIN HEADROOM", minGap, 112));
        controls.Children.Add(FilterControl("MIN CA", minCa, 82));
        controls.Children.Add(FilterControl("MAX VALUE", maxValue, 152));
        controls.Children.Add(FilterControl("POSITION", position, 150));
        controls.Children.Add(run); controls.Children.Add(export);
        intro.Children.Add(controls);
        var status = Txt("Loading the latest local and live player records…", 9, false, Muted, wrap: true);
        status.Margin = new Thickness(0, 8, 0, 0);
        intro.Children.Add(status);
        Grid.SetRow(header, 0); root.Children.Add(header);

        var grid = CreateGrid();
        grid.SelectionMode = DataGridSelectionMode.Single;
        grid.Columns.Add(Column("PLAYER", nameof(DevelopmentCandidate.Name), 1.25));
        grid.Columns.Add(Column("CLUB", nameof(DevelopmentCandidate.Club), 1.0));
        grid.Columns.Add(Column("POSITION", nameof(DevelopmentCandidate.Position), .75));
        grid.Columns.Add(Column("AGE", nameof(DevelopmentCandidate.Age), .4));
        grid.Columns.Add(Column("CA", nameof(DevelopmentCandidate.CurrentAbility), .4));
        grid.Columns.Add(Column("PA", nameof(DevelopmentCandidate.PotentialAbility), .4));
        grid.Columns.Add(Column("HEADROOM", nameof(DevelopmentCandidate.Headroom), .65));
        grid.Columns.Add(Column("VALUE · SOURCE UNITS", nameof(DevelopmentCandidate.Value), .85));
        grid.Columns.Add(Column("EVIDENCE", nameof(DevelopmentCandidate.Evidence), .9));
        grid.MouseDoubleClick += (_, _) =>
        {
            if (grid.SelectedItem is DevelopmentCandidate selected) ShowGlobalSearch(selected.Name);
        };
        var resultsCard = Card(grid, 14);
        Grid.SetRow(resultsCard, 1); root.Children.Add(resultsCard);

        List<DevelopmentCandidate> allCandidates = new();
        List<DevelopmentCandidate> visibleCandidates = new();

        async Task LoadSnapshotAsync()
        {
            try
            {
                var sourceRows = await Task.Run(async () =>
                {
                    const int limit = 50000;
                    var localTask = _database.SearchAsync("", null, null, null, null, null, null, limit: limit);
                    var liveTask = _database.SearchLivePlayersAsync("", limit);
                    await Task.WhenAll(localTask, liveTask).ConfigureAwait(false);
                    var local = await localTask.ConfigureAwait(false);
                    var live = await liveTask.ConfigureAwait(false);
                    static string Key(PlayerRecord p) => $"{p.Name.Trim().ToUpperInvariant()}|{(p.Club ?? "").Trim().ToUpperInvariant()}|{p.Age?.ToString() ?? "?"}";
                    static int Completeness(PlayerRecord p) =>
                        (p.Age.HasValue ? 1 : 0) + (p.CurrentAbility.HasValue ? 1 : 0) +
                        (p.PotentialAbility.HasValue ? 1 : 0) + (!string.IsNullOrWhiteSpace(p.Position) ? 1 : 0) +
                        (!string.IsNullOrWhiteSpace(p.Nation) ? 1 : 0) + (p.Value.HasValue ? 1 : 0);
                    return local.Concat(live).GroupBy(Key, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.OrderByDescending(Completeness).First()).ToList();
                });

                allCandidates = sourceRows
                    .Where(p => p.Age.HasValue && p.CurrentAbility is > 0 && p.PotentialAbility is > 0
                        && p.PotentialAbility >= p.CurrentAbility)
                    .Select(p => new DevelopmentCandidate(
                        p.SourceId, p.Name, p.Club, p.Position, p.Age!.Value,
                        p.CurrentAbility!.Value, p.PotentialAbility!.Value,
                        p.PotentialAbility.Value - p.CurrentAbility.Value, p.Value,
                        p.DataOrigin ?? "Imported player snapshot"))
                    .ToList();

                var positions = allCandidates.Select(x => x.Position).Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                var selectedPosition = position.SelectedItem?.ToString();
                position.Items.Clear(); position.Items.Add("Any position");
                foreach (var item in positions) position.Items.Add(item);
                position.SelectedItem = selectedPosition is not null && positions.Contains(selectedPosition, StringComparer.OrdinalIgnoreCase)
                    ? positions.First(x => x!.Equals(selectedPosition, StringComparison.OrdinalIgnoreCase))
                    : "Any position";
                status.Text = $"{allCandidates.Count:N0} players have valid positive CA and PA values with PA ≥ CA. Negative PA ranges, missing values and invalid records are excluded. Live and offline records are deduplicated by name, club and age.";
                await RunSearchAsync();
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Player Development snapshot load failed.", ex);
                status.Text = "Could not load development candidates: " + ex.Message;
            }
        }

        async Task RunSearchAsync()
        {
            int? ReadInt(TextBox box, string label, int min, int max)
            {
                if (string.IsNullOrWhiteSpace(box.Text)) return null;
                if (!int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
                    throw new FormatException($"{label} must be between {min} and {max}.");
                return value;
            }
            decimal? ReadDecimal(TextBox box)
            {
                if (string.IsNullOrWhiteSpace(box.Text)) return null;
                if (!decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
                    throw new FormatException("Maximum value must be a non-negative number in the source's own units.");
                return value;
            }

            try
            {
                var lowerAge = ReadInt(minAge, "Minimum age", 0, 120);
                var upperAge = ReadInt(maxAge, "Maximum age", 0, 120);
                var gap = ReadInt(minGap, "Minimum headroom", 0, 200) ?? 0;
                var ability = ReadInt(minCa, "Minimum CA", 0, 200);
                var valueLimit = ReadDecimal(maxValue);
                if (lowerAge.HasValue && upperAge.HasValue && lowerAge > upperAge)
                    throw new FormatException("Minimum age cannot exceed maximum age.");

                var positionText = position.SelectedItem?.ToString();
                visibleCandidates = allCandidates.Where(x =>
                    (!lowerAge.HasValue || x.Age >= lowerAge.Value) &&
                    (!upperAge.HasValue || x.Age <= upperAge.Value) &&
                    x.Headroom >= gap &&
                    (!ability.HasValue || x.CurrentAbility >= ability.Value) &&
                    (!valueLimit.HasValue || x.Value.HasValue && x.Value.Value <= valueLimit.Value) &&
                    (string.IsNullOrWhiteSpace(positionText) || positionText == "Any position" ||
                     (x.Position?.Contains(positionText, StringComparison.OrdinalIgnoreCase) ?? false)))
                    .OrderByDescending(x => x.Headroom)
                    .ThenBy(x => x.Age)
                    .ThenByDescending(x => x.CurrentAbility)
                    .Take(5000).ToList();
                grid.ItemsSource = visibleCandidates;
                status.Text = $"{visibleCandidates.Count:N0} development opportunities · ranked by CA–PA headroom, then age and CA. Headroom is a difference between imported values, not a predicted growth amount.";
            }
            catch (FormatException ex) { status.Text = ex.Message; }
            catch (Exception ex) { AtlasLog.Error("Player Development search failed.", ex); status.Text = "Development search failed: " + ex.Message; }
            await Task.CompletedTask;
        }

        void ExportRows()
        {
            if (visibleCandidates.Count == 0) { MessageBox.Show(this, "Run a development search before exporting.", "FM Atlas · Development"); return; }
            var dialog = new SaveFileDialog { Title = "Export development opportunities", FileName = "FM-Atlas-development-opportunities.csv", Filter = "CSV files (*.csv)|*.csv" };
            if (dialog.ShowDialog(this) != true) return;
            static string Q(string? value) => CsvExportSafety.QuoteText(value);
            var lines = new List<string> { "Player,SourceId,Club,Position,Age,CA,PA,CA-PA headroom,Value source units,Evidence" };
            lines.AddRange(visibleCandidates.Select(x => string.Join(",", new[]
            {
                Q(x.Name), Q(x.SourceId), Q(x.Club), Q(x.Position),
                Q(x.Age.ToString(CultureInfo.InvariantCulture)),
                Q(x.CurrentAbility.ToString(CultureInfo.InvariantCulture)),
                Q(x.PotentialAbility.ToString(CultureInfo.InvariantCulture)),
                Q(x.Headroom.ToString(CultureInfo.InvariantCulture)),
                Q(x.Value?.ToString(CultureInfo.InvariantCulture)), Q(x.Evidence)
            })));
            File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
            status.Text = $"Exported {visibleCandidates.Count:N0} development opportunities.";
        }

        await LoadSnapshotAsync();
        return root;
    }

    private async Task<UIElement> TacticalLabPageAsync()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var profiles = RoleFitService.GetProfiles().ToList();
        var header = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 10));
        var stack = (StackPanel)header.Child;
        stack.Children.Add(Txt("TACTICAL INTELLIGENCE · RECRUITMENT", 9, true, Teal));
        stack.Children.Add(Txt("Tactical role planner", 22, true, Ink));
        stack.Children.Add(Txt("Screen the imported player pool against transparent In Possession and Out of Possession role profiles. Each score is an Atlas estimate from available 1–20 attributes, not Football Manager's hidden role score. Exact tactical duties, team instructions and role weights remain separate until independently validated.", 10, false, Muted, wrap: true));

        var controls = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        var role = new ComboBox { Width = 260, Height = 36, Margin = new Thickness(0, 0, 8, 8), ItemsSource = profiles, DisplayMemberPath = nameof(RoleFitProfile.Name), IsTextSearchEnabled = true };
        if (profiles.Count > 0) role.SelectedIndex = 0;
        var minAge = Input("Min age"); minAge.Width = 76;
        var maxAge = Input("Max age"); maxAge.Width = 76;
        var minCa = Input("Min CA"); minCa.Width = 76;
        var minPa = Input("Min PA"); minPa.Width = 76;
        var coverage = new ComboBox { Width = 130, Height = 36, Margin = new Thickness(0, 0, 8, 8), ToolTip = "Minimum percentage of the role's weighted attributes that must be present." };
        foreach (var value in new[] { "50%", "60%", "70%", "80%", "90%", "100%" }) coverage.Items.Add(value);
        coverage.SelectedIndex = 1;
        var position = Input("Position contains"); position.Width = 125;
        var find = PrimaryButton("Find role fits", async (_, _) => await SearchAsync());
        var cancel = SoftButton("Cancel", (_, _) => activeCancellation?.Cancel()); cancel.IsEnabled = false;
        var export = SoftButton("Export CSV", (_, _) => ExportRows());
        controls.Children.Add(FilterControl("ROLE PROFILE", role, 270));
        controls.Children.Add(FilterControl("MIN AGE", minAge, 82));
        controls.Children.Add(FilterControl("MAX AGE", maxAge, 82));
        controls.Children.Add(FilterControl("MIN CA", minCa, 82));
        controls.Children.Add(FilterControl("MIN PA", minPa, 82));
        controls.Children.Add(FilterControl("COVERAGE", coverage, 136));
        controls.Children.Add(FilterControl("POSITION", position, 132));
        controls.Children.Add(find); controls.Children.Add(cancel); controls.Children.Add(export);
        stack.Children.Add(controls);

        var description = Txt(profiles.FirstOrDefault()?.Description ?? "No role profiles are available.", 9, false, Muted, wrap: true);
        description.Margin = new Thickness(0, 4, 0, 0);
        stack.Children.Add(description);
        role.SelectionChanged += (_, _) =>
        {
            if (role.SelectedItem is RoleFitProfile selected) description.Text = $"{selected.Phase} · {selected.PositionGroup} · {selected.Description} · Atlas estimate, not the game's role rating.";
        };

        var status = Txt("Choose a role and search the latest imported player snapshot.", 9, false, Muted, wrap: true);
        status.Margin = new Thickness(0, 8, 0, 0);
        stack.Children.Add(status);
        Grid.SetRow(header, 0); root.Children.Add(header);

        var grid = CreateGrid();
        grid.Columns.Add(Column("PLAYER", nameof(RoleFitCandidate.Name), 1.2));
        grid.Columns.Add(Column("CLUB", nameof(RoleFitCandidate.Club), 1.0));
        grid.Columns.Add(Column("AGE", nameof(RoleFitCandidate.Age), .4));
        grid.Columns.Add(Column("POSITION", nameof(RoleFitCandidate.Position), .7));
        grid.Columns.Add(Column("ATLAS ROLE FIT / 20", nameof(RoleFitCandidate.Score), .75));
        grid.Columns.Add(Column("COVERAGE", nameof(RoleFitCandidate.Coverage), .6));
        grid.Columns.Add(Column("ATTRIBUTE EVIDENCE", nameof(RoleFitCandidate.EvidenceSummary), 1.35));
        grid.Columns.Add(Column("CA", nameof(RoleFitCandidate.CurrentAbility), .4));
        grid.Columns.Add(Column("PA", nameof(RoleFitCandidate.PotentialAbility), .4));
        grid.MouseDoubleClick += (_, _) =>
        {
            if (grid.SelectedItem is RoleFitCandidate selected) ShowGlobalSearch(selected.Name);
        };
        var card = Card(grid, 14); Grid.SetRow(card, 1); root.Children.Add(card);

        List<RoleFitCandidate> results = new();
        CancellationTokenSource? activeCancellation = null;

        async Task SearchAsync()
        {
            if (role.SelectedItem is not RoleFitProfile selected || activeCancellation is not null) return;
            static int? Parse(TextBox box, string label, int min, int max)
            {
                if (string.IsNullOrWhiteSpace(box.Text)) return null;
                if (!int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
                    throw new FormatException($"{label} must be between {min} and {max}.");
                return value;
            }
            try
            {
                var lowerAge = Parse(minAge, "Minimum age", 0, 120);
                var upperAge = Parse(maxAge, "Maximum age", 0, 120);
                var lowerCa = Parse(minCa, "Minimum CA", 0, 200);
                var lowerPa = Parse(minPa, "Minimum PA", -10, 200);
                if (lowerAge.HasValue && upperAge.HasValue && lowerAge > upperAge)
                    throw new FormatException("Minimum age cannot exceed maximum age.");
                var minimumCoverage = coverage.SelectedIndex < 0 ? .6 : .5 + coverage.SelectedIndex * .1;
                var filters = new RoleFitSearchFilters(lowerAge, upperAge, lowerCa, lowerPa,
                    string.IsNullOrWhiteSpace(position.Text) ? null : position.Text.Trim(),
                    minimumCoverage, 500);
                using var cancellation = new CancellationTokenSource();
                activeCancellation = cancellation;
                find.IsEnabled = false; cancel.IsEnabled = true;
                status.Text = "Scoring source-backed player attributes against the selected tactical role…";
                var next = await RunDatabaseAsync(() => _database.SearchRoleFitCandidatesAsync(selected, 50000, filters, cancellation.Token));
                cancellation.Token.ThrowIfCancellationRequested();
                results = next;
                grid.ItemsSource = results;
                status.Text = results.Count == 0
                    ? "No candidates meet the selected role and data-coverage requirements. Import a save with broader player attribute coverage or relax the filters."
                    : $"{results.Count:N0} candidates · highest Atlas role estimate {results[0].Score:0.00}/20 · source coverage and attribute evidence shown. This is not an official FM role rating.";
                AtlasLog.Info($"Tactical Lab role screen completed: role={selected.Name}; results={results.Count}; coverage={minimumCoverage:P0}.");
            }
            catch (FormatException ex) { status.Text = ex.Message; }
            catch (OperationCanceledException) { status.Text = "Role screening cancelled. The last completed results are retained."; }
            catch (Exception ex) { AtlasLog.Error("Tactical Lab role screening failed.", ex); status.Text = "Tactical screening failed: " + ex.Message; }
            finally
            {
                activeCancellation?.Dispose();
                activeCancellation = null;
                find.IsEnabled = true; cancel.IsEnabled = false;
            }
        }

        void ExportRows()
        {
            if (results.Count == 0) { MessageBox.Show(this, "Run a tactical role search before exporting.", "FM Atlas · Tactical Lab"); return; }
            var dialog = new SaveFileDialog { Title = "Export tactical role-fit results", FileName = "FM-Atlas-tactical-role-fits.csv", Filter = "CSV files (*.csv)|*.csv" };
            if (dialog.ShowDialog(this) != true) return;
            static string Q(string? value) => CsvExportSafety.QuoteText(value);
            var lines = new List<string> { "Player,SourceId,Club,Age,Position,Role,AtlasRoleFit,DataCoverage,AttributeEvidence,CA,PA" };
            lines.AddRange(results.Select(x => string.Join(",", new[]
            {
                Q(x.Name), Q(x.SourceId), Q(x.Club), Q(x.Age?.ToString(CultureInfo.InvariantCulture)),
                Q(x.Position), Q((role.SelectedItem as RoleFitProfile)?.Name),
                Q(x.Score.ToString("0.00", CultureInfo.InvariantCulture)),
                Q(x.Coverage.ToString("P0", CultureInfo.InvariantCulture)),
                Q(x.EvidenceSummary), Q(x.CurrentAbility?.ToString(CultureInfo.InvariantCulture)),
                Q(x.PotentialAbility?.ToString(CultureInfo.InvariantCulture))
            })));
            File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
            status.Text = $"Exported {results.Count:N0} tactical role-fit candidates.";
        }

        return root;
    }
}
