using FMAtlas.Domain.Players;

namespace FMAtlas.Domain.Search;

public enum SearchEntityKind
{
    Player,
    Coach,
    Club
}

public enum AdvancedSearchCategoryKind
{
    General,
    Contract,
    TechnicalGoalkeeping,
    MentalAttributes,
    PhysicalHidden,
    PreferredMoves,
    Relations
}

public enum SearchFieldValueKind
{
    Text,
    IntegerRange,
    DecimalRange,
    Boolean,
    DateRange,
    EntityLookup,
    Enum,
    PositionRating,
    TraitSelector
}

public enum SearchImplementationStatus
{
    InProgress,
    Ready
}

public sealed record AdvancedSearchCategoryDefinition(
    AdvancedSearchCategoryKind Kind,
    string DisplayName,
    int Order);

/// <summary>
/// A documented search field. Evidence that a field exists is deliberately separate
/// from whether FM Atlas can currently read it or execute a query against it.
/// </summary>
public sealed record AdvancedSearchFieldDefinition(
    string Key,
    string DisplayName,
    AdvancedSearchCategoryKind Category,
    SearchFieldValueKind ValueKind,
    IReadOnlyList<SearchEntityKind> SupportedEntities,
    FieldEvidenceStatus EvidenceStatus = FieldEvidenceStatus.Documented,
    SearchImplementationStatus ImplementationStatus = SearchImplementationStatus.InProgress)
{
    public bool CanExecute => ImplementationStatus == SearchImplementationStatus.Ready;
}

/// <summary>
/// Source-backed field catalogue for the REF-002 FMRTE-style Advanced Search dialog.
/// This is a schema inventory only: all fields remain In Progress until a real provider,
/// field mapping and query implementation are tested.
/// </summary>
public static class AdvancedSearchCatalog
{
    private static readonly IReadOnlyList<SearchEntityKind> Players = [SearchEntityKind.Player];
    private static readonly IReadOnlyList<SearchEntityKind> PlayersAndCoaches =
        [SearchEntityKind.Player, SearchEntityKind.Coach];
    private static readonly IReadOnlyList<SearchEntityKind> AllEntities =
        [SearchEntityKind.Player, SearchEntityKind.Coach, SearchEntityKind.Club];

    public static IReadOnlyList<AdvancedSearchCategoryDefinition> Categories { get; } =
    [
        new(AdvancedSearchCategoryKind.General, "General", 0),
        new(AdvancedSearchCategoryKind.Contract, "Contract", 1),
        new(AdvancedSearchCategoryKind.TechnicalGoalkeeping, "Technical/GK", 2),
        new(AdvancedSearchCategoryKind.MentalAttributes, "Mental Attributes", 3),
        new(AdvancedSearchCategoryKind.PhysicalHidden, "Physical/Hidden", 4),
        new(AdvancedSearchCategoryKind.PreferredMoves, "Preferred Moves", 5),
        new(AdvancedSearchCategoryKind.Relations, "Relations", 6)
    ];

