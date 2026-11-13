using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class FootballMetricMathTests
{
    [TestMethod]
    public void ToPer90_UsesActualMinutesAsDenominator()
    {
        Assert.AreEqual(2d, FootballMetricMath.ToPer90(20, 900)!.Value, 0.0001);
        Assert.AreEqual(1.8d, FootballMetricMath.ToPer90(18, 900)!.Value, 0.0001);
    }

    [TestMethod]
    public void ToPer90_ReturnsMissingForNoMinutesOrZeroMinutes()
    {
        Assert.IsNull(FootballMetricMath.ToPer90(10, null));
        Assert.IsNull(FootballMetricMath.ToPer90(10, 0));
        Assert.IsNull(FootballMetricMath.ToPer90(null, 900));
    }

    [TestMethod]
    public void ToPercentage_UsesAttemptCountAndDoesNotDivideByZero()
    {
        Assert.AreEqual(75d, FootballMetricMath.ToPercentage(15, 20)!.Value, 0.0001);
        Assert.IsNull(FootballMetricMath.ToPercentage(0, 0));
        Assert.IsNull(FootballMetricMath.ToPercentage(3, null));
    }

    [TestMethod]
    public void CalculatePercentiles_RanksWithinSuppliedCohortAndHandlesTies()
    {
        var observations = new[]
        {
            new MetricObservation("P1", 1, 900),
            new MetricObservation("P2", 2, 900),
            new MetricObservation("P3", 2, 900),
            new MetricObservation("P4", 3, 900),
            new MetricObservation("P5", 4, 900)
        };

        var results = FootballMetricMath.CalculatePercentiles(
            observations, new MetricPercentileOptions(450, 5, true));

        Assert.AreEqual(0d, results.Single(x => x.SourceId == "P1").Percentile!.Value, 0.0001);
        Assert.AreEqual(37.5d, results.Single(x => x.SourceId == "P2").Percentile!.Value, 0.0001);
        Assert.AreEqual(37.5d, results.Single(x => x.SourceId == "P3").Percentile!.Value, 0.0001);
        Assert.AreEqual(100d, results.Single(x => x.SourceId == "P5").Percentile!.Value, 0.0001);
    }

    [TestMethod]
    public void CalculatePercentiles_CanRankLowerValuesAsBetter()
    {
        var observations = new[]
        {
            new MetricObservation("P1", 1, 900),
            new MetricObservation("P2", 2, 900),
            new MetricObservation("P3", 3, 900),
            new MetricObservation("P4", 4, 900),
            new MetricObservation("P5", 5, 900)
        };

        var results = FootballMetricMath.CalculatePercentiles(
            observations, new MetricPercentileOptions(450, 5, false));

        Assert.AreEqual(100d, results.Single(x => x.SourceId == "P1").Percentile!.Value, 0.0001);
        Assert.AreEqual(0d, results.Single(x => x.SourceId == "P5").Percentile!.Value, 0.0001);
    }

    [TestMethod]
    public void CalculatePercentiles_ExcludesPlayersBelowMinutesFloor()
    {
        var observations = new[]
        {
            new MetricObservation("P1", 10, 900),
            new MetricObservation("P2", 9, 899),
            new MetricObservation("P3", 8, 900),
            new MetricObservation("P4", 7, 900),
            new MetricObservation("P5", 6, 900),
            new MetricObservation("P6", 5, 900)
        };

        var results = FootballMetricMath.CalculatePercentiles(
            observations, new MetricPercentileOptions(900, 5, true));

        Assert.AreEqual(MetricRankStatus.InsufficientMinutes, results.Single(x => x.SourceId == "P2").Status);
        Assert.AreEqual(5, results.Single(x => x.SourceId == "P1").CohortSize);
        Assert.AreEqual(100d, results.Single(x => x.SourceId == "P1").Percentile!.Value, 0.0001);
    }

    [TestMethod]
    public void CalculatePercentiles_SeparatesMissingAndInsufficientCohort()
    {
        var observations = new[]
        {
            new MetricObservation("P1", 10, 900),
            new MetricObservation("P2", null, 900),
            new MetricObservation("P3", 8, 900)
        };

        var results = FootballMetricMath.CalculatePercentiles(
            observations, new MetricPercentileOptions(450, 5, true));

        Assert.AreEqual(MetricRankStatus.InsufficientCohort, results.Single(x => x.SourceId == "P1").Status);
        Assert.AreEqual(MetricRankStatus.MissingValue, results.Single(x => x.SourceId == "P2").Status);
        Assert.AreEqual(MetricRankStatus.InsufficientCohort, results.Single(x => x.SourceId == "P3").Status);
        Assert.IsNull(results.Single(x => x.SourceId == "P1").Percentile);
    }

    [TestMethod]
    public void CalculatePercentiles_RejectsDuplicateProviderIdsAndBadInput()
    {
        Assert.ThrowsExactly<ArgumentException>(() => FootballMetricMath.CalculatePercentiles(new[]
        {
            new MetricObservation("P1", 1, 900),
            new MetricObservation("p1", 2, 900)
        }));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => FootballMetricMath.CalculatePercentiles(new[]
        {
            new MetricObservation("P1", 1, -1)
        }));
    }
}
