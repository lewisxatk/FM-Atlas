using System.IO;
using FmAtlas.Data;
using FmAtlas.Models;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabaseShortlistIntegrationTests
{
    [TestMethod]
    public async Task ShortlistNotes_CanBeCreatedUpdatedAndClearedWithoutChangingPlayerIdentity()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-Shortlist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");

        try
        {
            var database = new ScoutDatabase(databasePath, pooling: false);
            await database.InitialiseAsync();

            await using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString()))
            {
                await connection.OpenAsync();
                var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO Players(SourceId,Name,Club,DataOrigin,UpdatedUtc)
                    VALUES('FMSAVE:player-1','Test Player','Test FC','FMSave fixture','2026-11-13T00:00:00Z');
                    """;
                await insert.ExecuteNonQueryAsync();
            }

            await database.CreateShortlistAsync("Summer recruitment");
            var player = new PlayerRecord { SourceId = "FMSAVE:player-1" };
            await database.AddToShortlistAsync(player, "Summer recruitment", "Monitor contract situation.");

            var first = (await database.GetShortlistAsync("Summer recruitment")).Single();
            Assert.AreEqual("Test Player", first.PlayerName);
            Assert.AreEqual("Monitor contract situation.", first.Notes);
            var addedUtc = first.AddedUtc;

            await database.AddToShortlistAsync(player, "Summer recruitment", "Scout again in January.");
            var updated = (await database.GetShortlistAsync("Summer recruitment")).Single();
            Assert.AreEqual("Scout again in January.", updated.Notes);
            Assert.AreEqual(addedUtc, updated.AddedUtc, "Updating a note must not reset when the player was added.");

            await database.AddToShortlistAsync(player, "Summer recruitment", null);
            var cleared = (await database.GetShortlistAsync("Summer recruitment")).Single();
            Assert.IsNull(cleared.Notes);
            Assert.AreEqual("FMSAVE:player-1", cleared.SourceId);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
    [TestMethod]
    public async Task RecruitmentProjectMetadata_PersistsOwnerTagsBudgetsAndCandidateCount()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-Recruitment-Project-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");
        try
        {
            var database = new ScoutDatabase(databasePath, pooling: false);
            await database.InitialiseAsync();
            await database.CreateShortlistAsync("Summer rebuild");
            await database.AddToShortlistAsync(new PlayerRecord { SourceId = "SAVE:1" }, "Summer rebuild");
            await database.SaveRecruitmentProjectAsync(
                "Summer rebuild",
                "Monitoring",
                "First-team scout",
                new[] { "Priority", "Under-23", "priority" },
                12500000m,
                45000m);

            var project = await database.GetRecruitmentProjectAsync("Summer rebuild");

            Assert.IsNotNull(project);
            Assert.AreEqual("Monitoring", project.Status);
            Assert.AreEqual("First-team scout", project.Owner);
            CollectionAssert.AreEqual(new[] { "Priority", "Under-23" }, project.Tags.ToArray());
            Assert.AreEqual(12500000m, project.TransferBudget);
            Assert.AreEqual(45000m, project.WeeklyWageBudget);
            Assert.AreEqual(1, project.CandidateCount);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecruitmentProjectMetadata_RejectsInvalidStatusBudgetsAndTagOverflow()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-Recruitment-Validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var database = new ScoutDatabase(Path.Combine(folder, "scouting.db"), pooling: false);
            await database.InitialiseAsync();
            await database.CreateShortlistAsync("Validation project");

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => database.SaveRecruitmentProjectAsync(
                "Validation project", "Unknown", null, Array.Empty<string>(), null, null));
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => database.SaveRecruitmentProjectAsync(
                "Validation project", "Active", null, Array.Empty<string>(), -1m, null));
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => database.SaveRecruitmentProjectAsync(
                "Validation project", "Active", null, Enumerable.Range(1, 13).Select(x => "tag-" + x).ToArray(), null, null));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecruitmentProjectBoard_ListsProjectsByStageWithCandidateCounts()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-Recruitment-Board-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var database = new ScoutDatabase(Path.Combine(folder, "scouting.db"), pooling: false);
            await database.InitialiseAsync();
            await database.CreateShortlistAsync("Zeta planning");
            await database.CreateShortlistAsync("Alpha active");
            await database.CreateShortlistAsync("Closed project");
            await database.AddToShortlistAsync(new PlayerRecord { SourceId = "SAVE:1" }, "Alpha active");
            await database.AddToShortlistAsync(new PlayerRecord { SourceId = "SAVE:2" }, "Alpha active");
            await database.SaveRecruitmentProjectAsync("Zeta planning", "Planning", "Scout A", Array.Empty<string>(), 1000000m, null);
            await database.SaveRecruitmentProjectAsync("Alpha active", "Active", "Scout B", new[] { "Priority" }, 2000000m, 15000m);
            await database.SaveRecruitmentProjectAsync("Closed project", "Closed", null, Array.Empty<string>(), null, null);

            var projects = await database.GetRecruitmentProjectsAsync();

            CollectionAssert.AreEqual(
                new[] { "Planning", "Active", "Closed" },
                projects.Select(x => x.Status).ToArray());
            Assert.AreEqual("Alpha active", projects[1].Name);
            Assert.AreEqual(2, projects[1].CandidateCount);
            Assert.AreEqual(1, projects[1].Tags.Count);
            Assert.AreEqual(0, projects[0].CandidateCount);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecruitmentProjectMigration_BackfillsLegacyShortlistsWithoutOverwritingThem()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-Recruitment-Migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");
        try
        {
            var database = new ScoutDatabase(databasePath, pooling: false);
            await database.InitialiseAsync();
            await using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString()))
            {
                await connection.OpenAsync();
                var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO Shortlists(SourceId,ShortlistName,Notes,AddedUtc)
                    VALUES('LEGACY:1','Legacy imported shortlist','Keep this note','2026-01-01T00:00:00Z');
                    """;
                await insert.ExecuteNonQueryAsync();
            }

            await database.InitialiseAsync();

            var project = (await database.GetRecruitmentProjectsAsync())
                .Single(x => x.Name == "Legacy imported shortlist");
            var savedCandidate = (await database.GetShortlistAsync("Legacy imported shortlist")).Single();

            Assert.AreEqual("Active", project.Status);
            Assert.AreEqual(1, project.CandidateCount);
            Assert.AreEqual("Keep this note", savedCandidate.Notes);
            Assert.AreEqual("LEGACY:1", savedCandidate.SourceId);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

}
