using System.Globalization;
using Microsoft.Data.Sqlite;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Search;

namespace FMAtlas.Infrastructure.Persistence;

/// <summary>
/// Executes only the explicit supported subset of the REF-002 search catalogue
/// against field_values. Unknown or In Progress filters are rejected. This service
/// is read-only and uses parameterised values for every user-supplied criterion.
/// </summary>
public sealed class SqliteAdvancedSearchProvider : IAdvancedSearchProvider, IIndexedAdvancedSearchFieldProvider
{
    private readonly string _connectionString;
    private readonly string _databasePath;

    public SqliteAdvancedSearchProvider(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("A database file path is required.", nameof(databasePath));

        _databasePath = Path.GetFullPath(databasePath);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        _connectionString = builder.ToString();
    }

    public async Task<IReadOnlyList<AdvancedSearchFieldDefinition>> GetIndexedFieldsAsync(
        SearchEntityKind entity,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_databasePath))
            return Array.Empty<AdvancedSearchFieldDefinition>();

        var entityKind = entity switch
        {
            SearchEntityKind.Player => EntityKind.Player,
            SearchEntityKind.Coach => EntityKind.Staff,
            SearchEntityKind.Club => EntityKind.Club,
            _ => throw new ArgumentOutOfRangeException(nameof(entity))
        };

        var knownFields = AdvancedSearchCatalog.GetAllFields(entity)
            .ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT field_key, value_kind
            FROM field_values
            WHERE entity_kind = $entityKind
            GROUP BY field_key, value_kind
            ORDER BY field_key, value_kind;
            """;
        command.Parameters.AddWithValue("$entityKind", entityKind.ToString());

        var discovered = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = reader.GetString(0);
            var kind = reader.GetString(1);
            if (!discovered.TryGetValue(key, out var kinds))
            {
                kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                discovered.Add(key, kinds);
            }

            kinds.Add(kind);
        }

        var results = new List<AdvancedSearchFieldDefinition>();
        foreach (var (key, kinds) in discovered)
        {
            // Mixed value types are ambiguous; omit them rather than picking a
            // storage column and risking false matches.
            if (kinds.Count != 1 || !TryMapValueKind(kinds.Single(), out var valueKind))
                continue;

            if (knownFields.TryGetValue(key, out var known))
            {
                // Include discovered known fields as well as dynamic fields. The
                // caller needs the complete set of fields actually present in this
                // database, including catalogue fields that were already executable.
                results.Add(known with
                {
                    ValueKind = valueKind,
                    EvidenceStatus = FieldEvidenceStatus.Mapped,
                    ImplementationStatus = SearchImplementationStatus.Ready
                });
                continue;
            }

            var (minimum, maximum) = GetSuggestedBounds(key, valueKind);
            results.Add(new AdvancedSearchFieldDefinition(
                key,
                HumaniseFieldKey(key),
                AdvancedSearchCategoryKind.IndexedFields,
                valueKind,
                [entity],
                FieldEvidenceStatus.Mapped,
                SearchImplementationStatus.Ready,
                minimum,
                maximum));
        }

        return results
            .OrderBy(field => field.Category)
            .ThenBy(field => field.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryMapValueKind(string valueKind, out SearchFieldValueKind searchKind)
    {
        searchKind = valueKind switch
        {
            "Text" => SearchFieldValueKind.Text,
            "Integer" => SearchFieldValueKind.IntegerRange,
            "Decimal" => SearchFieldValueKind.DecimalRange,
            "Date" => SearchFieldValueKind.DateRange,
            "Boolean" => SearchFieldValueKind.Boolean,
            _ => (SearchFieldValueKind)(-1)
        };

        return Enum.IsDefined(searchKind);
    }

    private static (decimal? Minimum, decimal? Maximum) GetSuggestedBounds(
        string key,
        SearchFieldValueKind valueKind)
    {
        if (valueKind is not (SearchFieldValueKind.IntegerRange or SearchFieldValueKind.DecimalRange))
            return (null, null);

        var normalised = key.ToLowerInvariant();
        if (normalised.StartsWith("attribute_", StringComparison.Ordinal) ||
            normalised.StartsWith("personality_", StringComparison.Ordinal) ||
            normalised.StartsWith("position_", StringComparison.Ordinal) ||
            normalised.StartsWith("knowledge_", StringComparison.Ordinal) ||
            normalised.StartsWith("role_preference_", StringComparison.Ordinal) ||
            normalised.StartsWith("facility_", StringComparison.Ordinal))
            return (0, 20);

        return (null, null);
    }

    private static string HumaniseFieldKey(string key)
    {
        var words = key.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            string.Join(' ', words));
    }

    public async Task<IReadOnlyList<EntitySearchResult>> ExecuteAsync(
        AdvancedSearchQuery query,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit), "Advanced Search limit must be between 1 and 500.");

        var validation = AdvancedSearchQueryValidator.Validate(query);
        if (!validation.IsValid)
        {
            var details = string.Join(
                Environment.NewLine,
                validation.Issues.Select(issue =>
                    string.IsNullOrWhiteSpace(issue.FieldKey)
                        ? issue.Message
                        : $"{issue.FieldKey}: {issue.Message}"));
            throw new ArgumentException(details, nameof(query));
        }

        var kind = query.Entity switch
        {
            SearchEntityKind.Player => EntityKind.Player,
            SearchEntityKind.Coach => EntityKind.Staff,
            SearchEntityKind.Club => EntityKind.Club,
            _ => throw new ArgumentOutOfRangeException(nameof(query), "Unsupported search entity.")
        };

        var parameters = new List<(string Name, object Value)>
        {
            ("$entityKind", kind.ToString()),
            ("$limit", limit)
        };
        var nextParameter = 0;
        var builtPredicates = new List<(string Predicate, bool IsPosition)>(query.Conditions.Count);

        for (var index = 0; index < query.Conditions.Count; index++)
        {
            var condition = query.Conditions[index];
            builtPredicates.Add((
                BuildCondition(query.Entity, condition, index, parameters, ref nextParameter, query.IndexedFields),
                IsPositionCondition(condition.FieldKey)));
        }

        var logic = query.Logic == SearchFilterLogic.All ? " AND " : " OR ";
        var positionLogic = query.PositionLogic == SearchFilterLogic.All ? " AND " : " OR ";

        var positionPredicates = builtPredicates.Where(item => item.IsPosition)
            .Select(item => item.Predicate).ToArray();
        var otherPredicates = builtPredicates.Where(item => !item.IsPosition)
            .Select(item => item.Predicate).ToArray();

        // Position criteria form their own group. Their All/Any rule remains
        // independent even when the overall query uses the opposite logic.
        var groups = new List<string>(2);
        if (otherPredicates.Length > 0)
            groups.Add($"({string.Join(logic, otherPredicates)})");
        if (positionPredicates.Length > 0)
            groups.Add($"({string.Join(positionLogic, positionPredicates)})");

        var criteriaPredicate = groups.Count == 1
            ? groups[0]
            : string.Join(logic, groups);

        // The global preference excludes only records explicitly identified as
        // female. Unknown gender is retained rather than silently discarded.
        var womenFilter = query.Entity == SearchEntityKind.Player && !query.IncludeWomenPlayers
            ? """
              AND NOT EXISTS (
                  SELECT 1
                  FROM field_values AS gender
                  WHERE gender.entity_kind = e.entity_kind
                    AND gender.source_id = e.source_id
                    AND gender.provider_key = e.provider_key
                    AND gender.field_key = 'gender'
                    AND LOWER(TRIM(gender.text_value)) IN ('female', 'woman', 'women', 'f')
              )
              """
            : string.Empty;

        var sql = $"""
            SELECT e.entity_kind, e.source_id, e.provider_key, e.display_name
            FROM entities AS e
            WHERE e.entity_kind = $entityKind
              AND ({criteriaPredicate})
              {womenFilter}
            ORDER BY e.display_name COLLATE NOCASE, e.provider_key, e.source_id
            LIMIT $limit;
            """;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        var results = new List<EntitySearchResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!Enum.TryParse<EntityKind>(reader.GetString(0), ignoreCase: true, out var resultKind))
                continue;

            results.Add(new EntitySearchResult(
                new EntityIdentity(resultKind, reader.GetString(1), reader.GetString(2)),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
        }

        return results.AsReadOnly();
    }

    private static bool IsPositionCondition(string fieldKey) =>
        fieldKey.StartsWith("position_", StringComparison.OrdinalIgnoreCase);

    private static string BuildCondition(
        SearchEntityKind entity,
        SearchFilterCondition condition,
        int conditionIndex,
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter,
        IReadOnlyList<AdvancedSearchFieldDefinition>? indexedFields = null)
    {
        if (condition.FieldKey.Equals("name", StringComparison.OrdinalIgnoreCase))
        {
            var name = condition.Operator == SearchFilterOperator.Equals
                ? condition.TextValue!
                : BuildContainsPattern(condition.TextValue!);
            var parameter = AddParameter(parameters, ref nextParameter, name);

            return condition.Operator == SearchFilterOperator.Equals
                ? $"e.display_name = {parameter} COLLATE NOCASE"
                : $"e.display_name LIKE {parameter} ESCAPE '\\'";
        }

        if (condition.FieldKey.Equals("relations", StringComparison.OrdinalIgnoreCase))
            return BuildRelationshipPredicate(condition, parameters, ref nextParameter);

        if (condition.FieldKey.Equals("preferred_moves", StringComparison.OrdinalIgnoreCase))
        {
            var trait = AddParameter(parameters, ref nextParameter, condition.TextValue!);
            var exists = $"""
                EXISTS (
                    SELECT 1
                    FROM field_values AS f{conditionIndex}
                    WHERE f{conditionIndex}.entity_kind = e.entity_kind
                      AND f{conditionIndex}.source_id = e.source_id
                      AND f{conditionIndex}.provider_key = e.provider_key
                      AND substr(f{conditionIndex}.field_key, 1, 15) = 'preferred_move_'
                      AND f{conditionIndex}.text_value = {trait} COLLATE NOCASE
                )
                """;

            return condition.Operator == SearchFilterOperator.HasTrait ? exists : $"NOT {exists}";
        }

        if (condition.FieldKey.Equals("ability_difference", StringComparison.OrdinalIgnoreCase))
        {
            return BuildAbilityDifferencePredicate(conditionIndex, condition, parameters, ref nextParameter);
        }

        if (condition.FieldKey.Equals("position_familiarity", StringComparison.OrdinalIgnoreCase))
        {
            var positionKey = "position_" + condition.TextValue!.Trim().ToLowerInvariant();
            return BuildValueExists(
                conditionIndex,
                [positionKey],
                SearchFieldValueKind.IntegerRange,
                condition,
                parameters,
                ref nextParameter);
        }

        var field = ResolveField(entity, condition.FieldKey, indexedFields)
            ?? throw new NotSupportedException($"No stored field mapping exists for '{condition.FieldKey}'.");

        var keys = ResolveStoredFieldKeys(entity, condition.FieldKey);
        if (keys.Count == 0 && indexedFields?.Any(indexed =>
                string.Equals(indexed.Key, condition.FieldKey, StringComparison.OrdinalIgnoreCase)) == true)
            keys = [condition.FieldKey];
        if (keys.Count == 0)
            throw new NotSupportedException($"No stored field mapping exists for '{condition.FieldKey}'.");

        if (condition.Operator == SearchFilterOperator.IsUnknown)
            return BuildMissingFieldPredicate(conditionIndex, keys, parameters, ref nextParameter);

        return BuildValueExists(
            conditionIndex,
            keys,
            field.ValueKind,
            condition,
            parameters,
            ref nextParameter);
    }

    private static string BuildValueExists(
        int conditionIndex,
        IReadOnlyList<string> fieldKeys,
        SearchFieldValueKind valueKind,
        SearchFilterCondition condition,
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter)
    {
        var alias = $"f{conditionIndex}";
        var keyParameters = new List<string>(fieldKeys.Count);
        foreach (var key in fieldKeys)
            keyParameters.Add(AddParameter(parameters, ref nextParameter, key));

        var predicate = valueKind switch
        {
            SearchFieldValueKind.IntegerRange or SearchFieldValueKind.DecimalRange =>
                BuildNumericPredicate(alias, valueKind, condition, parameters, ref nextParameter),
            SearchFieldValueKind.Text or SearchFieldValueKind.EntityLookup or SearchFieldValueKind.Enum =>
                BuildTextPredicate(alias, condition, parameters, ref nextParameter),
            SearchFieldValueKind.DateRange =>
                BuildDatePredicate(alias, condition, parameters, ref nextParameter),
            SearchFieldValueKind.Boolean =>
                BuildBooleanPredicate(alias, condition, parameters, ref nextParameter),
            _ => throw new NotSupportedException(
                $"Value kind {valueKind} is not supported by the SQLite query provider.")
        };

        return $"""
            EXISTS (
                SELECT 1
                FROM field_values AS {alias}
                WHERE {alias}.entity_kind = e.entity_kind
                  AND {alias}.source_id = e.source_id
                  AND {alias}.provider_key = e.provider_key
                  AND {alias}.field_key IN ({string.Join(", ", keyParameters)})
                  AND {predicate}
            )
            """;
    }

    private static string BuildRelationshipPredicate(
        SearchFilterCondition condition,
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter)
    {
        var value = condition.Operator == SearchFilterOperator.Equals
            ? condition.TextValue!
            : BuildContainsPattern(condition.TextValue!);
        var parameter = AddParameter(parameters, ref nextParameter, value);
        var comparison = condition.Operator == SearchFilterOperator.Equals
            ? $"= {parameter} COLLATE NOCASE"
            : $"LIKE {parameter} ESCAPE '\\'";

        return $"""
            (
                EXISTS (
                    SELECT 1
                    FROM entity_relationships AS rel_out
                    LEFT JOIN entities AS related_out
                      ON related_out.entity_kind = rel_out.to_entity_kind
                     AND related_out.source_id = rel_out.to_source_id
                     AND related_out.provider_key = rel_out.to_provider_key
                    WHERE rel_out.from_entity_kind = e.entity_kind
                      AND rel_out.from_source_id = e.source_id
                      AND rel_out.from_provider_key = e.provider_key
                      AND (
                          rel_out.relation_kind {comparison}
                          OR related_out.display_name {comparison}
                      )
                )
                OR
                EXISTS (
                    SELECT 1
                    FROM entity_relationships AS rel_in
                    LEFT JOIN entities AS related_in
                      ON related_in.entity_kind = rel_in.from_entity_kind
                     AND related_in.source_id = rel_in.from_source_id
                     AND related_in.provider_key = rel_in.from_provider_key
                    WHERE rel_in.to_entity_kind = e.entity_kind
                      AND rel_in.to_source_id = e.source_id
                      AND rel_in.to_provider_key = e.provider_key
                      AND (
                          rel_in.relation_kind {comparison}
                          OR related_in.display_name {comparison}
                      )
                )
            )
            """;
    }

    private static string BuildAbilityDifferencePredicate(
        int conditionIndex,
        SearchFilterCondition condition,
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter)
    {
        var ca = $"ca{conditionIndex}";
        var pa = $"pa{conditionIndex}";
        var clauses = new List<string>();

        if (condition.Minimum is decimal minimum)
        {
            var value = AddParameter(parameters, ref nextParameter, decimal.ToInt64(minimum));
            clauses.Add($"({pa}.integer_value - {ca}.integer_value) >= {value}");
        }

        if (condition.Maximum is decimal maximum)
        {
            var value = AddParameter(parameters, ref nextParameter, decimal.ToInt64(maximum));
            clauses.Add($"({pa}.integer_value - {ca}.integer_value) <= {value}");
        }

        return $"""
            EXISTS (
                SELECT 1 FROM field_values AS {ca}
                WHERE {ca}.entity_kind = e.entity_kind
                  AND {ca}.source_id = e.source_id
                  AND {ca}.provider_key = e.provider_key
                  AND {ca}.field_key = 'current_ability'
                  AND EXISTS (
                      SELECT 1 FROM field_values AS {pa}
                      WHERE {pa}.entity_kind = e.entity_kind
                        AND {pa}.source_id = e.source_id
                        AND {pa}.provider_key = e.provider_key
                        AND {pa}.field_key = 'potential_ability'
                        AND {string.Join(" AND ", clauses)}
                  )
            )
            """;
    }

    private static string BuildBooleanPredicate(
        string alias,
        SearchFilterCondition condition,
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter)
    {
        var expected = condition.Operator == SearchFilterOperator.IsTrue ? 1L : 0L;
        var parameter = AddParameter(parameters, ref nextParameter, expected);
        return $"{alias}.integer_value = {parameter}";
    }

    private static string BuildMissingFieldPredicate(
        int conditionIndex,
        IReadOnlyList<string> fieldKeys,
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter)
    {
        var alias = $"missing{conditionIndex}";
        var keyParameters = new List<string>(fieldKeys.Count);
        foreach (var key in fieldKeys)
            keyParameters.Add(AddParameter(parameters, ref nextParameter, key));

        return $"""
            NOT EXISTS (
                SELECT 1
                FROM field_values AS {alias}
                WHERE {alias}.entity_kind = e.entity_kind
                  AND {alias}.source_id = e.source_id
                  AND {alias}.provider_key = e.provider_key
                  AND {alias}.field_key IN ({string.Join(", ", keyParameters)})
            )
            """;
    }

    private static string BuildNumericPredicate(
        string alias,
        SearchFieldValueKind valueKind,
        SearchFilterCondition condition,
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter)
    {
        var column = valueKind switch
        {
            SearchFieldValueKind.IntegerRange => $"{alias}.integer_value",
            SearchFieldValueKind.DecimalRange => $"{alias}.decimal_value",
            _ => throw new ArgumentOutOfRangeException(nameof(valueKind))
        };

        var clauses = new List<string>();
        if (condition.Minimum is decimal minimum)
        {
            object value = valueKind == SearchFieldValueKind.IntegerRange
                ? decimal.ToInt64(minimum)
                : Convert.ToDouble(minimum, CultureInfo.InvariantCulture);
            var parameter = AddParameter(parameters, ref nextParameter, value);
            clauses.Add($"{column} >= {parameter}");
        }

        if (condition.Maximum is decimal maximum)
        {
            object value = valueKind == SearchFieldValueKind.IntegerRange
                ? decimal.ToInt64(maximum)
                : Convert.ToDouble(maximum, CultureInfo.InvariantCulture);
            var parameter = AddParameter(parameters, ref nextParameter, value);
            clauses.Add($"{column} <= {parameter}");
        }

        return string.Join(" AND ", clauses);
    }

    private static string BuildTextPredicate(
        string alias,
        SearchFilterCondition condition,
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter)
    {
        var value = condition.Operator == SearchFilterOperator.Equals
            ? condition.TextValue!
            : BuildContainsPattern(condition.TextValue!);
        var parameter = AddParameter(parameters, ref nextParameter, value);

        return condition.Operator == SearchFilterOperator.Equals
            ? $"{alias}.text_value = {parameter} COLLATE NOCASE"
            : $"{alias}.text_value LIKE {parameter} ESCAPE '\\'";
    }

    private static string BuildDatePredicate(
        string alias,
        SearchFilterCondition condition,
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter)
    {
        var clauses = new List<string>();

        if (condition.Operator == SearchFilterOperator.Before)
        {
            var parameter = AddParameter(parameters, ref nextParameter, FormatDate(condition.DateValue!.Value));
            clauses.Add($"{alias}.date_value < {parameter}");
        }
        else if (condition.Operator == SearchFilterOperator.After)
        {
            var parameter = AddParameter(parameters, ref nextParameter, FormatDate(condition.DateValue!.Value));
            clauses.Add($"{alias}.date_value > {parameter}");
        }
        else
        {
            if (condition.DateMinimum is DateOnly minimum)
            {
                var parameter = AddParameter(parameters, ref nextParameter, FormatDate(minimum));
                clauses.Add($"{alias}.date_value >= {parameter}");
            }

            if (condition.DateMaximum is DateOnly maximum)
            {
                var parameter = AddParameter(parameters, ref nextParameter, FormatDate(maximum));
                clauses.Add($"{alias}.date_value <= {parameter}");
            }
        }

        return string.Join(" AND ", clauses);
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string BuildContainsPattern(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return "%" + escaped + "%";
    }

    private static string AddParameter(
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter,
        object value)
    {
        var name = "$p" + nextParameter.ToString(CultureInfo.InvariantCulture);
        nextParameter++;
        parameters.Add((name, value));
        return name;
    }

    private static AdvancedSearchFieldDefinition? ResolveField(
        SearchEntityKind entity,
        string key,
        IReadOnlyList<AdvancedSearchFieldDefinition>? indexedFields = null)
    {
        var indexed = indexedFields?.FirstOrDefault(field =>
            field.SupportedEntities.Contains(entity) &&
            string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));
        if (indexed is not null)
            return indexed;

        var direct = AdvancedSearchCatalog.Fields.FirstOrDefault(field =>
            field.SupportedEntities.Contains(entity) &&
            string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));

        if (direct is not null)
            return direct;

        foreach (var category in new[]
        {
            AdvancedSearchCategoryKind.TechnicalGoalkeeping,
            AdvancedSearchCategoryKind.MentalAttributes,
            AdvancedSearchCategoryKind.PhysicalHidden
        })
        {
            var attribute = AdvancedSearchCatalog.GetAttributeFields(entity, category)
                .FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));
            if (attribute is not null)
                return attribute;
        }

        return null;
    }

    private static IReadOnlyList<string> ResolveStoredFieldKeys(SearchEntityKind entity, string key)
    {
        var normalised = key.ToLowerInvariant();
        if (normalised.StartsWith("attribute_", StringComparison.OrdinalIgnoreCase) ||
            normalised.StartsWith("personality_", StringComparison.OrdinalIgnoreCase) ||
            normalised.StartsWith("position_", StringComparison.OrdinalIgnoreCase) ||
            normalised.StartsWith("knowledge_", StringComparison.OrdinalIgnoreCase) ||
            normalised.StartsWith("facility_", StringComparison.OrdinalIgnoreCase) ||
            normalised.StartsWith("finance_", StringComparison.OrdinalIgnoreCase) ||
            normalised.StartsWith("role_preference_", StringComparison.OrdinalIgnoreCase) ||
            normalised is "is_newgen" or "wonderkid" or "eu_nationality" or
                "left_foot" or "right_foot" or "preferred_foot" or "gender" or "game_uid" or
                "international_caps" or "international_goals" or "continent" or
                "region" or "city_of_birth" or "ethnicity" or
                "training_status_club" or "training_status_nation" or
                "primary_role" or "training_rating" or
                "reputation_home" or "reputation_current" or "reputation_world" or
                "ability_difference" or "recommended_ca" or "contract_status" or
                "release_clause")
            return [normalised];

        return normalised switch
        {
            "age" => ["age"],
            "date_of_birth" => ["date_of_birth"],
            "nationality" => ["nationality"],
            "second_nationality" => ["second_nationality"],
            "team" => entity == SearchEntityKind.Player
                ? ["team_name", "club_name"]
                : ["club_name"],
            "height_cm" => ["height_cm"],
            "weight_kg" => ["weight_kg"],
            "current_ability" => ["current_ability"],
            "potential_ability" => ["potential_ability"],
            "weekly_wage" => ["wage"],
            "transfer_value" => ["transfer_value"],
            "contract_expiry" => ["contract_expires"],
            "club_reputation" => ["reputation"],
            "division" => ["league_name"],
            "club_founded_year" => ["founded_year"],
            "club_stadium_name" => ["stadium_name"],
            "club_stadium_capacity" => ["stadium_capacity"],
            "club_average_attendance" => ["average_attendance"],
            "club_training_facilities" => ["facility_training"],
            "club_youth_facilities" => ["facility_youth"],
            "club_youth_recruitment" => ["facility_youth_recruitment"],
            "club_junior_coaching" => ["facility_junior_coaching"],
            "club_medical_facilities" => ["facility_medical"],
            "club_sports_science_facilities" => ["facility_sports_science"],
            "club_corporate_facilities" => ["facility_corporate"],
            "club_youth_coaching" => ["facility_youth_coaching"],
            "club_youth_importance" => ["facility_youth_importance"],
            "club_minimum_attendance" => ["minimum_attendance"],
            "club_maximum_attendance" => ["maximum_attendance"],
            "club_ownership_type" => ["ownership_type"],
            "club_chairperson_title" => ["chairperson_title"],
            "club_first_team_reputation" => ["first_team_reputation"],
            "club_reserve_team_reputation" => ["reserve_team_reputation"],
            "club_youth_team_reputation" => ["youth_team_reputation"],
            "club_transfer_budget_season" => ["finance_transfer_budget_season"],
            "club_transfer_budget_remaining" => ["finance_transfer_budget_remaining"],
            "club_wage_used" => ["finance_wage_used"],
            "club_max_wage" => ["finance_max_wage"],
            "club_transfer_revenue_percentage" => ["finance_transfer_revenue_percentage"],
            "club_total_debt" => ["finance_total_debt"],
            "club_monthly_debt_repayments" => ["finance_monthly_debt_repayments"],
            "club_balance" => ["finance_balance"],
            "club_debt" => ["finance_debt"],
            "club_transfer_budget" => ["finance_transfer_budget"],
            "club_wage_budget" => ["finance_wage_budget"],
            "club_loans" => ["finance_loans"],
            "club_sponsorship" => ["finance_sponsorship"],
            "primary_role" => ["primary_role"],
            _ => Array.Empty<string>()
        };
    }
}
