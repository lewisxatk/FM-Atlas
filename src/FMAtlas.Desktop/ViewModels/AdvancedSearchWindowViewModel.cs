using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Search;

namespace FMAtlas.Desktop.ViewModels;

/// <summary>
/// Drives the REF-002 category structure and executes only fields which have
/// an implemented typed-record projection and SQLite query mapping.
/// </summary>
public sealed class AdvancedSearchWindowViewModel : INotifyPropertyChanged
{
    private readonly IAdvancedSearchProvider _searchProvider;
    private readonly IIndexedAdvancedSearchFieldProvider? _indexedFieldProvider;
    private readonly Dictionary<SearchEntityKind, IReadOnlyList<AdvancedSearchFieldDefinition>> _indexedFields = [];
    private readonly Dictionary<(SearchEntityKind Entity, string Key), AdvancedSearchFieldViewModel> _fieldState = [];
    private SearchEntityKind _selectedEntity = SearchEntityKind.Player;
    private AdvancedSearchCategoryKind _selectedCategory = AdvancedSearchCategoryKind.General;
    private SearchFilterLogic _selectedLogic = SearchFilterLogic.All;
    private SearchFilterLogic _selectedPositionLogic = SearchFilterLogic.All;
    private string _statusText = "Enter criteria in supported fields, then run the search.";
    private string _fieldSearchText = string.Empty;
    private bool _isBusy;

