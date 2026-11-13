using System.IO;
using FmAtlas.Models;
using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class MoneyballCompositeProfileStoreTests
{
    [TestMethod]
    public void SaveAndLoad_RoundTripsCompositeCriteriaWithoutPlayerData()
    {
        var (folder, path, store) = CreateStore();
        try
        {
            var saved = store.Save(CreateProfile("profile-1", "Attacking Output"));
            var loaded = store.Load().Single();

            Assert.AreEqual(saved.Id, loaded.Id);
            Assert.AreEqual("Attacking Output", loaded.Name);
            CollectionAssert.AreEqual(new[] { "goals", "assists", "key_passes" }, loaded.MetricKeys.ToArray());
            Assert.AreEqual(2, loaded.MinimumMetrics);
            Assert.AreEqual("Same primary position", loaded.CohortMode);
            Assert.IsTrue(loaded.HigherIsBetter);
            Assert.IsTrue(loaded.Per90);
            Assert.AreEqual(600, loaded.MinimumMinutes);
            Assert.AreEqual(450, loaded.MinimumSampleSize);
            Assert.AreEqual(2.5, loaded.MetricWeights["goals"], 0.0001);
            Assert.IsTrue(File.Exists(path));
            var json = File.ReadAllText(path);
            StringAssert.Contains(json, "MetricKeys");
            Assert.IsFalse(json.Contains("Player One", StringComparison.Ordinal));
        }
        finally { DeleteFolder(folder); }
    }

    [TestMethod]
    public void Save_UpdatesExistingProfileAndPreservesCreatedTimestamp()
    {
        var (folder, _, store) = CreateStore();
        try
        {
            var created = DateTime.UtcNow.AddHours(-1);
            var original = store.Save(CreateProfile("profile-1", "Original") with { CreatedUtc = created });
            var updated = store.Save(original with { Name = "Updated", MinimumMetrics = 3 });

            Assert.AreEqual("Updated", updated.Name);
            Assert.AreEqual(created, updated.CreatedUtc);
            Assert.AreEqual(3, store.Load().Single().MinimumMetrics);
        }
        finally { DeleteFolder(folder); }
    }

    [TestMethod]
    public void Save_RejectsDuplicateNamesAndDuplicateMetricKeys()
    {
        var (folder, _, store) = CreateStore();
        try
        {
            store.Save(CreateProfile("one", "Pressing"));
            Assert.ThrowsExactly<InvalidOperationException>(() => store.Save(CreateProfile("two", "pressing")));
            var duplicateKeys = CreateProfile("three", "Duplicate Fields") with
            {
                MetricKeys = new[] { "goals", "Goals" }
            };
            Assert.ThrowsExactly<ArgumentException>(() => store.Save(duplicateKeys));
        }
        finally { DeleteFolder(folder); }
    }

    [TestMethod]
    public void Save_RejectsInvalidCoverageCohortMinutesSamplesAndWeights()
    {
        var (folder, _, store) = CreateStore();
        try
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                store.Save(CreateProfile("bad-coverage", "Bad Coverage") with { MinimumMetrics = 4 }));
            Assert.ThrowsExactly<ArgumentException>(() =>
                store.Save(CreateProfile("bad-cohort", "Bad Cohort") with { CohortMode = "League Strength" }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                store.Save(CreateProfile("bad-minutes", "Bad Minutes") with { MinimumMinutes = 0 }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                store.Save(CreateProfile("bad-sample", "Bad Sample") with { MinimumSampleSize = 0 }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                store.Save(CreateProfile("bad-weight", "Bad Weight") with
                {
                    MetricWeights = new Dictionary<string, double> { ["goals"] = double.NaN }
                }));
            Assert.ThrowsExactly<ArgumentException>(() =>
                store.Save(CreateProfile("foreign-weight", "Foreign Weight") with
                {
                    MetricWeights = new Dictionary<string, double> { ["not_selected"] = 1 }
                }));
        }
        finally { DeleteFolder(folder); }
    }

    [TestMethod]
    public void Duplicate_CreatesIndependentCopyWithSameCriteria()
    {
        var (folder, _, store) = CreateStore();
        try
        {
            var original = store.Save(CreateProfile("profile-1", "Attacking Output"));
            var copy = store.Duplicate(original.Id);

            Assert.AreNotEqual(original.Id, copy.Id);
            Assert.AreEqual("Attacking Output (Copy)", copy.Name);
            CollectionAssert.AreEqual(original.MetricKeys.ToArray(), copy.MetricKeys.ToArray());
            Assert.AreEqual(original.MetricWeights["goals"], copy.MetricWeights["goals"], 0.0001);
            Assert.AreEqual(2, store.Load().Count);
        }
        finally { DeleteFolder(folder); }
    }

    [TestMethod]
    public void ExportImport_MergesProfilesAndReportsIdAndNameConflicts()
    {
        var (sourceFolder, _, source) = CreateStore();
        var (targetFolder, _, target) = CreateStore();
        try
        {
            source.Save(CreateProfile("shared-id", "Source Name"));
            source.Save(CreateProfile("new-profile", "New Profile"));
            source.Save(CreateProfile("name-conflict", "Local Only"));
            target.Save(CreateProfile("shared-id", "Local Name"));
            target.Save(CreateProfile("local-name", "Local Only"));

            var result = target.ImportJson(source.ExportJson());

            Assert.AreEqual(1, result.Added);
            Assert.AreEqual(2, result.Skipped);
            Assert.AreEqual(2, result.Conflicts.Count);
            Assert.AreEqual("Local Name", target.Load().Single(x => x.Id == "shared-id").Name);
            Assert.IsTrue(target.Load().Any(x => x.Id == "new-profile"));
            Assert.AreEqual(3, target.Load().Count);
        }
        finally
        {
            DeleteFolder(sourceFolder);
            DeleteFolder(targetFolder);
        }
    }

    [TestMethod]
    public void ImportJson_RejectsUnsupportedSchemaAndCorruptStoreIsPreserved()
    {
        var (folder, path, store) = CreateStore();
        try
        {
            var unsupported = "{\"SchemaVersion\":999,\"Profiles\":[]}";
            Assert.ThrowsExactly<InvalidDataException>(() => store.ImportJson(unsupported));

            const string corrupt = "{ broken";
            File.WriteAllText(path, corrupt);
            Assert.ThrowsExactly<InvalidDataException>(() => store.Load());
            Assert.AreEqual(corrupt, File.ReadAllText(path));
        }
        finally { DeleteFolder(folder); }
    }

    [TestMethod]
    public void Delete_RemovesOnlyRequestedProfile()
    {
        var (folder, _, store) = CreateStore();
        try
        {
            store.Save(CreateProfile("one", "One"));
            store.Save(CreateProfile("two", "Two"));
            Assert.IsTrue(store.Delete("one"));
            Assert.IsFalse(store.Delete("missing"));
            CollectionAssert.AreEqual(new[] { "two" }, store.Load().Select(x => x.Id).ToArray());
        }
        finally { DeleteFolder(folder); }
    }

    private static MoneyballCompositeProfile CreateProfile(string id, string name)
    {
        var now = DateTime.UtcNow;
        return new MoneyballCompositeProfile(
            id,
            name,
            new[] { "goals", "assists", "key_passes" },
            2,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["goals"] = 2.5 },
            "Same primary position",
            true,
            true,
            600,
            450,
            now,
            now);
    }

    private static (string Folder, string Path, MoneyballCompositeProfileStore Store) CreateStore()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "moneyball-profiles.json");
        return (folder, path, new MoneyballCompositeProfileStore(path));
    }

    private static void DeleteFolder(string folder)
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }
}
