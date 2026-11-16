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
    DateOnly? DateValue = null);

public sealed record AdvancedSearchQuery(
    SearchEntityKind Entity,
    SearchFilterLogic Logic,
    IReadOnlyList<SearchFilterCondition> Conditions)
{
    /// <summary>
    /// True only when every field has a ready implementation. The current catalogue
    /// deliberately returns false until real providers and query execution are tested.
    /// </summary>
    public bool CanExecute => Conditions.All(condition =>
        AdvancedSearchQueryValidator.ResolveField(Entity, condition.FieldKey)?.CanExecute == true);
}

public sealed record SearchValidationIssue(string FieldKey, string Message);

public sealed record SearchValidationResult(IReadOnlyList<SearchValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Validates query structure and field/operator compatibility. This does not execute
/// a query or claim that the source data is available.
/// </summary>
public static class AdvancedSearchQueryValidator
{
    public static SearchValidationResult Validate(AdvancedSearchQuery query)
    {
        var issues = new List<SearchValidationIssue>();

        foreach (var condition in query.Conditions)
        {
            var field = ResolveField(query.Entity, condition.FieldKey);
            if (field is null)
            {
                issues.Add(new SearchValidationIssue(condition.FieldKey, "The field is not documented for this entity."));
                continue;
            }

            ValidateOperatorAndValue(field, condition, issues);
        }

        return new SearchValidationResult(issues.AsReadOnly());
    }

    internal static AdvancedSearchFieldDefinition? ResolveField(SearchEntityKind entity, string fieldKey)
    {
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

    private static void ValidateOperatorAndValue(
        AdvancedSearchFieldDefinition field,
        SearchFilterCondition condition,
        ICollection<SearchValidationIssue> issues)
    {
        switch (field.ValueKind)
        {
            case SearchFieldValueKind.IntegerRange:
            case SearchFieldValueKind.DecimalRange:
            case SearchFieldValueKind.PositionRating:
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
                break;

            case SearchFieldValueKind.Text:
            case SearchFieldValueKind.EntityLookup:
                if (condition.Operator is not (SearchFilterOperator.Contains or SearchFilterOperator.Equals))
                    issues.Add(new SearchValidationIssue(field.Key, "This field requires Contains or Equals."));
                else if (string.IsNullOrWhiteSpace(condition.TextValue))
                    issues.Add(new SearchValidationIssue(field.Key, "Enter a search value."));
                break;

            case SearchFieldValueKind.Boolean:
                if (condition.Operator is not (SearchFilterOperator.IsTrue or SearchFilterOperator.IsFalse))
                    issues.Add(new SearchValidationIssue(field.Key, "This field requires a true/false operator."));
                break;

            case SearchFieldValueKind.DateRange:
                if (condition.Operator is not (SearchFilterOperator.Before or SearchFilterOperator.After or SearchFilterOperator.Range))
                    issues.Add(new SearchValidationIssue(field.Key, "This field requires a date comparison."));
                else if (condition.DateValue is null)
                    issues.Add(new SearchValidationIssue(field.Key, "Choose a date."));
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
