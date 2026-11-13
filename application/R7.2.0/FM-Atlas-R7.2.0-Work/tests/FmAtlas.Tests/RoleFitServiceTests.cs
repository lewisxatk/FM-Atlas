using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class RoleFitServiceTests
{
    [TestMethod]
    public void CalculateEstimate_UsesWeightedMeanAndReportsCoverage()
    {
        var profile = CreateProfile(("Acceleration", 2), ("Passing", 1), ("Technique", 1), ("Stamina", 2));
        var attributes = new Dictionary<string, double>
        {
            ["Acceleration"] = 10,
            ["Passing"] = 20,
            ["Technique"] = 15,
            ["Stamina"] = 21 // Out of FM's 1-20 attribute range; excluded.
        };

        var result = RoleFitService.CalculateEstimate(profile, attributes);

        Assert.IsNotNull(result);
        Assert.AreEqual(13.75, result.Score, 0.0001);
        Assert.AreEqual(3, result.AttributesAvailable);
        Assert.AreEqual(4, result.AttributesExpected);
        Assert.AreEqual(0.75, result.Coverage, 0.0001);
    }

    [TestMethod]
    public void CalculateEstimate_NormalizesAttributeNamesWithoutInventingMissingValues()
    {
        var profile = CreateProfile(("Off The Ball", 2), ("First Touch", 1), ("Passing", 1));
        var attributes = new Dictionary<string, double>
        {
            ["Off_The Ball"] = 18,
            ["First-Touch"] = 12
            // Passing is absent and is not treated as zero.
        };

        var result = RoleFitService.CalculateEstimate(profile, attributes);

        Assert.IsNotNull(result);
        Assert.AreEqual(16, result.Score, 0.0001);
        Assert.AreEqual(2, result.AttributesAvailable);
        Assert.AreEqual(3, result.AttributesExpected);
        Assert.AreEqual(2d / 3d, result.Coverage, 0.0001);
    }

    [TestMethod]
    public void CalculateEstimate_WithholdsScoreBelowMinimumCoverage()
    {
        var profile = CreateProfile(("Pace", 1), ("Acceleration", 1), ("Dribbling", 1), ("Finishing", 1));
        var attributes = new Dictionary<string, double> { ["Pace"] = 15 };

        Assert.IsNull(RoleFitService.CalculateEstimate(profile, attributes));
        Assert.IsNotNull(RoleFitService.CalculateEstimate(profile, attributes, minimumCoverage: 0.25));
    }

    [TestMethod]
    public void CalculateEstimate_ExcludesNonFiniteAndOutOfRangeAttributeValues()
    {
        var profile = CreateProfile(("Pace", 1), ("Acceleration", 1), ("Dribbling", 1), ("Finishing", 1));
        var attributes = new Dictionary<string, double>
        {
            ["Pace"] = double.NaN,
            ["Acceleration"] = double.PositiveInfinity,
            ["Dribbling"] = 0,
            ["Finishing"] = 21
        };

        Assert.IsNull(RoleFitService.CalculateEstimate(profile, attributes));
    }

    [TestMethod]
    public void CalculateEstimate_RejectsInvalidCoverageAndRoleWeights()
    {
        var profile = CreateProfile(("Pace", 1));
        var attributes = new Dictionary<string, double> { ["Pace"] = 15 };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            RoleFitService.CalculateEstimate(profile, attributes, minimumCoverage: double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            RoleFitService.CalculateEstimate(profile, attributes, minimumCoverage: 1.1));

        var invalidWeight = CreateProfile(("Pace", 0));
        Assert.ThrowsExactly<ArgumentException>(() =>
            RoleFitService.CalculateEstimate(invalidWeight, attributes));

        var duplicateNormalized = new RoleFitProfile(
            "Invalid",
            "Both",
            "Attacker",
            "Duplicate keys after normalization",
            new Dictionary<string, double> { ["Off The Ball"] = 1, ["Off_The Ball"] = 2 });
        Assert.ThrowsExactly<ArgumentException>(() =>
            RoleFitService.CalculateEstimate(duplicateNormalized, attributes));
    }

    [TestMethod]
    public void CalculateEstimate_HandlesEmptyProfileAndAttributes()
    {
        var emptyProfile = new RoleFitProfile("Empty", "Both", "Any", "No weights", new Dictionary<string, double>());
        Assert.IsNull(RoleFitService.CalculateEstimate(emptyProfile, new Dictionary<string, double>()));

        var profile = CreateProfile(("Pace", 1));
        Assert.IsNull(RoleFitService.CalculateEstimate(profile, new Dictionary<string, double>()));
    }

    private static RoleFitProfile CreateProfile(params (string Name, double Weight)[] weights)
        => new(
            "Test Role",
            "Both",
            "Attacker",
            "Test-only profile",
            weights.ToDictionary(x => x.Name, x => x.Weight, StringComparer.OrdinalIgnoreCase));
}
