using System.Globalization;
using Microsoft.Data.Sqlite;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Search;

namespace FMAtlas.Infrastructure.Persistence;

/// <summary>
/// Executes only the explicit supported subset of the REF-002 search catalogue
/// against field_values. Unknown or In Progress filters are rejected. This service
/// is read-only and uses parameterised values for every user-supplied criterion.
/// </summary>
public sealed class SqliteAdvancedSearchProvider : IAdvancedSearchProvider
{
    private readonly string _connectionString;

    public SqliteAdvancedSearchProvider(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("A database file path is required.", nameof(databasePath));

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        _connectionString = builder.ToString();
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
        var predicates = new List<string>(query.Conditions.Count);

        for (var index = 0; index < query.Conditions.Count; index++)
        {
            predicates.Add(BuildCondition(
                query.Entity,
                query.Conditions[index],
                index,
                parameters,
                ref nextParameter));
        }

        var logic = query.Logic == SearchFilterLogic.All ? " AND " : " OR ";
        var sql = $"""
            SELECT e.entity_kind, e.source_id, e.provider_key, e.display_name
            FROM entities AS e
            WHERE e.entity_kind = $entityKind
              AND ({string.Join(logic, predicates)})
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

    private static string BuildCondition(
        SearchEntityKind entity,
        SearchFilterCondition condition,
        int conditionIndex,
        ICollection<(string Name, object Value)> parameters,
        ref int nextParameter)
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

        var field = ResolveField(entity, condition.FieldKey)
            ?? throw new NotSupportedException($"No stored field mapping exists for '{condition.FieldKey}'.");

        var keys = ResolveStoredFieldKeys(entity, condition.FieldKey);
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
        var keyParameters = fieldKeys
            .Select(key => AddParameter(parameters, ref nextParameter, key))
            .ToArray();

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
        var keyParameters = fieldKeys.Select(key =>
            AddParameter(parameters, ref nextParameter, key)).ToArray();

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

    private static string? FormatDate(DateOnly date) =>
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

    private static AdvancedSearchFieldDefinition? ResolveField(SearchEntityKind entity, string key)
    {
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
            normalised is "is_newgen" or "wonderkid" or "eu_nationality" or
                "left_foot" or "right_foot" or "preferred_foot" or
                "international_caps" or "international_goals" or "continent" or
                "region" or "city_of_birth" or "ethnicity" or "division" or
                "training_status_club" or "training_status_nation" or
                "primary_role" or "training_rating" or "club_founded_year" or
                "club_stadium_name" or "club_stadium_capacity" or "club_average_attendance" or
                "reputation_home" or "reputation_current" or "reputation_world" or
                "ability_difference" or "recommended_ca" or "contract_status" or
                "release_clause" or "club_balance" or "club_debt" or "club_transfer_budget" or
                "club_wage_budget" or "club_loans" or "club_sponsorship")
            return [normalised];

        return normalised switch
        {
            "age" => ["age"],
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
            "club_founded_year" => ["founded_year"],
            "club_stadium_name" => ["stadium_name"],
            "club_stadium_capacity" => ["stadium_capacity"],
            "club_average_attendance" => ["average_attendance"],
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
