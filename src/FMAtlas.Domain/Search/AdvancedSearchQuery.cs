namespace FMAtlas.Domain.Search;

public enum SearchFilterLogic
{
    All,
    Any
}

public enum SearchFilterOperator
{
    Range,
    Contains,
    Equals,
    IsTrue,
    IsFalse,
    IsUnknown,
    Before,
    After,
    HasTrait,
    LacksTrait
}

public sealed record SearchFilterCondition(
    string FieldKey,
    SearchFilterOperator Operator,
    decimal? Minimum = null,
    decimal? Maximum = null,
    string? TextValue = null,
    DateOnly? DateValue = null,
    DateOnly? DateMinimum = null,
    DateOnly? DateMaximum = null);

public sealed record AdvancedSearchQuery(
    SearchEntityKind Entity,
    SearchFilterLogic Logic,
    IReadOnlyList<SearchFilterCondition> Conditions,
    SearchFilterLogic PositionLogic = SearchFilterLogic.All,
    IReadOnlyList<AdvancedSearchFieldDefinition>? IndexedFields = null,
    bool IncludeWomenPlayers = false)
{
    /// <summary>Indicates that every selected field has a query implementation.</summary>
    public bool CanExecute => Conditions.Count > 0 && Conditions.All(condition =>
        AdvancedSearchQueryValidator.ResolveField(Entity, condition.FieldKey, IndexedFields)?.CanExecute == true);
}

public sealed record SearchValidationIssue(string FieldKey, string Message);

