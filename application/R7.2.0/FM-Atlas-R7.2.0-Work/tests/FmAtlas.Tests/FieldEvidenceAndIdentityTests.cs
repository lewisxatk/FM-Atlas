using FmAtlas.Models;
using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class FieldEvidencePolicyTests
{
    [TestMethod]
    public void Assess_UnverifiedFieldsCannotDriveDecisionsOrWrites()
    {
        var result = FieldEvidencePolicy.Assess("Unverified");

        Assert.AreEqual(FieldEvidenceLevel.Unverified, result.Level);
        Assert.IsFalse(result.CanUseAsVerifiedDecisionInput);
        Assert.IsFalse(result.CanWriteToGame);
    }

    [TestMethod]
    public void Assess_CandidateFieldsRemainUnverified()
    {
        var result = FieldEvidencePolicy.Assess("Candidate");

        Assert.AreEqual(FieldEvidenceLevel.Candidate, result.Level);
        Assert.IsFalse(result.CanUseAsVerifiedDecisionInput);
        Assert.IsFalse(result.CanWriteToGame);
    }

    [TestMethod]
    public void Assess_ProfileMatchedProvisionalIsNotFieldVerification()
    {
        var result = FieldEvidencePolicy.Assess("ProfileMatched-Provisional");

        Assert.AreEqual(FieldEvidenceLevel.ProfileMatchedProvisional, result.Level);
        Assert.IsFalse(result.CanUseAsVerifiedDecisionInput);
        Assert.IsFalse(result.CanWriteToGame);
        StringAssert.Contains(result.Explanation, "not independently certified");
    }

    [TestMethod]
    public void Assess_FieldVerifiedStillCannotWriteWhileBuildGateIsClosed()
    {
        var result = FieldEvidencePolicy.Assess("Verified");

        Assert.AreEqual(FieldEvidenceLevel.FieldVerified, result.Level);
        Assert.IsTrue(result.CanUseAsVerifiedDecisionInput);
        Assert.IsFalse(LiveWriteCertification.IsCertified);
        Assert.IsFalse(result.CanWriteToGame);
        Assert.IsFalse(FieldEvidencePolicy.CanWrite("Verified"));
    }

    [TestMethod]
    public void Assess_UnknownAndMisleadingStatusesFailClosed()
    {
        foreach (var status in new[] { null, "", "Profile", "Unverified-Provisional", "NotVerified" })
        {
            var result = FieldEvidencePolicy.Assess(status);
            Assert.AreEqual(FieldEvidenceLevel.Unverified, result.Level, $"Unexpected classification for '{status}'.");
            Assert.IsFalse(result.CanUseAsVerifiedDecisionInput);
            Assert.IsFalse(result.CanWriteToGame);
        }
    }

    [TestMethod]
    public void Assess_OnlyExplicitUserWitnessStatusesAreDecisionReady()
    {
        foreach (var status in new[] { "Verified", "FieldVerified", "Verified-UserUIWitness-ReadBack" })
        {
            var result = FieldEvidencePolicy.Assess(status);
            Assert.AreEqual(FieldEvidenceLevel.FieldVerified, result.Level, $"Expected explicit witness status '{status}'.");
            Assert.IsTrue(result.CanUseAsVerifiedDecisionInput);
            Assert.IsFalse(result.CanWriteToGame, "The global live-write certification gate must remain closed.");
        }
    }

    [TestMethod]
    public void Assess_VerifiedPrefixesAndMixedStatusesFailClosed()
    {
        foreach (var status in new[]
        {
            "Verified-Provisional",
            "Verified-Needs-UI-Witness",
            "FieldVerified-Provisional",
            "Unverified-Verified",
            "VerifiedButUnconfirmed",
            "Candidate-Verified"
        })
        {
            var result = FieldEvidencePolicy.Assess(status);
            Assert.AreEqual(FieldEvidenceLevel.Unverified, result.Level, $"Unsafe status was accepted: '{status}'.");
            Assert.IsFalse(result.CanUseAsVerifiedDecisionInput);
            Assert.IsFalse(result.CanWriteToGame);
        }
    }
}

[TestClass]
public sealed class PlayerIdentityResolverTests
{
    [TestMethod]
    public void SameNameClubAndAgeWithDifferentProviderIdsMustRemainSeparate()
    {
        var live = new PlayerRecord
        {
            SourceId = "LIVE:123",
            Name = "Alex Example",
            Club = "Example FC",
            Age = 22,
            DataOrigin = "FM27 Live"
        };
        var save = new PlayerRecord
        {
            SourceId = "FMSAVE:987654",
            Name = "Alex Example",
            Club = "Example FC",
            Age = 22,
            DataOrigin = "Career Save"
        };

        var result = PlayerIdentityResolver.DeduplicateExactSourceIds(new[] { live, save });

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEquivalent(new[] { "LIVE:123", "FMSAVE:987654" }, result.Select(x => x.SourceId).ToArray());
    }

    [TestMethod]
    public void ExactSameSourceIdKeepsMostCompleteRecord()
    {
        var partial = new PlayerRecord { SourceId = "FMSAVE:123", Name = "Alex Example", Age = 22 };
        var complete = new PlayerRecord
        {
            SourceId = "FMSAVE:123",
            Name = "Alex Example",
            Age = 22,
            DateOfBirth = new DateTime(2004, 4, 17),
            Nation = "England",
            Position = "MC",
            CurrentAbility = 130,
            PotentialAbility = 160,
            Value = 1_000_000,
            League = "Example League",
            PreferredFoot = "Right"
        };

        var result = PlayerIdentityResolver.DeduplicateExactSourceIds(new[] { partial, complete });

        Assert.AreEqual(1, result.Count);
        Assert.AreSame(complete, result[0]);
    }

    [TestMethod]
    public void MissingSourceIdsMustNotCollapseRecords()
    {
        var first = new PlayerRecord { Name = "Unknown Player", Age = 20 };
        var second = new PlayerRecord { Name = "Unknown Player", Age = 20 };

        var result = PlayerIdentityResolver.DeduplicateExactSourceIds(new[] { first, second });

        Assert.AreEqual(2, result.Count);
    }
}
