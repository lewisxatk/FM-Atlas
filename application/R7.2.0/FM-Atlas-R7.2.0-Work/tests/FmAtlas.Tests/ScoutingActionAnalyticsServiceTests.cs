using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class ScoutingActionAnalyticsServiceTests
{
    private static UnifiedSoccerAction Action(string id, string type, double sx, double sy, double ex, double ey,
        bool success = true, bool shot = false, bool goal = false, double? xg = null, int sequence = 0) =>
        new("M1", "Test League", "2025/26", 1, sequence, 0, "P1", "T1", "Home",
            "P1", "Player One", type, type.ToLowerInvariant(), success,
            sx, sy, ex, ey, shot, goal, xg, "StatsBomb Open Data", id, sequence);

    [Fact]
    public void XThreatModelFitsFromObservedShotsAndSuccessfulMoves()
    {
        var actions = new[]
        {
            Action("1","pass",30,40,80,40,sequence:1),
            Action("2","shot",100,40,100,40,shot:true,goal:true,xg:.4,sequence:2),
            Action("3","shot",20,20,20,20,shot:true,goal:false,xg:.05,sequence:3)
        };
        var model = ScoutingActionAnalyticsService.FitExpectedThreat(actions, minimumTrainingMatches:1, iterations:100);
        Assert.Equal(16, model.Columns);
        Assert.Equal(12, model.Rows);
        Assert.Equal(192, model.Values.Length);
        Assert.All(model.Values, x => Assert.True(double.IsFinite(x)));
        Assert.Contains("StatsBomb", model.TrainingSource);
    }

    [Fact]
    public void XThreatOnlyValuesSuccessfulPassesAndCarries()
    {
        var actions = new[]
        {
            Action("1","pass",20,40,70,40,sequence:1),
            Action("2","pass",20,40,70,40,success:false,sequence:2),
            Action("3","carry",20,40,70,40,sequence:3),
            Action("4","shot",100,40,100,40,shot:true,goal:true,sequence:4)
        };
        var model = ScoutingActionAnalyticsService.FitExpectedThreat(actions);
        var valued = ScoutingActionAnalyticsService.ValueActions(actions, model);
        Assert.NotNull(valued[0].XtAdded);
        Assert.Null(valued[1].XtAdded);
        Assert.NotNull(valued[2].XtAdded);
        Assert.Null(valued[3].XtAdded);
    }

    [Fact]
    public void XThreatRequiresMinimumTrainingMatchCount()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ScoutingActionAnalyticsService.FitExpectedThreat(new[] { Action("1","pass",10,10,20,20) }, 2));
        Assert.Contains("at least 2", ex.Message);
    }

    [Fact]
    public void PossessionChainsSummariseActionsAndOutcomes()
    {
        var actions = new[]
        {
            Action("1","pass",10,10,20,20,sequence:1),
            Action("2","carry",20,20,30,30,sequence:2),
            Action("3","shot",30,30,110,40,shot:true,goal:true,xg:.3,sequence:3)
        };
        var model = ScoutingActionAnalyticsService.FitExpectedThreat(actions);
        var chains = ScoutingActionAnalyticsService.BuildPossessionChains(
            ScoutingActionAnalyticsService.ValueActions(actions, model));
        var chain = Assert.Single(chains);
        Assert.Equal(3, chain.ActionCount);
        Assert.Equal(1, chain.Passes);
        Assert.Equal(1, chain.Carries);
        Assert.Equal("Goal", chain.Outcome);
        Assert.Equal(1, chain.Goals);
    }

    [Fact]
    public void PlayerAggregationCalculatesPassCompletionAndKeepsXtPer90UnavailableWithoutMinutes()
    {
        var actions = new[]
        {
            Action("1","pass",10,10,20,20,sequence:1),
            Action("2","pass",20,20,30,30,success:false,sequence:2),
            Action("3","pressure",30,30,30,30,sequence:3)
        };
        var model = ScoutingActionAnalyticsService.FitExpectedThreat(actions);
        var player = Assert.Single(ScoutingActionAnalyticsService.AggregatePlayers(
            ScoutingActionAnalyticsService.ValueActions(actions, model)));
        Assert.Equal(2, player.Passes);
        Assert.Equal(1, player.CompletedPasses);
        Assert.Equal(50d, player.PassCompletionPct);
        Assert.Null(player.XtPer90);
    }
}
