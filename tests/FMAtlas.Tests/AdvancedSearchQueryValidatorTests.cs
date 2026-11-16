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
    public void PreservesExplicitAllAndAnyLogic()
    {
        var all = new AdvancedSearchQuery(SearchEntityKind.Player, SearchFilterLogic.All, []);
        var any = new AdvancedSearchQuery(SearchEntityKind.Player, SearchFilterLogic.Any, []);

        Assert.Equal(SearchFilterLogic.All, all.Logic);
        Assert.Equal(SearchFilterLogic.Any, any.Logic);
        Assert.True(AdvancedSearchQueryValidator.Validate(all).IsValid);
        Assert.True(AdvancedSearchQueryValidator.Validate(any).IsValid);
    }

    [Fact]
    public void DocumentedFieldsAreStructurallyValidButNotExecutableYet()
    {
        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("current_ability", SearchFilterOperator.Range, Minimum: 100)]);

        Assert.True(AdvancedSearchQueryValidator.Validate(query).IsValid);
        Assert.False(query.CanExecute);
    }
}
