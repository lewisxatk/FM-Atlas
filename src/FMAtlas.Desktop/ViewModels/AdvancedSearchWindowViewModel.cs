using System.Collections.ObjectModel;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Search;

namespace FMAtlas.Desktop.ViewModels;

/// <summary>
/// Drives the REF-002 Advanced Search shell. The catalogue describes fields,
/// but no filter is enabled until a real provider and query handler are tested.
/// </summary>
public sealed class AdvancedSearchWindowViewModel
{
    public AdvancedSearchWindowViewModel()
    {
        SelectEntity(SearchEntityKind.Player);
    }

    public ObservableCollection<AdvancedSearchSectionViewModel> Sections { get; } = [];

    public SearchEntityKind SelectedEntity { get; private set; } = SearchEntityKind.Player;
    public AdvancedSearchCategoryKind SelectedCategory { get; private set; } = AdvancedSearchCategoryKind.General;

    public bool ShowStaticGeneral =>
        SelectedEntity == SearchEntityKind.Player &&
        SelectedCategory == AdvancedSearchCategoryKind.General;

    public void SelectEntity(SearchEntityKind entity)
    {
        SelectedEntity = entity;
        RebuildSections();
    }

    public void SelectCategory(AdvancedSearchCategoryKind category)
    {
        SelectedCategory = category;
        RebuildSections();
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
            var fieldModels = group.Select(field => new AdvancedSearchFieldViewModel(
                field.Key,
                field.DisplayName,
                GetControlKind(field.ValueKind),
                field.EvidenceStatus.ToString(),
                field.ImplementationStatus.ToString(),
                false)).ToArray();

            Sections.Add(new AdvancedSearchSectionViewModel(
                group.Key,
                string.Empty,
                fieldModels));
        }
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
            AdvancedSearchCategoryKind.General when key == "position_familiarity"
                => "PLAYER POSITIONS",
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

    private static string GetControlKind(SearchFieldValueKind kind) => kind switch
    {
        SearchFieldValueKind.IntegerRange or SearchFieldValueKind.DecimalRange or SearchFieldValueKind.PositionRating
            => "Range",
        SearchFieldValueKind.Boolean
            => "Boolean",
        SearchFieldValueKind.DateRange
            => "Date",
        _ => "Text"
    };
}

public sealed record AdvancedSearchSectionViewModel(
    string Title,
    string EmptyMessage,
    IReadOnlyList<AdvancedSearchFieldViewModel> Fields);

public sealed record AdvancedSearchFieldViewModel(
    string Key,
    string DisplayName,
    string ControlKind,
    string EvidenceStatus,
    string ImplementationStatus,
    bool IsEnabled);
