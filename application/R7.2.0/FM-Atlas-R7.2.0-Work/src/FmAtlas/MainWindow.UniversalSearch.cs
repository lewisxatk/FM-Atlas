using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private sealed class AdvancedConditionUi
    {
        public required ComboBox Field { get; init; }
        public required ComboBox Operator { get; init; }
        public required TextBox Value { get; init; }
        public required TextBox UpperValue { get; init; }
        public required Border Container { get; init; }
    }

    private sealed class AdvancedGroupUi
    {
        public required int Depth { get; init; }
        public required ComboBox Logic { get; init; }
        public required StackPanel ClausesPanel { get; init; }
        public required StackPanel Body { get; init; }
        public required Border Container { get; set; }
        public List<object> ClauseOrder { get; } = new();
    }

    private sealed record UniversalSearchResult(PlayerRecord Player, int ConditionsMatched, int ConditionsEvaluated, string Explanation);

    private async Task<UIElement> UniversalAdvancedSearchPageAsync()
    {
        var store = new AdvancedFilterProfileStore();
        var fieldNames = new[]
        {
            "Name", "Age", "Club", "Nation", "League", "Position", "CurrentAbility", "PotentialAbility",
            "Value", "HomeGrownClub", "HomeGrownNation", "PreferredFoot", "LeftFoot", "RightFoot",
            "DataOrigin", "RegenStatus", "RegenConfidence"
        };
        var root = new StackPanel();
        var heading = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 12));
        var hs = (StackPanel)heading.Child;
        hs.Children.Add(Txt("UNIVERSAL PLAYER SEARCH", 9, true, Teal));
        hs.Children.Add(Txt("Advanced Search", 23, true, Ink));
        hs.Children.Add(Txt("Build a nested AND/OR filter from verified fields in the current local save snapshot. Each match includes a plain-language explanation. Missing values never pass numeric or not-equals filters.", 11, false, Muted, wrap: true));
        root.Children.Add(heading);

        var profilePicker = new ComboBox { Width = 245, Height = 35, Margin = new Thickness(3), DisplayMemberPath = nameof(AdvancedFilterProfile.Name), IsTextSearchEnabled = true };
        var profileName = Input("Name this search profile"); profileName.Width = 210;
        var profileRow = new WrapPanel();
        profileRow.Children.Add(profilePicker);
        profileRow.Children.Add(profileName);
        profileRow.Children.Add(SoftButton("Save", (_, _) => SaveProfile()));
        profileRow.Children.Add(SoftButton("Load", (_, _) => LoadProfile()));
        profileRow.Children.Add(SoftButton("Duplicate", (_, _) => DuplicateProfile()));
        profileRow.Children.Add(SoftButton("Delete", (_, _) => DeleteProfile()));
        profileRow.Children.Add(SoftButton("Import", (_, _) => ImportProfiles()));
        profileRow.Children.Add(SoftButton("Export", (_, _) => ExportProfiles()));
        root.Children.Add(Card(profileRow, 13, new Thickness(0, 0, 0, 10)));

        AdvancedGroupUi rootGroup = null!;
        TextBlock status = null!;
        var quickSearch = Input("Optional quick text filter (player / club / nation)"); quickSearch.Width = 320;
        var conditionActions = new WrapPanel();
        var addCondition = PrimaryButton("＋ Add condition", (_, _) => AddCondition(rootGroup));
        var addGroup = SoftButton("＋ Add nested group", (_, _) => AddNestedGroup(rootGroup));
        var runSearch = PrimaryButton("Run advanced search", async (_, _) => await RunSearchAsync());
        var clear = SoftButton("Clear filters", (_, _) => { ClearGroup(rootGroup); AddCondition(rootGroup); status.Text = "Filter conditions cleared."; });
        conditionActions.Children.Add(quickSearch);
        conditionActions.Children.Add(addCondition);
        conditionActions.Children.Add(addGroup);
        conditionActions.Children.Add(runSearch);
        conditionActions.Children.Add(clear);
        root.Children.Add(Card(conditionActions, 13, new Thickness(0, 0, 0, 10)));

        var groupHost = new StackPanel();
        rootGroup = CreateGroupUi(1, "ROOT GROUP");
        groupHost.Children.Add(rootGroup.Container);
        root.Children.Add(groupHost);

        status = Txt("Add a condition or nested group, then run the search.", 10, false, Muted, wrap: true);
        root.Children.Add(status);

        var results = CreateGrid();
        results.Columns.Add(Column("PLAYER", nameof(UniversalSearchResult.Player.Name), 1.3));
        results.Columns.Add(Column("CLUB", nameof(UniversalSearchResult.Player.Club), .9));
        results.Columns.Add(Column("POSITION", nameof(UniversalSearchResult.Player.Position), .7));
        results.Columns.Add(Column("AGE", nameof(UniversalSearchResult.Player.Age), .4));
        results.Columns.Add(Column("MATCHED", nameof(UniversalSearchResult.ConditionsMatched), .5));
        results.Columns.Add(Column("REASON", nameof(UniversalSearchResult.Explanation), 1.6));
        root.Children.Add(Card(results, 14, new Thickness(0, 0, 0, 10)));

        var profiles = new List<AdvancedFilterProfile>();
        void RefreshProfiles(string? id = null)
        {
            profiles.Clear();
            profiles.AddRange(store.Load());
            profilePicker.ItemsSource = null;
            profilePicker.ItemsSource = profiles.ToArray();
            if (id is not null) profilePicker.SelectedItem = profiles.FirstOrDefault(x => x.Id == id);
        }

        AdvancedGroupUi CreateGroupUi(int depth, string title)
        {
            var logic = new ComboBox
            {
                Width = 190,
                Height = 33,
                Margin = new Thickness(3),
                ItemsSource = new[] { "ALL conditions (AND)", "ANY condition (OR)" },
                SelectedIndex = 0
            };
            var clausePanel = new StackPanel();
            var body = new StackPanel();
            var toolbar = new WrapPanel();
            AdvancedGroupUi state = null!;
            toolbar.Children.Add(Txt(title, 9, true, Teal));
            toolbar.Children.Add(logic);
            toolbar.Children.Add(SoftButton("Add condition", (_, _) => AddCondition(state)));
            if (depth < AdvancedFilterEvaluator.MaximumDepth)
                toolbar.Children.Add(SoftButton("Add subgroup", (_, _) => AddNestedGroup(state)));
            state = new AdvancedGroupUi
            {
                Depth = depth,
                Logic = logic,
                ClausesPanel = clausePanel,
                Body = body,
                Container = new Border()
            };
            body.Children.Add(toolbar);
            body.Children.Add(clausePanel);
            state.Container = new Border
            {
                Background = Brush("#1B1F25"),
                BorderBrush = Brush("#343A43"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                Child = body
            };
            return state;
        }

        void AddCondition(AdvancedGroupUi group, AdvancedFilterCondition? seed = null)
        {
            if (CountConditions(rootGroup) >= AdvancedFilterEvaluator.MaximumConditions)
            {
                status.Text = $"A search can contain up to {AdvancedFilterEvaluator.MaximumConditions} conditions.";
                return;
            }

            var field = new ComboBox { Width = 170, Height = 33, Margin = new Thickness(3), ItemsSource = fieldNames, IsTextSearchEnabled = true };
            field.SelectedItem = seed?.FieldKey ?? "Name";
            var op = new ComboBox { Width = 145, Height = 33, Margin = new Thickness(3), ItemsSource = Enum.GetValues<AdvancedFilterOperator>() };
            op.SelectedItem = seed?.Operator ?? AdvancedFilterOperator.Contains;
            var value = Input("Value"); value.Width = 115; value.Margin = new Thickness(3);
            var upper = Input("Upper"); upper.Width = 115; upper.Margin = new Thickness(3);
            value.Text = seed?.Value ?? "";
            upper.Text = seed?.UpperValue ?? "";
            var row = new WrapPanel();
            row.Children.Add(field);
            row.Children.Add(op);
            row.Children.Add(value);
            row.Children.Add(upper);
            var container = new Border { Background = Brush("#252A32"), BorderBrush = Brush("#343A43"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(5), Margin = new Thickness(2, 4, 2, 4), Child = row };
            var state = new AdvancedConditionUi { Field = field, Operator = op, Value = value, UpperValue = upper, Container = container };
            var remove = SoftButton("Remove", (_, _) =>
            {
                group.ClauseOrder.Remove(state);
                group.ClausesPanel.Children.Remove(container);
            });
            row.Children.Add(remove);
            op.SelectionChanged += (_, _) => UpdateInputVisibility(state);
            UpdateInputVisibility(state);
            group.ClauseOrder.Add(state);
            group.ClausesPanel.Children.Add(container);
        }

        void UpdateInputVisibility(AdvancedConditionUi row)
        {
            var selected = row.Operator.SelectedItem is AdvancedFilterOperator value ? value : AdvancedFilterOperator.Equals;
            row.Value.Visibility = selected is AdvancedFilterOperator.IsSet or AdvancedFilterOperator.IsMissing ? Visibility.Collapsed : Visibility.Visible;
            row.UpperValue.Visibility = selected == AdvancedFilterOperator.Between ? Visibility.Visible : Visibility.Collapsed;
        }

        void AddNestedGroup(AdvancedGroupUi parent)
        {
            if (parent.Depth >= AdvancedFilterEvaluator.MaximumDepth)
            {
                status.Text = $"Nested groups are limited to {AdvancedFilterEvaluator.MaximumDepth} levels.";
                return;
            }
            var group = CreateGroupUi(parent.Depth + 1, "NESTED GROUP");
            var remove = SoftButton("Remove group", (_, _) =>
            {
                parent.ClauseOrder.Remove(group);
                parent.ClausesPanel.Children.Remove(group.Container);
            });
            ((WrapPanel)group.Body.Children[0]).Children.Add(remove);
            parent.ClauseOrder.Add(group);
            parent.ClausesPanel.Children.Add(group.Container);
            AddCondition(group);
        }

        void ClearGroup(AdvancedGroupUi group)
        {
            foreach (var clause in group.ClauseOrder.OfType<AdvancedGroupUi>().ToArray())
                ClearGroup(clause);
            group.ClauseOrder.Clear();
            group.ClausesPanel.Children.Clear();
            group.Logic.SelectedIndex = 0;
        }

        int CountConditions(AdvancedGroupUi group)
        {
            var count = 0;
            foreach (var clause in group.ClauseOrder)
                count += clause is AdvancedConditionUi ? 1 : clause is AdvancedGroupUi nested ? CountConditions(nested) : 0;
            return count;
        }

        AdvancedFilterGroup BuildGroup(AdvancedGroupUi group)
        {
            var clauses = new List<AdvancedFilterClause>();
            foreach (var clause in group.ClauseOrder)
            {
                if (clause is AdvancedConditionUi condition)
                {
                    if (condition.Field.SelectedItem is not string selectedField
                        || condition.Operator.SelectedItem is not AdvancedFilterOperator selectedOperator)
                        throw new ArgumentException("Choose a field and operator for every condition.");
                    clauses.Add(AdvancedFilterClause.ForCondition(new AdvancedFilterCondition(
                        selectedField, selectedOperator,
                        condition.Value.Text.Trim().Length == 0 ? null : condition.Value.Text.Trim(),
                        condition.UpperValue.Text.Trim().Length == 0 ? null : condition.UpperValue.Text.Trim())));
                }
                else if (clause is AdvancedGroupUi nested)
                    clauses.Add(AdvancedFilterClause.ForGroup(BuildGroup(nested)));
            }

            var logic = group.Logic.SelectedIndex == 1 ? AdvancedFilterLogic.Any : AdvancedFilterLogic.All;
            return new AdvancedFilterGroup(logic, clauses);
        }

        void PopulateGroup(AdvancedGroupUi target, AdvancedFilterGroup source)
        {
            ClearGroup(target);
            target.Logic.SelectedIndex = source.Logic == AdvancedFilterLogic.Any ? 1 : 0;
            foreach (var clause in source.Clauses)
            {
                if (clause.Condition is not null) AddCondition(target, clause.Condition);
                else if (clause.Group is not null)
                {
                    var nested = CreateGroupUi(target.Depth + 1, "NESTED GROUP");
                    var remove = SoftButton("Remove group", (_, _) =>
                    {
                        target.ClauseOrder.Remove(nested);
                        target.ClausesPanel.Children.Remove(nested.Container);
                    });
                    ((WrapPanel)nested.Body.Children[0]).Children.Add(remove);
                    target.ClauseOrder.Add(nested);
                    target.ClausesPanel.Children.Add(nested.Container);
                    PopulateGroup(nested, clause.Group);
                }
            }
        }

        void SaveProfile()
        {
            try
            {
                var name = profileName.Text.Trim();
                if (name.Length == 0) throw new ArgumentException("Enter a profile name first.");
                var selected = profilePicker.SelectedItem as AdvancedFilterProfile;
                var profile = new AdvancedFilterProfile(
                    selected?.Id ?? Guid.NewGuid().ToString("N"),
                    name,
                    BuildGroup(rootGroup),
                    selected?.CreatedUtc ?? DateTime.UtcNow,
                    DateTime.UtcNow);
                var saved = store.Save(profile);
                RefreshProfiles(saved.Id);
                status.Text = $"Saved '{saved.Name}'. Profiles store search criteria only, never player or career-save records.";
            }
            catch (Exception ex) { status.Text = "Could not save profile: " + ex.Message; }
        }

        void LoadProfile()
        {
            if (profilePicker.SelectedItem is not AdvancedFilterProfile selected) { status.Text = "Select a saved profile."; return; }
            PopulateGroup(rootGroup, selected.Root);
            profileName.Text = selected.Name;
            status.Text = $"Loaded '{selected.Name}'. Review the current field options before running it against this save.";
        }

        void DuplicateProfile()
        {
            if (profilePicker.SelectedItem is not AdvancedFilterProfile selected) { status.Text = "Select a profile to duplicate."; return; }
            try
            {
                var copy = store.Duplicate(selected.Id);
                RefreshProfiles(copy.Id);
                profileName.Text = copy.Name;
                PopulateGroup(rootGroup, copy.Root);
                status.Text = $"Duplicated '{selected.Name}'.";
            }
            catch (Exception ex) { status.Text = "Could not duplicate profile: " + ex.Message; }
        }

        void DeleteProfile()
        {
            if (profilePicker.SelectedItem is not AdvancedFilterProfile selected) { status.Text = "Select a profile to delete."; return; }
            if (!store.Delete(selected.Id)) { status.Text = "The selected profile no longer exists."; return; }
            RefreshProfiles();
            status.Text = $"Deleted '{selected.Name}'.";
        }

        void ExportProfiles()
        {
            try
            {
                var dialog = new SaveFileDialog { Title = "Export FM Atlas advanced-search profiles", FileName = "FM-Atlas-advanced-search-profiles.json", Filter = "JSON files (*.json)|*.json" };
                if (dialog.ShowDialog(this) != true) return;
                File.WriteAllText(dialog.FileName, store.ExportJson());
                status.Text = "Exported search criteria only. No player records or save data are included.";
            }
            catch (Exception ex) { status.Text = "Could not export profiles: " + ex.Message; }
        }

        void ImportProfiles()
        {
            try
            {
                var dialog = new OpenFileDialog { Title = "Import FM Atlas advanced-search profiles", Filter = "JSON files (*.json)|*.json", CheckFileExists = true };
                if (dialog.ShowDialog(this) != true) return;
                var imported = store.ImportJson(File.ReadAllText(dialog.FileName));
                RefreshProfiles();
                status.Text = $"Imported {imported.Added:N0} profiles · skipped {imported.Skipped:N0} conflicts. Existing profile IDs and names were not overwritten.";
            }
            catch (Exception ex) { status.Text = "Could not import profiles: " + ex.Message; }
        }

        async Task RunSearchAsync()
        {
            runSearch.IsEnabled = false;
            try
            {
                var group = BuildGroup(rootGroup);
                AdvancedFilterEvaluator.Validate(group);
                var candidates = await RunDatabaseAsync(() => _database.SearchAsync(
                    quickSearch.Text.Trim(), null, null, null, null, null, null, limit: 5000));
                var matched = new List<UniversalSearchResult>();
                foreach (var player in candidates)
                {
                    var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["SourceId"] = player.SourceId,
                        ["Name"] = player.Name,
                        ["Age"] = player.Age,
                        ["Club"] = player.Club,
                        ["Nation"] = player.Nation,
                        ["League"] = player.League,
                        ["Position"] = player.Position,
                        ["CurrentAbility"] = player.CurrentAbility,
                        ["PotentialAbility"] = player.PotentialAbility,
                        ["Value"] = player.Value,
                        ["HomeGrownClub"] = player.HomeGrownClub,
                        ["HomeGrownNation"] = player.HomeGrownNation,
                        ["PreferredFoot"] = player.PreferredFoot,
                        ["LeftFoot"] = player.LeftFoot,
                        ["RightFoot"] = player.RightFoot,
                        ["DataOrigin"] = player.DataOrigin,
                        ["RegenStatus"] = player.RegenStatus,
                        ["RegenConfidence"] = player.RegenConfidence
                    };
                    var evaluation = AdvancedFilterEvaluator.Evaluate(group, values);
                    if (evaluation.Matches)
                    {
                        matched.Add(new UniversalSearchResult(
                            player,
                            evaluation.ConditionsMatched,
                            evaluation.ConditionsEvaluated,
                            string.Join(" · ", evaluation.Explanations.Where(x => x.Contains("matched", StringComparison.OrdinalIgnoreCase)).Take(3))));
                    }
                }

                results.ItemsSource = matched.Take(1000).ToArray();
                status.Text = $"{matched.Count:N0} matches from {candidates.Count:N0} local save/local candidates. Results are capped at 1,000; every evaluated field came from the PlayerRecord snapshot. Add parser field-level evidence before treating all imported facts as verified.";
            }
            catch (Exception ex)
            {
                AtlasLog.Warning("Advanced search was not run: " + ex.Message);
                status.Text = "Check the search conditions: " + ex.Message;
            }
            finally { runSearch.IsEnabled = true; }
        }

        RefreshProfiles();
        AddCondition(rootGroup);
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