    private static readonly IReadOnlyList<AdvancedSearchFieldDefinition> GeneralFields =
    [
        F("name", "Name", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Text, AllEntities),
        F("age", "Age", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches),
        F("nationality", "Nationality", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, PlayersAndCoaches),
        F("second_nationality", "Second Nationality", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Players),
        F("continent", "Continent", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, PlayersAndCoaches),
        F("region", "Region", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, PlayersAndCoaches),
        F("city_of_birth", "City of Birth", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Text, Players),
        F("ethnicity", "Ethnicity", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Players),
        F("eu_nationality", "EU Nationality", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Boolean, Players),
        F("international_caps", "International Caps", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Players),
        F("international_goals", "International Goals", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Players),
        F("height_cm", "Height (cm)", AdvancedSearchCategoryKind.General, SearchFieldValueKind.DecimalRange, Players),
        F("weight_kg", "Weight (kg)", AdvancedSearchCategoryKind.General, SearchFieldValueKind.DecimalRange, Players),
        F("division", "Division", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Players),
        F("team", "Team / Club", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, PlayersAndCoaches),
        F("wonderkid", "Wonderkid", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Boolean, Players),
        F("regenerated", "Regenerated Player", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Boolean, Players),
        F("reputation_home", "Home Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches),
        F("reputation_current", "Current Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches),
        F("reputation_world", "World Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches),
        F("current_ability", "CA", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches),
        F("potential_ability", "PA", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches),
        F("ability_difference", "Difference", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches),
        F("recommended_ca", "Recommended CA / RCA", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Players),
        F("position_familiarity", "Player Positions", AdvancedSearchCategoryKind.General, SearchFieldValueKind.PositionRating, Players),
        F("training_status_club", "Training Status at Club", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Players),
        F("training_status_nation", "Training Status at Nation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Players),
        F("club_facilities", "Club Facilities", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, [SearchEntityKind.Club]),
        F("club_reputation", "Club Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, [SearchEntityKind.Club])
    ];

    private static readonly IReadOnlyList<AdvancedSearchFieldDefinition> ContractFields =
    [
        F("weekly_wage", "Weekly Wage", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, PlayersAndCoaches),
        F("transfer_value", "Transfer Value", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Players),
        F("contract_expiry", "Contract Expiry", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DateRange, PlayersAndCoaches),
        F("contract_status", "Contract Status", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.Enum, Players),
        F("release_clause", "Release Clause", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.IntegerRange, Players),
        F("club_balance", "Club Balance", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.IntegerRange, [SearchEntityKind.Club]),
        F("club_debt", "Club Debt", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.IntegerRange, [SearchEntityKind.Club]),
        F("club_transfer_budget", "Transfer Budget", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.IntegerRange, [SearchEntityKind.Club]),
        F("club_wage_budget", "Wage Budget", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.IntegerRange, [SearchEntityKind.Club])
    ];

    private static readonly IReadOnlyList<AdvancedSearchFieldDefinition> CoachFields =
    [
        F("coaching_attributes", "Coaching Attributes", AdvancedSearchCategoryKind.TechnicalGoalkeeping, SearchFieldValueKind.IntegerRange, [SearchEntityKind.Coach]),
        F("scouting_attributes", "Scouting Attributes", AdvancedSearchCategoryKind.TechnicalGoalkeeping, SearchFieldValueKind.IntegerRange, [SearchEntityKind.Coach]),
        F("training_rating", "Training Rating", AdvancedSearchCategoryKind.TechnicalGoalkeeping, SearchFieldValueKind.IntegerRange, [SearchEntityKind.Coach])
    ];

    private static readonly IReadOnlyList<AdvancedSearchFieldDefinition> PreferredMoveFields =
    [
        F("preferred_moves", "Preferred Moves / Traits", AdvancedSearchCategoryKind.PreferredMoves, SearchFieldValueKind.TraitSelector, Players)
    ];

    private static readonly IReadOnlyList<AdvancedSearchFieldDefinition> RelationFields =
    [
        F("relations", "Relationships", AdvancedSearchCategoryKind.Relations, SearchFieldValueKind.EntityLookup, AllEntities)
    ];

    public static IReadOnlyList<AdvancedSearchFieldDefinition> Fields { get; } = BuildFields();

    public static IReadOnlyList<AdvancedSearchFieldDefinition> GetFields(
        SearchEntityKind entity,
        AdvancedSearchCategoryKind category) =>
        Fields.Where(field => field.Category == category && field.SupportedEntities.Contains(entity)).ToArray();

    public static IReadOnlyList<AdvancedSearchFieldDefinition> GetAttributeFields(
        SearchEntityKind entity,
        AdvancedSearchCategoryKind category)
    {
        if (entity != SearchEntityKind.Player)
            return Array.Empty<AdvancedSearchFieldDefinition>();

        var group = category switch
        {
            AdvancedSearchCategoryKind.TechnicalGoalkeeping => PlayerAttributeGroupKind.Technical,
            AdvancedSearchCategoryKind.MentalAttributes => PlayerAttributeGroupKind.Mental,
            AdvancedSearchCategoryKind.PhysicalHidden => PlayerAttributeGroupKind.Physical,
            _ => (PlayerAttributeGroupKind?)null
        };

        if (group is null)
            return Array.Empty<AdvancedSearchFieldDefinition>();

        var attributes = PlayerAttributeCatalog.GetProfileGroups(false)
            .Where(x => x.Kind == group.Value)
            .SelectMany(x => x.Attributes)
            .Select(attribute => F(
                $"attribute_{attribute.Key}",
                attribute.DisplayName,
                category,
                SearchFieldValueKind.IntegerRange,
                Players))
            .ToList();

        if (category == AdvancedSearchCategoryKind.MentalAttributes)
        {
            attributes.AddRange(PlayerAttributeCatalog.PersonalityAttributes
                .Select(attribute => F(
                    $"personality_{attribute.Key}",
                    $"Personality: {attribute.DisplayName}",
                    category,
                    SearchFieldValueKind.IntegerRange,
                    Players)));
        }

        if (category == AdvancedSearchCategoryKind.TechnicalGoalkeeping)
        {
            attributes.AddRange(PlayerAttributeCatalog.GetProfileGroups(true)
                .Where(x => x.Kind == PlayerAttributeGroupKind.Goalkeeping)
                .SelectMany(x => x.Attributes)
                .Select(attribute => F(
                    $"attribute_{attribute.Key}",
                    attribute.DisplayName,
                    category,
                    SearchFieldValueKind.IntegerRange,
                    Players)));
        }
        else if (category == AdvancedSearchCategoryKind.PhysicalHidden)
        {
            attributes.AddRange(PlayerAttributeCatalog.GetProfileGroups(false)
                .Where(x => x.Kind == PlayerAttributeGroupKind.Hidden)
                .SelectMany(x => x.Attributes)
                .Select(attribute => F(
                    $"attribute_{attribute.Key}",
                    attribute.DisplayName,
                    category,
                    SearchFieldValueKind.IntegerRange,
                    Players)));
        }

        return attributes
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    private static IReadOnlyList<AdvancedSearchFieldDefinition> BuildFields()
    {
        var fields = new List<AdvancedSearchFieldDefinition>();
        fields.AddRange(GeneralFields);
        fields.AddRange(ContractFields);
        fields.AddRange(CoachFields);
        fields.AddRange(PreferredMoveFields);
        fields.AddRange(RelationFields);
        return fields.AsReadOnly();
    }

    private static AdvancedSearchFieldDefinition F(
        string key,
        string label,
        AdvancedSearchCategoryKind category,
        SearchFieldValueKind valueKind,
        IReadOnlyList<SearchEntityKind> entities)
    {
        // These are the only fields backed by typed records and a tested query
        // implementation in the initial search provider. All other documented
        // fields remain visible in the UI but explicitly In Progress.
        var ready =
            key is "name" or "age" or "nationality" or "second_nationality" or "team" or
                "height_cm" or "weight_kg" or "current_ability" or "potential_ability" or
                "weekly_wage" or "transfer_value" or "contract_expiry" or
                "position_familiarity" or "preferred_moves" or "club_reputation" ||
            key.StartsWith("attribute_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("personality_", StringComparison.OrdinalIgnoreCase);

        return new AdvancedSearchFieldDefinition(
            key,
            label,
            category,
            valueKind,
            entities,
            FieldEvidenceStatus.Documented,
            ready ? SearchImplementationStatus.Ready : SearchImplementationStatus.InProgress);
    }
}
