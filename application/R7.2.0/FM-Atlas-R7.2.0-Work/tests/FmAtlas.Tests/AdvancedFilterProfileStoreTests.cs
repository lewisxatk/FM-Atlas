using System.IO;
using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class AdvancedFilterProfileStoreTests
{
    [TestMethod]
    public void Store_RoundTripsNestedProfileAndDuplicatesIndependently()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-AdvancedProfiles-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "profiles.json");
        try
        {
            var store = new AdvancedFilterProfileStore(path);
            var group = new AdvancedFilterGroup(AdvancedFilterLogic.All, new[]
            {
                AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Age", AdvancedFilterOperator.Between, "18", "24")),
                AdvancedFilterClause.ForGroup(new AdvancedFilterGroup(AdvancedFilterLogic.Any, new[]
                {
                    AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Position", AdvancedFilterOperator.Contains, "Winger")),
                    AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Position", AdvancedFilterOperator.Contains, "Forward"))
                }))
            });
            var saved = store.Save(new AdvancedFilterProfile("profile-1", "Young attackers", group, DateTime.UtcNow, DateTime.UtcNow));

            var loaded = store.Load().Single();
            Assert.AreEqual("Young attackers", loaded.Name);
            Assert.AreEqual(AdvancedFilterLogic.All, loaded.Root.Logic);
            Assert.AreEqual(2, loaded.Root.Clauses.Count);
            Assert.IsNotNull(loaded.Root.Clauses[1].Group);
            Assert.AreEqual(AdvancedFilterLogic.Any, loaded.Root.Clauses[1].Group!.Logic);

            var duplicate = store.Duplicate(saved.Id);
            Assert.AreNotEqual(saved.Id, duplicate.Id);
            Assert.AreEqual("Young attackers copy", duplicate.Name);
            Assert.AreEqual(2, store.Load().Count);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void Store_ImportsWithoutOverwritingIdOrNameConflicts()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-AdvancedProfiles-" + Guid.NewGuid().ToString("N"));
        var pathA = Path.Combine(folder, "a.json");
        var pathB = Path.Combine(folder, "b.json");
        try
        {
            var a = new AdvancedFilterProfileStore(pathA);
            var b = new AdvancedFilterProfileStore(pathB);
            var root = new AdvancedFilterGroup(AdvancedFilterLogic.All, new[]
            {
                AdvancedFilterClause.ForCondition(new AdvancedFilterCondition("Name", AdvancedFilterOperator.Contains, "Alex"))
            });
            a.Save(new AdvancedFilterProfile("same-id", "First", root, DateTime.UtcNow, DateTime.UtcNow));
            b.Save(new AdvancedFilterProfile("same-id", "Imported conflict", root, DateTime.UtcNow, DateTime.UtcNow));
            b.Save(new AdvancedFilterProfile("new-id", "First", root, DateTime.UtcNow, DateTime.UtcNow));
            var imported = a.ImportJson(b.ExportJson());

            Assert.AreEqual(0, imported.Added);
            Assert.AreEqual(2, imported.Skipped);
            Assert.AreEqual(1, a.Load().Count);
            Assert.AreEqual("First", a.Load()[0].Name);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void Store_RejectsInvalidGroupAndPreservesCorruptFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-AdvancedProfiles-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "profiles.json");
        Directory.CreateDirectory(folder);
        try
        {
            var invalid = new AdvancedFilterGroup(AdvancedFilterLogic.Any, Array.Empty<AdvancedFilterClause>());
            var store = new AdvancedFilterProfileStore(path);
            Assert.ThrowsExactly<ArgumentException>(() =>
                store.Save(new AdvancedFilterProfile("bad", "Bad", invalid, DateTime.UtcNow, DateTime.UtcNow)));

            const string corrupt = "{not-json";
            File.WriteAllText(path, corrupt);
            Assert.ThrowsExactly<InvalidDataException>(() => store.Load());
            Assert.AreEqual(corrupt, File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }
}
