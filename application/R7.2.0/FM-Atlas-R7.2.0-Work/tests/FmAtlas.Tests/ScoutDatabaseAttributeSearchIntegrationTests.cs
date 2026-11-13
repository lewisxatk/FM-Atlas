using System.IO;
using FmAtlas.Data;
using FmAtlas.Models;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabaseAttributeSearchIntegrationTests
{
    [TestMethod]
    public async Task SearchPlayerAttributeCriteriaAsync_DefaultsToAllAndSupportsAnyWithoutChangingTextFilter()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas.Tests", Guid.NewGuid().ToString("N"));
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

                await InsertPlayer(connection, "LOCAL:OR-BOTH", "Both Attributes", "Evidence Test FC");
                await InsertPlayer(connection, "LOCAL:OR-FINISH", "Finishing Only", "Evidence Test FC");
                await InsertPlayer(connection, "LOCAL:OR-HEADING", "Heading Only", "Evidence Test FC");
                await InsertPlayer(connection, "LOCAL:OR-NONE", "Neither Attribute", "Evidence Test FC");

                await InsertAttribute(connection, "LOCAL:OR-BOTH", "Finishing", 18);
                await InsertAttribute(connection, "LOCAL:OR-BOTH", "Heading", 18);
                await InsertAttribute(connection, "LOCAL:OR-FINISH", "Finishing", 18);
                await InsertAttribute(connection, "LOCAL:OR-FINISH", "Heading", 10);
                await InsertAttribute(connection, "LOCAL:OR-HEADING", "Finishing", 10);
                await InsertAttribute(connection, "LOCAL:OR-HEADING", "Heading", 18);
                await InsertAttribute(connection, "LOCAL:OR-NONE", "Finishing", 10);
                await InsertAttribute(connection, "LOCAL:OR-NONE", "Heading", 10);
            }

            var criteria = new[]
            {
                new AttributeSearchCriterion("Finishing", "Finishing", 15, null),
                new AttributeSearchCriterion("Heading", "Heading", 15, null)
            };

            // The old/default behaviour remains ALL/AND for every existing caller.
            var allMatches = await database.SearchPlayerAttributeCriteriaAsync(null, criteria, 500);
            CollectionAssert.AreEqual(new[] { "LOCAL:OR-BOTH" }, allMatches.Select(x => x.SourceId).ToArray());

            var anyMatches = await database.SearchPlayerAttributeCriteriaAsync(null, criteria, 500, matchAny: true);
            CollectionAssert.AreEquivalent(
                new[] { "LOCAL:OR-BOTH", "LOCAL:OR-FINISH", "LOCAL:OR-HEADING" },
                anyMatches.Select(x => x.SourceId).ToArray());

            // The free-text/name constraint remains ANDed with the attribute OR group.
            var textFiltered = await database.SearchPlayerAttributeCriteriaAsync("Heading", criteria, 500, matchAny: true);
            CollectionAssert.AreEqual(new[] { "LOCAL:OR-HEADING" }, textFiltered.Select(x => x.SourceId).ToArray());
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task InsertPlayer(SqliteConnection connection, string sourceId, string name, string club)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Players(SourceId,Name,Age,Club,Position,DataOrigin,UpdatedUtc)
            VALUES($source,$name,22,$club,'ST','Test fixture','2026-11-13T00:00:00Z');
            """;
        command.Parameters.AddWithValue("$source", sourceId);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$club", club);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertAttribute(SqliteConnection connection, string sourceId, string name, int value)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc)
            VALUES($source,$name,'Technical',$value,'2026-11-13T00:00:00Z');
            """;
        command.Parameters.AddWithValue("$source", sourceId);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }
    [TestMethod]
    public async Task GetPlayerAttributeMapsAsync_BatchesRequestedPlayersAndKeepsCategoryLabels()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas.AttributeMaps-" + Guid.NewGuid().ToString("N"));
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
                await InsertPlayer(connection, "LOCAL:MAP-1", "Map One", "Map FC");
                await InsertPlayer(connection, "LOCAL:MAP-2", "Map Two", "Map FC");
                await InsertAttribute(connection, "LOCAL:MAP-1", "Finishing", 18);
                await InsertAttribute(connection, "LOCAL:MAP-1", "Composure", 16);
                await InsertAttribute(connection, "LOCAL:MAP-2", "Passing", 17);
            }

            var maps = await database.GetPlayerAttributeMapsAsync(
                new[] { "LOCAL:MAP-1", "LOCAL:MAP-2", "LOCAL:MISSING", "LOCAL:MAP-1" });

            Assert.AreEqual(3, maps.Count);
            Assert.AreEqual(18d, maps["LOCAL:MAP-1"]["Finishing"], 0.0001);
            Assert.AreEqual(16d, maps["LOCAL:MAP-1"]["Composure"], 0.0001);
            Assert.AreEqual(17d, maps["LOCAL:MAP-2"]["Passing"], 0.0001);
            Assert.AreEqual(0, maps["LOCAL:MISSING"].Count);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetPlayerAttributeMapsAsync_RejectsMoreThanOneThousandSourceIds()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas.AttributeMaps-Limit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var database = new ScoutDatabase(Path.Combine(folder, "scouting.db"), pooling: false);
            await database.InitialiseAsync();
            var ids = Enumerable.Range(0, 1001).Select(x => "LOCAL:" + x).ToArray();
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => database.GetPlayerAttributeMapsAsync(ids));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

}
