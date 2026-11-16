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
    private readonly Dictionary<(SearchEntityKind Entity, string Key), AdvancedSearchFieldViewModel> _fieldState = [];
    private SearchEntityKind _selectedEntity = SearchEntityKind.Player;
    private AdvancedSearchCategoryKind _selectedCategory = AdvancedSearchCategoryKind.General;
    private SearchFilterLogic _selectedLogic = SearchFilterLogic.All;
    private SearchFilterLogic _selectedPositionLogic = SearchFilterLogic.All;
    private string _statusText = "Enter criteria in supported fields, then run the search.";
    private bool _isBusy;

    public AdvancedSearchWindowViewModel(IAdvancedSearchProvider searchProvider)
    {
        _searchProvider = searchProvider ?? throw new ArgumentNullException(nameof(searchProvider));
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

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public void SelectEntity(SearchEntityKind entity)
    {
        SelectedEntity = entity;
        RebuildSections();
        Results.Clear();
        StatusText = "Entity changed. Review the filters and run the search.";
    }

    public void SelectCategory(AdvancedSearchCategoryKind category)
    {
        SelectedCategory = category;
        RebuildSections();
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

    public async Task RunSearchAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;

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

        var query = new AdvancedSearchQuery(SelectedEntity, SelectedLogic, conditions, SelectedPositionLogic);
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

        var fields = AdvancedSearchCatalog.GetFields(SelectedEntity, SelectedCategory)
            .Concat(AdvancedSearchCatalog.GetAttributeFields(SelectedEntity, SelectedCategory))
            .ToArray();

        if (fields.Length == 0)
        {
            Sections.Add(new AdvancedSearchSectionViewModel(
                "NO DOCUMENTED FILTERS",
                "There are no documented fields for this entity and category. No substitute fields have been invented.",
                Array.Empty<AdvancedSearchFieldViewModel>()));
            return;
        }

        foreach (var group in fields.GroupBy(GetSectionTitle))
        {
            var fieldModels = group.Select(GetOrCreateFieldModel).ToArray();
            Sections.Add(new AdvancedSearchSectionViewModel(group.Key, string.Empty, fieldModels));
        }
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
        ImplementationStatus = definition.ImplementationStatus == SearchImplementationStatus.Ready
            ? "READY"
            : "IN PROGRESS";
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
