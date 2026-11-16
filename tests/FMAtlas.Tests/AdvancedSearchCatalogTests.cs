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
            AdvancedSearchCategoryKind.Relations,
            AdvancedSearchCategoryKind.IndexedFields
        }, AdvancedSearchCatalog.Categories.OrderBy(x => x.Order).Select(x => x.Kind));
    }

    [Fact]
    public void OnlyFieldsWithAnImplementedStorageMappingAreReady()
    {
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "name" && x.CanExecute);
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "age" && x.CanExecute);
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "date_of_birth" && x.CanExecute);
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "contract_expiry" && x.CanExecute);
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "wonderkid" && x.CanExecute);
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "is_newgen" && x.CanExecute);
        Assert.Contains(AdvancedSearchCatalog.Fields, x => x.Key == "relations" && x.CanExecute);
    }

    [Fact]
    public void GeneralCategoryExposesPlayerCoachAndClubFieldsByEntity()
    {
        Assert.Contains(AdvancedSearchCatalog.GetFields(SearchEntityKind.Player, AdvancedSearchCategoryKind.General), x => x.Key == "age");
        Assert.Contains(AdvancedSearchCatalog.GetFields(SearchEntityKind.Coach, AdvancedSearchCategoryKind.General), x => x.Key == "nationality");
        Assert.Contains(AdvancedSearchCatalog.GetFields(SearchEntityKind.Club, AdvancedSearchCategoryKind.General), x => x.Key == "club_training_facilities");
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
    public void CoachReceivesStaffAttributesAndClubDoesNotReceivePersonAttributes()
    {
        var coachTechnical = AdvancedSearchCatalog.GetAttributeFields(
            SearchEntityKind.Coach, AdvancedSearchCategoryKind.TechnicalGoalkeeping);
        var coachMental = AdvancedSearchCatalog.GetAttributeFields(
            SearchEntityKind.Coach, AdvancedSearchCategoryKind.MentalAttributes);

        Assert.Contains(coachTechnical, x => x.Key == "attribute_attacking" && x.CanExecute);
        Assert.Contains(coachTechnical, x => x.Key == "attribute_working_with_youngsters" && x.CanExecute);
        Assert.Contains(coachMental, x => x.Key == "knowledge_judging_potential" && x.CanExecute);
        Assert.Empty(AdvancedSearchCatalog.GetAttributeFields(
            SearchEntityKind.Club, AdvancedSearchCategoryKind.MentalAttributes));
    }

    [Fact]
    public void PositionAndFootFieldsExposeIndependentZeroToTwentyRanges()
    {
        var general = AdvancedSearchCatalog.GetFields(SearchEntityKind.Player, AdvancedSearchCategoryKind.General);
        var positions = general.Where(x => x.Key.StartsWith("position_", StringComparison.Ordinal)).ToArray();

        Assert.Equal(15, positions.Length);
        Assert.All(positions, field =>
        {
            Assert.True(field.CanExecute);
            Assert.Equal(0m, field.MinimumAllowed);
            Assert.Equal(20m, field.MaximumAllowed);
        });
        Assert.Contains(general, x => x.Key == "left_foot" && x.CanExecute);
        Assert.Contains(general, x => x.Key == "right_foot" && x.CanExecute);
    }

    [Fact]
    public void StaffRoleAndPersonalityCataloguesAreSearchableAtOneToTwenty()
    {
        var general = AdvancedSearchCatalog.GetFields(SearchEntityKind.Coach, AdvancedSearchCategoryKind.General);
        var mental = AdvancedSearchCatalog.GetAttributeFields(SearchEntityKind.Coach, AdvancedSearchCategoryKind.MentalAttributes);

        Assert.Contains(general, x => x.Key == "role_preference_scout" && x.CanExecute);
        Assert.Contains(general, x => x.Key == "role_preference_manager" && x.CanExecute);
        Assert.Contains(mental, x => x.Key == "personality_professionalism" && x.CanExecute);
        Assert.Contains(mental, x => x.Key == "knowledge_judging_ability" && x.CanExecute);
        Assert.All(general.Where(x => x.Key.StartsWith("role_preference_", StringComparison.Ordinal)), field =>
        {
            Assert.Equal(0m, field.MinimumAllowed);
            Assert.Equal(20m, field.MaximumAllowed);
        });
    }

    [Fact]
    public void ClubSearchContainsTheFmrteFacilitiesAttendanceAndFinanceFields()
    {
        var general = AdvancedSearchCatalog.GetFields(SearchEntityKind.Club, AdvancedSearchCategoryKind.General);
        var contract = AdvancedSearchCatalog.GetFields(SearchEntityKind.Club, AdvancedSearchCategoryKind.Contract);

        Assert.Contains(general, x => x.Key == "club_corporate_facilities" && x.CanExecute);
        Assert.Contains(general, x => x.Key == "club_youth_importance" && x.CanExecute);
        Assert.Contains(general, x => x.Key == "club_minimum_attendance" && x.CanExecute);
        Assert.Contains(general, x => x.Key == "club_maximum_attendance" && x.CanExecute);
        Assert.Contains(contract, x => x.Key == "club_total_debt" && x.CanExecute);
        Assert.Contains(contract, x => x.Key == "club_monthly_debt_repayments" && x.CanExecute);
    }

    [Fact]
    public void AllPlayerAttributesAndPersonalityRangesAreBoundedToZeroThroughTwenty()
    {
        var categories = new[]
        {
            AdvancedSearchCategoryKind.TechnicalGoalkeeping,
            AdvancedSearchCategoryKind.MentalAttributes,
            AdvancedSearchCategoryKind.PhysicalHidden
        };

        var fields = categories.SelectMany(category =>
            AdvancedSearchCatalog.GetAttributeFields(SearchEntityKind.Player, category)).ToArray();

        Assert.Contains(fields, x => x.Key == "attribute_dribbling");
        Assert.Contains(fields, x => x.Key == "attribute_injury_proneness");
        Assert.Contains(fields, x => x.Key == "personality_professionalism");
        Assert.All(fields, field =>
        {
            Assert.True(field.CanExecute);
            Assert.Equal(0m, field.MinimumAllowed);
            Assert.Equal(20m, field.MaximumAllowed);
        });
    }
}
