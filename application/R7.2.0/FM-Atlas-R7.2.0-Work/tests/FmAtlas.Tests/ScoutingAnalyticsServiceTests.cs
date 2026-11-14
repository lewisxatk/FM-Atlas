using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class ScoutingAnalyticsServiceTests
{
    private static ScoutingPlayerSeason Player(string id, double value, double minutes = 900,
        string position = "Centre-back", string competition = "Premier League") =>
        new(id, id, "Club " + id, competition, "2025/26", position, minutes,
            new Dictionary<string, double?> { ["progressive_passes"] = value },
            "Test fixture", "1", true);

    [Fact]
    public void Per90NormalisationUsesActualMinutesAndPreservesRawTotal()
    {
        var result = ScoutingAnalyticsService.RankMetric(
            new[] { Player("A", 20, 900), Player("B", 40, 1800) },
            new ScoutingMetricDefinition("progressive_passes", "Progressive passes", "count", true),
            minimumCohortSize: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(2d, result.Single(x => x.SourceId == "A").Per90Value);
        Assert.Equal(2d, result.Single(x => x.SourceId == "B").Per90Value);
        Assert.Equal(20d, result.Single(x => x.SourceId == "A").RawValue);
    }

    [Fact]
    public void MinimumMinutesGateExcludesInsufficientSamples()
    {
        var result = ScoutingAnalyticsService.RankMetric(
            new[] { Player("A", 20, 300), Player("B", 40, 900), Player("C", 60, 1200) },
            new ScoutingMetricDefinition("progressive_passes", "Progressive passes", "count", true, true, 450),
            minimumCohortSize: 2);

        Assert.DoesNotContain(result, x => x.SourceId == "A");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void PercentilesAreCompetitionSeasonAndPositionScoped()
    {
        var players = new[]
        {
            Player("A", 10), Player("B", 20), Player("C", 30),
            Player("D", 100, position: "Striker"),
            Player("E", 100, competition: "La Liga")
        };
        var result = ScoutingAnalyticsService.RankMetric(players,
            new ScoutingMetricDefinition("progressive_passes", "Progressive passes", "count"),
            competition: "Premier League", season: "2025/26", positionGroup: "Centre-back",
            minimumCohortSize: 3);

        Assert.Equal(3, result.Count);
        Assert.All(result, x => Assert.Equal(3, x.CohortSize));
        Assert.Equal(100, result[0].Percentile);
        Assert.Equal(0, result[^1].Percentile);
    }

    [Fact]
    public void LowerIsBetterReversesPercentileDirection()
    {
        var result = ScoutingAnalyticsService.RankMetric(
            new[] { Player("A", 1), Player("B", 2), Player("C", 3) },
            new ScoutingMetricDefinition("progressive_passes", "Turnovers", "count", false, false),
            minimumCohortSize: 3);

        Assert.Equal("A", result[0].SourceId);
        Assert.Equal(100, result[0].Percentile);
    }

    [Fact]
    public void SmallCohortDoesNotInventPercentile()
    {
        var result = ScoutingAnalyticsService.RankMetric(
            new[] { Player("A", 1), Player("B", 2) },
            new ScoutingMetricDefinition("progressive_passes", "Progressive passes", "count"),
            minimumCohortSize: 5);

        Assert.All(result, x => Assert.Null(x.Percentile));
    }

    [Fact]
    public void CoverageCountsOnlyFinitePresentFacts()
    {
        var metrics = new Dictionary<string, double?>
        {
            ["a"] = 10, ["b"] = null, ["c"] = double.NaN
        };
        Assert.Equal(1d / 3d, ScoutingAnalyticsService.CalculateCoverage(metrics, new[] { "a", "b", "c" }), 6);
    }
}
