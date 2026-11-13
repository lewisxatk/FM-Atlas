using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class MoneyballCompositeServiceTests
{
    [TestMethod]
    public void Calculate_UsesEqualWeightsByDefaultAndRanksByComposite()
    {
        var observations = new[]
        {
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "goals", 90),
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "assists", 70),
            new MoneyballMetricPercentileObservation("P2", "Player Two", "2025/26", "goals", 60),
            new MoneyballMetricPercentileObservation("P2", "Player Two", "2025/26", "assists", 80)
        };

        var results = MoneyballCompositeService.Calculate(observations, minimumMetrics: 2);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("P1", results[0].SourceId);
        Assert.AreEqual(80d, results[0].CompositePercentile, 0.0001);
        Assert.AreEqual(70d, results[1].CompositePercentile, 0.0001);
        Assert.AreEqual(2, results[0].MetricsAvailable);
        StringAssert.Contains(results[0].Basis, "Atlas composite only");
    }

    [TestMethod]
    public void Calculate_AppliesConfiguredWeightsAndKeepsPlayerPeriodsSeparate()
    {
        var observations = new[]
        {
            new MoneyballMetricPercentileObservation("P1", "Player One", "2024/25", "goals", 100),
            new MoneyballMetricPercentileObservation("P1", "Player One", "2024/25", "assists", 0),
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "goals", 20),
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "assists", 80)
        };
        var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["goals"] = 3,
            ["assists"] = 1
        };

        var results = MoneyballCompositeService.Calculate(observations, weights, minimumMetrics: 2);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual(75d, results.Single(x => x.Period == "2024/25").CompositePercentile, 0.0001);
        Assert.AreEqual(35d, results.Single(x => x.Period == "2025/26").CompositePercentile, 0.0001);
    }

    [TestMethod]
    public void Calculate_RequiresMinimumValidMetricsAndExcludesInvalidPercentiles()
    {
        var observations = new[]
        {
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "goals", 80),
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "assists", null),
            new MoneyballMetricPercentileObservation("P2", "Player Two", "2025/26", "goals", 80),
            new MoneyballMetricPercentileObservation("P2", "Player Two", "2025/26", "assists", 70),
            new MoneyballMetricPercentileObservation("P2", "Player Two", "2025/26", "tackles", 101)
        };

        var results = MoneyballCompositeService.Calculate(observations, minimumMetrics: 2);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("P2", results[0].SourceId);
        Assert.AreEqual(75d, results[0].CompositePercentile, 0.0001);
        Assert.AreEqual(2, results[0].MetricsAvailable);
    }

    [TestMethod]
    public void Calculate_RejectsDuplicatePlayerPeriodMetricRows()
    {
        var observations = new[]
        {
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "goals", 80),
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "goals", 85)
        };

        Assert.ThrowsExactly<ArgumentException>(() => MoneyballCompositeService.Calculate(observations));
    }

    [TestMethod]
    public void Calculate_RejectsInvalidWeightsAndMinimumCoverage()
    {
        var observations = new[]
        {
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "goals", 80)
        };
        var weights = new Dictionary<string, double> { ["goals"] = double.NaN };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MoneyballCompositeService.Calculate(observations, weights));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MoneyballCompositeService.Calculate(observations, minimumMetrics: 0));
    }

    [TestMethod]
    public void Calculate_OnlyReportsGroupWhenAllMetricsShareTheSameGroup()
    {
        var observations = new[]
        {
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "goals", 80, "STC"),
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "assists", 70, "STC"),
            new MoneyballMetricPercentileObservation("P2", "Player Two", "2025/26", "goals", 80, "STC"),
            new MoneyballMetricPercentileObservation("P2", "Player Two", "2025/26", "assists", 70, "AMC")
        };

        var results = MoneyballCompositeService.Calculate(observations, minimumMetrics: 2);

        Assert.AreEqual("STC", results.Single(x => x.SourceId == "P1").GroupKey);
        Assert.IsNull(results.Single(x => x.SourceId == "P2").GroupKey);
    }
    [TestMethod]
    public void Calculate_RejectsDuplicateWeightKeysAfterNormalisation()
    {
        var observations = new[]
        {
            new MoneyballMetricPercentileObservation("P1", "Player One", "2025/26", "goals", 80)
        };
        var weights = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["goals"] = 1,
            [" Goals "] = 2
        };

        Assert.ThrowsExactly<ArgumentException>(() => MoneyballCompositeService.Calculate(observations, weights));
    }

}