    public AdvancedSearchWindowViewModel(IAdvancedSearchProvider searchProvider)
    {
        _searchProvider = searchProvider ?? throw new ArgumentNullException(nameof(searchProvider));
        _indexedFieldProvider = searchProvider as IIndexedAdvancedSearchFieldProvider;
        Results = [];
        SelectEntity(SearchEntityKind.Player);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AdvancedSearchSectionViewModel> Sections { get; } = [];
    public ObservableCollection<AdvancedSearchResultViewModel> Results { get; }

    public SearchEntityKind SelectedEntity
    {
        get => _selectedEntity;
        private set => SetField(ref _selectedEntity, value);
    }

    public AdvancedSearchCategoryKind SelectedCategory
    {
        get => _selectedCategory;
        private set => SetField(ref _selectedCategory, value);
    }

    public SearchFilterLogic SelectedLogic
    {
        get => _selectedLogic;
        set => SetField(ref _selectedLogic, value);
    }

    public SearchFilterLogic SelectedPositionLogic
    {
        get => _selectedPositionLogic;
        set => SetField(ref _selectedPositionLogic, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string FieldSearchText
    {
        get => _fieldSearchText;
        set
        {
            if (SetField(ref _fieldSearchText, value))
                RebuildSections();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public void SelectEntity(SearchEntityKind entity)
    {
        SelectedEntity = entity;
        SelectedPositionLogic = SearchFilterLogic.All;
        RebuildSections();
        Results.Clear();
        StatusText = "Entity changed. Review the filters and run the search.";
    }

    public void SelectCategory(AdvancedSearchCategoryKind category)
    {
        // A category click exits cross-category field-finder mode so the left
        // navigation always has an immediate, predictable effect.
        if (!string.IsNullOrWhiteSpace(FieldSearchText))
            FieldSearchText = string.Empty;

        SelectedCategory = category;
        RebuildSections();
    }

    public async Task RefreshIndexedFieldsAsync(
        SearchEntityKind? entity = null,
        CancellationToken cancellationToken = default)
    {
        if (_indexedFieldProvider is null)
            return;

        var targetEntity = entity ?? SelectedEntity;
        try
        {
            var fields = await _indexedFieldProvider.GetIndexedFieldsAsync(targetEntity, cancellationToken);
            _indexedFields[targetEntity] = fields;

            if (targetEntity == SelectedEntity)
                RebuildSections();

            if (fields.Count > 0 && targetEntity == SelectedEntity)
                StatusText = $"Discovered {fields.Count} additional/data-backed field(s) in the local index.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (targetEntity == SelectedEntity)
                StatusText = $"Indexed-field discovery unavailable: {ex.Message}";
        }
    }

    public void ClearFilters()
    {
        foreach (var field in _fieldState
                     .Where(pair => pair.Key.Entity == SelectedEntity)
                     .Select(pair => pair.Value))
        {
            field.Clear();
        }

        Results.Clear();
        SelectedLogic = SearchFilterLogic.All;
        SelectedPositionLogic = SearchFilterLogic.All;
        StatusText = "Filters cleared for the selected entity.";
    }

    public void SetStatus(string message) => StatusText = message;

    public AdvancedSearchPreset? CreatePreset(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText = "Enter a name for this saved search.";
            return null;
        }

        var conditions = new List<SearchFilterCondition>();
        var inputIssues = new List<string>();
        foreach (var field in _fieldState
                     .Where(pair => pair.Key.Entity == SelectedEntity)
                     .Select(pair => pair.Value))
        {
            if (!field.IsEnabled)
                continue;

            if (!TryBuildCondition(field, out var condition, out var issue))
            {
                if (!string.IsNullOrWhiteSpace(issue))
                    inputIssues.Add($"{field.DisplayName}: {issue}");
                continue;
            }

            if (condition is not null)
                conditions.Add(condition);
        }

        if (inputIssues.Count > 0)
        {
            StatusText = string.Join("  ·  ", inputIssues);
            return null;
        }

        var query = new AdvancedSearchQuery(
            SelectedEntity, SelectedLogic, conditions, SelectedPositionLogic, GetIndexedFieldsFor(SelectedEntity));
        var validation = AdvancedSearchQueryValidator.Validate(query);
        if (!validation.IsValid)
        {
            StatusText = string.Join("  ·  ", validation.Issues.Select(issue => issue.Message));
            return null;
        }

        StatusText = $"Saved search “{name.Trim()}” is ready to write.";
        return new AdvancedSearchPreset(
            Version: 1,
            Name: name.Trim(),
            Entity: SelectedEntity,
            Logic: SelectedLogic,
            PositionLogic: SelectedPositionLogic,
            Conditions: conditions);
    }

    public bool ApplyPreset(AdvancedSearchPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (preset.Version != 1)
        {
            StatusText = $"Preset version {preset.Version} is not supported.";
            return false;
        }

        if (!Enum.IsDefined(preset.Entity) ||
            !Enum.IsDefined(preset.Logic) ||
            !Enum.IsDefined(preset.PositionLogic))
        {
            StatusText = "The saved search contains an unknown entity or matching mode.";
            return false;
        }

        var validation = AdvancedSearchQueryValidator.Validate(
            new AdvancedSearchQuery(
                preset.Entity, preset.Logic, preset.Conditions, preset.PositionLogic, GetIndexedFieldsFor(preset.Entity)));
        if (!validation.IsValid)
        {
            StatusText = $"The saved search is invalid: {string.Join("  ·  ", validation.Issues.Select(issue => issue.Message))}";
            return false;
        }

        FieldSearchText = string.Empty;
        SelectEntity(preset.Entity);
        foreach (var field in _fieldState
                     .Where(pair => pair.Key.Entity == SelectedEntity)
                     .Select(pair => pair.Value))
        {
            field.Clear();
        }

        SelectedLogic = preset.Logic;
        SelectedPositionLogic = preset.PositionLogic;

        var definitions = GetAllFieldDefinitions(SelectedEntity)
            .ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);

        var skipped = new List<string>();
        foreach (var condition in preset.Conditions)
        {
            if (!definitions.TryGetValue(condition.FieldKey, out var definition) || !definition.CanExecute)
            {
                skipped.Add(condition.FieldKey);
                continue;
            }

            var field = GetOrCreateFieldModel(definition);
            field.Clear();
            field.MinimumText = condition.Minimum?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            field.MaximumText = condition.Maximum?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            field.TextValue = condition.TextValue ?? string.Empty;
            field.DateMinimumText = condition.DateMinimum?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
            field.DateMaximumText = condition.DateMaximum?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

            if (definition.ValueKind == SearchFieldValueKind.Boolean)
            {
                field.BooleanSelection = condition.Operator switch
                {
                    SearchFilterOperator.IsTrue => 1,
                    SearchFilterOperator.IsFalse => 2,
                    SearchFilterOperator.IsUnknown => 3,
                    _ => 0
                };
            }
            else if (definition.ValueKind == SearchFieldValueKind.TraitSelector)
            {
                field.IncludeTrait = condition.Operator == SearchFilterOperator.HasTrait;
            }
        }

        var firstCategory = preset.Conditions
            .Select(condition => definitions.GetValueOrDefault(condition.FieldKey)?.Category)
            .FirstOrDefault(category => category is not null);
        SelectedCategory = firstCategory ?? AdvancedSearchCategoryKind.General;
        RebuildSections();
        Results.Clear();
        StatusText = skipped.Count == 0
            ? $"Loaded saved search “{preset.Name}”. Review the filters, then run Search."
            : $"Loaded “{preset.Name}”; skipped unsupported fields: {string.Join(", ", skipped)}.";
        return skipped.Count == 0;
    }

    public async Task RunSearchAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;

        await RefreshIndexedFieldsAsync(cancellationToken: cancellationToken);

        var conditions = new List<SearchFilterCondition>();
        var inputIssues = new List<string>();

        foreach (var field in _fieldState
                     .Where(pair => pair.Key.Entity == SelectedEntity)
                     .Select(pair => pair.Value))
        {
            if (!field.IsEnabled)
                continue;

            if (!TryBuildCondition(field, out var condition, out var issue))
            {
                if (!string.IsNullOrWhiteSpace(issue))
                    inputIssues.Add($"{field.DisplayName}: {issue}");
                continue;
            }

            if (condition is not null)
                conditions.Add(condition);
        }

        if (inputIssues.Count > 0)
        {
            StatusText = string.Join("  ·  ", inputIssues);
            return;
        }

        var query = new AdvancedSearchQuery(
            SelectedEntity, SelectedLogic, conditions, SelectedPositionLogic, GetIndexedFieldsFor(SelectedEntity));
        var validation = AdvancedSearchQueryValidator.Validate(query);
        if (!validation.IsValid)
        {
            StatusText = string.Join("  ·  ", validation.Issues.Select(issue => issue.Message));
            return;
        }

        IsBusy = true;
        StatusText = "Searching imported records…";
        Results.Clear();

        try
        {
            var results = await _searchProvider.ExecuteAsync(query, limit: 200, cancellationToken);

            foreach (var result in results)
                Results.Add(new AdvancedSearchResultViewModel(result));

            StatusText = results.Count switch
            {
                0 => "No matching imported records were found.",
                1 => "1 matching record.",
                _ => $"{results.Count} matching records."
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Search cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildSections()
    {
        Sections.Clear();

        var allFields = GetAllFieldDefinitions(SelectedEntity);
        var isFieldSearch = !string.IsNullOrWhiteSpace(FieldSearchText);
        IEnumerable<AdvancedSearchFieldDefinition> fields = isFieldSearch
            ? allFields
            : allFields.Where(field => field.Category == SelectedCategory);

        var search = FieldSearchText.Trim();
        var filteredFields = (isFieldSearch
                ? fields.Where(field =>
                    field.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    field.Key.Contains(search, StringComparison.OrdinalIgnoreCase))
                : fields)
            .GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (filteredFields.Length == 0)
        {
            Sections.Add(new AdvancedSearchSectionViewModel(
                isFieldSearch ? "NO MATCHING FIELDS" : "NO DOCUMENTED FILTERS",
                isFieldSearch
                    ? $"No {SelectedEntity.ToString().ToLowerInvariant()} filters match “{search}”. Try another field name or key."
                    : "There are no documented fields for this entity and category. No substitute fields have been invented.",
                Array.Empty<AdvancedSearchFieldViewModel>()));
            return;
        }

        if (isFieldSearch)
        {
            foreach (var categoryGroup in filteredFields.GroupBy(field => field.Category))
            {
                var categoryName = AdvancedSearchCatalog.Categories
                    .First(category => category.Kind == categoryGroup.Key).DisplayName.ToUpperInvariant();
                var fieldModels = categoryGroup.Select(GetOrCreateFieldModel).ToArray();
                Sections.Add(new AdvancedSearchSectionViewModel(
                    $"{categoryName} · {fieldModels.Length} MATCHING",
                    string.Empty,
                    fieldModels));
            }

            return;
        }

        foreach (var group in filteredFields.GroupBy(GetSectionTitle))
        {
            var fieldModels = group.Select(GetOrCreateFieldModel).ToArray();
            Sections.Add(new AdvancedSearchSectionViewModel(group.Key, string.Empty, fieldModels));
        }
    }

    private IReadOnlyList<AdvancedSearchFieldDefinition> GetIndexedFieldsFor(SearchEntityKind entity) =>
        _indexedFields.TryGetValue(entity, out var fields)
            ? fields
            : Array.Empty<AdvancedSearchFieldDefinition>();

    private IReadOnlyList<AdvancedSearchFieldDefinition> GetAllFieldDefinitions(SearchEntityKind entity)
    {
        var fields = AdvancedSearchCatalog.GetAllFields(entity)
            .ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var indexed in GetIndexedFieldsFor(entity))
            fields[indexed.Key] = indexed;

        return fields.Values
            .OrderBy(field => field.Category)
            .ThenBy(field => field.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private AdvancedSearchFieldViewModel GetOrCreateFieldModel(AdvancedSearchFieldDefinition definition)
    {
        var key = (SelectedEntity, definition.Key);
        if (_fieldState.TryGetValue(key, out var existing))
        {
            existing.RefreshDefinition(definition);
            return existing;
        }

        var created = new AdvancedSearchFieldViewModel(definition);
        _fieldState.Add(key, created);
        return created;
    }

    private string GetSectionTitle(AdvancedSearchFieldDefinition field)
    {
        var key = field.Key;

        return SelectedCategory switch
        {
            AdvancedSearchCategoryKind.General when key.StartsWith("reputation_", StringComparison.OrdinalIgnoreCase)
                => "REPUTATION",
            AdvancedSearchCategoryKind.General when key is "current_ability" or "potential_ability" or "ability_difference" or "recommended_ca"
                => "ABILITY",
            AdvancedSearchCategoryKind.General when key == "position_familiarity" ||
                key.StartsWith("position_", StringComparison.OrdinalIgnoreCase)
                => "PLAYER POSITIONS",
            AdvancedSearchCategoryKind.General when key is "left_foot" or "right_foot"
                => "FOOT RATINGS",
            AdvancedSearchCategoryKind.General when key is "is_newgen" or "wonderkid"
                => "PLAYER ORIGIN / DEVELOPMENT",
            AdvancedSearchCategoryKind.General when key.StartsWith("role_preference_", StringComparison.OrdinalIgnoreCase)
                => "STAFF ROLE PREFERENCES",
            AdvancedSearchCategoryKind.General when key is "gender" or "game_uid"
                => "IDENTITY / DATABASE",
            AdvancedSearchCategoryKind.General when key is "division" or "team" or "training_status_club" or "training_status_nation"
                => "TEAM / DIVISION / TRAINING",
            AdvancedSearchCategoryKind.General when key.StartsWith("club_", StringComparison.OrdinalIgnoreCase)
                => "CLUB INFORMATION",
            AdvancedSearchCategoryKind.General
                => "PERSONAL DETAILS",

            AdvancedSearchCategoryKind.Contract when key.StartsWith("club_", StringComparison.OrdinalIgnoreCase)
                => "CLUB FINANCES",
            AdvancedSearchCategoryKind.Contract
                => "CONTRACT / VALUE",

            AdvancedSearchCategoryKind.TechnicalGoalkeeping
                when key is "coaching_attributes" or "scouting_attributes" or "training_rating"
                => "COACHING / SCOUTING",
            AdvancedSearchCategoryKind.TechnicalGoalkeeping
                when IsGoalkeepingField(key)
                => "GOALKEEPING ATTRIBUTES",
            AdvancedSearchCategoryKind.TechnicalGoalkeeping
                => "TECHNICAL ATTRIBUTES",

            AdvancedSearchCategoryKind.MentalAttributes when key.StartsWith("personality_", StringComparison.OrdinalIgnoreCase)
                => "PERSONALITY",
            AdvancedSearchCategoryKind.MentalAttributes
                => "MENTAL ATTRIBUTES",

            AdvancedSearchCategoryKind.PhysicalHidden when IsHiddenField(key)
                => "HIDDEN ATTRIBUTES",
            AdvancedSearchCategoryKind.PhysicalHidden
                => "PHYSICAL ATTRIBUTES",

            AdvancedSearchCategoryKind.PreferredMoves => "PREFERRED MOVES / TRAITS",
            AdvancedSearchCategoryKind.Relations => "RELATIONSHIPS",
            _ => "FILTERS"
        };
    }

    private static bool TryBuildCondition(
        AdvancedSearchFieldViewModel field,
        out SearchFilterCondition? condition,
        out string? issue)
    {
        condition = null;
        issue = null;

        switch (field.ValueKind)
        {
            case SearchFieldValueKind.IntegerRange:
            case SearchFieldValueKind.DecimalRange:
            {
                if (string.IsNullOrWhiteSpace(field.MinimumText) &&
                    string.IsNullOrWhiteSpace(field.MaximumText))
                    return true;

                if (!TryParseDecimal(field.MinimumText, out var minimum) ||
                    !TryParseDecimal(field.MaximumText, out var maximum))
                {
                    issue = "Use numeric minimum and maximum values.";
                    return false;
                }

                condition = new SearchFilterCondition(field.Key, SearchFilterOperator.Range, minimum, maximum);
                return true;
            }

            case SearchFieldValueKind.PositionRating:
            {
                if (string.IsNullOrWhiteSpace(field.TextValue) &&
                    string.IsNullOrWhiteSpace(field.MinimumText) &&
                    string.IsNullOrWhiteSpace(field.MaximumText))
                    return true;

                if (string.IsNullOrWhiteSpace(field.TextValue))
                {
                    issue = "Enter a position code (for example DR or ST).";
                    return false;
                }

                if (!TryParseDecimal(field.MinimumText, out var minimum) ||
                    !TryParseDecimal(field.MaximumText, out var maximum))
                {
                    issue = "Use numeric minimum and maximum familiarity values.";
                    return false;
                }

                condition = new SearchFilterCondition(
                    field.Key,
                    SearchFilterOperator.Range,
                    minimum,
                    maximum,
                    field.TextValue.Trim());
                return true;
            }

            case SearchFieldValueKind.Boolean:
            {
                var op = field.BooleanSelection switch
                {
                    1 => SearchFilterOperator.IsTrue,
                    2 => SearchFilterOperator.IsFalse,
                    3 => SearchFilterOperator.IsUnknown,
                    _ => (SearchFilterOperator?)null
                };
                if (op is null)
                    return true;

                condition = new SearchFilterCondition(field.Key, op.Value);
                return true;
            }

            case SearchFieldValueKind.Enum:
            {
                if (string.IsNullOrWhiteSpace(field.TextValue))
                    return true;

                condition = new SearchFilterCondition(
                    field.Key,
                    SearchFilterOperator.Equals,
                    TextValue: field.TextValue.Trim());
                return true;
            }

            case SearchFieldValueKind.Text:
            case SearchFieldValueKind.EntityLookup:
            {
                if (string.IsNullOrWhiteSpace(field.TextValue))
                    return true;

                condition = new SearchFilterCondition(
                    field.Key,
                    SearchFilterOperator.Contains,
                    TextValue: field.TextValue.Trim());
                return true;
            }

            case SearchFieldValueKind.DateRange:
            {
                if (string.IsNullOrWhiteSpace(field.DateMinimumText) &&
                    string.IsNullOrWhiteSpace(field.DateMaximumText))
                    return true;

                if (!TryParseDate(field.DateMinimumText, out var minimum) ||
                    !TryParseDate(field.DateMaximumText, out var maximum))
                {
                    issue = "Use dates in YYYY-MM-DD format.";
                    return false;
                }

                condition = new SearchFilterCondition(
                    field.Key,
                    SearchFilterOperator.Range,
                    DateMinimum: minimum,
                    DateMaximum: maximum);
                return true;
            }

            case SearchFieldValueKind.TraitSelector:
            {
                if (string.IsNullOrWhiteSpace(field.TextValue))
                    return true;

                condition = new SearchFilterCondition(
                    field.Key,
                    field.IncludeTrait ? SearchFilterOperator.HasTrait : SearchFilterOperator.LacksTrait,
                    TextValue: field.TextValue.Trim());
                return true;
            }

            default:
                return true;
        }
    }

    private static bool TryParseDecimal(string? text, out decimal? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseDate(string? text, out DateOnly? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static bool IsGoalkeepingField(string key)
    {
        const string prefix = "attribute_";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var attributeKey = key[prefix.Length..];
        return PlayerAttributeCatalog.GetProfileGroups(true)
            .Where(group => group.Kind == PlayerAttributeGroupKind.Goalkeeping)
            .SelectMany(group => group.Attributes)
            .Any(attribute => string.Equals(attribute.Key, attributeKey, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHiddenField(string key)
    {
        const string prefix = "attribute_";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var attributeKey = key[prefix.Length..];
        return PlayerAttributeCatalog.GetProfileGroups(false)
            .Where(group => group.Kind == PlayerAttributeGroupKind.Hidden)
            .SelectMany(group => group.Attributes)
            .Any(attribute => string.Equals(attribute.Key, attributeKey, StringComparison.OrdinalIgnoreCase));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed record AdvancedSearchSectionViewModel(
    string Title,
    string EmptyMessage,
    IReadOnlyList<AdvancedSearchFieldViewModel> Fields);

public sealed class AdvancedSearchFieldViewModel : INotifyPropertyChanged
{
    private string _minimumText = string.Empty;
    private string _maximumText = string.Empty;
    private string _textValue = string.Empty;
    private string _dateMinimumText = string.Empty;
    private string _dateMaximumText = string.Empty;
    private bool _includeTrait = true;
    private int _booleanSelection;
    private bool _isEnabled;

    public AdvancedSearchFieldViewModel(AdvancedSearchFieldDefinition definition) =>
        RefreshDefinition(definition);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string ControlKind { get; private set; } = "Text";
    public string EvidenceStatus { get; private set; } = string.Empty;
    public string ImplementationStatus { get; private set; } = string.Empty;
    public string HelpText { get; private set; } = string.Empty;
    public string BooleanTrueLabel { get; private set; } = "Yes";
    public string BooleanFalseLabel { get; private set; } = "No";
    public string BooleanUnknownLabel { get; private set; } = "Unknown";
    public SearchFieldValueKind ValueKind { get; private set; }

    public string MinimumText { get => _minimumText; set => SetField(ref _minimumText, value); }
    public string MaximumText { get => _maximumText; set => SetField(ref _maximumText, value); }
    public string TextValue { get => _textValue; set => SetField(ref _textValue, value); }
    public string DateMinimumText { get => _dateMinimumText; set => SetField(ref _dateMinimumText, value); }
    public string DateMaximumText { get => _dateMaximumText; set => SetField(ref _dateMaximumText, value); }
    public bool IncludeTrait { get => _includeTrait; set => SetField(ref _includeTrait, value); }
    /// <summary>0 = any, 1 = true, 2 = false, 3 = unknown/unavailable.</summary>
    public int BooleanSelection { get => _booleanSelection; set => SetField(ref _booleanSelection, value); }

    public bool IsEnabled
    {
        get => _isEnabled;
        private set => SetField(ref _isEnabled, value);
    }

    public void Clear()
    {
        MinimumText = string.Empty;
        MaximumText = string.Empty;
        TextValue = string.Empty;
        DateMinimumText = string.Empty;
        DateMaximumText = string.Empty;
        IncludeTrait = true;
        BooleanSelection = 0;
    }

    public void RefreshDefinition(AdvancedSearchFieldDefinition definition)
    {
        Key = definition.Key;
        DisplayName = definition.DisplayName;
        EvidenceStatus = definition.EvidenceStatus.ToString();
        ImplementationStatus = definition.EvidenceStatus == FieldEvidenceStatus.Mapped
            ? "INDEXED"
            : definition.ImplementationStatus == SearchImplementationStatus.Ready
                ? "READY"
                : "IN PROGRESS";
        HelpText = definition.Key.Equals("is_newgen", StringComparison.OrdinalIgnoreCase)
            ? "Yes = the data source explicitly identifies this record as generated. No = the source explicitly confirms an original database person. Unknown = not classified. FM Atlas will not guess from age, PA, face or name."
            : definition.EvidenceStatus == FieldEvidenceStatus.Mapped
                ? $"{definition.DisplayName} · discovered from values already in the local index · not proof of a live FM27 read."
                : $"{definition.DisplayName} · {definition.ValueKind} · Evidence: {definition.EvidenceStatus} · Query: {ImplementationStatus}";
        if (definition.Key.Equals("is_newgen", StringComparison.OrdinalIgnoreCase))
        {
            BooleanTrueLabel = "Generated / newgen";
            BooleanFalseLabel = "Original database";
            BooleanUnknownLabel = "Unknown / unclassified";
        }
        else
        {
            BooleanTrueLabel = "Yes";
            BooleanFalseLabel = "No";
            BooleanUnknownLabel = "Unknown";
        }
        ValueKind = definition.ValueKind;
        IsEnabled = definition.CanExecute;
        ControlKind = definition.ValueKind switch
        {
            SearchFieldValueKind.IntegerRange or SearchFieldValueKind.DecimalRange => "Range",
            SearchFieldValueKind.PositionRating => "Position",
            SearchFieldValueKind.Boolean => "Boolean",
            SearchFieldValueKind.DateRange => "Date",
            SearchFieldValueKind.Enum => "Enum",
            SearchFieldValueKind.TraitSelector => "Trait",
            _ => "Text"
        };

        OnPropertyChanged(nameof(Key));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(EvidenceStatus));
        OnPropertyChanged(nameof(ImplementationStatus));
        OnPropertyChanged(nameof(HelpText));
        OnPropertyChanged(nameof(BooleanTrueLabel));
        OnPropertyChanged(nameof(BooleanFalseLabel));
        OnPropertyChanged(nameof(BooleanUnknownLabel));
        OnPropertyChanged(nameof(ValueKind));
        OnPropertyChanged(nameof(ControlKind));
        OnPropertyChanged(nameof(BooleanSelection));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class AdvancedSearchResultViewModel
{
    public AdvancedSearchResultViewModel(EntitySearchResult result)
    {
        DisplayName = result.DisplayName;
        EntityType = result.Identity.Kind.ToString().ToUpperInvariant();
        SourceId = result.Identity.SourceId;
        Provider = result.Identity.ProviderKey;
    }

    public string DisplayName { get; }
    public string EntityType { get; }
    public string SourceId { get; }
    public string Provider { get; }
}
