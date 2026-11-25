using FMAtlas.Domain.Players;

namespace FMAtlas.Domain.Search;

public enum SearchEntityKind { Player, Coach, Club }
public enum AdvancedSearchCategoryKind { General, Contract, TechnicalGoalkeeping, Goalkeeping, MentalAttributes, PhysicalHidden, PreferredMoves, Relations, IndexedFields }
public enum SearchFieldValueKind { Text, IntegerRange, DecimalRange, Boolean, DateRange, EntityLookup, Enum, PositionRating, TraitSelector }
public enum SearchImplementationStatus { InProgress, Ready }

public sealed record AdvancedSearchCategoryDefinition(AdvancedSearchCategoryKind Kind, string DisplayName, int Order);

/// <summary>
/// A source-documented field plus its query implementation and optional value bounds.
/// READY means the local index can query this key; it does not mean a provider has populated it.
/// </summary>
public sealed record AdvancedSearchFieldDefinition(
    string Key,
    string DisplayName,
    AdvancedSearchCategoryKind Category,
    SearchFieldValueKind ValueKind,
    IReadOnlyList<SearchEntityKind> SupportedEntities,
    FieldEvidenceStatus EvidenceStatus = FieldEvidenceStatus.Documented,
    SearchImplementationStatus ImplementationStatus = SearchImplementationStatus.InProgress,
    decimal? MinimumAllowed = null,
    decimal? MaximumAllowed = null)
{
    public bool CanExecute => ImplementationStatus == SearchImplementationStatus.Ready;
}

public static class AdvancedSearchCatalog
{
    private static readonly IReadOnlyList<SearchEntityKind> Players = [SearchEntityKind.Player];
    private static readonly IReadOnlyList<SearchEntityKind> Coaches = [SearchEntityKind.Coach];
    private static readonly IReadOnlyList<SearchEntityKind> Clubs = [SearchEntityKind.Club];
    private static readonly IReadOnlyList<SearchEntityKind> PlayersAndCoaches = [SearchEntityKind.Player, SearchEntityKind.Coach];
    private static readonly IReadOnlyList<SearchEntityKind> AllEntities = [SearchEntityKind.Player, SearchEntityKind.Coach, SearchEntityKind.Club];

    private static readonly (string Key, string Label)[] Positions =
    [
        ("gk", "Goalkeeper"), ("sw", "Sweeper"), ("dl", "Left Back"), ("dc", "Centre Back"),
        ("dr", "Right Back"), ("dm", "Defensive Midfielder"), ("ml", "Left Midfielder"),
        ("mc", "Central Midfielder"), ("mr", "Right Midfielder"), ("aml", "Left Winger"),
        ("amc", "Attacking Midfielder"), ("amr", "Right Winger"), ("st", "Striker"),
        ("wbl", "Left Wing Back"), ("wbr", "Right Wing Back")
    ];

    private static readonly (string Key, string Label)[] CoachTechnical =
    [
        ("attacking", "Attacking"), ("defending", "Defending"), ("fitness", "Fitness"),
        ("goalkeeping", "Goalkeeping"), ("possession", "Possession"), ("set_pieces", "Set Pieces"),
        ("tactical", "Tactical"), ("technical", "Technical"), ("working_with_youngsters", "Working With Youngsters")
    ];

    private static readonly (string Key, string Label)[] CoachMental =
    [
        ("authority", "Authority"), ("determination", "Determination"),
        ("motivating", "Motivating"), ("people_management", "People Management")
    ];

    private static readonly (string Key, string Label)[] CoachKnowledge =
    [
        ("data_analysis", "Data Analysis"), ("judging_ability", "Judging Player Ability"),
        ("judging_potential", "Judging Player Potential"), ("judging_staff", "Judging Staff"),
        ("negotiating", "Negotiating"), ("tactical_knowledge", "Tactical Knowledge")
    ];

    private static readonly (string Key, string Label)[] CoachMedical =
    [
        ("physiotherapy", "Physiotherapy"), ("sports_science", "Sports Science"),
        ("judging_player_data", "Judging Player Data"), ("judging_team_data", "Judging Team Data"),
        ("presenting_data", "Presenting Data")
    ];

    // Staff job-preference ratings documented in the FM27 reverse-engineering reference.
    // The source's group 7 remains unnamed and is intentionally omitted.
    private static readonly (string Key, string Label)[] StaffRolePreferences =
    [
        ("role_preference_manager", "Manager"),
        ("role_preference_assistant_manager", "Assistant Manager"),
        ("role_preference_coach", "Coach"),
        ("role_preference_physio", "Physiotherapist"),
        ("role_preference_scout", "Scout"),
        ("role_preference_goalkeeper_coach", "Goalkeeping Coach"),
        ("role_preference_fitness_coach", "Fitness Coach"),
        ("role_preference_board", "Board / Chairperson"),
        ("role_preference_director_of_football", "Director of Football"),
        ("role_preference_head_of_youth_development", "Head of Youth Development"),
        ("role_preference_loan_manager", "Loan Manager"),
        ("role_preference_analyst", "Analyst"),
        ("role_preference_sports_scientist", "Sports Scientist"),
        ("role_preference_technical_director", "Technical Director")
    ];

