using FMAtlas.Domain.Search;
using Xunit;

namespace FMAtlas.Tests;

public sealed class AdvancedSearchQueryValidatorTests
{
    [Fact]
    public void RejectsNumericRangeWhenMinimumExceedsMaximum()
    {
        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("current_ability", SearchFilterOperator.Range, 180, 120)]);

        var result = AdvancedSearchQueryValidator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Message.Contains("minimum cannot be greater"));
    }

    [Fact]
    public void AcceptsOneSidedNumericRange()
    {
        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("current_ability", SearchFilterOperator.Range, Minimum: 150)]);

        Assert.True(AdvancedSearchQueryValidator.Validate(query).IsValid);
        Assert.True(query.CanExecute);
    }

    [Fact]
    public void RejectsNumericRangeWithoutBounds()
    {
        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("current_ability", SearchFilterOperator.Range)]);

        Assert.False(AdvancedSearchQueryValidator.Validate(query).IsValid);
    }

    [Fact]
    public void RejectsUnknownOrWrongEntityFields()
    {
        var query = new AdvancedSearchQuery(
            SearchEntityKind.Club,
            SearchFilterLogic.All,
            [new SearchFilterCondition("age", SearchFilterOperator.Range, Minimum: 18)]);

        Assert.False(AdvancedSearchQueryValidator.Validate(query).IsValid);
    }

    [Fact]
    public void TextAndBooleanFieldsRequireCompatibleOperators()
    {
        var textQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("name", SearchFilterOperator.Contains)]);

        var booleanQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("wonderkid", SearchFilterOperator.Contains, TextValue: "yes")]);

        Assert.False(AdvancedSearchQueryValidator.Validate(textQuery).IsValid);
        Assert.False(AdvancedSearchQueryValidator.Validate(booleanQuery).IsValid);
    }

    [Fact]
    public void PreservesExplicitAllAndAnyLogicAndRejectsEmptyQueries()
    {
        var all = new AdvancedSearchQuery(SearchEntityKind.Player, SearchFilterLogic.All, []);
        var any = new AdvancedSearchQuery(SearchEntityKind.Player, SearchFilterLogic.Any, []);

        Assert.Equal(SearchFilterLogic.All, all.Logic);
        Assert.Equal(SearchFilterLogic.Any, any.Logic);
        Assert.False(AdvancedSearchQueryValidator.Validate(all).IsValid);
        Assert.False(AdvancedSearchQueryValidator.Validate(any).IsValid);
        Assert.False(all.CanExecute);
        Assert.False(any.CanExecute);
    }

    [Fact]
    public void RejectsDocumentedButUnimplementedFields()
    {
        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("recommended_ca", SearchFilterOperator.Range, Minimum: 150)]);

        Assert.False(AdvancedSearchQueryValidator.Validate(query).IsValid);
        Assert.False(query.CanExecute);
    }

    [Fact]
    public void IntegerRangesRequireWholeNumberBoundaries()
    {
        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("age", SearchFilterOperator.Range, Minimum: 18.5m)]);

        Assert.False(AdvancedSearchQueryValidator.Validate(query).IsValid);
    }

    [Fact]
    public void AttributeRangesAcceptOneSidedBoundsButRejectValuesOutsideZeroToTwenty()
    {
        var minimumOnly = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("attribute_dribbling", SearchFilterOperator.Range, Minimum: 12)]);
        var maximumOnly = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("attribute_dribbling", SearchFilterOperator.Range, Maximum: 8)]);
        var outOfRange = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("attribute_dribbling", SearchFilterOperator.Range, Minimum: 21)]);

        Assert.True(AdvancedSearchQueryValidator.Validate(minimumOnly).IsValid);
        Assert.True(AdvancedSearchQueryValidator.Validate(maximumOnly).IsValid);
        Assert.False(AdvancedSearchQueryValidator.Validate(outOfRange).IsValid);
    }

    [Fact]
    public void BooleanFieldsAcceptUnknownAsASeparateState()
    {
        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("is_newgen", SearchFilterOperator.IsUnknown)]);

        Assert.True(AdvancedSearchQueryValidator.Validate(query).IsValid);
        Assert.True(query.CanExecute);
    }

    [Fact]
    public void StaffRoleAndNewgenFiltersAreValidOneSidedRangesAndUnknownOptions()
    {
        var roleQuery = new AdvancedSearchQuery(
            SearchEntityKind.Coach,
            SearchFilterLogic.All,
            [new SearchFilterCondition("role_preference_scout", SearchFilterOperator.Range, Minimum: 12)]);
        var newgenQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("is_newgen", SearchFilterOperator.IsTrue)]);

        Assert.True(AdvancedSearchQueryValidator.Validate(roleQuery).IsValid);
        Assert.True(AdvancedSearchQueryValidator.Validate(newgenQuery).IsValid);
    }

    [Fact]
    public void SavedSearchPresetRoundTripsAllCriteriaAndMatchingModes()
    {
        var preset = new AdvancedSearchPreset(
            Version: 1,
            Name: "Creative right winger",
            Entity: SearchEntityKind.Player,
            Logic: SearchFilterLogic.All,
            PositionLogic: SearchFilterLogic.Any,
            Conditions:
            [
                new SearchFilterCondition("attribute_dribbling", SearchFilterOperator.Range, Minimum: 12),
                new SearchFilterCondition("position_amr", SearchFilterOperator.Range, Minimum: 15),
                new SearchFilterCondition("is_newgen", SearchFilterOperator.IsTrue),
                new SearchFilterCondition("contract_expiry", SearchFilterOperator.Range,
                    DateMinimum: new DateOnly(2027, 1, 1))
            ]);

        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var json = System.Text.Json.JsonSerializer.Serialize(preset, options);
        var restored = System.Text.Json.JsonSerializer.Deserialize<AdvancedSearchPreset>(json, options);

        Assert.NotNull(restored);
        Assert.Equal(preset.Name, restored.Name);
        Assert.Equal(preset.Entity, restored.Entity);
        Assert.Equal(preset.Logic, restored.Logic);
        Assert.Equal(preset.PositionLogic, restored.PositionLogic);
        Assert.Equal(preset.Conditions, restored.Conditions);
    }

    [Fact]
    public void DateRangeRequiresValidOrderedBounds()
    {
        var valid = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition(
                "contract_expiry", SearchFilterOperator.Range,
                DateMinimum: new DateOnly(2027, 1, 1),
                DateMaximum: new DateOnly(2028, 1, 1))]);

        var invalid = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition(
                "contract_expiry", SearchFilterOperator.Range,
                DateMinimum: new DateOnly(2028, 1, 1),
                DateMaximum: new DateOnly(2027, 1, 1))]);

        Assert.True(AdvancedSearchQueryValidator.Validate(valid).IsValid);
        Assert.False(AdvancedSearchQueryValidator.Validate(invalid).IsValid);
    }
}
