using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class RecruitmentIntelligenceServiceTests
{
    [TestMethod]
    public void RankOpportunities_DoesNotSubstituteMarketValueForMissingAskingPrice()
    {
        var result = RecruitmentIntelligenceService.RankOpportunities(
            new[] { new RecruitmentSlotNeed("ST", "Striker", 12) },
            new[]
            {
                new RecruitmentTargetCandidate("SAVE:1", "Target",
                    new Dictionary<string, double> { ["ST"] = 17 },
                    MarketValue: 1500000m, AskingPrice: null, WeeklyWage: null)
            },
            new RecruitmentBudgetLimit(2000000m, 25000m));

        var opportunity = result.Single();
        Assert.AreEqual(5d, opportunity.Improvement!.Value, 0.0001);
        Assert.AreEqual(1500000m, opportunity.MarketValue);
        Assert.IsNull(opportunity.AskingPrice);
        Assert.AreEqual(RecruitmentBudgetAssessment.DataUnavailable, opportunity.TransferBudgetAssessment);
        Assert.AreEqual(RecruitmentBudgetAssessment.DataUnavailable, opportunity.WageBudgetAssessment);
    }

    [TestMethod]
    public void RankOpportunities_UsesAskingPriceAndWeeklyWageAgainstSeparateBudgets()
    {
        var result = RecruitmentIntelligenceService.RankOpportunities(
            new[] { new RecruitmentSlotNeed("MC", "Midfielder", 14) },
            new[]
            {
                new RecruitmentTargetCandidate("SAVE:2", "Target",
                    new Dictionary<string, double> { ["MC"] = 18 },
                    MarketValue: 10000000m, AskingPrice: 3000000m, WeeklyWage: 15000m)
            },
            new RecruitmentBudgetLimit(2000000m, 20000m));

        Assert.AreEqual(RecruitmentBudgetAssessment.OverBudget, result.Single().TransferBudgetAssessment);
        Assert.AreEqual(RecruitmentBudgetAssessment.WithinBudget, result.Single().WageBudgetAssessment);
        Assert.AreEqual(10000000m, result.Single().MarketValue);
    }

    [TestMethod]
    public void RankOpportunities_ReportsUnconfiguredBudgetsAndUnknownIncumbentLift()
    {
        var result = RecruitmentIntelligenceService.RankOpportunities(
            new[] { new RecruitmentSlotNeed("GK", "Goalkeeper", null) },
            new[]
            {
                new RecruitmentTargetCandidate("SAVE:3", "Keeper",
                    new Dictionary<string, double> { ["GK"] = 16 },
                    AskingPrice: 1000000m, WeeklyWage: 10000m)
            },
            new RecruitmentBudgetLimit(null, null));

        Assert.IsNull(result.Single().Improvement);
        Assert.AreEqual(RecruitmentBudgetAssessment.BudgetNotConfigured, result.Single().TransferBudgetAssessment);
        Assert.AreEqual(RecruitmentBudgetAssessment.BudgetNotConfigured, result.Single().WageBudgetAssessment);
    }

    [TestMethod]
    public void RankOpportunities_RejectsInvalidScoresAndDuplicateSourceIds()
    {
        var slots = new[] { new RecruitmentSlotNeed("ST", "Striker", 10) };
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RecruitmentIntelligenceService.RankOpportunities(
            slots,
            new[] { new RecruitmentTargetCandidate("SAVE:1", "Invalid", new Dictionary<string, double> { ["ST"] = 21 }) },
            new RecruitmentBudgetLimit(null, null)));

        var duplicate = new[]
        {
            new RecruitmentTargetCandidate("SAVE:1", "One", new Dictionary<string, double> { ["ST"] = 10 }),
            new RecruitmentTargetCandidate("SAVE:1", "Two", new Dictionary<string, double> { ["ST"] = 11 })
        };
        Assert.ThrowsExactly<ArgumentException>(() => RecruitmentIntelligenceService.RankOpportunities(
            slots, duplicate, new RecruitmentBudgetLimit(null, null)));
    }
}
