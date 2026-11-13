using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class PlayerSimilarityServiceTests
{
    [TestMethod]
    public void Rank_ReturnsIdenticalPlayerAsOneHundredAndExcludesTargetIdentity()
    {
        var target = new Dictionary<string, double>
        {
            ["Acceleration"] = 18, ["Pace"] = 17, ["Finishing"] = 16,
            ["Technique"] = 15, ["Composure"] = 14, ["Off The Ball"] = 13
        };
        var candidates = new[]
        {
            new PlayerSimilarityCandidate("SAVE:target", "Target", "Target FC", "ST", target),
            new PlayerSimilarityCandidate("SAVE:identical", "Identical", "Other FC", "ST",
                new Dictionary<string, double>(target)),
            new PlayerSimilarityCandidate("SAVE:close", "Close", "Other FC", "ST",
                new Dictionary<string, double> { ["Acceleration"] = 18, ["Pace"] = 17, ["Finishing"] = 15, ["Technique"] = 15, ["Composure"] = 14, ["Off The Ball"] = 13 })
        };

        var results = PlayerSimilarityService.Rank("SAVE:target", target, candidates);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("SAVE:identical", results[0].SourceId);
        Assert.AreEqual(100d, results[0].Similarity, 0.0001);
        Assert.IsFalse(results.Any(x => x.SourceId == "SAVE:target"));
        Assert.IsTrue(results[1].Similarity < results[0].Similarity);
    }

    [TestMethod]
    public void Rank_NormalizesKeysAndReportsActualCoverage()
    {
        var target = new Dictionary<string, double>
        {
            ["Off The Ball"] = 18, ["First Touch"] = 12, ["Passing"] = 15,
            ["Vision"] = 16, ["Technique"] = 14, ["Composure"] = 13
        };
        var candidate = new PlayerSimilarityCandidate("SAVE:2", "Candidate", "Club", "AMC",
            new Dictionary<string, double>
            {
                ["Off_The Ball"] = 18, ["First-Touch"] = 12, ["Passing"] = 15,
                ["Vision"] = 16, ["Technique"] = 14
            });

        var result = PlayerSimilarityService.Rank("SAVE:1", target, new[] { candidate }).Single();

        Assert.AreEqual(5, result.SharedAttributeCount);
        Assert.AreEqual(6, result.TargetAttributeCount);
        Assert.AreEqual(5, result.CandidateAttributeCount);
        Assert.AreEqual(5d / 6d, result.TargetCoverage, 0.0001);
        Assert.AreEqual(1d, result.CandidateCoverage, 0.0001);
        Assert.AreEqual(100d, result.Similarity, 0.0001);
    }

    [TestMethod]
    public void Rank_WithholdsCandidatesBelowCommonAttributeOrCoverageThreshold()
    {
        var target = new Dictionary<string, double>
        {
            ["A"] = 10, ["B"] = 10, ["C"] = 10, ["D"] = 10,
            ["E"] = 10, ["F"] = 10, ["G"] = 10, ["H"] = 10
        };
        var candidates = new[]
        {
            new PlayerSimilarityCandidate("SAVE:few", "Few", null, null,
                new Dictionary<string, double> { ["A"] = 10, ["B"] = 10, ["C"] = 10, ["D"] = 10 }),
            new PlayerSimilarityCandidate("SAVE:low-coverage", "Low coverage", null, null,
                new Dictionary<string, double> { ["A"] = 10, ["B"] = 10, ["C"] = 10, ["D"] = 10, ["E"] = 10 })
        };

        Assert.AreEqual(0, PlayerSimilarityService.Rank("SAVE:target", target, candidates,
            minimumCommonAttributes: 5, minimumTargetCoverage: 0.75).Count);
    }

    [TestMethod]
    public void Rank_UsesOnlyValidFiniteOneToTwentyValuesAndIsDeterministic()
    {
        var target = new Dictionary<string, double>
        {
            ["A"] = 1, ["B"] = 5, ["C"] = 10, ["D"] = 15,
            ["E"] = 20, ["Invalid"] = double.NaN
        };
        var candidates = new[]
        {
            new PlayerSimilarityCandidate("SAVE:z", "Zulu", null, null,
                new Dictionary<string, double> { ["A"] = 1, ["B"] = 5, ["C"] = 10, ["D"] = 15, ["E"] = 20, ["Out"] = 21 }),
            new PlayerSimilarityCandidate("SAVE:a", "Alpha", null, null,
                new Dictionary<string, double> { ["A"] = 1, ["B"] = 5, ["C"] = 10, ["D"] = 15, ["E"] = 20 })
        };

        var result = PlayerSimilarityService.Rank("SAVE:target", target, candidates, minimumTargetCoverage: 1);

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.All(x => x.Similarity == 100d));
        CollectionAssert.AreEqual(new[] { "SAVE:a", "SAVE:z" }, result.Select(x => x.SourceId).ToArray());
    }

    [TestMethod]
    public void Rank_RejectsInvalidThresholdsAndMissingTarget()
    {
        var attributes = new Dictionary<string, double> { ["Pace"] = 15 };
        var candidates = Array.Empty<PlayerSimilarityCandidate>();

        Assert.ThrowsExactly<ArgumentException>(() => PlayerSimilarityService.Rank("", attributes, candidates));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PlayerSimilarityService.Rank("SAVE:1", attributes, candidates, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PlayerSimilarityService.Rank("SAVE:1", attributes, candidates, minimumTargetCoverage: double.NaN));
    }
}
