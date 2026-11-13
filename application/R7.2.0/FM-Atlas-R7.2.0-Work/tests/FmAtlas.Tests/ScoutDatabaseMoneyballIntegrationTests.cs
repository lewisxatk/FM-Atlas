using System.IO;
using FmAtlas.Data;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabaseMoneyballIntegrationTests
{
    [TestMethod]
    public async Task SearchSeasonMetricAsync_AppliesMinutesFloorAndUsesIndexedFacts()
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

                var run = connection.CreateCommand();
                run.CommandText = """
                    INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc)
                    VALUES('fixture.fmsave',100,'2026-11-13T00:00:00Z');
                    SELECT last_insert_rowid();
                    """;
                var snapshotId = Convert.ToInt32(await run.ExecuteScalarAsync());

                await InsertPlayer(connection, snapshotId, "P1", "FMSAVE:1", "Low Minutes Player");
                await InsertPlayer(connection, snapshotId, "P2", "FMSAVE:2", "Qualified Player");

                await InsertFact(connection, snapshotId, "P1", "player_season_stats", 0, "2025/26", 100, "goals", 50);
                await InsertFact(connection, snapshotId, "P2", "player_season_stats", 0, "2025/26", 500, "goals", 30);
                await InsertFact(connection, snapshotId, "P2", "player_season_stats", 1, "2025/26", 900, "goals", 40);
            }

            var qualified = await database.SearchSeasonMetricAsync("goals", null, null, 5000, 450);
            Assert.AreEqual(1, qualified.Count, "A raw-total Moneyball cohort must apply the displayed minimum-minutes floor.");
            Assert.AreEqual("Qualified Player", qualified[0].Name);
            Assert.AreEqual(40d, qualified[0].Value, 0.0001,
                "Duplicate player/period rows must retain the row with the greatest minutes, not inflate the cohort.");

            var unrestricted = await database.SearchSeasonMetricAsync("goals", null, null, 5000, 0);
            Assert.AreEqual(2, unrestricted.Count);
            Assert.IsTrue(unrestricted.Any(x => x.Name == "Low Minutes Player"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task InsertPlayer(
        SqliteConnection connection, int snapshotId, string uid, string sourceId, string name)
    {
        var player = connection.CreateCommand();
        player.CommandText = """
            INSERT INTO Players(SourceId,Name,Club,DataOrigin,UpdatedUtc)
            VALUES($source,$name,'Test FC','Moneyball fixture','2026-11-13T00:00:00Z');
            INSERT INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson)
            VALUES($snapshot,$uid,$source,'{}');
            """;
        player.Parameters.AddWithValue("$source", sourceId);
        player.Parameters.AddWithValue("$name", name);
        player.Parameters.AddWithValue("$snapshot", snapshotId);
        player.Parameters.AddWithValue("$uid", uid);
        await player.ExecuteNonQueryAsync();
    }

    private static async Task InsertFact(
        SqliteConnection connection,
        int snapshotId,
        string uid,
        string table,
        int rowIndex,
        string period,
        double minutes,
        string field,
        double value)
    {
        var fact = connection.CreateCommand();
        fact.CommandText = """
            INSERT INTO FmsaveStatFacts(
                SnapshotId,TableName,RowIndex,PlayerUid,PeriodKey,Minutes,FieldKey,NumericValue)
            VALUES($snapshot,$table,$row,$uid,$period,$minutes,$field,$value);
            """;
        fact.Parameters.AddWithValue("$snapshot", snapshotId);
        fact.Parameters.AddWithValue("$table", table);
        fact.Parameters.AddWithValue("$row", rowIndex);
        fact.Parameters.AddWithValue("$uid", uid);
        fact.Parameters.AddWithValue("$period", period);
        fact.Parameters.AddWithValue("$minutes", minutes);
        fact.Parameters.AddWithValue("$field", field);
        fact.Parameters.AddWithValue("$value", value);
        await fact.ExecuteNonQueryAsync();
    }
}