public sealed record SearchValidationResult(IReadOnlyList<SearchValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Validates query structure, implementation status and field/operator compatibility.
/// Validation does not itself execute a query.
/// </summary>
public static class AdvancedSearchQueryValidator
{
    public static SearchValidationResult Validate(AdvancedSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var issues = new List<SearchValidationIssue>();
        if (query.Conditions.Count == 0)
            issues.Add(new SearchValidationIssue(string.Empty, "Add at least one search condition."));

        foreach (var condition in query.Conditions)
        {
            var field = ResolveField(query.Entity, condition.FieldKey, query.IndexedFields);
            if (field is null)
            {
                issues.Add(new SearchValidationIssue(condition.FieldKey, "The field is not documented for this entity."));
                continue;
            }

            if (!field.CanExecute)
            {
                issues.Add(new SearchValidationIssue(
                    condition.FieldKey,
                    "This field is documented but its query implementation is still In Progress."));
                continue;
            }

            ValidateOperatorAndValue(field, condition, issues);
        }

        return new SearchValidationResult(issues.AsReadOnly());
    }

    internal static AdvancedSearchFieldDefinition? ResolveField(
        SearchEntityKind entity,
        string fieldKey,
        IReadOnlyList<AdvancedSearchFieldDefinition>? indexedFields = null)
    {
        var indexed = indexedFields?.FirstOrDefault(field =>
            field.SupportedEntities.Contains(entity) &&
            string.Equals(field.Key, fieldKey, StringComparison.OrdinalIgnoreCase));
        if (indexed is not null)
            return indexed;

        var direct = AdvancedSearchCatalog.Fields.FirstOrDefault(field =>
            field.SupportedEntities.Contains(entity) &&
            string.Equals(field.Key, fieldKey, StringComparison.OrdinalIgnoreCase));

        if (direct is not null)
            return direct;

        var attributeCategories = new[]
        {
            AdvancedSearchCategoryKind.TechnicalGoalkeeping,
            AdvancedSearchCategoryKind.MentalAttributes,
            AdvancedSearchCategoryKind.PhysicalHidden
        };

        foreach (var category in attributeCategories)
        {
            var attribute = AdvancedSearchCatalog.GetAttributeFields(entity, category)
                .FirstOrDefault(field => string.Equals(field.Key, fieldKey, StringComparison.OrdinalIgnoreCase));
            if (attribute is not null)
                return attribute;
        }

        return null;
    }

    private static void ValidateBounds(
        AdvancedSearchFieldDefinition field,
        decimal? minimum,
        decimal? maximum,
        ICollection<SearchValidationIssue> issues)
    {
        if (field.MinimumAllowed is decimal lower &&
            (minimum is decimal minimumBelow && minimumBelow < lower ||
             maximum is decimal maximumBelow && maximumBelow < lower))
            issues.Add(new SearchValidationIssue(field.Key, $"Values cannot be below {lower}."));

        if (field.MaximumAllowed is decimal upper &&
            (minimum is decimal minimumAbove && minimumAbove > upper ||
             maximum is decimal maximumAbove && maximumAbove > upper))
            issues.Add(new SearchValidationIssue(field.Key, $"Values cannot be above {upper}."));
    }

    private static void ValidateOperatorAndValue(
        AdvancedSearchFieldDefinition field,
        SearchFilterCondition condition,
        ICollection<SearchValidationIssue> issues)
    {
        switch (field.ValueKind)
        {
            case SearchFieldValueKind.IntegerRange:
            case SearchFieldValueKind.DecimalRange:
                if (condition.Operator != SearchFilterOperator.Range)
                {
                    issues.Add(new SearchValidationIssue(field.Key, "This field requires a numeric range operator."));
                    return;
                }

                if (condition.Minimum is null && condition.Maximum is null)
                    issues.Add(new SearchValidationIssue(field.Key, "Enter at least one range boundary."));

                if (condition.Minimum is not null && condition.Maximum is not null &&
                    condition.Minimum > condition.Maximum)
                    issues.Add(new SearchValidationIssue(field.Key, "The minimum cannot be greater than the maximum."));

                ValidateBounds(field, condition.Minimum, condition.Maximum, issues);

                if (field.ValueKind == SearchFieldValueKind.IntegerRange &&
                    (condition.Minimum is decimal min && decimal.Truncate(min) != min ||
                     condition.Maximum is decimal max && decimal.Truncate(max) != max))
                    issues.Add(new SearchValidationIssue(field.Key, "Integer fields require whole-number boundaries."));
                break;

            case SearchFieldValueKind.PositionRating:
                if (condition.Operator != SearchFilterOperator.Range)
                {
                    issues.Add(new SearchValidationIssue(field.Key, "This field requires a numeric range operator."));
                    return;
                }

                if (string.IsNullOrWhiteSpace(condition.TextValue))
                    issues.Add(new SearchValidationIssue(field.Key, "Choose a position."));

                if (condition.Minimum is null && condition.Maximum is null)
                    issues.Add(new SearchValidationIssue(field.Key, "Enter at least one range boundary."));

                if (condition.Minimum is not null && condition.Maximum is not null &&
                    condition.Minimum > condition.Maximum)
                    issues.Add(new SearchValidationIssue(field.Key, "The minimum cannot be greater than the maximum."));
                ValidateBounds(field, condition.Minimum, condition.Maximum, issues);
                break;

            case SearchFieldValueKind.Text:
            case SearchFieldValueKind.EntityLookup:
                if (condition.Operator is not (SearchFilterOperator.Contains or SearchFilterOperator.Equals))
                    issues.Add(new SearchValidationIssue(field.Key, "This field requires Contains or Equals."));
                else if (string.IsNullOrWhiteSpace(condition.TextValue))
                    issues.Add(new SearchValidationIssue(field.Key, "Enter a search value."));
                break;

            case SearchFieldValueKind.Boolean:
                if (condition.Operator is not (SearchFilterOperator.IsTrue or SearchFilterOperator.IsFalse or SearchFilterOperator.IsUnknown))
                    issues.Add(new SearchValidationIssue(field.Key, "This field requires a true/false operator."));
                break;

            case SearchFieldValueKind.DateRange:
                if (condition.Operator is SearchFilterOperator.Before or SearchFilterOperator.After)
                {
                    if (condition.DateValue is null)
                        issues.Add(new SearchValidationIssue(field.Key, "Choose a comparison date."));
                }
                else if (condition.Operator == SearchFilterOperator.Range)
                {
                    if (condition.DateMinimum is null && condition.DateMaximum is null)
                        issues.Add(new SearchValidationIssue(field.Key, "Choose at least one date boundary."));

                    if (condition.DateMinimum is not null && condition.DateMaximum is not null &&
                        condition.DateMinimum > condition.DateMaximum)
                        issues.Add(new SearchValidationIssue(field.Key, "The start date cannot be after the end date."));
                }
                else
                {
                    issues.Add(new SearchValidationIssue(field.Key, "This field requires a date comparison."));
                }
                break;

            case SearchFieldValueKind.Enum:
                if (condition.Operator != SearchFilterOperator.Equals || string.IsNullOrWhiteSpace(condition.TextValue))
                    issues.Add(new SearchValidationIssue(field.Key, "Choose a supported option."));
                break;

            case SearchFieldValueKind.TraitSelector:
                if (condition.Operator is not (SearchFilterOperator.HasTrait or SearchFilterOperator.LacksTrait) ||
                    string.IsNullOrWhiteSpace(condition.TextValue))
                    issues.Add(new SearchValidationIssue(field.Key, "Choose a trait and whether it is present."));
                break;

            default:
                issues.Add(new SearchValidationIssue(field.Key, "The field type is not supported by the query model."));
                break;
        }
    }
}
