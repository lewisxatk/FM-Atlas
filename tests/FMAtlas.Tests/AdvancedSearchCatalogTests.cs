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
    public void AllSearchFieldsRemainInProgressUntilProvidersAreVerified()
    {
        Assert.NotEmpty(AdvancedSearchCatalog.Fields);
        Assert.All(AdvancedSearchCatalog.Fields, field =>
        {
            Assert.Equal(FieldEvidenceStatus.Documented, field.EvidenceStatus);
            Assert.Equal(SearchImplementationStatus.InProgress, field.ImplementationStatus);
            Assert.False(field.CanExecute);
        });
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

        Assert.Contains(fields, x => x.Key == "attribute_corners");
        Assert.Contains(fields, x => x.Key == "attribute_aerial_reach");
        Assert.Contains(fields, x => x.Key == "attribute_first_touch");
        Assert.Equal(fields.Count, fields.Select(x => x.Key).Distinct().Count());
    }

    [Fact]
    public void MentalSearchIncludesSeparatePersonalityFields()
    {
        var fields = AdvancedSearchCatalog.GetAttributeFields(
            SearchEntityKind.Player, AdvancedSearchCategoryKind.MentalAttributes);

        Assert.Contains(fields, x => x.Key == "attribute_work_rate");
        Assert.Contains(fields, x => x.Key == "personality_professionalism");
        Assert.Contains(fields, x => x.DisplayName == "Personality: Professionalism");
    }

    [Fact]
    public void PhysicalAndHiddenSearchFieldsRemainTogetherInTheirFmrteCategory()
    {
        var fields = AdvancedSearchCatalog.GetAttributeFields(
            SearchEntityKind.Player, AdvancedSearchCategoryKind.PhysicalHidden);

        Assert.Contains(fields, x => x.Key == "attribute_pace");
        Assert.Contains(fields, x => x.Key == "attribute_consistency");
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
