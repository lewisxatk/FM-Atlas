using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class TacticalFormationServiceTests
{
    [TestMethod]
    public void GetFormations_ProvidesFourElevenPlayerTemplates()
    {
        var formations = TacticalFormationService.GetFormations();

        CollectionAssert.AreEquivalent(new[] { "4-3-3", "4-2-3-1", "4-4-2", "3-5-2" }, formations.ToArray());
        foreach (var formation in formations)
        {
            var slots = TacticalFormationService.GetSlots(formation);
            Assert.AreEqual(11, slots.Count, formation + " should contain eleven distinct player slots.");
            Assert.AreEqual(11, slots.Select(x => x.SlotId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.IsTrue(slots.All(x => !string.IsNullOrWhiteSpace(x.RoleProfileName)));
            Assert.IsTrue(slots.All(x => x.AllowedPrimaryPositions.Count > 0));
        }
    }

    [TestMethod]
    public void GetSlots_UsesConservativePrimaryPositionEligibility()
    {
        var slots = TacticalFormationService.GetSlots("4-3-3");
        var goalkeeper = slots.Single(x => x.SlotId == "GK");
        var striker = slots.Single(x => x.SlotId == "ST");
        var winger = slots.Single(x => x.SlotId == "RW");

        CollectionAssert.AreEquivalent(new[] { "GK" }, goalkeeper.AllowedPrimaryPositions.ToArray());
        CollectionAssert.AreEquivalent(new[] { "ST", "STC" }, striker.AllowedPrimaryPositions.ToArray());
        CollectionAssert.AreEquivalent(new[] { "AMR", "MR" }, winger.AllowedPrimaryPositions.ToArray());
    }

    [TestMethod]
    public void GetSlots_RejectsUnknownFormation()
    {
        Assert.ThrowsExactly<ArgumentException>(() => TacticalFormationService.GetSlots("4-1-4-1"));
        Assert.ThrowsExactly<ArgumentException>(() => TacticalFormationService.GetSlots(""));
    }
}
