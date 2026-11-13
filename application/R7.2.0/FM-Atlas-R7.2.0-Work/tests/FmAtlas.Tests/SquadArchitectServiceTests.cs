using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class SquadArchitectServiceTests
{
    [TestMethod]
    public void Optimize_CombinesRoleFitWithSourceBackedPositionRating()
    {
        var roles = new Dictionary<string, IReadOnlyList<RoleFitCandidate>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Goalkeeper"] = new[]
            {
                Candidate("A", "High familiarity keeper", 18),
                Candidate("B", "Low familiarity keeper", 20)
            }
        };
        var positions = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = new(StringComparer.OrdinalIgnoreCase) { ["gk"] = 20 },
            ["B"] = new(StringComparer.OrdinalIgnoreCase) { ["gk"] = 10 }
        };

        var result = SquadArchitectService.Optimize("4-3-3", roles, positions);
        var goalkeeper = result.Selection.Assignments.Single(x => x.Slot.SlotId == "GK");

        Assert.AreEqual("A", goalkeeper.Candidate?.SourceId);
        Assert.AreEqual(18d, goalkeeper.Score!.Value, 0.0001);
        var breakdown = result.ScoreBreakdown[SquadArchitectService.GetBreakdownKey("A", "GK")];
        Assert.AreEqual(18d, breakdown.RoleFitScore, 0.0001);
        Assert.AreEqual(20d, breakdown.PositionRating, 0.0001);
        Assert.AreEqual(18d, breakdown.CompositeScore, 0.0001);
        Assert.AreEqual(10d, result.ScoreBreakdown[SquadArchitectService.GetBreakdownKey("B", "GK")].CompositeScore, 0.0001);
    }

    [TestMethod]
    public void Optimize_LeavesSlotsUnfilledWithoutPositionEvidence()
    {
        var roles = new Dictionary<string, IReadOnlyList<RoleFitCandidate>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Goalkeeper"] = new[] { Candidate("A", "Unverified keeper", 20) }
        };
        var result = SquadArchitectService.Optimize(
            "4-3-3",
            roles,
            new Dictionary<string, Dictionary<string, double>>());

        Assert.IsNull(result.Selection.Assignments.Single(x => x.Slot.SlotId == "GK").Candidate);
        Assert.AreEqual(0, result.Selection.FilledSlots);
        Assert.AreEqual(11, result.Selection.UnfilledSlots.Count);
    }

    [TestMethod]
    public void Optimize_NeverAssignsOneVersatilePlayerToTwoFormationSlots()
    {
        var player = Candidate("A", "Versatile defender", 19);
        var roles = new Dictionary<string, IReadOnlyList<RoleFitCandidate>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Full-Back"] = new[] { player },
            ["Centre-Back"] = new[] { player }
        };
        var positions = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = new(StringComparer.OrdinalIgnoreCase) { ["dl"] = 20, ["dc"] = 20, ["dr"] = 20 }
        };

        var result = SquadArchitectService.Optimize("4-3-3", roles, positions);
        var assigned = result.Selection.Assignments.Where(x => x.Candidate?.SourceId == "A").ToArray();

        Assert.AreEqual(1, assigned.Length);
        Assert.AreEqual(1, result.Selection.SelectedCandidates.Select(x => x.SourceId).Distinct().Count());
    }

    [TestMethod]
    public void GetFormationSlots_ProvidesElevenSlotsAndKnownRoleProfiles()
    {
        var knownRoles = RoleFitService.GetProfiles().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var formation in SquadArchitectService.GetFormationNames())
        {
            var slots = SquadArchitectService.GetFormationSlots(formation);
            Assert.AreEqual(11, slots.Count, formation);
            Assert.AreEqual(11, slots.Select(x => x.SlotId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.IsTrue(slots.All(x => knownRoles.Contains(x.RoleProfileName)),
                formation + " contains a role without an Atlas profile.");
        }
    }

    private static RoleFitCandidate Candidate(string id, string name, double score)
        => new(id, name, "Test FC", 24, "GK", 150, 170, score, 10, 10, 1);
}
