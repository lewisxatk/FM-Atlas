using System.Globalization;
using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>Supported operators for the universal, typed filter builder.</summary>
public enum AdvancedFilterOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    Between,
    Contains,
    StartsWith,
    IsSet,
    IsMissing
}

public enum AdvancedFilterLogic
{
    All,
    Any
}

public sealed record AdvancedFilterCondition(
    string FieldKey,
    AdvancedFilterOperator Operator,
    string? Value = null,
    string? UpperValue = null);

/// <summary>A clause is either one condition or one nested group, never both.</summary>
public sealed record AdvancedFilterClause(
    AdvancedFilterCondition? Condition,
    AdvancedFilterGroup? Group)
{
    public static AdvancedFilterClause ForCondition(AdvancedFilterCondition condition)
        => new(condition ?? throw new ArgumentNullException(nameof(condition)), null);

    public static AdvancedFilterClause ForGroup(AdvancedFilterGroup group)
        => new(null, group ?? throw new ArgumentNullException(nameof(group)));
}

/// <summary>
/// A nested AND/OR group. Empty groups are invalid; this prevents an accidental
/// empty-ANY group from matching every player.
/// </summary>
public sealed record AdvancedFilterGroup(
    AdvancedFilterLogic Logic,
    IReadOnlyList<AdvancedFilterClause> Clauses);

public sealed record AdvancedFilterEvaluation(
    bool Matches,
    int ConditionsEvaluated,
    int ConditionsMatched,
    IReadOnlyList<string> Explanations);

/// <summary>
/// Evaluates validated nested search criteria against a single entity's field map.
/// Missing fields never match numeric/string comparisons, including NotEquals.
/// </summary>
public static class AdvancedFilterEvaluator
{
    public const int MaximumDepth = 5;
    public const int MaximumConditions = 200;

    public static AdvancedFilterEvaluation Evaluate(
        AdvancedFilterGroup root,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(values);
        Validate(root);

        var evaluated = 0;
        var matched = 0;
        var explanations = new List<string>();

        bool EvaluateGroup(AdvancedFilterGroup group, string path, int depth)
        {
            var outcomes = new List<bool>(group.Clauses.Count);
            for (var i = 0; i < group.Clauses.Count; i++)
            {
                var clause = group.Clauses[i];
                if (clause.Condition is not null)
                {
                    var condition = clause.Condition;
                    var result = EvaluateCondition(condition, values, out var explanation);
                    evaluated++;
                    if (result) matched++;
                    if (explanations.Count < MaximumConditions)
                        explanations.Add($"{path}.{i + 1}: {explanation}");
                    outcomes.Add(result);
                }
                else if (clause.Group is not null)
                {
                    outcomes.Add(EvaluateGroup(clause.Group, $"{path}.{i + 1}", depth + 1));
                }
            }

            return group.Logic == AdvancedFilterLogic.All
                ? outcomes.All(x => x)
                : outcomes.Any(x => x);
        }

        var doesMatch = EvaluateGroup(root, "Group", 1);
        return new AdvancedFilterEvaluation(doesMatch, evaluated, matched, explanations);
    }

    public static void Validate(AdvancedFilterGroup root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var count = 0;

        void Visit(AdvancedFilterGroup group, int depth)
        {
            if (depth > MaximumDepth)
                throw new ArgumentException($"Filter groups may be nested no deeper than {MaximumDepth} levels.");
            if (group.Clauses is null || group.Clauses.Count == 0)
                throw new ArgumentException("A filter group must contain at least one condition or subgroup.");

            foreach (var clause in group.Clauses)
            {
                if ((clause.Condition is null) == (clause.Group is null))
                    throw new ArgumentException("Each filter clause must contain exactly one condition or subgroup.");

                if (clause.Condition is { } condition)
                {
                    count++;
                    if (count > MaximumConditions)
                        throw new ArgumentException($"A filter can contain at most {MaximumConditions} conditions.");
                    ValidateCondition(condition);
                }
                else
                {
                    Visit(clause.Group!, depth + 1);
                }
            }
        }

        Visit(root, 1);
    }

    private static void ValidateCondition(AdvancedFilterCondition condition)
    {
        if (string.IsNullOrWhiteSpace(condition.FieldKey))
            throw new ArgumentException("Every filter condition must select a field.");
        if (condition.FieldKey.Length > 160)
            throw new ArgumentException("Filter field keys cannot exceed 160 characters.");

        if (condition.Operator is AdvancedFilterOperator.IsSet or AdvancedFilterOperator.IsMissing)
            return;

        if (string.IsNullOrWhiteSpace(condition.Value))
            throw new ArgumentException($"A value is required for {condition.Operator} on '{condition.FieldKey}'.");
        if (condition.Value.Length > 512 || (condition.UpperValue?.Length ?? 0) > 512)
            throw new ArgumentException("Filter values cannot exceed 512 characters.");

        if (condition.Operator == AdvancedFilterOperator.Between)
        {
            if (string.IsNullOrWhiteSpace(condition.UpperValue))
                throw new ArgumentException($"An upper value is required for Between on '{condition.FieldKey}'.");
            if (TryNumber(condition.Value, out var lower) && TryNumber(condition.UpperValue, out var upper) && lower > upper)
                throw new ArgumentException($"The lower boundary cannot exceed the upper boundary for '{condition.FieldKey}'.");
        }
    }

