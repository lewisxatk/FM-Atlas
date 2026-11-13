using System.IO;
using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class IntegratedIntelligenceTests
{
    [TestMethod]
    public void AdvancedFilterEvaluator_EvaluatesNestedAllAnyGroups()
    {
        var group = new AdvancedFilterGroup(AdvancedFilterLogic.All, new[]
        {
            AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Age", AdvancedFilterOperator.Between, "18", "24")),
            AdvancedFilterClause.ForGroup(new AdvancedFilterGroup(AdvancedFilterLogic.Any, new[]
            {
                AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Position", AdvancedFilterOperator.Contains, "Winger")),
                AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Position", AdvancedFilterOperator.Equals, "Attacking Midfielder"))
            }))
        });

        var values = new Dictionary<string, object?>
        {
            ["age"] = 21,
            ["position"] = "Right Winger"
        };

        var result = AdvancedFilterEvaluator.Evaluate(group, values);

        Assert.IsTrue(result.Matches);
        Assert.AreEqual(3, result.ConditionsEvaluated);
        Assert.AreEqual(2, result.ConditionsMatched);
        Assert.AreEqual(3, result.Explanations.Count);
    }

    [TestMethod]
    public void AdvancedFilterEvaluator_MissingFieldsDoNotPassNotEquals()
    {
        var group = new AdvancedFilterGroup(AdvancedFilterLogic.All, new[]
        {
            AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("PotentialAbility", AdvancedFilterOperator.NotEquals, "200"))
        });

        var result = AdvancedFilterEvaluator.Evaluate(group, new Dictionary<string, object?>());

        Assert.IsFalse(result.Matches);
        Assert.AreEqual(0, result.ConditionsMatched);
    }

    [TestMethod]
    public void AdvancedFilterEvaluator_IsMissingCanMatchAnAbsentField()
    {
        var group = new AdvancedFilterGroup(AdvancedFilterLogic.All, new[]
        {
            AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Nationality", AdvancedFilterOperator.IsMissing))
        });

        Assert.IsTrue(AdvancedFilterEvaluator.Evaluate(group, new Dictionary<string, object?>()).Matches);
    }

    [TestMethod]
    public void AdvancedFilterEvaluator_RejectsEmptyGroupsAndReversedRanges()
    {
        Assert.ThrowsExactly<ArgumentException>(() => AdvancedFilterEvaluator.Validate(
            new AdvancedFilterGroup(AdvancedFilterLogic.Any, Array.Empty<AdvancedFilterClause>())));

        var reversed = new AdvancedFilterGroup(AdvancedFilterLogic.All, new[]
        {
            AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Age", AdvancedFilterOperator.Between, "25", "18"))
        });
        Assert.ThrowsExactly<ArgumentException>(() => AdvancedFilterEvaluator.Validate(reversed));
    }

    [TestMethod]
    public void AdvancedFilterEvaluator_RejectsMalformedEnumsAndNullClauses()
    {
        var validCondition = AdvancedFilterClause.ForCondition(
            new AdvancedFilterCondition("Age", AdvancedFilterOperator.GreaterThan, "18"));

        var invalidLogic = new AdvancedFilterGroup((AdvancedFilterLogic)99, new[] { validCondition });
        Assert.ThrowsExactly<ArgumentException>(() => AdvancedFilterEvaluator.Validate(invalidLogic));

        var invalidOperator = new AdvancedFilterGroup(AdvancedFilterLogic.All, new[]
        {
            AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Age", (AdvancedFilterOperator)99, "18"))
        });
        Assert.ThrowsExactly<ArgumentException>(() => AdvancedFilterEvaluator.Validate(invalidOperator));

        var nullClause = new AdvancedFilterGroup(
            AdvancedFilterLogic.All,
            new AdvancedFilterClause[] { null! });
        Assert.ThrowsExactly<ArgumentException>(() => AdvancedFilterEvaluator.Validate(nullClause));
    }

    [TestMethod]
    public void AdvancedFilterEvaluator_InvalidNumericInputDoesNotPassNotEquals()
    {
        var group = new AdvancedFilterGroup(AdvancedFilterLogic.All, new[]
        {
            AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Age", AdvancedFilterOperator.NotEquals, "not-a-number"))
        });

        var result = AdvancedFilterEvaluator.Evaluate(
            group,
            new Dictionary<string, object?> { ["Age"] = 21 });

        Assert.IsFalse(result.Matches);
        Assert.AreEqual(0, result.ConditionsMatched);
    }

    [TestMethod]
    public void AdvancedFilterEvaluator_NumericAndBooleanComparisonsRemainTyped()
    {
        var numeric = new AdvancedFilterGroup(AdvancedFilterLogic.All, new[]
        {
            AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Age", AdvancedFilterOperator.GreaterThan, "18")),
            AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("HomeGrownClub", AdvancedFilterOperator.Equals, "true"))
        });

        var result = AdvancedFilterEvaluator.Evaluate(
            numeric,
            new Dictionary<string, object?> { ["Age"] = 21, ["HomeGrownClub"] = true });

        Assert.IsTrue(result.Matches);
        Assert.AreEqual(2, result.ConditionsMatched);
    }

    [TestMethod]
    public void CohortPercentiles_UseMidRanksAndWithholdSmallSamples()
    {
        var observations = new[]
        {
            new CohortMetricObservation("a", "A", 10),
            new CohortMetricObservation("b", "B", 10),
            new CohortMetricObservation("c", "C", 20),
            new CohortMetricObservation("d", "D", 30),
            new CohortMetricObservation("e", "E", 40)
        };

        var results = CohortPercentileService.Calculate(observations, minimumSampleSize: 5);

        var ten = results.Single(x => x.Id == "a");
        var forty = results.Single(x => x.Id == "e");
        Assert.AreEqual(20d, ten.Percentile!.Value, 0.0001);
        Assert.AreEqual(90d, forty.Percentile!.Value, 0.0001);
        Assert.AreEqual(5, ten.SampleSize);

        var small = CohortPercentileService.Calculate(observations.Take(4).ToArray(), minimumSampleSize: 5);
        Assert.IsTrue(small.All(x => x.Percentile is null));
    }

    [TestMethod]
    public void CohortPercentiles_ScaleToFiveThousandRowsWithStableTieRanks()
    {
        var observations = Enumerable.Range(0, 50000)
            .Select(index => new CohortMetricObservation(
                "player-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Player " + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                index % 1000))
            .ToArray();

        var results = CohortPercentileService.Calculate(observations, minimumSampleSize: 50000);

        Assert.AreEqual(50000, results.Count);
        Assert.AreEqual(99.95d, results.First().Percentile!.Value, 0.0001);
        Assert.AreEqual(0.05d, results.Last().Percentile!.Value, 0.0001);
        Assert.IsTrue(results.All(x => x.SampleSize == 50000));
    }

    [TestMethod]
    public void GraphicsIndexer_IndexesExistingTargetsAndRejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "FmAtlas-Graphics-" + Guid.NewGuid().ToString("N"));
        var pack = Path.Combine(root, "faces");
        Directory.CreateDirectory(Path.Combine(pack, "graphics", "pictures", "person"));
        try
        {
            var configDirectory = Path.Combine(pack, "graphics");
            File.WriteAllText(Path.Combine(configDirectory, "config.xml"),
                "<record><list id=\"people\"><record from=\"1001\" to=\"pictures/person/1001.png\"/>" +
                "<record from=\"1002\" to=\"../../outside.png\"/></list></record>");
            File.WriteAllBytes(Path.Combine(configDirectory, "pictures", "person", "1001.png"), new byte[] { 1, 2, 3 });

            var result = new GraphicsLibraryIndexer().Scan(root);

            Assert.AreEqual(1, result.MappingsFound);
            Assert.AreEqual(1, result.RejectedPaths);
            Assert.AreEqual(0, result.MissingTargets);
            Assert.IsTrue(result.Mappings.Single().TargetExists);
            Assert.AreEqual("1001", result.Mappings.Single().RecordFrom);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void GraphicsIndexer_SkipsExternalEntityDeclarations()
    {
        var root = Path.Combine(Path.GetTempPath(), "FmAtlas-Graphics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "config.xml"),
                "<!DOCTYPE root [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><root>&x;</root>");

            var result = new GraphicsLibraryIndexer().Scan(root);

            Assert.AreEqual(0, result.MappingsFound);
            Assert.IsTrue(result.Warnings.Count > 0);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ModResearchCatalog_ContainsLicenceAndCompatibilityBoundaries()
    {
        var entries = ModResearchCatalog.GetEntries();

        Assert.IsTrue(entries.Count >= 8);
        Assert.IsTrue(entries.Any(x => x.Name == "FMLibreEditor" && x.RecordedLicence.Contains("GPL", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(entries.All(x => x.RepositoryUrl.StartsWith("https://", StringComparison.Ordinal)));
        Assert.IsTrue(entries.All(x => !string.IsNullOrWhiteSpace(x.Boundary)));
    }
}
