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
}