    private static bool EvaluateCondition(
        AdvancedFilterCondition condition,
        IReadOnlyDictionary<string, object?> values,
        out string explanation)
    {
        if (!TryGetValue(values, condition.FieldKey, out var raw))
        {
            var missingResult = condition.Operator == AdvancedFilterOperator.IsMissing;
            explanation = missingResult ? $"'{condition.FieldKey}' is not present." : $"'{condition.FieldKey}' is unavailable; missing values do not match.";
            return missingResult;
        }

        var text = Convert.ToString(raw, CultureInfo.InvariantCulture);
        var isSet = raw is not null && !string.IsNullOrWhiteSpace(text);

        if (condition.Operator == AdvancedFilterOperator.IsSet)
        {
            explanation = isSet ? $"'{condition.FieldKey}' is present." : $"'{condition.FieldKey}' is empty.";
            return isSet;
        }
        if (condition.Operator == AdvancedFilterOperator.IsMissing)
        {
            explanation = isSet ? $"'{condition.FieldKey}' is present." : $"'{condition.FieldKey}' is empty.";
            return !isSet;
        }
        if (!isSet)
        {
            explanation = $"'{condition.FieldKey}' is empty; empty values do not match.";
            return false;
        }

        var expected = condition.Value ?? string.Empty;
        var actualText = text ?? string.Empty;
        bool result;
        switch (condition.Operator)
        {
            case AdvancedFilterOperator.Equals:
                result = TryCompare(raw, expected, out var equalsComparison) && equalsComparison == 0;
                break;
            case AdvancedFilterOperator.NotEquals:
                // An invalid value never turns into a successful "not equals" result.
                result = TryCompare(raw, expected, out var notEqualsComparison) && notEqualsComparison != 0;
                break;
            case AdvancedFilterOperator.GreaterThan:
                result = TryCompare(raw, expected, out var greaterComparison) && greaterComparison > 0;
                break;
            case AdvancedFilterOperator.LessThan:
                result = TryCompare(raw, expected, out var lessComparison) && lessComparison < 0;
                break;
            case AdvancedFilterOperator.Between:
                var high = condition.UpperValue ?? string.Empty;
                result = TryCompare(raw, expected, out var lowerComparison)
                    && TryCompare(raw, high, out var upperComparison)
                    && lowerComparison >= 0 && upperComparison <= 0;
                break;
            case AdvancedFilterOperator.Contains:
                result = actualText.Contains(expected, StringComparison.OrdinalIgnoreCase);
                break;
            case AdvancedFilterOperator.StartsWith:
                result = actualText.StartsWith(expected, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(condition), "Unsupported filter operator.");
        }

        explanation = result
            ? $"'{condition.FieldKey}' matched {condition.Operator} '{expected}'."
            : $"'{condition.FieldKey}' did not match {condition.Operator} '{expected}'.";
        return result;
    }

    private static bool TryCompare(object? actual, string expected, out int comparison)
    {
        comparison = 0;
        if (actual is null) return false;
        var text = Convert.ToString(actual, CultureInfo.InvariantCulture) ?? string.Empty;

        if (actual is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            if (!TryNumber(text, out var actualNumber) || !TryNumber(expected, out var expectedNumber))
                return false;
            comparison = actualNumber.CompareTo(expectedNumber);
            return true;
        }

        if (actual is bool actualBoolean)
        {
            if (!bool.TryParse(expected, out var expectedBoolean))
                return false;
            comparison = actualBoolean.CompareTo(expectedBoolean);
            return true;
        }

        if (actual is DateTime or DateTimeOffset)
        {
            if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var actualDate)
                || !DateTimeOffset.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expectedDate))
                return false;
            comparison = actualDate.CompareTo(expectedDate);
            return true;
        }

        comparison = string.Compare(text, expected, StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool TryNumber(string value, out decimal number)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);

    private static bool TryGetValue(
        IReadOnlyDictionary<string, object?> values,
        string fieldKey,
        out object? value)
    {
        if (values.TryGetValue(fieldKey, out value))
            return true;

        foreach (var pair in values)
        {
            if (string.Equals(pair.Key, fieldKey, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
