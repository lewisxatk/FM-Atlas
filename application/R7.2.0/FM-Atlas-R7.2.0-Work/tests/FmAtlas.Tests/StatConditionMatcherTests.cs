using FmAtlas.Models;
using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class StatConditionMatcherTests
{
    private static readonly IReadOnlyList<StatFilterCriterion> TwoConditions = new[]
    {
        new StatFilterCriterion("goals", "Goals", 10, null),
        new StatFilterCriterion("assists", "Assists", 5, null)
    };

    [TestMethod]
    public void Evaluate_MatchAllRequiresEveryCondition()
    {
        var values = new Dictionary<string, double>
        {
            ["goals"] = 12,
            ["assists"] = 7
        };

        var result = StatConditionMatcher.Evaluate(values, TwoConditions, matchAny: false);

        Assert.IsTrue(result.Matches);
        Assert.AreEqual(2, result.MatchedCount);
    }

    [TestMethod]
    public void Evaluate_MatchAnyAllowsOneConditionToMatch()
    {
        var values = new Dictionary<string, double>
        {
            ["goals"] = 12,
            ["assists"] = 2
        };

        var result = StatConditionMatcher.Evaluate(values, TwoConditions, matchAny: true);

        Assert.IsTrue(result.Matches);
        Assert.AreEqual(1, result.MatchedCount);
        Assert.AreEqual(1, result.MatchedValues.Count);
    }

    [TestMethod]
    public void Evaluate_MatchAllRejectsMissingFieldsButMatchAnyCanContinue()
    {
        var values = new Dictionary<string, double>
        {
            ["goals"] = 12
        };

        var all = StatConditionMatcher.Evaluate(values, TwoConditions, matchAny: false);
        var any = StatConditionMatcher.Evaluate(values, TwoConditions, matchAny: true);

        Assert.IsFalse(all.Matches);
        Assert.IsTrue(any.Matches);
        Assert.AreEqual(1, any.MatchedCount);
    }

    [TestMethod]
    public void Evaluate_MissingFieldIsNeverTreatedAsZero()
    {
        var result = StatConditionMatcher.Evaluate(
            new Dictionary<string, double>(),
            new[] { new StatFilterCriterion("goals", "Goals", 0, null) },
            matchAny: true);

        Assert.IsFalse(result.Matches);
        Assert.AreEqual(0, result.MatchedCount);
    }

    [TestMethod]
    public void Evaluate_Per90UsesPlayerPeriodMinutesAndRefusesZeroMinutes()
    {
        var values = new Dictionary<string, double> { ["goals"] = 20 };
        var criterion = new[] { new StatFilterCriterion("goals", "Goals per 90", 2, 2) };

        var valid = StatConditionMatcher.Evaluate(values, criterion, matchAny: false, per90: true, minutes: 900);
        var invalid = StatConditionMatcher.Evaluate(values, criterion, matchAny: false, per90: true, minutes: 0);

        Assert.IsTrue(valid.Matches);
        Assert.AreEqual(1, valid.MatchedCount);
        Assert.IsFalse(invalid.Matches);
    }

    [TestMethod]
    public void Evaluate_RejectsInvalidBoundaries()
    {
        Assert.ThrowsExactly<ArgumentException>(() => StatConditionMatcher.Evaluate(
            new Dictionary<string, double> { ["goals"] = 5 },
            new[] { new StatFilterCriterion("goals", "Goals", 10, 2) },
            matchAny: true));
    }

    [TestMethod]
    public void Evaluate_RejectsNonFiniteBoundaries()
    {
        foreach (var boundary in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => StatConditionMatcher.Evaluate(
                new Dictionary<string, double> { ["goals"] = 5 },
                new[] { new StatFilterCriterion("goals", "Goals", boundary, null) },
                matchAny: true));
        }
    }
}
