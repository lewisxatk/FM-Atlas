using System.IO;
using FmAtlas.Data;
using FmAtlas.Services;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabaseRoleFitIntegrationTests
{
    [TestMethod]
    public async Task SearchRoleFitCandidatesAsync_ReturnsScoreContributionsFromStoredAttributes()
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

                var player = connection.CreateCommand();
                player.CommandText = """
                    INSERT INTO Players(SourceId,Name,Age,Club,Position,DataOrigin,UpdatedUtc)
                    VALUES('LOCAL:ROLEFIT-1','Role Fit Test Player',22,'Evidence Test FC','AMC','Test fixture','2026-11-13T00:00:00Z');
                    """;
                await player.ExecuteNonQueryAsync();

                await InsertAttribute(connection, "LOCAL:ROLEFIT-1", "Off The Ball", "Mental", 18);
                await InsertAttribute(connection, "LOCAL:ROLEFIT-1", "First Touch", "Technical", 12);
                // Passing is deliberately absent. The query must report 2/3 coverage,
                // not silently turn the missing value into zero.
            }

            var profile = new RoleFitProfile(
                "Integration Test Role",
                "In Possession",
                "Attacker",
                "Verifies SQLite candidate data and explainable contribution transport.",
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Off The Ball"] = 2,
                    ["First Touch"] = 1,
                    ["Passing"] = 1
                });

            var results = await database.SearchRoleFitCandidatesAsync(profile);

            Assert.AreEqual(1, results.Count);
            var candidate = results.Single();
            Assert.AreEqual("LOCAL:ROLEFIT-1", candidate.SourceId);
            Assert.AreEqual("Role Fit Test Player", candidate.Name);
            Assert.AreEqual(16d, candidate.Score, 0.0001);
            Assert.AreEqual(2, candidate.AttributesAvailable);
            Assert.AreEqual(3, candidate.AttributesExpected);
            Assert.AreEqual(2d / 3d, candidate.Coverage, 0.0001);
            Assert.AreEqual(2, candidate.Contributions.Count);

            var offTheBall = candidate.Contributions.Single(x => x.Attribute == "Off The Ball");
            Assert.AreEqual(18d, offTheBall.Value, 0.0001);
            Assert.AreEqual(2d, offTheBall.Weight, 0.0001);
            Assert.AreEqual(12d, offTheBall.ScoreContribution, 0.0001);

            var firstTouch = candidate.Contributions.Single(x => x.Attribute == "First Touch");
            Assert.AreEqual(12d, firstTouch.Value, 0.0001);
            Assert.AreEqual(1d, firstTouch.Weight, 0.0001);
            Assert.AreEqual(4d, firstTouch.ScoreContribution, 0.0001);

            Assert.AreEqual(candidate.Score, candidate.Contributions.Sum(x => x.ScoreContribution), 0.0001);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task InsertAttribute(
        SqliteConnection connection,
        string sourceId,
        string name,
        string category,
        double value)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc)
            VALUES($source,$name,$category,$value,'2026-11-13T00:00:00Z');
            """;
        command.Parameters.AddWithValue("$source", sourceId);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }
}
