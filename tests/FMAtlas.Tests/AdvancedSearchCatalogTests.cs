using FMAtlas.Domain.Players;
using FMAtlas.Domain.Search;
using Xunit;

namespace FMAtlas.Tests;

public sealed class AdvancedSearchCatalogTests
{
    [Fact]
    public void CategoriesMatchTheApprovedFmrteNavigationOrder()
    {
        Assert.Equal(new[]
        {
            AdvancedSearchCategoryKind.General,
            AdvancedSearchCategoryKind.Contract,
            AdvancedSearchCategoryKind.TechnicalGoalkeeping,
            AdvancedSearchCategoryKind.MentalAttributes,
            AdvancedSearchCategoryKind.PhysicalHidden,
            AdvancedSearchCategoryKind.PreferredMoves,
            AdvancedSearchCategoryKind.Relations
        }, AdvancedSearchCatalog.Categories.OrderBy(x => x.Order).Select(x => x.Kind));
    }

    [Fact]
    public void OnlyFieldsWithAnImplementedStorageMappingAreReady()
    {
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "name" && x.CanExecute);
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "age" && x.CanExecute);
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "contract_expiry" && x.CanExecute);
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "wonderkid" && !x.CanExecute);
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "relations" && !x.CanExecute);
    }

    [Fact]
    public void GeneralCategoryExposesPlayerCoachAndClubFieldsByEntity()
    {
        Assert.Contains(AdvancedSearchCatalog.GetFields(SearchEntityKind.Player, AdvancedSearchCategoryKind.General), x => x.Key == "age");
        Assert.Contains(AdvancedSearchCatalog.GetFields(SearchEntityKind.Coach, AdvancedSearchCategoryKind.General), x => x.Key == "nationality");
        Assert.Contains(AdvancedSearchCatalog.GetFields(SearchEntityKind.Club, AdvancedSearchCategoryKind.General), x => x.Key == "club_facilities");
        Assert.DoesNotContain(AdvancedSearchCatalog.GetFields(SearchEntityKind.Club, AdvancedSearchCategoryKind.General), x => x.Key == "age");
    }

    [Fact]
    public void PlayerTechnicalAndGoalkeepingSearchFieldsUseTheAttributeCatalogue()
    {
        var fields = AdvancedSearchCatalog.GetAttributeFields(
            SearchEntityKind.Player, AdvancedSearchCategoryKind.TechnicalGoalkeeping);

        Assert.Contains(fields, x => x.Key == "attribute_corners" && x.CanExecute);
        Assert.Contains(fields, x => x.Key == "attribute_aerial_reach" && x.CanExecute);
        Assert.Contains(fields, x => x.Key == "attribute_first_touch" && x.CanExecute);
        Assert.Equal(fields.Count, fields.Select(x => x.Key).Distinct().Count());
    }

    [Fact]
    public void MentalSearchIncludesSeparatePersonalityFields()
    {
        var fields = AdvancedSearchCatalog.GetAttributeFields(
            SearchEntityKind.Player, AdvancedSearchCategoryKind.MentalAttributes);

        Assert.Contains(fields, x => x.Key == "attribute_work_rate" && x.CanExecute);
        Assert.Contains(fields, x => x.Key == "personality_professionalism" && x.CanExecute);
        Assert.Contains(fields, x => x.DisplayName == "Personality: Professionalism");
    }

    [Fact]
    public void PhysicalAndHiddenSearchFieldsRemainTogetherInTheirFmrteCategory()
    {
        var fields = AdvancedSearchCatalog.GetAttributeFields(
            SearchEntityKind.Player, AdvancedSearchCategoryKind.PhysicalHidden);

        Assert.Contains(fields, x => x.Key == "attribute_pace" && x.CanExecute);
        Assert.Contains(fields, x => x.Key == "attribute_consistency" && x.CanExecute);
        Assert.Equal(fields.Count, fields.Select(x => x.Key).Distinct().Count());
    }

    [Fact]
    public void CoachAndClubDoNotReceivePlayerOnlyAttributeRules()
    {
        Assert.Empty(AdvancedSearchCatalog.GetAttributeFields(
            SearchEntityKind.Coach, AdvancedSearchCategoryKind.TechnicalGoalkeeping));
        Assert.Empty(AdvancedSearchCatalog.GetAttributeFields(
            SearchEntityKind.Club, AdvancedSearchCategoryKind.MentalAttributes));
    }
}
