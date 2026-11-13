using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class TacticalSquadAnalysisServiceTests
{
    [TestMethod]
    public void Compare_BuildsSeparatePhasesAndReportsSharedPlayers()
    {
        var roles = new Dictionary<string, IReadOnlyList<RoleFitCandidate>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Inside Forward"] = new[] { Candidate("SAVE:1", "Two-phase player", 18) },
            ["Pressing Midfielder"] = new[] { Candidate("SAVE:1", "Two-phase player", 17) }
        };
        var positions = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SAVE:1"] = new(StringComparer.OrdinalIgnoreCase) { ["amr"] = 20, ["mc"] = 20 }
        };

        var result = TacticalSquadAnalysisService.Compare(
            new TacticalShapeDefinition("IP shape", "In Possession",
                new[] { new TacticalShapeSlot("IP-AMR", "Right attacker", "amr", "Inside Forward") }),
            new TacticalShapeDefinition("OOP shape", "Out of Possession",
                new[] { new TacticalShapeSlot("OOP-MC", "Pressing midfielder", "mc", "Pressing Midfielder") }),
            roles,
            positions);

        Assert.AreEqual("In Possession", result.InPossession.Phase);
        Assert.AreEqual("Out of Possession", result.OutOfPossession.Phase);
        Assert.AreEqual("SAVE:1", result.InPossession.Selection.Assignments.Single().Candidate?.SourceId);
        Assert.AreEqual("SAVE:1", result.OutOfPossession.Selection.Assignments.Single().Candidate?.SourceId);
        Assert.AreEqual("SAVE:1", result.SharedPlayerSourceIds.Single());
    }

    [TestMethod]
    public void Optimize_RejectsRoleFromTheWrongTacticalPhase()
    {
        var shape = new TacticalShapeDefinition("Invalid OOP", "Out of Possession",
            new[] { new TacticalShapeSlot("AMR", "Right attacker", "amr", "Inside Forward") });

        Assert.ThrowsExactly<ArgumentException>(() => TacticalSquadAnalysisService.Optimize(
            shape, "Out of Possession",
            new Dictionary<string, IReadOnlyList<RoleFitCandidate>>(),
            new Dictionary<string, Dictionary<string, double>>()));
    }

    [TestMethod]
    public void DefaultShapesContainElevenPhaseCompatibleSlots()
    {
        var profiles = RoleFitService.GetProfiles().ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var shape in new[]
        {
            TacticalSquadAnalysisService.GetDefaultInPossessionShape(),
            TacticalSquadAnalysisService.GetDefaultOutOfPossessionShape()
        })
        {
            Assert.AreEqual(11, shape.Slots.Count, shape.Name);
            Assert.AreEqual(11, shape.Slots.Select(x => x.SlotId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.IsTrue(shape.Slots.All(slot =>
            {
                var profile = profiles[slot.RoleProfileName];
                return profile.Phase.Equals("Both", StringComparison.OrdinalIgnoreCase)
                    || profile.Phase.Equals(shape.Phase, StringComparison.OrdinalIgnoreCase);
            }), shape.Name + " has a role assigned to the wrong tactical phase.");
        }
    }

    private static RoleFitCandidate Candidate(string id, string name, double score)
        => new(id, name, "Test FC", 24, "AMR/MC", 150, 170, score, 10, 10, 1);
}
