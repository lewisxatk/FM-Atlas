using System.IO;
using FmAtlas.Models;
using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class SavedSearchProfileStoreTests
{
    [TestMethod]
    public void SaveAndLoad_RoundTripsStatisticsAndAttributeProfiles()
    {
        var (folder, path, store) = CreateStore();
        try
        {
            var created = DateTime.UtcNow;
            var stats = store.Save(new SavedSearchProfile(
                "stats-1",
                "High Press Creators",
                SavedSearchKind.Statistics,
                "Liverpool",
                MatchAny: false,
                Per90: true,
                MinimumMinutes: 700,
                new[]
                {
                    new SavedSearchCondition("key_passes", "Key Passes", 1.5, null),
                    new SavedSearchCondition("xA", "Expected Assists", .2, .8)
                },
                created,
                created));

            var attributes = store.Save(new SavedSearchProfile(
                "attributes-1",
                "Complete Forward",
                SavedSearchKind.Attributes,
                null,
                MatchAny: false,
                Per90: false,
                MinimumMinutes: 0,
                new[]
                {
                    new SavedSearchCondition("finishing", "Finishing", 15, null),
                    new SavedSearchCondition("off_the_ball", "Off The Ball", 14, null)
                },
                created,
                created));

            var loaded = store.Load();

            Assert.AreEqual(2, loaded.Count);
            var loadedStats = loaded.Single(x => x.Id == stats.Id);
            Assert.AreEqual(SavedSearchKind.Statistics, loadedStats.Kind);
            Assert.AreEqual("Liverpool", loadedStats.TextFilter);
            Assert.IsTrue(loadedStats.Per90);
            Assert.AreEqual(700, loadedStats.MinimumMinutes);
            Assert.AreEqual(2, loadedStats.Conditions.Count);
            Assert.AreEqual(1.5, loadedStats.Conditions[0].Minimum!.Value, 0.0001);

            var loadedAttributes = loaded.Single(x => x.Id == attributes.Id);
            Assert.AreEqual(SavedSearchKind.Attributes, loadedAttributes.Kind);
            Assert.AreEqual(2, loadedAttributes.Conditions.Count);
            Assert.AreEqual(15, loadedAttributes.Conditions[0].Minimum!.Value, 0.0001);
            Assert.IsFalse(loadedAttributes.MatchAny);
            Assert.IsFalse(loadedAttributes.Per90);
            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [TestMethod]
    public void Save_UpdatesExistingIdAndPreservesCreationTimestamp()
    {
        var (folder, _, store) = CreateStore();
        try
        {
            var created = DateTime.UtcNow.AddMinutes(-10);
            var original = CreateStatisticsProfile("profile-1", "First Name", created);
            store.Save(original);

            var updated = store.Save(original with
            {
                Name = "Updated Name",
                Conditions = new[]
                {
                    new SavedSearchCondition("goals", "Goals", 10, null)
                }
            });

            Assert.AreEqual("Updated Name", updated.Name);
            Assert.AreEqual(created, updated.CreatedUtc);
            Assert.IsTrue(updated.UpdatedUtc >= created);
            Assert.AreEqual(1, store.Load().Count);
            Assert.AreEqual("Updated Name", store.Load().Single().Name);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [TestMethod]
    public void Save_RejectsDuplicateNamesWithinCategoryButAllowsDifferentCategories()
    {
        var (folder, _, store) = CreateStore();
        try
        {
            store.Save(CreateStatisticsProfile("stats-1", "Pressing"));
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                store.Save(CreateStatisticsProfile("stats-2", "pressing")));

            var attributes = new SavedSearchProfile(
                "attributes-1",
                "Pressing",
                SavedSearchKind.Attributes,
                null,
                false,
                false,
                0,
                new[] { new SavedSearchCondition("work_rate", "Work Rate", 14, null) },
                DateTime.UtcNow,
                DateTime.UtcNow);

            store.Save(attributes);
            Assert.AreEqual(2, store.Load().Count);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [TestMethod]
    public void Delete_RemovesOnlyTheRequestedProfile()
    {
        var (folder, _, store) = CreateStore();
        try
        {
            store.Save(CreateStatisticsProfile("one", "One"));
            store.Save(CreateStatisticsProfile("two", "Two"));

            Assert.IsTrue(store.Delete("one"));
            Assert.IsFalse(store.Delete("missing"));
            CollectionAssert.AreEqual(new[] { "two" }, store.Load().Select(x => x.Id).ToArray());
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [TestMethod]
    public void Duplicate_CreatesIndependentCopyWithUniqueNameAndSameCriteria()
    {
        var (folder, _, store) = CreateStore();
        try
        {
            var original = store.Save(CreateStatisticsProfile("profile-1", "High Press"));
            var copy = store.Duplicate(original.Id);

            Assert.AreNotEqual(original.Id, copy.Id);
            Assert.AreEqual("High Press (Copy)", copy.Name);
            Assert.AreEqual(original.Kind, copy.Kind);
            Assert.AreEqual(original.Conditions.Count, copy.Conditions.Count);
            Assert.AreEqual(original.Conditions[0], copy.Conditions[0]);
            Assert.IsTrue(copy.CreatedUtc >= original.CreatedUtc);
            Assert.AreEqual(2, store.Load().Count);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [TestMethod]
    public void ExportJsonAndImportJson_PreserveProfilesAndMergeWithoutOverwritingConflicts()
    {
        var (sourceFolder, _, source) = CreateStore();
        var (targetFolder, _, target) = CreateStore();
        try
        {
            source.Save(CreateStatisticsProfile("shared-id", "Different Source Name"));
            source.Save(new SavedSearchProfile(
                "attribute-2",
                "Complete Forward",
                SavedSearchKind.Attributes,
                "Liverpool",
                false,
                false,
                0,
                new[] { new SavedSearchCondition("finishing", "Finishing", 16, null) },
                DateTime.UtcNow,
                DateTime.UtcNow));

            target.Save(CreateStatisticsProfile("shared-id", "Existing Local Name"));
            target.Save(CreateStatisticsProfile("local-name", "Existing Name"));

            var exported = source.ExportJson();
            var result = target.ImportJson(exported);

            Assert.AreEqual(1, result.Added);
            Assert.AreEqual(1, result.Skipped);
            Assert.AreEqual(1, result.Conflicts.Count);
            Assert.IsTrue(result.Conflicts[0].Contains("same ID", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(3, target.Load().Count);
            Assert.AreEqual("Existing Local Name", target.Load().Single(x => x.Id == "shared-id").Name);
            Assert.IsTrue(target.Load().Any(x => x.Id == "attribute-2" && x.Kind == SavedSearchKind.Attributes));

            var attributeOnly = source.ExportJson(SavedSearchKind.Attributes);
            var filteredTarget = new SavedSearchProfileStore(Path.Combine(targetFolder, "attributes-only.json"));
            var filteredImport = filteredTarget.ImportJson(attributeOnly);
            Assert.AreEqual(1, filteredImport.Added);
            Assert.AreEqual(SavedSearchKind.Attributes, filteredTarget.Load().Single().Kind);
        }
        finally
        {
            DeleteFolder(sourceFolder);
            DeleteFolder(targetFolder);
        }
    }

    [TestMethod]
    public void ImportJson_SkipsNameConflictsAndRejectsUnsupportedSchema()
    {
        var (folder, _, target) = CreateStore();
        var (sourceFolder, _, source) = CreateStore();
        try
        {
            target.Save(CreateStatisticsProfile("local", "Pressing"));
            source.Save(CreateStatisticsProfile("incoming", "Pressing"));

            var conflict = target.ImportJson(source.ExportJson());
            Assert.AreEqual(0, conflict.Added);
            Assert.AreEqual(1, conflict.Skipped);
            Assert.IsTrue(conflict.Conflicts.Single().Contains("same name", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(1, target.Load().Count);

            var unsupported = source.ExportJson().Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 999", StringComparison.Ordinal);
            Assert.ThrowsExactly<InvalidDataException>(() => target.ImportJson(unsupported));
        }
        finally
        {
            DeleteFolder(folder);
            DeleteFolder(sourceFolder);
        }
    }

    [TestMethod]
    public void Save_RejectsNonFiniteAndInvalidAttributeBounds()
    {
        var (folder, _, store) = CreateStore();
        try
        {
            var profile = CreateStatisticsProfile("bad", "Bad") with
            {
                Conditions = new[]
                {
                    new SavedSearchCondition("goals", "Goals", double.NaN, null)
                }
            };
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.Save(profile));

            var invalidAttribute = new SavedSearchProfile(
                "bad-attribute",
                "Bad Attribute",
                SavedSearchKind.Attributes,
                null,
                false,
                false,
                0,
                new[] { new SavedSearchCondition("finishing", "Finishing", 20.5, null) },
                DateTime.UtcNow,
                DateTime.UtcNow);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.Save(invalidAttribute));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [TestMethod]
    public void Load_ReportsCorruptJsonWithoutOverwritingTheOriginalFile()
    {
        var (folder, path, store) = CreateStore();
        try
        {
            const string corrupt = "{ not valid json";
            File.WriteAllText(path, corrupt);

            Assert.ThrowsExactly<InvalidDataException>(() => store.Load());
            Assert.AreEqual(corrupt, File.ReadAllText(path));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    private static SavedSearchProfile CreateStatisticsProfile(
        string id,
        string name,
        DateTime? createdUtc = null)
    {
        var now = createdUtc ?? DateTime.UtcNow;
        return new SavedSearchProfile(
            id,
            name,
            SavedSearchKind.Statistics,
            null,
            false,
            false,
            450,
            new[] { new SavedSearchCondition("goals", "Goals", 10, null) },
            now,
            now);
    }

    private static (string Folder, string Path, SavedSearchProfileStore Store) CreateStore()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "search-profiles.json");
        return (folder, path, new SavedSearchProfileStore(path));
    }

    private static void DeleteFolder(string folder)
    {
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }
}
