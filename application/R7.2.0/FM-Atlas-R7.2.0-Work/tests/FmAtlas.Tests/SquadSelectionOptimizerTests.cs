using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class SquadSelectionOptimizerTests
{
    [TestMethod]
    public void Optimize_FindsGlobalOptimumInsteadOfGreedyDuplicateChoice()
    {
        var slots = new[]
        {
            new SquadSelectionSlot("A", "Left role"),
            new SquadSelectionSlot("B", "Right role")
        };
        var candidates = new[]
        {
            new SquadSelectionCandidate("SAVE:1", "Player One",
                new Dictionary<string, double> { ["A"] = 10, ["B"] = 9 }),
            new SquadSelectionCandidate("SAVE:2", "Player Two",
                new Dictionary<string, double> { ["A"] = 9, ["B"] = 1 })
        };

        var result = SquadSelectionOptimizer.Optimize(slots, candidates);

        Assert.AreEqual(2, result.FilledSlots);
        Assert.AreEqual(0, result.UnfilledSlots.Count);
        Assert.AreEqual(18d, result.TotalScore, 0.0001);
        Assert.AreEqual("SAVE:2", result.Assignments.Single(x => x.Slot.SlotId == "A").Candidate?.SourceId);
        Assert.AreEqual("SAVE:1", result.Assignments.Single(x => x.Slot.SlotId == "B").Candidate?.SourceId);
        Assert.AreEqual(2, result.SelectedCandidates.Select(x => x.SourceId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [TestMethod]
    public void Optimize_LeavesSlotUnfilledWhenNoEligibleCandidateExists()
    {
        var slots = new[]
        {
            new SquadSelectionSlot("GK", "Goalkeeper"),
            new SquadSelectionSlot("ST", "Striker")
        };
        var candidates = new[]
        {
            new SquadSelectionCandidate("SAVE:1", "Keeper",
                new Dictionary<string, double> { ["GK"] = 16 })
        };

        var result = SquadSelectionOptimizer.Optimize(slots, candidates);

        Assert.AreEqual(1, result.FilledSlots);
        Assert.AreEqual("SAVE:1", result.Assignments.Single(x => x.Slot.SlotId == "GK").Candidate?.SourceId);
        Assert.IsNull(result.Assignments.Single(x => x.Slot.SlotId == "ST").Candidate);
        Assert.AreEqual("ST", result.UnfilledSlots.Single().SlotId);
    }

    [TestMethod]
    public void Optimize_NeverSelectsSameSourceIdTwice()
    {
        var slots = new[]
        {
            new SquadSelectionSlot("A", "Role A"),
            new SquadSelectionSlot("B", "Role B")
        };
        var candidates = new[]
        {
            new SquadSelectionCandidate("SAVE:1", "Versatile Player",
                new Dictionary<string, double> { ["A"] = 19, ["B"] = 19 })
        };

        var result = SquadSelectionOptimizer.Optimize(slots, candidates);

        Assert.AreEqual(1, result.FilledSlots);
        Assert.AreEqual(1, result.SelectedCandidates.Count);
        Assert.AreEqual(1, result.SelectedCandidates.Select(x => x.SourceId).Distinct().Count());
    }

    [TestMethod]
    public void Optimize_RejectsDuplicateCandidateAndSlotIdentifiers()
    {
        var slots = new[] { new SquadSelectionSlot("A", "Role A") };
        var candidates = new[]
        {
            new SquadSelectionCandidate("SAVE:1", "Player One", new Dictionary<string, double> { ["A"] = 10 }),
            new SquadSelectionCandidate("SAVE:1", "Duplicate Player", new Dictionary<string, double> { ["A"] = 11 })
        };

        Assert.ThrowsExactly<ArgumentException>(() => SquadSelectionOptimizer.Optimize(slots, candidates));
        Assert.ThrowsExactly<ArgumentException>(() => SquadSelectionOptimizer.Optimize(
            new[] { new SquadSelectionSlot("A", "One"), new SquadSelectionSlot("A", "Duplicate") },
            Array.Empty<SquadSelectionCandidate>()));
    }

    [TestMethod]
    public void Optimize_RejectsOutOfRangeAndNonFiniteScores()
    {
        var slots = new[] { new SquadSelectionSlot("A", "Role A") };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SquadSelectionOptimizer.Optimize(
            slots,
            new[] { new SquadSelectionCandidate("SAVE:1", "Player", new Dictionary<string, double> { ["A"] = 21 }) }));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SquadSelectionOptimizer.Optimize(
            slots,
            new[] { new SquadSelectionCandidate("SAVE:2", "Player", new Dictionary<string, double> { ["A"] = double.NaN }) }));
    }
}
