using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FmAtlas.Data;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private sealed record DnaAttributeRow(string Category, string Attribute, double Value);
    private sealed record MoneyballRow(string SourceId, string Name, string Club, string Position, string Period, double Value, double? Percentile, int SampleSize, string Basis);
    private sealed record TacticalCandidateRow(int Rank, string Name, string Club, string Position, double Score, string Coverage);
    private sealed record TacticalLineupRow(string Slot, string Player, string Club, string PrimaryPosition, string RoleFit, string PositionRating, string CompositeScore, string Assignment);
    private sealed record GraphicsMappingRow(string Id, string Pack, string Target, string Status);

    private async Task<UIElement> PlayerDnaPageAsync()
    {
        var root = new StackPanel();
        Button find = null!;
        DataGrid results = null!;
        TextBlock detailStatus = null!;
        DataGrid attributes = null!;
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("PLAYER INTELLIGENCE", 9, true, Teal));
        hs.Children.Add(Txt("Player DNA & comparison", 23, true, Ink));
        hs.Children.Add(Txt("Inspect imported attributes and compare source-backed player records side by side. Values remain tied to the source snapshot; estimates are not presented as observed facts.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var searchRow = new WrapPanel();
        var search = Input("Search player name, club or nation"); search.Width = 300;
        find = PrimaryButton("Find players", async (_, _) => await FindPlayersAsync());
        var compare = SoftButton("Compare selected", async (_, _) => await CompareSelectedAsync());
        searchRow.Children.Add(search); searchRow.Children.Add(find); searchRow.Children.Add(compare);
        root.Children.Add(Card(searchRow, 13, new Thickness(0, 0, 0, 10)));

        results = CreateGrid();
        results.SelectionMode = DataGridSelectionMode.Extended;
        results.Columns.Add(Column("PLAYER", nameof(PlayerRecord.Name), 1.4));
        results.Columns.Add(Column("CLUB", nameof(PlayerRecord.Club), 1));
        results.Columns.Add(Column("POSITION", nameof(PlayerRecord.Position), .7));
        results.Columns.Add(Column("AGE", nameof(PlayerRecord.Age), .4));
        results.Columns.Add(Column("CA", nameof(PlayerRecord.CurrentAbility), .4));
        results.Columns.Add(Column("PA", nameof(PlayerRecord.PotentialAbility), .4));
        results.Columns.Add(Column("SOURCE", nameof(PlayerRecord.DataOrigin), .7));
        var resultCard = Card(results, 14, new Thickness(0, 0, 0, 10));
        root.Children.Add(resultCard);

        var detail = new StackPanel();
        detailStatus = Txt("Search for players, then select up to three to compare.", 10, false, Muted, wrap: true);
        detail.Children.Add(detailStatus);
        attributes = CreateGrid();
        attributes.Columns.Add(Column("CATEGORY", nameof(DnaAttributeRow.Category), .8));
        attributes.Columns.Add(Column("ATTRIBUTE", nameof(DnaAttributeRow.Attribute), 1.4));
        attributes.Columns.Add(Column("VALUE", nameof(DnaAttributeRow.Value), .5));
        detail.Children.Add(attributes);
        root.Children.Add(Card(detail, 14));

        async Task FindPlayersAsync()
        {
            find.IsEnabled = false;
            detailStatus.Text = "Searching local save snapshot records…";
            try
            {
                var players = await RunDatabaseAsync(() => _database.SearchAsync(search.Text.Trim(), null, null,
                    null, null, null, null, limit: 250));
                results.ItemsSource = players;
                detailStatus.Text = $"{players.Count:N0} save/local records found. Live identity mirrors remain separate from the generic save search.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Player DNA search failed.", ex);
                detailStatus.Text = "Player search failed: " + ex.Message;
            }
            finally { find.IsEnabled = true; }
        }

        async Task CompareSelectedAsync()
        {
            var selected = results.SelectedItems.Cast<PlayerRecord>().Take(3).ToArray();
            if (selected.Length == 0 && results.SelectedItem is PlayerRecord single) selected = new[] { single };
            if (selected.Length == 0) { detailStatus.Text = "Select at least one player."; return; }
            try
            {
                var rows = new List<DnaAttributeRow>();
                foreach (var player in selected)
                {
                    var values = await RunDatabaseAsync(() => _database.GetPlayerAttributesAsync(player.SourceId));
                    foreach (var pair in values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                        rows.Add(new DnaAttributeRow(player.Name, pair.Key, pair.Value));
                }
                attributes.ItemsSource = rows;
                detailStatus.Text = $"{selected.Length} player(s) loaded · attribute rows are stored local facts. The current importer does not yet attach field-level parser coverage to every normalized attribute, so verify important values in-game.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Player DNA comparison failed.", ex);
                detailStatus.Text = "Attribute comparison failed: " + ex.Message;
            }
        }

        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async Task<UIElement> ScoutingMatrixPageAsync()
    {
        var root = new StackPanel();
        var status = Txt("Loading saved recruitment lists…", 10, false, Muted, wrap: true);
        DataGrid candidates = null!;
        DataGrid saved = null!;
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("RECRUITMENT INTELLIGENCE", 9, true, Teal));
        hs.Children.Add(Txt("Scouting Matrix", 23, true, Ink));
        hs.Children.Add(Txt("Turn your existing saved shortlists into an active recruitment board. Candidates are sourced from the imported save snapshot; this view does not infer availability or transfer asking prices.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var profileRow = new WrapPanel();
        var shortlistPicker = new ComboBox { Width = 230, Height = 35, Margin = new Thickness(3) };
        var newName = Input("New shortlist name"); newName.Width = 200;
        var create = SoftButton("Create shortlist", async (_, _) =>
        {
            var name = newName.Text.Trim();
            if (name.Length == 0) { status.Text = "Enter a shortlist name."; return; }
            try
            {
                await RunDatabaseAsync(() => _database.CreateShortlistAsync(name));
                await LoadShortlistsAsync(name);
                newName.Text = "";
                status.Text = $"Created shortlist '{name}'.";
            }
            catch (Exception ex) { status.Text = "Could not create shortlist: " + ex.Message; }
        });
        profileRow.Children.Add(shortlistPicker); profileRow.Children.Add(newName); profileRow.Children.Add(create);
        root.Children.Add(Card(profileRow, 13, new Thickness(0, 0, 0, 10)));

        TextBox notesInput = null!;
        var candidateRow = new WrapPanel();
        var candidateSearch = Input("Find candidate by name or club"); candidateSearch.Width = 280;
        var searchButton = PrimaryButton("Search candidates", async (_, _) => await SearchCandidatesAsync());
        var addButton = SoftButton("Add selected candidate", async (_, _) => await AddCandidateAsync());
        var removeButton = SoftButton("Remove from shortlist", async (_, _) => await RemoveCandidateAsync());
        candidateRow.Children.Add(candidateSearch); candidateRow.Children.Add(searchButton); candidateRow.Children.Add(addButton); candidateRow.Children.Add(removeButton);
        root.Children.Add(Card(candidateRow, 13, new Thickness(0, 0, 0, 10)));

        candidates = CreateGrid();
        candidates.Columns.Add(Column("PLAYER", nameof(PlayerRecord.Name), 1.3));
        candidates.Columns.Add(Column("CLUB", nameof(PlayerRecord.Club), 1));
        candidates.Columns.Add(Column("POSITION", nameof(PlayerRecord.Position), .7));
        candidates.Columns.Add(Column("AGE", nameof(PlayerRecord.Age), .4));
        candidates.Columns.Add(Column("VALUE", nameof(PlayerRecord.DisplayValue), .6));
        root.Children.Add(Card(candidates, 14, new Thickness(0, 0, 0, 10)));

        saved = CreateGrid();
        saved.Columns.Add(Column("PLAYER", nameof(ShortlistRecord.PlayerName), 1.2));
        saved.Columns.Add(Column("CLUB", nameof(ShortlistRecord.DisplayClub), 1));
        saved.Columns.Add(Column("POSITION", nameof(ShortlistRecord.DisplayPosition), .7));
        saved.Columns.Add(Column("NOTES", nameof(ShortlistRecord.Notes), 1.4));
        root.Children.Add(Card(saved, 14, new Thickness(0, 0, 0, 10)));

        var notesRow = new WrapPanel();
        notesInput = Input("Recruitment notes for selected player"); notesInput.Width = 360;
        var saveNotesButton = SoftButton("Save notes", async (_, _) => await SaveNotesAsync());
        notesRow.Children.Add(notesInput);
        notesRow.Children.Add(saveNotesButton);
        root.Children.Add(Card(notesRow, 12, new Thickness(0, 0, 0, 10)));
        root.Children.Add(status);

        async Task LoadShortlistsAsync(string? selectName = null)
        {
            var names = await RunDatabaseAsync(() => _database.GetShortlistNamesAsync());
            shortlistPicker.ItemsSource = names;
            if (!string.IsNullOrWhiteSpace(selectName)) shortlistPicker.SelectedItem = selectName;
            else if (names.Count > 0 && shortlistPicker.SelectedItem is null) shortlistPicker.SelectedIndex = 0;
            await LoadSavedRowsAsync();
        }
        async Task LoadSavedRowsAsync()
        {
            if (shortlistPicker.SelectedItem is not string name) { saved.ItemsSource = Array.Empty<ShortlistRecord>(); return; }
            var rows = await RunDatabaseAsync(() => _database.GetShortlistAsync(name));
            saved.ItemsSource = rows;
        }
        async Task SearchCandidatesAsync()
        {
            try
            {
                var rows = await RunDatabaseAsync(() => _database.SearchAsync(candidateSearch.Text.Trim(), null, null,
                    null, null, null, null, limit: 200));
                candidates.ItemsSource = rows;
                status.Text = $"{rows.Count:N0} candidate records found. Select a row to add it to the active shortlist.";
            }
            catch (Exception ex) { status.Text = "Candidate search failed: " + ex.Message; }
        }
        async Task AddCandidateAsync()
        {
            if (shortlistPicker.SelectedItem is not string name) { status.Text = "Create or select a shortlist first."; return; }
            if (candidates.SelectedItem is not PlayerRecord player) { status.Text = "Select a candidate first."; return; }
            try
            {
                await RunDatabaseAsync(() => _database.AddToShortlistAsync(player, name));
                await LoadSavedRowsAsync();
                status.Text = $"{player.Name} added to '{name}'.";
            }
            catch (Exception ex) { status.Text = "Could not add candidate: " + ex.Message; }
        }
        async Task RemoveCandidateAsync()
        {
            if (shortlistPicker.SelectedItem is not string name || saved.SelectedItem is not ShortlistRecord selected)
            { status.Text = "Select a shortlist and a saved candidate to remove."; return; }
            try
            {
                await RunDatabaseAsync(() => _database.RemoveFromShortlistAsync(selected.SourceId, name));
                notesInput.Text = "";
                await LoadSavedRowsAsync();
                status.Text = $"Removed {selected.PlayerName} from '{name}'.";
            }
            catch (Exception ex) { status.Text = "Could not remove candidate: " + ex.Message; }
        }

        async Task SaveNotesAsync()
        {
            if (shortlistPicker.SelectedItem is not string name || saved.SelectedItem is not ShortlistRecord selected)
            { status.Text = "Select a saved shortlist candidate before editing notes."; return; }
            var note = notesInput.Text.Trim();
            if (note.Length > 2000)
            { status.Text = "Recruitment notes are limited to 2,000 characters."; return; }
            try
            {
                // AddToShortlistAsync is an UPSERT keyed only by provider-specific SourceId
                // and shortlist name; passing the selected identity safely updates its notes.
                var noteOnlyRecord = new PlayerRecord { SourceId = selected.SourceId };
                await RunDatabaseAsync(() => _database.AddToShortlistAsync(noteOnlyRecord, name, note.Length == 0 ? null : note));
                await LoadSavedRowsAsync();
                saved.SelectedItem = ((IEnumerable<ShortlistRecord>)saved.ItemsSource!)
                    .FirstOrDefault(x => x.SourceId.Equals(selected.SourceId, StringComparison.OrdinalIgnoreCase));
                status.Text = $"Notes saved for {selected.PlayerName}.";
            }
            catch (Exception ex) { status.Text = "Could not save recruitment notes: " + ex.Message; }
        }

        saved.SelectionChanged += (_, _) =>
        {
            notesInput.Text = saved.SelectedItem is ShortlistRecord selected ? selected.Notes ?? "" : "";
        };
        shortlistPicker.SelectionChanged += async (_, _) =>
        {
            notesInput.Text = "";
            await LoadSavedRowsAsync();
        };
        await LoadShortlistsAsync();
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async Task<UIElement> MoneyballScoutPageAsync()
    {
        var root = new StackPanel();
        Button run = null!;
        Button export = null!;
        DataGrid grid = null!;
        MoneyballRow[] currentRows = Array.Empty<MoneyballRow>();
        string? currentMetricKey = null;
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("PERFORMANCE SCOUTING", 9, true, Teal));
        hs.Children.Add(Txt("Moneyball Scout", 23, true, Ink));
        hs.Children.Add(Txt("Rank one seasonal metric at a time. Compare the full imported cohort or calculate independent percentiles within each player's imported primary-position group. League-strength adjustment is not inferred when the source does not provide a validated competition mapping.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var allMetrics = await RunDatabaseAsync(() => _database.GetSeasonMetricCatalogAsync());
        // The general catalogue also contains match-only fields. Moneyball compares season
        // observations, so expose only fields backed by season tables or an FM View export.
        var metrics = allMetrics.Where(metric =>
            (metric.SourceTables ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(source => source.Equals("player_season_stats", StringComparison.OrdinalIgnoreCase)
                    || source.Equals("player_season_history", StringComparison.OrdinalIgnoreCase)
                    || source.Equals("FM View Export", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var status = Txt("Loading statistical cohort…", 10, false, Muted, wrap: true);
        var options = new WrapPanel();
        var field = new ComboBox { Width = 320, Height = 35, Margin = new Thickness(3), ItemsSource = metrics, DisplayMemberPath = nameof(StatMetricField.PickerLabel), IsTextSearchEnabled = true };
        if (metrics.Count > 0) field.SelectedIndex = 0;
        var cohort = new ComboBox
        {
            Width = 205,
            Height = 35,
            Margin = new Thickness(3),
            ItemsSource = new[] { "All players", "Same primary position" },
            SelectedIndex = 0,
            ToolTip = "Position-normalised cohorts use the imported primary-position label. Players without a usable position are excluded from those groups."
        };
        var per90 = new CheckBox { Content = "Per 90", Margin = new Thickness(10, 9, 8, 3), Foreground = Ink, VerticalAlignment = VerticalAlignment.Center };
        var minimumMinutes = Input("Minimum minutes"); minimumMinutes.Text = "450"; minimumMinutes.Width = 130;
        var minimumSample = Input("Minimum cohort size"); minimumSample.Text = "5"; minimumSample.Width = 140;
        void UpdatePer90Availability()
        {
            var supportsPer90 = field.SelectedItem is StatMetricField selectedMetric && selectedMetric.SupportsPer90;
            per90.IsEnabled = supportsPer90;
            if (!supportsPer90) per90.IsChecked = false;
            // The sample-size floor applies to raw totals and per-90 rates alike.
            minimumMinutes.IsEnabled = field.SelectedItem is StatMetricField;
            minimumMinutes.ToolTip = "Minimum playing minutes required for a player to enter this cohort.";
            per90.ToolTip = supportsPer90
                ? "Convert a cumulative count to a per-90 rate. Existing rates and percentages cannot be converted again."
                : "This field is already a rate/percentage or is not suitable for per-90 conversion.";
        }
        void InvalidateResults()
        {
            currentRows = Array.Empty<MoneyballRow>();
            currentMetricKey = null;
            grid.ItemsSource = null;
            export.IsEnabled = false;
            status.Text = "Criteria changed. Rank the cohort again before exporting.";
        }
        UpdatePer90Availability();
        run = PrimaryButton("Rank cohort", async (_, _) => await RankAsync());
        export = SoftButton("Export CSV", (_, _) => ExportRows());
        export.IsEnabled = false;
        options.Children.Add(field); options.Children.Add(cohort); options.Children.Add(per90); options.Children.Add(minimumMinutes); options.Children.Add(minimumSample); options.Children.Add(run); options.Children.Add(export);
        root.Children.Add(Card(options, 13, new Thickness(0, 0, 0, 10)));

        grid = CreateGrid();
        grid.Columns.Add(Column("PLAYER", nameof(MoneyballRow.Name), 1.3));
        grid.Columns.Add(Column("CLUB", nameof(MoneyballRow.Club), .9));
        grid.Columns.Add(Column("PRIMARY POSITION", nameof(MoneyballRow.Position), .7));
        grid.Columns.Add(Column("PERIOD", nameof(MoneyballRow.Period), .8));
        grid.Columns.Add(Column("VALUE", nameof(MoneyballRow.Value), .6));
        grid.Columns.Add(Column("PERCENTILE", nameof(MoneyballRow.Percentile), .7));
        grid.Columns.Add(Column("SAMPLE", nameof(MoneyballRow.SampleSize), .5));
        grid.Columns.Add(Column("BASIS", nameof(MoneyballRow.Basis), 1.4));
        root.Children.Add(Card(grid, 14));
        status.Text = metrics.Count == 0 ? "No seasonal metric fields found. Import a copied career save with season statistics or an FM View CSV first." : $"{metrics.Count:N0} seasonal metrics available from the latest imports.";
        root.Children.Add(status);

        // Attach invalidation handlers only after the results grid and export button exist.
        field.SelectionChanged += (_, _) => { UpdatePer90Availability(); InvalidateResults(); };
        cohort.SelectionChanged += (_, _) => InvalidateResults();
        per90.Checked += (_, _) => { UpdatePer90Availability(); InvalidateResults(); };
        per90.Unchecked += (_, _) => { UpdatePer90Availability(); InvalidateResults(); };
        minimumMinutes.TextChanged += (_, _) => InvalidateResults();
        minimumSample.TextChanged += (_, _) => InvalidateResults();

        async Task RankAsync()
        {
            if (field.SelectedItem is not StatMetricField selected) { status.Text = "No metric is selected."; return; }
            var mins = int.TryParse(minimumMinutes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMinutes) ? Math.Clamp(parsedMinutes, 1, 10000) : 450;
            var sample = int.TryParse(minimumSample.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSample) ? Math.Clamp(parsedSample, 1, 100000) : 5;
            run.IsEnabled = false;
            export.IsEnabled = false;
            currentRows = Array.Empty<MoneyballRow>();
            currentMetricKey = null;
            grid.ItemsSource = null;
            try
            {
                var rows = per90.IsChecked == true
                    ? await RunDatabaseAsync(() => _database.SearchSeasonMetricPer90Async(selected.Key, null, null, mins, 50000))
                    : await RunDatabaseAsync(() => _database.SearchSeasonMetricAsync(selected.Key, null, null, 50000, mins));
                var indexedRows = rows.Select((row, index) => new
                {
                    Key = row.SourceId + "|" + row.Season + "|" + index.ToString(CultureInfo.InvariantCulture),
                    Row = row
                }).ToArray();
                var observations = indexedRows.Select(x => new CohortMetricObservation(
                    x.Key,
                    x.Row.Name + " · " + x.Row.Season,
                    x.Row.Value,
                    x.Row.PrimaryPosition)).ToArray();
                var positionNormalised = string.Equals(cohort.SelectedItem as string, "Same primary position", StringComparison.Ordinal);
                var ranked = positionNormalised
                    ? CohortPercentileService.CalculateByGroup(observations, sample)
                    : CohortPercentileService.Calculate(observations, sample);
                var byId = indexedRows.ToDictionary(x => x.Key, x => x.Row, StringComparer.OrdinalIgnoreCase);
                var displayRows = ranked.Select(x =>
                {
                    var row = byId[x.Id];
                    return new MoneyballRow(row.SourceId, row.Name, row.Club ?? "—", row.PrimaryPosition ?? "Unclassified", row.Season, row.Value,
                        x.Percentile, x.SampleSize, x.Basis);
                }).ToArray();
                currentRows = displayRows;
                currentMetricKey = selected.Key;
                grid.ItemsSource = displayRows;
                export.IsEnabled = displayRows.Length > 0;
                var unclassified = rows.Count(x => string.IsNullOrWhiteSpace(x.PrimaryPosition));
                var cohortDescription = positionNormalised
                    ? $"independent primary-position groups; {unclassified:N0} rows without a usable primary-position label excluded"
                    : "the full returned cohort";
                status.Text = $"{displayRows.Length:N0} observations · {selected.DisplayName}{(per90.IsChecked == true ? " per 90" : "")} · {cohortDescription}. Source tables: {selected.SourceTables}. Percentiles are mid-ranks within each displayed sample of up to 50,000 rows; no cross-league strength adjustment or parser field-level certification is implied.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Moneyball cohort ranking failed.", ex);
                status.Text = "Could not rank the cohort: " + ex.Message;
            }
            finally { run.IsEnabled = true; }
        }

        void ExportRows()
        {
            if (currentRows.Length == 0)
            {
                status.Text = "Rank a cohort before exporting.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Moneyball cohort",
                FileName = "FM-Atlas-moneyball-cohort.csv",
                Filter = "CSV files (*.csv)|*.csv"
            };
            if (dialog.ShowDialog(this) != true) return;

            static string Q(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
            var lines = new List<string>
            {
                "Metric,PlayerSourceId,Player,Club,PrimaryPosition,Period,Value,Percentile,SampleSize,EvidenceBasis"
            };
            lines.AddRange(currentRows.Select(row => string.Join(",",
                Q(currentMetricKey),
                Q(row.SourceId),
                Q(row.Name),
                Q(row.Club),
                Q(row.Position),
                Q(row.Period),
                row.Value.ToString("0.####", CultureInfo.InvariantCulture),
                row.Percentile?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty,
                row.SampleSize.ToString(CultureInfo.InvariantCulture),
                Q(row.Basis))));

            try
            {
                File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
                status.Text = $"Exported {currentRows.Length:N0} Moneyball observations with source identity and evidence basis.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Moneyball CSV export failed.", ex);
                status.Text = "Could not export Moneyball cohort: " + ex.Message;
            }
        }

        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async Task<UIElement> TacticalStudioPageAsync()
    {
        var root = new StackPanel();
        var status = Txt("Select a tactical role to rank imported players, or build a best-fit XI from a formation.", 10, false, Muted, wrap: true);
        Button run = null!;
        Button buildLineup = null!;
        DataGrid grid = null!;
        DataGrid lineupGrid = null!;
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("TACTICAL INTELLIGENCE", 9, true, Teal));
        hs.Children.Add(Txt("Tactical Studio", 23, true, Ink));
        hs.Children.Add(Txt("Explore role-specific squad fit and assemble a one-player-per-slot best-fit XI. Scores are Atlas's transparent attribute-weighted estimates, not Football Manager's hidden tactical effectiveness, familiarity, selection or buy-in formulas.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var profiles = RoleFitService.GetProfiles();
        var profile = new ComboBox { Width = 300, Height = 36, Margin = new Thickness(3), ItemsSource = profiles, DisplayMemberPath = nameof(RoleFitProfile.Name), IsTextSearchEnabled = true };
        if (profiles.Count > 0) profile.SelectedIndex = 0;
        run = PrimaryButton("Analyse role fit", async (_, _) => await AnalyseAsync());
        var controls = new WrapPanel(); controls.Children.Add(profile); controls.Children.Add(run);
        root.Children.Add(Card(controls, 13, new Thickness(0, 0, 0, 10)));

        grid = CreateGrid();
        grid.Columns.Add(Column("RANK", nameof(TacticalCandidateRow.Rank), .4));
        grid.Columns.Add(Column("PLAYER", nameof(TacticalCandidateRow.Name), 1.2));
        grid.Columns.Add(Column("CLUB", nameof(TacticalCandidateRow.Club), .8));
        grid.Columns.Add(Column("POSITION", nameof(TacticalCandidateRow.Position), .7));
        grid.Columns.Add(Column("ATLAS FIT", nameof(TacticalCandidateRow.Score), .6));
        grid.Columns.Add(Column("COVERAGE", nameof(TacticalCandidateRow.Coverage), .7));
        root.Children.Add(Card(grid, 14));

        var formationControls = new WrapPanel();
        var formation = new ComboBox
        {
            Width = 230, Height = 36, Margin = new Thickness(3),
            ItemsSource = SquadArchitectService.GetFormationNames(),
            SelectedIndex = 0,
            ToolTip = "Formation slots use each player's imported primary position as a conservative eligibility check."
        };
        buildLineup = PrimaryButton("Build best-fit XI", async (_, _) => await BuildLineupAsync());
        formationControls.Children.Add(formation);
        formationControls.Children.Add(buildLineup);
        root.Children.Add(Txt("FORMATION BUILDER", 9, true, Teal) is var formationTitle ? Configure(formationTitle, new Thickness(0, 18, 0, 5)) : new TextBlock());
        root.Children.Add(Card(formationControls, 13, new Thickness(0, 0, 0, 8)));
        lineupGrid = CreateGrid();
        lineupGrid.Columns.Add(Column("SLOT", nameof(TacticalLineupRow.Slot), .8));
        lineupGrid.Columns.Add(Column("PLAYER", nameof(TacticalLineupRow.Player), 1.2));
        lineupGrid.Columns.Add(Column("CLUB", nameof(TacticalLineupRow.Club), .9));
        lineupGrid.Columns.Add(Column("PRIMARY POSITION", nameof(TacticalLineupRow.PrimaryPosition), .8));
        lineupGrid.Columns.Add(Column("ROLE FIT", nameof(TacticalLineupRow.RoleFit), .55));
        lineupGrid.Columns.Add(Column("POSITION /20", nameof(TacticalLineupRow.PositionRating), .6));
        lineupGrid.Columns.Add(Column("COMPOSITE", nameof(TacticalLineupRow.CompositeScore), .6));
        lineupGrid.Columns.Add(Column("ASSIGNMENT", nameof(TacticalLineupRow.Assignment), .8));
        root.Children.Add(Card(lineupGrid, 14));
        root.Children.Add(status);

        async Task AnalyseAsync()
        {
            if (profile.SelectedItem is not RoleFitProfile selected) { status.Text = "Choose a role."; return; }
            run.IsEnabled = false;
            try
            {
                var rows = await RunDatabaseAsync(() => _database.SearchRoleFitCandidatesAsync(selected, 500));
                var display = rows.OrderByDescending(x => x.Score).Select((x, index) =>
                    new TacticalCandidateRow(index + 1, x.Name, x.Club ?? "—", x.Position ?? "—",
                        Math.Round(x.Score, 2), $"{x.AttributesAvailable}/{x.AttributesExpected} · {x.Coverage:P0}")).ToArray();
                grid.ItemsSource = display;
                status.Text = $"{display.Length:N0} candidates · {selected.Name} · Atlas's weighted 1–20 attribute estimate, not SI's internal role rating. Position familiarity is not added unless source-backed.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Tactical Studio analysis failed.", ex);
                status.Text = "Role fit analysis failed: " + ex.Message;
            }
            finally { run.IsEnabled = true; }
        }

        async Task BuildLineupAsync()
        {
            var selectedFormation = formation.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selectedFormation)) { status.Text = "Choose a formation."; return; }
            var slots = SquadArchitectService.GetFormationSlots(selectedFormation);
            var profileLookup = profiles.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var missingProfiles = slots.Select(x => x.RoleProfileName).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(x => !profileLookup.ContainsKey(x)).ToArray();
            if (missingProfiles.Length > 0)
            {
                status.Text = "Formation cannot be built because these Atlas role profiles are missing: " + string.Join(", ", missingProfiles);
                return;
            }

            buildLineup.IsEnabled = false;
            run.IsEnabled = false;
            lineupGrid.ItemsSource = null;
            status.Text = $"Calculating source-backed role and position ratings for {selectedFormation}…";
            try
            {
                var roleNames = slots.Select(x => x.RoleProfileName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var roleResults = await Task.WhenAll(roleNames.Select(async roleName =>
                {
                    var roleProfile = profileLookup[roleName];
                    var candidates = await RunDatabaseAsync(() => _database.SearchRoleFitCandidatesAsync(roleProfile, 500));
                    return (Role: roleName, Candidates: candidates);
                }));

                var roleCandidates = roleResults.ToDictionary(
                    x => x.Role,
                    x => (IReadOnlyList<RoleFitCandidate>)x.Candidates,
                    StringComparer.OrdinalIgnoreCase);
                var sourceIds = roleResults.SelectMany(x => x.Candidates)
                    .Select(x => x.SourceId)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var positionRatings = await RunDatabaseAsync(() => _database.GetPlayerPositionRatingsAsync(sourceIds));
                var architect = SquadArchitectService.Optimize(selectedFormation, roleCandidates, positionRatings);

                var candidateLookup = roleResults.SelectMany(x => x.Candidates)
                    .GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

                var displayRows = architect.Selection.Assignments.Select(assignment =>
                {
                    if (assignment.Candidate is null)
                        return new TacticalLineupRow(assignment.Slot.DisplayName, "—", "—", "—", "—", "—", "—", "No verified position-rating candidate");
                    var sourceId = assignment.Candidate.SourceId;
                    var source = candidateLookup[sourceId];
                    architect.ScoreBreakdown.TryGetValue(
                        SquadArchitectService.GetBreakdownKey(sourceId, slots.Single(x => x.DisplayName == assignment.Slot.DisplayName).SlotId),
                        out var breakdown);
                    return new TacticalLineupRow(
                        assignment.Slot.DisplayName,
                        source.Name,
                        source.Club ?? "—",
                        source.Position ?? "—",
                        breakdown?.RoleFitScore.ToString("0.00", CultureInfo.InvariantCulture) ?? "—",
                        breakdown?.PositionRating.ToString("0.##", CultureInfo.InvariantCulture) ?? "—",
                        breakdown?.CompositeScore.ToString("0.00", CultureInfo.InvariantCulture) ?? assignment.Score?.ToString("0.00", CultureInfo.InvariantCulture) ?? "—",
                        "Selected · source-backed");
                }).ToArray();

                lineupGrid.ItemsSource = displayRows;
                status.Text = $"{architect.Selection.FilledSlots}/11 slots filled · composite Atlas score {architect.Selection.TotalScore:0.00} · {selectedFormation}. Each player is assigned once. Eligibility requires a source-backed 15-position rating and sufficient attribute coverage for the selected role. Composite = role-fit estimate × position rating / 20; this is not FM's hidden tactical formula, tactical familiarity or buy-in.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Tactical formation builder failed.", ex);
                status.Text = "Formation builder failed: " + ex.Message;
            }
            finally
            {
                buildLineup.IsEnabled = true;
                run.IsEnabled = true;
            }
        }

            catch (Exception ex)
            {
                AtlasLog.Error("Tactical formation builder failed.", ex);
                status.Text = "Formation builder failed: " + ex.Message;
            }
            finally
            {
                buildLineup.IsEnabled = true;
                run.IsEnabled = true;
            }
        }

        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async Task<UIElement> MatchLabPageAsync()
    {
        var root = new StackPanel();
        var status = Txt("Search for a player to inspect available performance facts.", 10, false, Muted, wrap: true);
        ComboBox players = null!;
        DataGrid grid = null!;
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("MATCH PERFORMANCE", 9, true, Teal));
        hs.Children.Add(Txt("Match Lab", 23, true, Ink));
        hs.Children.Add(Txt("Inspect player performance facts retained by the latest imported save. This view does not fabricate event timelines or match ratings when the source only contains aggregate rows.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var search = Input("Find player"); search.Width = 260;
        var find = PrimaryButton("Find", async (_, _) => await FindAsync());
        var pickRow = new WrapPanel(); pickRow.Children.Add(search); pickRow.Children.Add(find);
        root.Children.Add(Card(pickRow, 13, new Thickness(0, 0, 0, 10)));
        players = new ComboBox { Height = 36, Margin = new Thickness(3), DisplayMemberPath = nameof(PlayerRecord.Name), IsTextSearchEnabled = true };
        var load = SoftButton("Load performance facts", async (_, _) => await LoadAsync());
        var chooseRow = new WrapPanel(); chooseRow.Children.Add(players); chooseRow.Children.Add(load);
        root.Children.Add(Card(chooseRow, 13, new Thickness(0, 0, 0, 10)));

        grid = CreateGrid();
        grid.Columns.Add(Column("GROUP", nameof(AtlasFactRecord.Group), .7));
        grid.Columns.Add(Column("METRIC", nameof(AtlasFactRecord.Field), 1.2));
        grid.Columns.Add(Column("SOURCE VALUE", nameof(AtlasFactRecord.Value), 1));
        root.Children.Add(Card(grid, 14));
        root.Children.Add(status);

        async Task FindAsync()
        {
            try
            {
                var rows = await RunDatabaseAsync(() => _database.SearchAsync(search.Text.Trim(), null, null, null, null, null, null, limit: 200));
                players.ItemsSource = rows;
                if (rows.Count > 0) players.SelectedIndex = 0;
                status.Text = $"{rows.Count:N0} player records found in the current save/local dataset.";
            }
            catch (Exception ex) { status.Text = "Player search failed: " + ex.Message; }
        }
        async Task LoadAsync()
        {
            if (players.SelectedItem is not PlayerRecord player) { status.Text = "Select a player first."; return; }
            try
            {
                var rows = await RunDatabaseAsync(() => _database.GetPlayerPerformanceFactsAsync(player.SourceId));
                grid.ItemsSource = rows;
                status.Text = rows.Count == 0
                    ? "No performance facts are linked to this player in the latest snapshot. Import a save with season or match statistics."
                    : $"{rows.Count:N0} source facts · inspect group/metric/value. Event-level match coverage and parser verification are not implied.";
            }
            catch (Exception ex) { status.Text = "Could not load performance facts: " + ex.Message; }
        }
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async Task<UIElement> DevelopmentPageAsync()
    {
        var root = new StackPanel();
        var status = Txt("No player selected.", 10, false, Muted, wrap: true);
        ComboBox picker = null!;
        DataGrid facts = null!;
        DataGrid developmentHistory = null!;
        DataGrid history = null!;
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("PLAYER PATHWAY", 9, true, Teal));
        hs.Children.Add(Txt("Development & career tracker", 23, true, Ink));
        hs.Children.Add(Txt("Review current snapshot facts and Atlas-local edit history. A real attribute-development trend requires multiple dated snapshots; this page will not invent a growth curve from a single import.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var search = Input("Find player"); search.Width = 270;
        var find = PrimaryButton("Find player", async (_, _) => await FindAsync());
        picker = new ComboBox { Width = 350, Height = 36, Margin = new Thickness(3), DisplayMemberPath = nameof(PlayerRecord.Name), IsTextSearchEnabled = true };
        var load = SoftButton("Inspect pathway", async (_, _) => await LoadAsync());
        var controls = new WrapPanel(); controls.Children.Add(search); controls.Children.Add(find); controls.Children.Add(picker); controls.Children.Add(load);
        root.Children.Add(Card(controls, 13, new Thickness(0, 0, 0, 10)));

        facts = CreateGrid();
        facts.Columns.Add(Column("GROUP", nameof(AtlasFactRecord.Group), .7));
        facts.Columns.Add(Column("FIELD", nameof(AtlasFactRecord.Field), 1));
        facts.Columns.Add(Column("CURRENT SNAPSHOT", nameof(AtlasFactRecord.Value), 1));
        root.Children.Add(Card(facts, 14, new Thickness(0, 0, 0, 10)));

        var developmentHeading = Txt("LONGITUDINAL PLAYER DEVELOPMENT", 9, true, Teal);
        developmentHeading.Margin = new Thickness(0, 0, 0, 5);
        root.Children.Add(developmentHeading);
        var developmentNote = Txt("Values are compared only between consecutive imported snapshots. Player attributes are parser-verified; CA, PA, age and birth-date identity fields are marked unconfirmed by fmsave. A changed birth date, or a changed name when birth-date evidence is missing, starts a new identity segment. Same-name ID reuse without DOB evidence cannot be ruled out.", 9, false, Muted, wrap: true);
        developmentNote.Margin = new Thickness(0, 0, 0, 6);
        root.Children.Add(developmentNote);
        developmentHistory = CreateGrid();
        developmentHistory.Columns.Add(Column("IMPORTED", "Imported", 1.1));
        developmentHistory.Columns.Add(Column("IN-GAME DATE", "GameDate", .9));
        developmentHistory.Columns.Add(Column("FIELD", "Field", 1.1));
        developmentHistory.Columns.Add(Column("VALUE", "Value", .55));
        developmentHistory.Columns.Add(Column("CHANGE", "Change", .75));
        developmentHistory.Columns.Add(Column("EVIDENCE", "Evidence", 1.0));
        developmentHistory.Columns.Add(Column("BUILD", "Build", .8));
        developmentHistory.Columns.Add(Column("SOURCE FILE", "SourceFile", 1.2));
        root.Children.Add(Card(developmentHistory, 14, new Thickness(0, 0, 0, 10)));

        history = CreateGrid();
        history.Columns.Add(Column("DATE", nameof(AtlasHistoryRecord.ChangedUtc), 1));
        history.Columns.Add(Column("FIELD", nameof(AtlasHistoryRecord.FieldName), .9));
        history.Columns.Add(Column("OLD", nameof(AtlasHistoryRecord.OldValue), .6));
        history.Columns.Add(Column("NEW", nameof(AtlasHistoryRecord.NewValue), .6));
        history.Columns.Add(Column("CHANGE SOURCE", nameof(AtlasHistoryRecord.ChangeSource), 1.2));
        root.Children.Add(Card(history, 14));
        root.Children.Add(status);

        async Task FindAsync()
        {
            try
            {
                var rows = await RunDatabaseAsync(() => _database.SearchAsync(search.Text.Trim(), null, null, null, null, null, null, limit: 200));
                picker.ItemsSource = rows;
                if (rows.Count > 0) picker.SelectedIndex = 0;
                status.Text = $"{rows.Count:N0} players available.";
            }
            catch (Exception ex) { status.Text = "Search failed: " + ex.Message; }
        }
        async Task LoadAsync()
        {
            if (picker.SelectedItem is not PlayerRecord player) { status.Text = "Choose a player."; return; }
            try
            {
                var groups = new[] { "general", "positions", "reputation", "state", "contract", "traits" };
                var factsTasks = groups.Select(group => RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, group))).ToArray();
                var performanceTask = RunDatabaseAsync(() => _database.GetPlayerPerformanceFactsAsync(player.SourceId));
                var developmentTask = RunDatabaseAsync(() => _database.GetPlayerDevelopmentHistoryAsync(player.SourceId));
                var historyTask = RunDatabaseAsync(() => _database.GetRecentEditHistoryAsync(1000));
                await Task.WhenAll(factsTasks.Cast<Task>().Append(performanceTask).Append(developmentTask).Append(historyTask));
                var currentFacts = factsTasks.SelectMany(t => t.Result).Concat(await performanceTask).ToArray();
                var developmentRows = await developmentTask;
                var edits = (await historyTask).Where(x => x.SourceId.Equals(player.SourceId, StringComparison.OrdinalIgnoreCase)).ToArray();
                facts.ItemsSource = currentFacts;
                developmentHistory.ItemsSource = developmentRows
                    .OrderByDescending(x => x.SnapshotId)
                    .ThenBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new
                    {
                        Imported = x.ImportedUtc,
                        GameDate = x.GameDate ?? "—",
                        Field = x.FieldName,
                        Value = x.Value.ToString("0.##", CultureInfo.InvariantCulture),
                        Change = x.IdentityBoundary
                            ? "Identity reset"
                            : x.Delta.HasValue
                                ? (x.Delta.Value > 0 ? "+" : string.Empty) + x.Delta.Value.ToString("0.##", CultureInfo.InvariantCulture)
                                : "—",
                        Evidence = x.FieldName is "Current Ability" or "Potential Ability" or "Age"
                            ? "Unconfirmed source field"
                            : "Parser-verified attribute",
                        Build = x.GameBuild ?? "—",
                        SourceFile = x.SourceFileName
                    }).ToArray();
                history.ItemsSource = edits;
                var snapshots = developmentRows.Select(x => x.SnapshotId).Distinct().Count();
                status.Text = $"{currentFacts.Length:N0} current snapshot facts · {snapshots:N0} linked save snapshots · {developmentRows.Count:N0} dated development observations · {edits.Length:N0} Atlas-local edit history rows. Trends are source-backed comparisons, not predictions.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Development tracker load failed.", ex);
                status.Text = "Could not inspect pathway: " + ex.Message;
            }
        }
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async Task<UIElement> SaveInspectorPageAsync()
    {
        var root = new StackPanel();
        var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("READ-ONLY DATA INSPECTION", 9, true, Teal));
        hs.Children.Add(Txt("Save Inspector", 23, true, Ink));
        hs.Children.Add(Txt("Inspect the last imported snapshot, retained table counts and live field evidence registry. No career save or database records are modified by this view.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var summary = new StackPanel();
        AddDetailRow(summary, "Game", metrics.SaveGame);
        AddDetailRow(summary, "Build", metrics.SaveBuild);
        AddDetailRow(summary, "In-game date", metrics.SaveGameDate);
        AddDetailRow(summary, "Build layout", metrics.SaveBuildKnown switch { true => "Registered parser layout", false => "Unregistered parser layout", null => "Unknown" });
        AddDetailRow(summary, "Source file", metrics.LastSaveFileName);
        AddDetailRow(summary, "Imported", metrics.LastSaveImportUtc);
        AddDetailRow(summary, "Manager", metrics.CurrentManager);
        AddDetailRow(summary, "Manager club", metrics.CurrentManagerClub);
        AddDetailRow(summary, "Save players", metrics.SavePlayers.ToString("N0", CultureInfo.InvariantCulture));
        AddDetailRow(summary, "Save clubs", metrics.SaveClubs.ToString("N0", CultureInfo.InvariantCulture));
        AddDetailRow(summary, "Save staff", metrics.SaveStaff.ToString("N0", CultureInfo.InvariantCulture));
        AddDetailRow(summary, "Known build?", metrics.SaveBuildKnown?.ToString() ?? "Not reported");
        summary.Children.Add(Txt("Parser validation summary", 10, true, Teal));
        var validation = (metrics.SaveValidationJson ?? "No parser validation report was recorded.");
        summary.Children.Add(Txt(validation.Length > 2400 ? validation[..2400] + "…" : validation, 9, false, Muted, wrap: true));
        root.Children.Add(Card(summary, 15, new Thickness(0, 0, 0, 10)));

        var tableCounts = await RunDatabaseAsync(() => _database.GetFmsaveExtraTableCountsAsync());
        var tables = CreateGrid();
        tables.Columns.Add(Column("RETAINED SOURCE TABLE", "TableName", 1.6));
        tables.Columns.Add(Column("ROWS", "RowCount", .5));
        tables.ItemsSource = tableCounts.Select(x => new { TableName = x.TableName, RowCount = x.RowCount }).ToArray();
        root.Children.Add(Card(tables, 14, new Thickness(0, 0, 0, 10)));

        var registryRows = await RunDatabaseAsync(() => _database.GetLiveFieldRegistryAsync());
        var registry = CreateGrid();
        registry.Columns.Add(Column("ENTITY", nameof(LiveFieldRegistryRecord.EntityType), .6));
        registry.Columns.Add(Column("FIELD", nameof(LiveFieldRegistryRecord.DisplayName), 1.1));
        registry.Columns.Add(Column("STATUS", nameof(LiveFieldRegistryRecord.ValidationStatus), .8));
        registry.Columns.Add(Column("EVIDENCE", nameof(LiveFieldRegistryRecord.Evidence), 1.6));
        registry.ItemsSource = registryRows;
        root.Children.Add(Card(registry, 14));
        root.Children.Add(Txt($"{tableCounts.Count:N0} retained source tables · {registryRows.Count:N0} candidate field registry entries. A matching module profile is not equivalent to field verification.", 10, false, Muted, wrap: true));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private UIElement GraphicsSkinsPage()
    {
        var root = new StackPanel();
        var summary = Txt("Choose a graphics root to start the read-only index.", 10, false, Muted, wrap: true);
        Button scan = null!;
        DataGrid grid = null!;
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("GRAPHICS & SKIN WORKSPACE", 9, true, Teal));
        hs.Children.Add(Txt("Graphics & Skins", 23, true, Ink));
        hs.Children.Add(Txt("Index user-owned graphics packs in read-only mode. The scanner reads config.xml and checks image targets; it never moves, edits or bundles artwork. Set the actual Sports Interactive user-data graphics root, especially on Game Pass.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var path = Input("Full path to your Football Manager graphics folder"); path.Margin = new Thickness(3); path.MinWidth = 420;
        scan = PrimaryButton("Scan graphics folder", async (_, _) => await ScanAsync());
        var controls = new WrapPanel(); controls.Children.Add(path); controls.Children.Add(scan);
        root.Children.Add(Card(controls, 13, new Thickness(0, 0, 0, 10)));
        root.Children.Add(summary);
        grid = CreateGrid();
        grid.Columns.Add(Column("RECORD ID", nameof(GraphicsMappingRow.Id), .6));
        grid.Columns.Add(Column("PACK", nameof(GraphicsMappingRow.Pack), .7));
        grid.Columns.Add(Column("TARGET", nameof(GraphicsMappingRow.Target), 1.8));
        grid.Columns.Add(Column("STATUS", nameof(GraphicsMappingRow.Status), .5));
        root.Children.Add(Card(grid, 14));

        async Task ScanAsync()
        {
            if (string.IsNullOrWhiteSpace(path.Text)) { summary.Text = "Enter the full path to the actual graphics folder."; return; }
            scan.IsEnabled = false;
            summary.Text = "Scanning pack config.xml files…";
            try
            {
                var result = await Task.Run(() => new GraphicsLibraryIndexer().Scan(path.Text.Trim()));
                grid.ItemsSource = result.Mappings.Take(2000).Select(x => new GraphicsMappingRow(
                    x.RecordFrom, x.PackName, x.TargetPath, x.TargetExists ? "Found" : "Missing")).ToArray();
                summary.Text = $"{result.MappingsFound:N0} mappings · {result.ConfigFilesScanned:N0} config files · {result.DirectoriesVisited:N0} folders · {result.DuplicateIds:N0} duplicate IDs · {result.MissingTargets:N0} missing images · {result.RejectedPaths:N0} rejected paths. Showing up to 2,000 rows. {string.Join(" · ", result.Warnings.Take(3))}";
                AtlasLog.Info($"Graphics index scan completed: configs={result.ConfigFilesScanned}, mappings={result.MappingsFound}, missing={result.MissingTargets}, rejected={result.RejectedPaths}.");
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Graphics index scan failed.", ex);
                summary.Text = "Graphics scan failed: " + ex.Message;
            }
            finally { scan.IsEnabled = true; }
        }
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private UIElement ModdingWorkbenchPage()
    {
        var root = new StackPanel();
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("MODDING RESEARCH", 9, true, Teal));
        hs.Children.Add(Txt("Modding Workbench", 23, true, Ink));
        hs.Children.Add(Txt("A research catalogue for save readers, live-memory tools, scouting applications, database editors and graphics utilities. Atlas does not download or execute third-party programs, and recorded licences must be rechecked before reuse.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        foreach (var entry in ModResearchCatalog.GetEntries())
        {
            var stack = new StackPanel();
            stack.Children.Add(Txt(entry.Category.ToUpperInvariant(), 8, true, Teal));
            stack.Children.Add(Txt(entry.Name, 17, true, Ink));
            AddDetailRow(stack, "Target", entry.Target);
            AddDetailRow(stack, "Recorded licence", entry.RecordedLicence);
            stack.Children.Add(Txt(entry.AtlasUse, 10, false, Muted, wrap: true));
            stack.Children.Add(Txt("BOUNDARY · " + entry.Boundary, 9, false, Muted, wrap: true));
            stack.Children.Add(SoftButton("Copy reference URL", (_, _) =>
            {
                try { Clipboard.SetText(entry.RepositoryUrl); }
                catch (Exception ex) { AtlasLog.Warning("Could not copy research URL: " + ex.GetType().Name); }
            }));
            root.Children.Add(Card(stack, 14, new Thickness(0, 0, 0, 8)));
        }

        root.Children.Add(Txt("Research status: desk research and static source/package inspection only. The external EXEs were not executed. GPL projects remain research-only; no third-party source or game assets have been copied into FM Atlas.", 10, false, Muted, wrap: true));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
