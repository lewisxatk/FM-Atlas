using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class PositionSuitabilityServiceTests
{
    [TestMethod]
    public void Build_MapsAllFifteenFmsavePositionRatingsIncludingStcAndWings()
    {
        var ratings = PositionSuitabilityService.Build(new Dictionary<string, double>
        {
            ["positions.gk"] = 20,
            ["sw"] = 9,
            ["position_dl"] = 16,
            ["dc"] = 14,
            ["dr"] = 13,
            ["dm"] = 12,
            ["ml"] = 11,
            ["mc"] = 10,
            ["mr"] = 9,
            ["aml"] = 8,
            ["amc"] = 7,
            ["amr"] = 6,
            ["stc"] = 19,
            ["wbl"] = 18,
            ["pos_wbr"] = 15
        });

        Assert.AreEqual(15, ratings.Count);
        Assert.AreEqual(20d, ratings.Single(x => x.Key == "gk").Rating);
        Assert.AreEqual(16d, ratings.Single(x => x.Key == "dl").Rating);
        Assert.AreEqual(19d, ratings.Single(x => x.Key == "stc").Rating);
        Assert.AreEqual(15d, ratings.Single(x => x.Key == "wbr").Rating);
        Assert.IsTrue(ratings.All(x => x.Rating.HasValue));
    }

    [TestMethod]
    public void Build_UsesFiveExplicitBandsAndKeepsMissingOrInvalidRatingsUnavailable()
    {
        var ratings = PositionSuitabilityService.Build(new Dictionary<string, double>
        {
            ["gk"] = 20,
            ["sw"] = 18,
            ["dl"] = 17,
            ["dc"] = 15,
            ["dr"] = 14,
            ["dm"] = 10,
            ["ml"] = 9,
            ["mc"] = 5,
            ["mr"] = 4,
            ["aml"] = 0,
            ["amc"] = 21,
            ["amr"] = double.NaN
        });

        Assert.AreEqual(PositionSuitabilityBand.Natural, ratings.Single(x => x.Key == "gk").Band);
        Assert.AreEqual(PositionSuitabilityBand.Natural, ratings.Single(x => x.Key == "sw").Band);
        Assert.AreEqual(PositionSuitabilityBand.Accomplished, ratings.Single(x => x.Key == "dl").Band);
        Assert.AreEqual(PositionSuitabilityBand.Accomplished, ratings.Single(x => x.Key == "dc").Band);
        Assert.AreEqual(PositionSuitabilityBand.Developing, ratings.Single(x => x.Key == "dr").Band);
        Assert.AreEqual(PositionSuitabilityBand.Developing, ratings.Single(x => x.Key == "dm").Band);
        Assert.AreEqual(PositionSuitabilityBand.Limited, ratings.Single(x => x.Key == "ml").Band);
        Assert.AreEqual(PositionSuitabilityBand.Limited, ratings.Single(x => x.Key == "mc").Band);
        Assert.AreEqual(PositionSuitabilityBand.Low, ratings.Single(x => x.Key == "mr").Band);
        Assert.AreEqual(PositionSuitabilityBand.Low, ratings.Single(x => x.Key == "aml").Band);

        var unavailable = ratings.Where(x => x.Key is "amc" or "amr" or "stc" or "wbl" or "wbr").ToArray();
        Assert.IsTrue(unavailable.All(x => x.Band == PositionSuitabilityBand.Unavailable && x.Rating is null));
    }

    [TestMethod]
    public void GetBand_RejectsNonFiniteAndOutOfRangeInputs()
    {
        Assert.AreEqual(PositionSuitabilityBand.Unavailable, PositionSuitabilityService.GetBand(null));
        Assert.AreEqual(PositionSuitabilityBand.Unavailable, PositionSuitabilityService.GetBand(double.NaN));
        Assert.AreEqual(PositionSuitabilityBand.Unavailable, PositionSuitabilityService.GetBand(20.1));
        Assert.AreEqual(PositionSuitabilityBand.Unavailable, PositionSuitabilityService.GetBand(-0.1));
    }
}
