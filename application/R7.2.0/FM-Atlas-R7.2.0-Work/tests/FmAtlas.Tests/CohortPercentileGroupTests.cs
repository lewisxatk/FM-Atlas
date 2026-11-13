using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class CohortPercentileGroupTests
{
    [TestMethod]
    public void CalculateByGroup_RanksEachPrimaryPositionIndependently()
    {
        var observations = new[]
        {
            new CohortMetricObservation("gk-1", "Keeper One", 10, "GK"),
            new CohortMetricObservation("gk-2", "Keeper Two", 20, "GK"),
            new CohortMetricObservation("gk-3", "Keeper Three", 30, "GK"),
            new CohortMetricObservation("st-1", "Striker One", 10, "STC"),
            new CohortMetricObservation("st-2", "Striker Two", 20, "STC"),
            new CohortMetricObservation("st-3", "Striker Three", 30, "STC")
        };

        var results = CohortPercentileService.CalculateByGroup(observations, minimumSampleSize: 3);

        Assert.AreEqual(6, results.Count);
        Assert.IsTrue(results.All(x => x.SampleSize == 3));
        Assert.IsTrue(results.All(x => x.Basis.Contains("Cohort group", StringComparison.Ordinal)));
        Assert.AreEqual(50d, results.Single(x => x.Id == "gk-2").Percentile!.Value, 0.0001);
        Assert.AreEqual(50d, results.Single(x => x.Id == "st-2").Percentile!.Value, 0.0001);
        Assert.AreEqual(16.6667d, results.Single(x => x.Id == "gk-1").Percentile!.Value, 0.001);
        Assert.AreEqual(83.3333d, results.Single(x => x.Id == "st-3").Percentile!.Value, 0.001);
    }

    [TestMethod]
    public void CalculateByGroup_ExcludesUnknownPositionAndAppliesMinimumPerGroup()
    {
        var observations = new[]
        {
            new CohortMetricObservation("gk-1", "Keeper One", 10, "GK"),
            new CohortMetricObservation("gk-2", "Keeper Two", 20, "GK"),
            new CohortMetricObservation("st-1", "Striker One", 10, "STC"),
            new CohortMetricObservation("unknown", "Unclassified Player", 999, null),
            new CohortMetricObservation("blank", "Blank Position", 999, " ")
        };

        var results = CohortPercentileService.CalculateByGroup(observations, minimumSampleSize: 3);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.All(x => x.Percentile is null));
        Assert.IsTrue(results.All(x => x.SampleSize < 3));
        Assert.IsFalse(results.Any(x => x.Id is "unknown" or "blank"));
    }

    [TestMethod]
    public void CalculateByGroup_LowerIsBetterRanksSmallerValuesHigherAndPreservesTies()
    {
        var observations = new[]
        {
            new CohortMetricObservation("low", "Low value", 10, "STC"),
            new CohortMetricObservation("middle-a", "Middle A", 20, "STC"),
            new CohortMetricObservation("middle-b", "Middle B", 20, "STC"),
            new CohortMetricObservation("high", "High value", 30, "STC")
        };

        var results = CohortPercentileService.CalculateByGroup(observations, minimumSampleSize: 4, higherIsBetter: false);

        var lowPercentile = results.Single(x => x.Id == "low").Percentile!.Value;
        var highPercentile = results.Single(x => x.Id == "high").Percentile!.Value;
        Assert.AreEqual(100d - highPercentile, lowPercentile, 0.0001);
        Assert.IsTrue(lowPercentile > highPercentile);
        Assert.AreEqual(results.Single(x => x.Id == "middle-a").Percentile!.Value, results.Single(x => x.Id == "middle-b").Percentile!.Value, 0.0001);
        Assert.IsTrue(results.All(x => x.Basis.Contains("lower values rank higher", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CalculateByGroup_UsesExactAgeBandAndImportedLeagueLabelsAsIndependentKeys()
    {
        var observations = new[]
        {
            new CohortMetricObservation("age-1", "Age 1", 10, "20–24"),
            new CohortMetricObservation("age-2", "Age 2", 20, "20–24"),
            new CohortMetricObservation("age-3", "Age 3", 30, "20–24"),
            new CohortMetricObservation("league-1", "League 1", 5, "Premier Division"),
            new CohortMetricObservation("league-2", "League 2", 15, "Premier Division"),
            new CohortMetricObservation("league-3", "League 3", 25, "Premier Division")
        };

        var results = CohortPercentileService.CalculateByGroup(observations, minimumSampleSize: 3);

        Assert.AreEqual(6, results.Count);
        Assert.AreEqual(3, results.Single(x => x.Id == "age-2").SampleSize);
        Assert.AreEqual(3, results.Single(x => x.Id == "league-2").SampleSize);
        Assert.IsTrue(results.Single(x => x.Id == "age-2").Basis.Contains("Cohort group '20–24'"));
        Assert.IsTrue(results.Single(x => x.Id == "league-2").Basis.Contains("Cohort group 'Premier Division'"));
        Assert.AreEqual(50d, results.Single(x => x.Id == "age-2").Percentile!.Value, 0.0001);
        Assert.AreEqual(50d, results.Single(x => x.Id == "league-2").Percentile!.Value, 0.0001);
    }

}
