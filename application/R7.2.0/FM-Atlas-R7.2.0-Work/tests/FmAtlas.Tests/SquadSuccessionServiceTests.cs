using FmAtlas.Models;
using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class SquadSuccessionServiceTests
{
    [TestMethod]
    public void Analyse_ReportsPositionAgeAndHomeGrownFactsFromImportedRecords()
    {
        var players = new[]
        {
            new PlayerRecord { SourceId = "S1", Name = "Young Striker", Position = "STC", Age = 20, HomeGrownClub = true, HomeGrownNation = true },
            new PlayerRecord { SourceId = "S2", Name = "Prime Striker", Position = "STC", Age = 27, HomeGrownClub = false, HomeGrownNation = true },
            new PlayerRecord { SourceId = "S3", Name = "Old Keeper", Position = "GK", Age = 32, HomeGrownClub = true, HomeGrownNation = false },
            new PlayerRecord { SourceId = "S4", Name = "Unknown Age", Position = "GK", Age = null, HomeGrownClub = null, HomeGrownNation = null }
        };

        var result = SquadSuccessionService.Analyse(players);

        Assert.AreEqual(4, result.Players);
        Assert.AreEqual(1, result.Under23);
        Assert.AreEqual(1, result.Under21);
        Assert.AreEqual(2, result.HomeGrownClub);
        Assert.AreEqual(2, result.HomeGrownNation);

        var striker = result.Positions.Single(x => x.Position == "STC");
        Assert.AreEqual(2, striker.Players);
        Assert.AreEqual(1, striker.Under23);
        Assert.AreEqual(1, striker.Under21);
        Assert.AreEqual(1, striker.HomeGrownClub);
        Assert.AreEqual(23.5d, striker.AverageAge!.Value, 0.0001);
        Assert.AreEqual("U23 cover present", striker.SuccessionStatus);

        var keeper = result.Positions.Single(x => x.Position == "GK");
        Assert.AreEqual(0, keeper.Under23);
        Assert.AreEqual(32d, keeper.AverageAge!.Value, 0.0001);
        Assert.AreEqual("No U23 cover in imported squad", keeper.SuccessionStatus);
    }

    [TestMethod]
    public void Analyse_UsesUnspecifiedPositionAndWithholdsAverageForUnknownAges()
    {
        var players = new[]
        {
            new PlayerRecord { SourceId = "S1", Name = "Unknown One", Position = null, Age = null },
            new PlayerRecord { SourceId = "S2", Name = "Unknown Two", Position = " ", Age = 12 },
            new PlayerRecord { SourceId = "S3", Name = "Outside Age", Position = "DC", Age = 99 }
        };

        var result = SquadSuccessionService.Analyse(players);

        Assert.AreEqual(1, result.Positions.Count(x => x.Position == "Unspecified"));
        Assert.IsNull(result.Positions.Single(x => x.Position == "Unspecified").AverageAge);
        Assert.AreEqual(0, result.Under23);
        Assert.IsNull(result.Positions.Single(x => x.Position == "DC").AverageAge);
    }

    [TestMethod]
    public void Analyse_HandlesEmptySquad()
    {
        var result = SquadSuccessionService.Analyse(Array.Empty<PlayerRecord>());

        Assert.AreEqual(0, result.Players);
        Assert.AreEqual(0, result.Under23);
        Assert.AreEqual(0, result.HomeGrownClub);
        Assert.AreEqual(0, result.Positions.Count);
    }
}