    public static IReadOnlyList<AdvancedSearchCategoryDefinition> Categories { get; } =
    [
        new(AdvancedSearchCategoryKind.General, "General", 0),
        new(AdvancedSearchCategoryKind.Contract, "Contract", 1),
        new(AdvancedSearchCategoryKind.TechnicalGoalkeeping, "Technical", 2),
        new(AdvancedSearchCategoryKind.Goalkeeping, "Goalkeeping", 3),
        new(AdvancedSearchCategoryKind.MentalAttributes, "Mental Attributes", 4),
        new(AdvancedSearchCategoryKind.PhysicalHidden, "Physical / Hidden", 5),
        new(AdvancedSearchCategoryKind.PreferredMoves, "Preferred Moves", 6),
        new(AdvancedSearchCategoryKind.Relations, "Relations", 7),
        new(AdvancedSearchCategoryKind.IndexedFields, "Indexed Data", 8)
    ];

    private static readonly IReadOnlyList<AdvancedSearchFieldDefinition> GeneralFields = BuildGeneralFields();
    private static readonly IReadOnlyList<AdvancedSearchFieldDefinition> ContractFields =
    [
        F("weekly_wage", "Weekly Wage", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, PlayersAndCoaches, 0),
        F("transfer_value", "Transfer Value", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Players, 0),
        F("contract_expiry", "Contract Expiry", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DateRange, PlayersAndCoaches),
        F("contract_status", "Contract Status", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.Enum, PlayersAndCoaches),
        F("release_clause", "Release Clause", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Players, 0),
        F("club_balance", "Club Balance", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_debt", "Club Debt", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_transfer_budget", "Transfer Budget", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_wage_budget", "Wage Budget", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_transfer_budget_season", "Season Transfer Budget", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_transfer_budget_remaining", "Transfer Budget Remaining", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_wage_used", "Wage Used", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_max_wage", "Maximum Wage", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_transfer_revenue_percentage", "Transfer Revenue Available (%)", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs, 0, 100),
        F("club_total_debt", "Total Original Debt", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_monthly_debt_repayments", "Monthly Debt Repayments", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_loans", "Club Loans", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs),
        F("club_sponsorship", "Sponsorship Income", AdvancedSearchCategoryKind.Contract, SearchFieldValueKind.DecimalRange, Clubs)
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

    public static IReadOnlyList<AdvancedSearchFieldDefinition> GetFields(SearchEntityKind entity, AdvancedSearchCategoryKind category) =>
        Fields.Where(field => field.Category == category && field.SupportedEntities.Contains(entity)).ToArray();

    /// <summary>All documented core fields, including generated attribute catalogues.</summary>
    public static IReadOnlyList<AdvancedSearchFieldDefinition> GetAllFields(SearchEntityKind entity) =>
        Categories
            .SelectMany(category => GetFields(entity, category.Kind)
                .Concat(GetAttributeFields(entity, category.Kind)))
            .GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

    public static IReadOnlyList<AdvancedSearchFieldDefinition> GetAttributeFields(SearchEntityKind entity, AdvancedSearchCategoryKind category)
    {
        if (entity == SearchEntityKind.Coach)
        {
            var staffFields = new List<AdvancedSearchFieldDefinition>();
            if (category == AdvancedSearchCategoryKind.TechnicalGoalkeeping)
            {
                staffFields.AddRange(CoachTechnical.Select(item => F($"attribute_{item.Key}", item.Label, category, SearchFieldValueKind.IntegerRange, Coaches, 0, 20)));
                staffFields.AddRange(CoachMedical.Select(item => F($"attribute_{item.Key}", item.Label, category, SearchFieldValueKind.IntegerRange, Coaches, 0, 20)));
            }
            else if (category == AdvancedSearchCategoryKind.MentalAttributes)
            {
                staffFields.AddRange(CoachMental.Select(item => F($"attribute_{item.Key}", item.Label, category, SearchFieldValueKind.IntegerRange, Coaches, 0, 20)));
                staffFields.AddRange(CoachKnowledge.Select(item => F($"knowledge_{item.Key}", item.Label, category, SearchFieldValueKind.IntegerRange, Coaches, 0, 20)));
                staffFields.AddRange(PlayerAttributeCatalog.PersonalityAttributes.Select(attribute =>
                    F($"personality_{attribute.Key}", $"Personality: {attribute.DisplayName}", category,
                        SearchFieldValueKind.IntegerRange, Coaches, 0, 20)));
            }
            return staffFields;
        }

        if (entity != SearchEntityKind.Player)
            return Array.Empty<AdvancedSearchFieldDefinition>();

        if (category == AdvancedSearchCategoryKind.Goalkeeping)
            return PlayerAttributeCatalog.GetProfileGroups(true)
                .Where(x => x.Kind == PlayerAttributeGroupKind.Goalkeeping)
                .SelectMany(x => x.Attributes)
                .Select(attribute => F($"attribute_{attribute.Key}", attribute.DisplayName, category, SearchFieldValueKind.IntegerRange, Players, 0, 20))
                .ToArray();

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
            .Select(attribute => F($"attribute_{attribute.Key}", attribute.DisplayName, category, SearchFieldValueKind.IntegerRange, Players, 0, 20))
            .ToList();

        if (category == AdvancedSearchCategoryKind.MentalAttributes)
            attributes.AddRange(PlayerAttributeCatalog.PersonalityAttributes.Select(attribute =>
                F($"personality_{attribute.Key}", $"Personality: {attribute.DisplayName}", category, SearchFieldValueKind.IntegerRange, Players, 0, 20)));

        if (category == AdvancedSearchCategoryKind.PhysicalHidden)
            attributes.AddRange(PlayerAttributeCatalog.GetProfileGroups(false)
                .Where(x => x.Kind == PlayerAttributeGroupKind.Hidden)
                .SelectMany(x => x.Attributes)
                .Select(attribute => F($"attribute_{attribute.Key}", attribute.DisplayName, category, SearchFieldValueKind.IntegerRange, Players, 0, 20)));

        return attributes.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToArray();
    }

    private static IReadOnlyList<AdvancedSearchFieldDefinition> BuildGeneralFields()
    {
        var fields = new List<AdvancedSearchFieldDefinition>
        {
            F("name", "Name", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Text, AllEntities),
            F("age", "Age", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches, 0, 100),
            F("date_of_birth", "Date of Birth", AdvancedSearchCategoryKind.General, SearchFieldValueKind.DateRange, PlayersAndCoaches),
            F("nationality", "Nationality", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, PlayersAndCoaches),
            F("gender", "Gender", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Enum, PlayersAndCoaches),
            F("game_uid", "Game UID", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Text, PlayersAndCoaches),
            F("second_nationality", "Second Nationality", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Players),
            F("continent", "Continent", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, PlayersAndCoaches),
            F("region", "Region", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, PlayersAndCoaches),
            F("city_of_birth", "City of Birth", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Text, PlayersAndCoaches),
            F("ethnicity", "Ethnicity", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Players),
            F("eu_nationality", "EU Nationality", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Boolean, Players),
            F("international_caps", "International Caps", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Players, 0, 1000),
            F("international_goals", "International Goals", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Players, 0, 1000),
            F("height_cm", "Height (cm)", AdvancedSearchCategoryKind.General, SearchFieldValueKind.DecimalRange, Players, 0, 250),
            F("weight_kg", "Weight (kg)", AdvancedSearchCategoryKind.General, SearchFieldValueKind.DecimalRange, Players, 0, 250),
            F("division", "Division", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Players),
            F("team", "Team / Club", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, PlayersAndCoaches),
            F("preferred_foot", "Preferred Foot", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Enum, Players),
            F("wonderkid", "Wonderkid", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Boolean, Players),
            F("is_newgen", "Newgen / Original Database Status", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Boolean, PlayersAndCoaches),
            F("reputation_home", "Home Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches, 0, 10000),
            F("reputation_current", "Current Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches, 0, 10000),
            F("reputation_world", "World Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches, 0, 10000),
            F("current_ability", "CA", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches, 0, 200),
            F("potential_ability", "PA", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, PlayersAndCoaches, 0, 200),
            F("ability_difference", "PA − CA Difference", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Players, -200, 200),
            F("recommended_ca", "Recommended CA / RCA", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Players, 0, 200),
            F("left_foot", "Left Foot", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Players, 0, 20),
            F("right_foot", "Right Foot", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Players, 0, 20),
            F("position_familiarity", "Position Familiarity", AdvancedSearchCategoryKind.General, SearchFieldValueKind.PositionRating, Players, 0, 20),
            F("training_status_club", "Training Status at Club", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Players),
            F("training_status_nation", "Training Status at Nation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Players),
            F("primary_role", "Current Role / Job", AdvancedSearchCategoryKind.General, SearchFieldValueKind.EntityLookup, Coaches),
            F("training_rating", "Training Rating", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Coaches, 0, 20),
            F("club_founded_year", "Founded Year", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 2200),
            F("club_reputation", "Club Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 10000),
            F("club_stadium_name", "Stadium Name", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Text, Clubs),
            F("club_stadium_capacity", "Stadium Capacity", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 200000),
            F("club_average_attendance", "Average Attendance", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 200000),
            F("club_minimum_attendance", "Minimum Attendance", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 200000),
            F("club_maximum_attendance", "Maximum Attendance", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 200000),
            F("club_ownership_type", "Ownership Type", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Enum, Clubs),
            F("club_chairperson_title", "Chairperson Title", AdvancedSearchCategoryKind.General, SearchFieldValueKind.Enum, Clubs),
            F("club_first_team_reputation", "First Team Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 10000),
            F("club_reserve_team_reputation", "Reserve Team Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 10000),
            F("club_youth_team_reputation", "Youth Team Reputation", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 10000)
        };
        fields.AddRange(new[]
        {
            F("club_corporate_facilities", "Corporate Facilities", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 20),
            F("club_youth_coaching", "Youth Coaching", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 20),
            F("club_youth_importance", "Youth Importance", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 20),
            F("club_training_facilities", "Training Facilities", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 20),
            F("club_youth_facilities", "Youth Facilities", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 20),
            F("club_youth_recruitment", "Youth Recruitment", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 20),
            F("club_junior_coaching", "Junior Coaching", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 20),
            F("club_medical_facilities", "Medical Facilities", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 20),
            F("club_sports_science_facilities", "Sports Science Facilities", AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Clubs, 0, 20)
        });
        fields.AddRange(Positions.Select(position => F(
            $"position_{position.Key}", $"{position.Label} Rating", AdvancedSearchCategoryKind.General,
            SearchFieldValueKind.IntegerRange, Players, 0, 20)));
        fields.AddRange(StaffRolePreferences.Select(role => F(
            role.Key, $"Preferred Role: {role.Label}", AdvancedSearchCategoryKind.General,
            SearchFieldValueKind.IntegerRange, Coaches, 0, 20)));
        fields.AddRange(PlayerAttributeCatalog.FootAttributes.Select(foot => F(
            foot.Key, foot.DisplayName, AdvancedSearchCategoryKind.General, SearchFieldValueKind.IntegerRange, Players, 0, 20)));
        return fields.AsReadOnly();
    }

    private static IReadOnlyList<AdvancedSearchFieldDefinition> BuildFields()
    {
        var fields = new List<AdvancedSearchFieldDefinition>();
        fields.AddRange(GeneralFields);
        fields.AddRange(ContractFields);
        fields.AddRange(PreferredMoveFields);
        fields.AddRange(RelationFields);
        return fields.AsReadOnly();
    }

    private static AdvancedSearchFieldDefinition F(
        string key,
        string label,
        AdvancedSearchCategoryKind category,
        SearchFieldValueKind valueKind,
        IReadOnlyList<SearchEntityKind> entities,
        decimal? minimumAllowed = null,
        decimal? maximumAllowed = null)
    {
        var ready =
            key is "name" or "age" or "date_of_birth" or "nationality" or "second_nationality" or "team" or
                "height_cm" or "weight_kg" or "current_ability" or "potential_ability" or
                "weekly_wage" or "transfer_value" or "contract_expiry" or
                "position_familiarity" or "preferred_moves" or "club_reputation" or
                "is_newgen" or "wonderkid" or "eu_nationality" or "preferred_foot" or
                "club_founded_year" or "club_stadium_name" or "club_stadium_capacity" or
                "club_average_attendance" or "international_caps" or "international_goals" or
                "reputation_home" or "reputation_current" or "reputation_world" or
                "ability_difference" or "continent" or "region" or
                "city_of_birth" or "ethnicity" or "gender" or "game_uid" or "division" or "training_status_club" or
                "training_status_nation" or "primary_role" or "training_rating" or
                "contract_status" or "release_clause" or "club_balance" or "club_debt" or
                "club_transfer_budget" or "club_wage_budget" or "club_loans" or "club_sponsorship" ||
            key.StartsWith("attribute_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("personality_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("position_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("knowledge_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("facility_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("finance_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("club_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("role_preference_", StringComparison.OrdinalIgnoreCase) ||
            key is "left_foot" or "right_foot" or "relations";

        return new AdvancedSearchFieldDefinition(
            key, label, category, valueKind, entities, FieldEvidenceStatus.Documented,
            ready ? SearchImplementationStatus.Ready : SearchImplementationStatus.InProgress,
            minimumAllowed, maximumAllowed);
    }
}
