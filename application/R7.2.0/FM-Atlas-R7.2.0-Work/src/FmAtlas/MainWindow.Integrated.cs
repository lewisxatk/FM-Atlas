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
    private sealed record PlayerSimilarityRow(int Rank, string Name, string? Club, string? Position, double Similarity, int SharedAttributes, string Coverage, string Basis);
    private sealed record MoneyballRow(string SourceId, string Name, string Club, string Position, int? Age, string League, string Period, string Direction, double Value, double? Percentile, int SampleSize, string Basis);
    private sealed record RecruitmentCandidateRow(string SourceId, string Name, string? Club, string? Position, int? Age, string Value, double? AtlasFit, string Coverage);
    private sealed record TacticalCandidateRow(int Rank, string Name, string Club, string Position, double Score, string Coverage);
    private sealed record TacticalShapeCandidateRow(string Phase, string Slot, string Player, string RoleFit, string PositionRating, string AtlasScore);
    private sealed record GraphicsMappingRow(string Id, string Pack, string Target, string Status);

    private async Task<UIElement> PlayerDnaPageAsync()
    {
        var root = new StackPanel();
        Button find = null!;
        DataGrid results = null!;
        TextBlock detailStatus = null!;
        DataGrid attributes = null!;
        DataGrid similarGrid = null!;
        Button findSimilarButton = null!;
        List<PlayerRecord> currentSearchPlayers = new();
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
        findSimilarButton = SoftButton("Find similar players", async (_, _) => await FindSimilarAsync());
        searchRow.Children.Add(search); searchRow.Children.Add(find); searchRow.Children.Add(compare); searchRow.Children.Add(findSimilarButton);
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

        var similarityPanel = new StackPanel();
        similarityPanel.Children.Add(Txt("SIMILAR PLAYER FINDER", 9, true, Teal));
        similarityPanel.Children.Add(Txt("Closest attribute profiles", 18, true, Ink));
        similarityPanel.Children.Add(Txt("Ranks only the players in your current search results using shared, valid 1–20 attributes. Similarity is an Atlas comparison, not FM's internal rating; names, age, club, CA/PA and position are not score inputs.", 9, false, Muted, wrap: true));
        similarGrid = CreateGrid();
        similarGrid.Columns.Add(Column("RANK", nameof(PlayerSimilarityRow.Rank), .35));
        similarGrid.Columns.Add(Column("PLAYER", nameof(PlayerSimilarityRow.Name), 1.2));
        similarGrid.Columns.Add(Column("CLUB", nameof(PlayerSimilarityRow.Club), .8));
        similarGrid.Columns.Add(Column("POSITION", nameof(PlayerSimilarityRow.Position), .7));
        similarGrid.Columns.Add(Column("SIMILARITY", nameof(PlayerSimilarityRow.Similarity), .65));
        similarGrid.Columns.Add(Column("SHARED", nameof(PlayerSimilarityRow.SharedAttributes), .45));
        similarGrid.Columns.Add(Column("COVERAGE", nameof(PlayerSimilarityRow.Coverage), .7));
        similarGrid.Columns.Add(Column("BASIS", nameof(PlayerSimilarityRow.Basis), 1.4));
        similarityPanel.Children.Add(similarGrid);
        root.Children.Add(Card(similarityPanel, 14));

        async Task FindPlayersAsync()
        {
            find.IsEnabled = false;
            detailStatus.Text = "Searching local save snapshot records…";
            try
            {
                var players = await RunDatabaseAsync(() => _database.SearchAsync(search.Text.Trim(), null, null,
                    null, null, null, null, limit: 250));
                currentSearchPlayers = players;
                results.ItemsSource = players;
                similarGrid.ItemsSource = null;
                detailStatus.Text = $"{players.Count:N0} save/local records found. Similarity search will compare only this result set; live identity mirrors remain separate.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Player DNA search failed.", ex);
                detailStatus.Text = "Player search failed: " + ex.Message;
            }
            finally { find.IsEnabled = true; }
        }

        async Task FindSimilarAsync()
        {
            var target = results.SelectedItem as PlayerRecord
                ?? results.SelectedItems.Cast<PlayerRecord>().FirstOrDefault();
            if (target is null)
            {
                detailStatus.Text = "Select one target player before finding similar players.";
                return;
            }

            findSimilarButton.IsEnabled = false;
            similarGrid.ItemsSource = null;
            detailStatus.Text = $"Comparing source-backed attributes for {target.Name}…";
            try
            {
                var targetAttributes = await RunDatabaseAsync(() => _database.GetPlayerAttributesAsync(target.SourceId));
                if (targetAttributes.Count == 0)
                {
                    detailStatus.Text = $"{target.Name} has no stored player attributes. Import a save or attribute dataset containing this player's ratings first.";
                    return;
                }

                var candidates = currentSearchPlayers
                    .Where(x => !x.SourceId.Equals(target.SourceId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (candidates.Length == 0)
                {
                    detailStatus.Text = "Run a broader player search first. Similarity only compares the currently loaded result set.";
                    return;
                }

                var attributeMaps = await RunDatabaseAsync(() =>
                    _database.GetPlayerAttributeMapsAsync(candidates.Select(x => x.SourceId).ToArray()));
                var comparisonCandidates = candidates.Select(player =>
                    new PlayerSimilarityCandidate(
                        player.SourceId,
                        player.Name,
                        player.Club,
                        player.Position,
                        attributeMaps.TryGetValue(player.SourceId, out var values)
                            ? values
                            : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)))
                    .ToArray();

                var ranked = PlayerSimilarityService.Rank(
                    target.SourceId,
                    targetAttributes,
                    comparisonCandidates,
                    minimumCommonAttributes: 5,
                    minimumTargetCoverage: 0.35);

                similarGrid.ItemsSource = ranked.Select((match, index) =>
                    new PlayerSimilarityRow(
                        index + 1,
                        match.Name,
                        match.Club ?? "—",
                        match.Position ?? "—",
                        Math.Round(match.Similarity, 1),
                        match.SharedAttributeCount,
                        $"{match.TargetCoverage:P0} / {match.CandidateCoverage:P0}",
                        match.Basis))
                    .ToArray();

                detailStatus.Text = ranked.Count == 0
                    ? $"No similar players met the minimum overlap for {target.Name}. Need at least five common valid attributes and 35% target coverage."
                    : $"{ranked.Count:N0} similar players ranked for {target.Name}. Scores use only shared finite 1–20 attributes; the table reports target/candidate coverage.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Player similarity ranking failed.", ex);
                detailStatus.Text = "Could not find similar players: " + ex.Message;
            }
            finally
            {
                findSimilarButton.IsEnabled = true;
            }
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
        Button refreshBoardButton = null!;
        ComboBox shortlistPicker = null!;
        ComboBox projectStatus = null!;
        TextBox projectOwner = null!;
        TextBox projectTags = null!;
        TextBox transferBudget = null!;
        TextBox weeklyWageBudget = null!;
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("RECRUITMENT INTELLIGENCE", 9, true, Teal));
        hs.Children.Add(Txt("Scouting Matrix", 23, true, Ink));
        hs.Children.Add(Txt("Turn your existing saved shortlists into an active recruitment board. Candidates are sourced from the imported save snapshot; this view does not infer availability or transfer asking prices.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var projectBoard = new System.Windows.Controls.Primitives.UniformGrid
        {
            Columns = 4,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var projectBoardStatus = Txt("Loading recruitment projects…", 9, false, Muted, wrap: true);
        refreshBoardButton = SoftButton("Refresh projects", async (_, _) => await LoadProjectsBoardAsync());
        var projectBoardHeading = new WrapPanel();
        projectBoardHeading.Children.Add(Txt("RECRUITMENT PROJECT BOARD", 9, true, Teal));
        projectBoardHeading.Children.Add(refreshBoardButton);
        var projectBoardStack = new StackPanel();
        projectBoardStack.Children.Add(projectBoardHeading);
        projectBoardStack.Children.Add(projectBoard);
        projectBoardStack.Children.Add(projectBoardStatus);
        root.Children.Add(Card(projectBoardStack, 15, new Thickness(0, 0, 0, 10)));

        var profileRow = new WrapPanel();
        shortlistPicker = new ComboBox { Width = 230, Height = 35, Margin = new Thickness(3) };
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

        var projectSettings = new WrapPanel();
        projectStatus = new ComboBox
        {
            Width = 155,
            Height = 35,
            Margin = new Thickness(3),
            ItemsSource = new[] { "Planning", "Active", "Monitoring", "Closed" },
            SelectedItem = "Active"
        };
        projectOwner = Input("Owner"); projectOwner.Width = 155;
        projectTags = Input("Tags (comma separated)"); projectTags.Width = 250;
        transferBudget = Input("Transfer budget"); transferBudget.Width = 145;
        weeklyWageBudget = Input("Weekly wage budget"); weeklyWageBudget.Width = 160;
        var saveProject = SoftButton("Save project details", async (_, _) => await SaveProjectDetailsAsync());
        projectSettings.Children.Add(projectStatus);
        projectSettings.Children.Add(projectOwner);
        projectSettings.Children.Add(projectTags);
        projectSettings.Children.Add(transferBudget);
        projectSettings.Children.Add(weeklyWageBudget);
        projectSettings.Children.Add(saveProject);
        var projectCard = Card(projectSettings, 12, new Thickness(0, 0, 0, 10));
        root.Children.Add(projectCard);

        TextBox notesInput = null!;
        var candidateRow = new WrapPanel();
        var candidateSearch = Input("Find candidate by name or club"); candidateSearch.Width = 250;
        var searchButton = PrimaryButton("Search candidates", async (_, _) => await SearchCandidatesAsync());
        var rolePicker = new ComboBox { Width = 245, Height = 35, Margin = new Thickness(3), ItemsSource = RoleFitService.GetProfiles(), DisplayMemberPath = nameof(RoleFitProfile.Name), IsTextSearchEnabled = true };
        if (rolePicker.Items.Count > 0) rolePicker.SelectedIndex = 0;
        var roleRankButton = SoftButton("Rank role fits", async (_, _) => await SearchRoleFitCandidatesAsync());
        var addButton = SoftButton("Add selected candidate", async (_, _) => await AddCandidateAsync());
        var removeButton = SoftButton("Remove from shortlist", async (_, _) => await RemoveCandidateAsync());
        candidateRow.Children.Add(candidateSearch); candidateRow.Children.Add(searchButton);
        candidateRow.Children.Add(rolePicker); candidateRow.Children.Add(roleRankButton);
        candidateRow.Children.Add(addButton); candidateRow.Children.Add(removeButton);
        root.Children.Add(Txt("Role-fit rankings are Atlas estimates from imported attributes, not FM's hidden role stars. Transfer availability, asking price and wages are not inferred.", 9, false, Muted, wrap: true));
        root.Children.Add(Card(candidateRow, 13, new Thickness(0, 0, 0, 10)));

        candidates = CreateGrid();
        candidates.Columns.Add(Column("PLAYER", nameof(RecruitmentCandidateRow.Name), 1.3));
        candidates.Columns.Add(Column("CLUB", nameof(RecruitmentCandidateRow.Club), 1));
        candidates.Columns.Add(Column("POSITION", nameof(RecruitmentCandidateRow.Position), .7));
        candidates.Columns.Add(Column("AGE", nameof(RecruitmentCandidateRow.Age), .4));
        candidates.Columns.Add(Column("VALUE", nameof(RecruitmentCandidateRow.Value), .6));
        candidates.Columns.Add(Column("ATLAS FIT", nameof(RecruitmentCandidateRow.AtlasFit), .6));
        candidates.Columns.Add(Column("COVERAGE", nameof(RecruitmentCandidateRow.Coverage), .7));
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
            await LoadProjectDetailsAsync();
            await LoadProjectsBoardAsync();
        }
        async Task LoadProjectDetailsAsync()
        {
            if (shortlistPicker.SelectedItem is not string name)
            {
                projectStatus.SelectedItem = "Active";
                projectOwner.Text = "";
                projectTags.Text = "";
                transferBudget.Text = "";
                weeklyWageBudget.Text = "";
                return;
            }

            var project = await RunDatabaseAsync(() => _database.GetRecruitmentProjectAsync(name));
            projectStatus.SelectedItem = project?.Status ?? "Active";
            projectOwner.Text = project?.Owner ?? "";
            rolePicker.SelectedItem = project?.RoleProfileName is string savedRole
                ? RoleFitService.GetProfiles().FirstOrDefault(x => x.Name.Equals(savedRole, StringComparison.OrdinalIgnoreCase))
                : null;
            projectTags.Text = project is null ? "" : string.Join(", ", project.Tags);
            transferBudget.Text = project?.TransferBudget?.ToString(CultureInfo.InvariantCulture) ?? "";
            weeklyWageBudget.Text = project?.WeeklyWageBudget?.ToString(CultureInfo.InvariantCulture) ?? "";
            if (project is not null)
                status.Text = $"{project.Name} · {project.CandidateCount:N0} candidates · {project.Status}"
                    + (string.IsNullOrWhiteSpace(project.Owner) ? "" : $" · Owner: {project.Owner}");
        }
        async Task LoadProjectsBoardAsync()
        {
            refreshBoardButton.IsEnabled = false;
            try
            {
                var projects = await RunDatabaseAsync(() => _database.GetRecruitmentProjectsAsync());
                projectBoard.Children.Clear();
                var stages = new[] { "Planning", "Active", "Monitoring", "Closed" };
                foreach (var stage in stages)
                {
                    var stageProjects = projects
                        .Where(x => x.Status.Equals(stage, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    var columnContent = new StackPanel();
                    columnContent.Children.Add(Txt(stage.ToUpperInvariant() + " · " + stageProjects.Length.ToString("N0"), 9, true, Teal));
                    if (stageProjects.Length == 0)
                    {
                        columnContent.Children.Add(Txt("No projects", 9, false, Muted));
                    }
                    else
                    {
                        foreach (var project in stageProjects)
                        {
                            var projectContent = new StackPanel();
                            var open = SoftButton(project.Name, async (_, _) => await LoadShortlistsAsync(project.Name));
                            open.HorizontalContentAlignment = HorizontalAlignment.Left;
                            open.ToolTip = "Open recruitment project";
                            projectContent.Children.Add(open);
                            AddDetailRow(projectContent, "Candidates", project.CandidateCount.ToString("N0"));
                            AddDetailRow(projectContent, "Owner", project.Owner ?? "Unassigned");
                            AddDetailRow(projectContent, "Target role", project.RoleProfileName ?? "Not set");
                            AddDetailRow(projectContent, "Tags", project.Tags.Count == 0 ? "—" : string.Join(", ", project.Tags));
                            AddDetailRow(projectContent, "Transfer", project.TransferBudget?.ToString("C0", CultureInfo.GetCultureInfo("en-GB")) ?? "Not set");
                            AddDetailRow(projectContent, "Weekly wages", project.WeeklyWageBudget?.ToString("C0", CultureInfo.GetCultureInfo("en-GB")) ?? "Not set");
                            columnContent.Children.Add(Card(projectContent, 10, new Thickness(0, 4, 0, 5)));
                        }
                    }
                    projectBoard.Children.Add(Card(columnContent, 12, new Thickness(3)));
                }

                projectBoardStatus.Text = $"{projects.Count:N0} recruitment projects · Select a project name to open its candidate list and update its details. Financial budgets are planning limits only; candidate affordability remains unavailable until asking-price and wage source fields are mapped.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Recruitment project board refresh failed.", ex);
                projectBoardStatus.Text = "Could not load recruitment projects: " + ex.Message;
            }
            finally
            {
                refreshBoardButton.IsEnabled = true;
            }
        }

        async Task SaveProjectDetailsAsync()
        {
            if (shortlistPicker.SelectedItem is not string name)
            {
                status.Text = "Create or select a recruitment project first.";
                return;
            }

            static decimal? ParseBudget(string text, string label)
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                if (!decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
                    throw new ArgumentException($"{label} must be a non-negative number, or left blank.");
                return value;
            }

            try
            {
                var transfer = ParseBudget(transferBudget.Text, "Transfer budget");
                var wages = ParseBudget(weeklyWageBudget.Text, "Weekly wage budget");
                var tags = projectTags.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var selectedStatus = projectStatus.SelectedItem as string ?? "Active";
                await RunDatabaseAsync(() => _database.SaveRecruitmentProjectAsync(
                    name, selectedStatus, projectOwner.Text, tags, transfer, wages,
                    (rolePicker.SelectedItem as RoleFitProfile)?.Name));
                await LoadProjectDetailsAsync();
                await LoadProjectsBoardAsync();
                status.Text = $"Saved project details for '{name}'.";
            }
            catch (Exception ex)
            {
                status.Text = "Could not save project details: " + ex.Message;
            }
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
                candidates.ItemsSource = rows.Select(player => new RecruitmentCandidateRow(
                    player.SourceId, player.Name, player.Club, player.Position, player.Age,
                    player.DisplayValue, null, "Not ranked")).ToArray();
                status.Text = $"{rows.Count:N0} candidate records found. Select a row to add it to the active shortlist.";
            }
            catch (Exception ex) { status.Text = "Candidate search failed: " + ex.Message; }
        }
        async Task SearchRoleFitCandidatesAsync()
        {
            if (rolePicker.SelectedItem is not RoleFitProfile selectedRole)
            {
                status.Text = "Choose a tactical role before ranking candidates.";
                return;
            }
            try
            {
                var rows = await RunDatabaseAsync(() => _database.SearchRoleFitCandidatesAsync(selectedRole, 5000, resultLimit: 5000));
                candidates.ItemsSource = rows
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new RecruitmentCandidateRow(
                        x.SourceId, x.Name, x.Club, x.Position, x.Age, "—",
                        Math.Round(x.Score, 2), $"{x.AttributesAvailable}/{x.AttributesExpected} · {x.Coverage:P0}"))
                    .ToArray();
                status.Text = $"{rows.Count:N0} {selectedRole.Name} candidates ranked by Atlas role-fit estimate. Fees, wages, availability and FM-native role stars are not supplied by this query.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Recruitment role-fit search failed.", ex);
                status.Text = "Could not rank role-fit candidates: " + ex.Message;
            }
        }
        async Task AddCandidateAsync()
        {
            if (shortlistPicker.SelectedItem is not string name) { status.Text = "Create or select a shortlist first."; return; }
            if (candidates.SelectedItem is not RecruitmentCandidateRow selected) { status.Text = "Select a candidate first."; return; }
            var player = new PlayerRecord
            {
                SourceId = selected.SourceId,
                Name = selected.Name,
                Club = selected.Club,
                Position = selected.Position,
                Age = selected.Age
            };
            try
            {
                await RunDatabaseAsync(() => _database.AddToShortlistAsync(player, name));
                await LoadSavedRowsAsync();
                await LoadProjectsBoardAsync();
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
                await LoadProjectsBoardAsync();
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
            await LoadProjectDetailsAsync();
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
            Width = 230,
            Height = 35,
            Margin = new Thickness(3),
            ItemsSource = new[] { "All players", "Same primary position", "Same age band (5-year)", "Same league", "Same position + age band" },
            SelectedIndex = 0,
            ToolTip = "Groups use imported player metadata only. Missing values are excluded; league grouping compares identical imported league labels and is not a strength adjustment."
        };
        var direction = new ComboBox
        {
            Width = 175,
            Height = 35,
            Margin = new Thickness(3),
            ItemsSource = new[] { "Higher is better", "Lower is better" },
            SelectedIndex = 0,
            ToolTip = "Set the ranking direction for this statistic. Atlas does not guess whether a metric is desirable."
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
        options.Children.Add(field); options.Children.Add(cohort); options.Children.Add(direction); options.Children.Add(per90); options.Children.Add(minimumMinutes); options.Children.Add(minimumSample); options.Children.Add(run); options.Children.Add(export);
        root.Children.Add(Card(options, 13, new Thickness(0, 0, 0, 10)));

        grid = CreateGrid();
        grid.Columns.Add(Column("PLAYER", nameof(MoneyballRow.Name), 1.3));
        grid.Columns.Add(Column("CLUB", nameof(MoneyballRow.Club), .9));
        grid.Columns.Add(Column("PRIMARY POSITION", nameof(MoneyballRow.Position), .7));
        grid.Columns.Add(Column("AGE", nameof(MoneyballRow.Age), .4));
        grid.Columns.Add(Column("LEAGUE", nameof(MoneyballRow.League), .8));
        grid.Columns.Add(Column("PERIOD", nameof(MoneyballRow.Period), .8));
        grid.Columns.Add(Column("DIRECTION", nameof(MoneyballRow.Direction), .7));
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
        direction.SelectionChanged += (_, _) => InvalidateResults();
        per90.Checked += (_, _) => { UpdatePer90Availability(); InvalidateResults(); };
        per90.Unchecked += (_, _) => { UpdatePer90Availability(); InvalidateResults(); };
        minimumMinutes.TextChanged += (_, _) => InvalidateResults();
        minimumSample.TextChanged += (_, _) => InvalidateResults();

        async Task RankAsync()
        {
            if (field.SelectedItem is not StatMetricField selected) { status.Text = "No metric is selected."; return; }
            var mins = int.TryParse(minimumMinutes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMinutes) ? Math.Clamp(parsedMinutes, 1, 10000) : 450;
            var sample = int.TryParse(minimumSample.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSample) ? Math.Clamp(parsedSample, 1, 100000) : 5;
            var higherIsBetter = !string.Equals(direction.SelectedItem as string, "Lower is better", StringComparison.Ordinal);
            var directionLabel = higherIsBetter ? "Higher is better" : "Lower is better";
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
                    x.Row.Value)).ToArray();
                var byId = indexedRows.ToDictionary(x => x.Key, x => x.Row, StringComparer.OrdinalIgnoreCase);
                var cohortMode = cohort.SelectedItem as string ?? "All players";
                static string? AgeBand(int? age)
                {
                    if (!age.HasValue || age.Value < 15 || age.Value > 60) return null;
                    var lower = (age.Value / 5) * 5;
                    return $"{lower}–{lower + 4}";
                }
                var groupedObservations = observations.Select(observation =>
                {
                    var row = byId[observation.Id];
                    var position = string.IsNullOrWhiteSpace(row.PrimaryPosition) ? null : row.PrimaryPosition.Trim();
                    var ageBand = AgeBand(row.Age);
                    var league = string.IsNullOrWhiteSpace(row.League) ? null : row.League.Trim();
                    var groupKey = cohortMode switch
                    {
                        "Same primary position" => position,
                        "Same age band (5-year)" => ageBand,
                        "Same league" => league,
                        "Same position + age band" => position is not null && ageBand is not null ? position + " · " + ageBand : null,
                        _ => null
                    };
                    return observation with { GroupKey = groupKey };
                }).ToArray();
                var grouped = cohortMode != "All players";
                var ranked = grouped
                    ? CohortPercentileService.CalculateByGroup(groupedObservations, sample, higherIsBetter)
                    : CohortPercentileService.Calculate(observations, sample, higherIsBetter);
                var displayRows = ranked.Select(x =>
                {
                    var row = byId[x.Id];
                    return new MoneyballRow(row.SourceId, row.Name, row.Club ?? "—", row.PrimaryPosition ?? "Unclassified", row.Age,
                        row.League ?? "—", row.Season, directionLabel, row.Value, x.Percentile, x.SampleSize, x.Basis);
                }).ToArray();
                currentRows = displayRows;
                currentMetricKey = selected.Key;
                grid.ItemsSource = displayRows;
                export.IsEnabled = displayRows.Length > 0;
                var missingGroupMetadata = cohortMode switch
                {
                    "Same primary position" => rows.Count(x => string.IsNullOrWhiteSpace(x.PrimaryPosition)),
                    "Same age band (5-year)" => rows.Count(x => AgeBand(x.Age) is null),
                    "Same league" => rows.Count(x => string.IsNullOrWhiteSpace(x.League)),
                    "Same position + age band" => rows.Count(x => string.IsNullOrWhiteSpace(x.PrimaryPosition) || AgeBand(x.Age) is null),
                    _ => 0
                };
                var cohortDescription = grouped
                    ? $"independent {cohortMode} groups; {missingGroupMetadata:N0} rows without required metadata excluded"
                    : "the full returned cohort";
                status.Text = $"{displayRows.Length:N0} observations · {selected.DisplayName}{(per90.IsChecked == true ? " per 90" : "")} · {directionLabel} · {cohortDescription}. Source tables: {selected.SourceTables}. Percentiles are mid-ranks within each displayed sample of up to 50,000 rows; no cross-league strength adjustment or parser field-level certification is implied.";
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
                "Metric,Direction,PlayerSourceId,Player,Club,PrimaryPosition,Age,League,Period,Value,Percentile,SampleSize,EvidenceBasis"
            };
            lines.AddRange(currentRows.Select(row => string.Join(",",
                Q(currentMetricKey),
                Q(row.Direction),
                Q(row.SourceId),
                Q(row.Name),
                Q(row.Club),
                Q(row.Position),
                row.Age?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Q(row.League),
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
        var status = Txt("Select a tactical role to rank imported players.", 10, false, Muted, wrap: true);
        Button run = null!;
        DataGrid grid = null!;
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("TACTICAL INTELLIGENCE", 9, true, Teal));
        hs.Children.Add(Txt("Tactical Studio", 23, true, Ink));
        hs.Children.Add(Txt("Explore role-specific squad fit using Atlas's transparent attribute-weighted models. Football Manager's hidden tactical effectiveness and buy-in formulas are not claimed or reconstructed here. Use Squad Architect for formation-based best-XI selection.", 11, false, Muted, wrap: true));
        root.Children.Add(header);

        var profiles = RoleFitService.GetProfiles();
        var phasePicker = new ComboBox
        {
            Width = 190,
            Height = 36,
            Margin = new Thickness(3),
            ItemsSource = new[] { "All phases", "In Possession", "Out of Possession" },
            SelectedIndex = 0,
            ToolTip = "Filter role profiles by their documented tactical phase."
        };
        var profile = new ComboBox { Width = 300, Height = 36, Margin = new Thickness(3), DisplayMemberPath = nameof(RoleFitProfile.Name), IsTextSearchEnabled = true };
        void RefreshRoleProfiles()
        {
            var previous = (profile.SelectedItem as RoleFitProfile)?.Name;
            var phase = phasePicker.SelectedItem as string ?? "All phases";
            var available = profiles.Where(x => phase == "All phases"
                || x.Phase.Equals("Both", StringComparison.OrdinalIgnoreCase)
                || x.Phase.Equals(phase, StringComparison.OrdinalIgnoreCase)).ToArray();
            profile.ItemsSource = available;
            profile.SelectedItem = available.FirstOrDefault(x => x.Name.Equals(previous, StringComparison.OrdinalIgnoreCase));
            if (profile.SelectedItem is null && available.Length > 0) profile.SelectedIndex = 0;
        }
        RefreshRoleProfiles();
        phasePicker.SelectionChanged += (_, _) =>
        {
            RefreshRoleProfiles();
            grid.ItemsSource = null;
            status.Text = "Role phase changed. Analyse again to refresh the candidate ranking.";
        };
        run = PrimaryButton("Analyse role fit", async (_, _) => await AnalyseAsync());
        var controls = new WrapPanel(); controls.Children.Add(phasePicker); controls.Children.Add(profile); controls.Children.Add(run);
        root.Children.Add(Card(controls, 13, new Thickness(0, 0, 0, 10)));

        grid = CreateGrid();
        grid.Columns.Add(Column("RANK", nameof(TacticalCandidateRow.Rank), .4));
        grid.Columns.Add(Column("PLAYER", nameof(TacticalCandidateRow.Name), 1.2));
        grid.Columns.Add(Column("CLUB", nameof(TacticalCandidateRow.Club), .8));
        grid.Columns.Add(Column("POSITION", nameof(TacticalCandidateRow.Position), .7));
        grid.Columns.Add(Column("ATLAS FIT", nameof(TacticalCandidateRow.Score), .6));
        grid.Columns.Add(Column("COVERAGE", nameof(TacticalCandidateRow.Coverage), .7));
        root.Children.Add(Card(grid, 14));
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

        // A second tactical view compares two distinct tactical phases with phase-compatible
        // role profiles. It is deliberately separate from the single-role ranking above.
        var phasePlanStack = new StackPanel();
        phasePlanStack.Children.Add(Txt("DUAL-PHASE SQUAD ANALYSIS", 9, true, Teal));
        phasePlanStack.Children.Add(Txt("In-possession and out-of-possession shapes", 18, true, Ink));
        phasePlanStack.Children.Add(Txt("Atlas builds two separate one-player-per-slot selections from registered position ratings and transparent role-fit estimates. The sample shapes are starting templates, not imported FM tactics or SI effectiveness scores.", 10, false, Muted, wrap: true));

        var phasePlayers = await RunDatabaseAsync(() => _database.SearchAsync("", null, null, null, null, null, null, limit: 50000));
        var phaseClubs = phasePlayers.Select(x => x.Club)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var phaseClubPicker = new ComboBox
        {
            Width = 300,
            Height = 36,
            Margin = new Thickness(3),
            ItemsSource = phaseClubs,
            IsTextSearchEnabled = true
        };
        if (phaseClubs.Length > 0) phaseClubPicker.SelectedIndex = 0;
        Button compareShapesButton = null!;
        DataGrid phaseGrid = null!;
        TextBlock phaseStatus = null!;
        compareShapesButton = PrimaryButton("Compare both shapes", async (_, _) => await CompareShapesAsync());
        var phaseControls = new WrapPanel();
        phaseControls.Children.Add(phaseClubPicker);
        phaseControls.Children.Add(compareShapesButton);
        phasePlanStack.Children.Add(phaseControls);

        phaseGrid = CreateGrid();
        phaseGrid.Columns.Add(Column("PHASE", nameof(TacticalShapeCandidateRow.Phase), .8));
        phaseGrid.Columns.Add(Column("SLOT", nameof(TacticalShapeCandidateRow.Slot), 1));
        phaseGrid.Columns.Add(Column("PLAYER", nameof(TacticalShapeCandidateRow.Player), 1.2));
        phaseGrid.Columns.Add(Column("ROLE FIT", nameof(TacticalShapeCandidateRow.RoleFit), .6));
        phaseGrid.Columns.Add(Column("POSITION", nameof(TacticalShapeCandidateRow.PositionRating), .6));
        phaseGrid.Columns.Add(Column("ATLAS SCORE", nameof(TacticalShapeCandidateRow.AtlasScore), .7));
        phasePlanStack.Children.Add(phaseGrid);
        phaseStatus = Txt("Choose a club and compare the two sample tactical shapes. The latest registered snapshot is required for position eligibility.", 9, false, Muted, wrap: true);
        phasePlanStack.Children.Add(phaseStatus);
        root.Children.Add(Card(phasePlanStack, 15, new Thickness(0, 14, 0, 0)));

        async Task CompareShapesAsync()
        {
            if (phaseClubPicker.SelectedItem is not string selectedClub || string.IsNullOrWhiteSpace(selectedClub))
            {
                phaseStatus.Text = "No imported club is available. Import a save snapshot first.";
                return;
            }

            compareShapesButton.IsEnabled = false;
            phaseGrid.ItemsSource = null;
            phaseStatus.Text = "Calculating separate in-possession and out-of-possession selections…";
            try
            {
                var inShape = TacticalSquadAnalysisService.GetDefaultInPossessionShape();
                var outShape = TacticalSquadAnalysisService.GetDefaultOutOfPossessionShape();
                var profileMap = RoleFitService.GetProfiles()
                    .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
                var requiredProfiles = inShape.Slots.Concat(outShape.Slots)
                    .Select(x => x.RoleProfileName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var roleTasks = requiredProfiles.ToDictionary(
                    name => name,
                    name => RunDatabaseAsync(() => _database.SearchRoleFitCandidatesAsync(
                        profileMap[name], 50000, selectedClub, resultLimit: 5000)),
                    StringComparer.OrdinalIgnoreCase);
                var positionsTask = RunDatabaseAsync(() => _database.GetPlayerPositionRatingsForClubAsync(selectedClub));
                await Task.WhenAll(roleTasks.Values.Cast<Task>().Append(positionsTask));

                if (!string.Equals(phaseClubPicker.SelectedItem?.ToString(), selectedClub, StringComparison.OrdinalIgnoreCase))
                {
                    phaseStatus.Text = "Club selection changed while analysis was running. Compare again using the selected club.";
                    return;
                }

                var positions = await positionsTask;
                if (positions.Count == 0)
                {
                    phaseStatus.Text = "No registered position ratings are available for this club. Unknown or unregistered game builds are not used for position eligibility.";
                    return;
                }

                var roleCandidates = roleTasks.ToDictionary(
                    x => x.Key,
                    x => (IReadOnlyList<RoleFitCandidate>)x.Value.Result,
                    StringComparer.OrdinalIgnoreCase);
                var comparison = TacticalSquadAnalysisService.Compare(inShape, outShape, roleCandidates, positions);

                static IEnumerable<TacticalShapeCandidateRow> ToRows(TacticalPhaseSquadResult result)
                {
                    foreach (var assignment in result.Selection.Assignments)
                    {
                        if (assignment.Candidate is null)
                        {
                            yield return new TacticalShapeCandidateRow(result.Phase, assignment.Slot.DisplayName,
                                "No eligible player", "—", "—", "—");
                            continue;
                        }

                        var key = SquadArchitectService.GetBreakdownKey(
                            assignment.Candidate.SourceId, assignment.Slot.SlotId);
                        var detail = result.ScoreBreakdown[key];
                        yield return new TacticalShapeCandidateRow(
                            result.Phase,
                            assignment.Slot.DisplayName,
                            assignment.Candidate.Name,
                            detail.RoleFitScore.ToString("0.0", CultureInfo.InvariantCulture),
                            detail.PositionRating.ToString("0.0", CultureInfo.InvariantCulture),
                            detail.CompositeScore.ToString("0.0", CultureInfo.InvariantCulture));
                    }
                }

                phaseGrid.ItemsSource = ToRows(comparison.InPossession)
                    .Concat(ToRows(comparison.OutOfPossession))
                    .ToArray();
                phaseStatus.Text = comparison.InPossession.ShapeName + ": "
                    + comparison.InPossession.Selection.FilledSlots + "/"
                    + comparison.InPossession.Selection.TotalSlots + " slots · "
                    + comparison.OutOfPossession.ShapeName + ": "
                    + comparison.OutOfPossession.Selection.FilledSlots + "/"
                    + comparison.OutOfPossession.Selection.TotalSlots + " slots · "
                    + comparison.SharedPlayerSourceIds.Count + " player(s) selected in both phases. "
                    + "Shared-player count is descriptive only; FM tactical cohesion, familiarity and player buy-in are not calculated.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Dual-phase tactical analysis failed.", ex);
                phaseStatus.Text = "Could not compare the tactical shapes: " + ex.Message;
            }
            finally
            {
                compareShapesButton.IsEnabled = true;
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
