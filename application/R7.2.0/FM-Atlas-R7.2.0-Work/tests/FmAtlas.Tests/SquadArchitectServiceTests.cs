using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class SquadArchitectServiceTests
{
    [TestMethod]
    public void Optimize_UsesPositionAndRoleEvidenceAndAssignsEachPlayerAtMostOnce()
    {
        var roles = new Dictionary<string, IReadOnlyList<RoleFitCandidate>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Goalkeeper"] = new[] { Candidate("keeper", "Keeper", 18), Candidate("out", "Out of Position", 20) },
            ["Full-Back"] = new[] { Candidate("left", "Left Back", 17), Candidate("right", "Right Back", 16) },
            ["Centre-Back"] = new[] { Candidate("dc1", "Centre Back One", 16), Candidate("dc2", "Centre Back Two", 17), Candidate("dc3", "Centre Back Three", 19) },
            ["Defensive Midfielder"] = new[] { Candidate("dm1", "Pivot One", 16), Candidate("dm2", "Pivot Two", 15) },
            ["Deep-Lying Playmaker"] = new[] { Candidate("dm1", "Pivot One", 17), Candidate("dm2", "Pivot Two", 18) },
            ["Inside Forward"] = new[] { Candidate("aml", "Left Forward", 17), Candidate("amr", "Right Forward", 16) },
            ["Attacking Midfielder"] = new[] { Candidate("amc", "Number Ten", 16) },
            ["Centre Forward"] = new[] { Candidate("striker", "Striker", 19) }
        };

        var positions = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["keeper"] = new() { ["gk"] = 20 },
            ["left"] = new() { ["dl"] = 18 },
            ["right"] = new() { ["dr"] = 18 },
            ["dc1"] = new() { ["dc"] = 18 },
            ["dc2"] = new() { ["dc"] = 18 },
            ["dc3"] = new() { ["dc"] = 20 },
            ["dm1"] = new() { ["dm"] = 20 },
            ["dm2"] = new() { ["dm"] = 18 },
            ["aml"] = new() { ["aml"] = 18 },
            ["amc"] = new() { ["amc"] = 18 },
            ["amr"] = new() { ["amr"] = 18 },
            ["striker"] = new() { ["stc"] = 18 },
            ["out"] = new() { ["stc"] = 20 }
        };

        var result = SquadArchitectService.Optimize("4-2-3-1", roles, positions);

        Assert.AreEqual(11, result.Selection.TotalSlots);
        Assert.AreEqual(11, result.Selection.FilledSlots);
        Assert.AreEqual(0, result.Selection.UnfilledSlots.Count);
        Assert.AreEqual(11, result.Selection.SelectedCandidates.Select(x => x.SourceId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.IsFalse(result.Selection.SelectedCandidates.Any(x => x.SourceId == "out"),
            "A high role score without the required position rating must not create an eligible edge.");

        var dcAssignments = result.Selection.Assignments
            .Where(x => x.Slot.SlotId.StartsWith("DC-", StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(2, dcAssignments.Length);
        Assert.AreEqual(2, dcAssignments.Select(x => x.Candidate!.SourceId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.IsTrue(dcAssignments.Any(x => x.Candidate!.SourceId == "dc3"));

        var bestDc = dcAssignments.Single(x => x.Candidate!.SourceId == "dc3");
        var breakdown = result.ScoreBreakdown[SquadArchitectService.GetBreakdownKey("dc3", bestDc.Slot.SlotId)];
        Assert.AreEqual(19d, breakdown.RoleFitScore, 0.0001);
        Assert.AreEqual(20d, breakdown.PositionRating, 0.0001);
        Assert.AreEqual(19d, breakdown.CompositeScore, 0.0001);
        Assert.AreEqual(breakdown.CompositeScore, bestDc.Score!.Value, 0.0001);
    }

    [TestMethod]
    public void GetFormationSlots_ProvidesElevenSlotsForSupportedFormations()
    {
        Assert.AreEqual(4, SquadArchitectService.GetFormationNames().Count);
        foreach (var formation in SquadArchitectService.GetFormationNames())
        {
            var slots = SquadArchitectService.GetFormationSlots(formation);
            Assert.AreEqual(11, slots.Count, formation);
            Assert.AreEqual(11, slots.Select(x => x.SlotId).Distinct(StringComparer.OrdinalIgnoreCase).Count(), formation);
        }
        Assert.ThrowsExactly<ArgumentException>(() => SquadArchitectService.GetFormationSlots("Unsupported"));
    }

    private static RoleFitCandidate Candidate(string id, string name, double score)
        => new(id, name, "Atlas Test FC", 24, null, 140, 160, score, 10, 10, 1);
}
