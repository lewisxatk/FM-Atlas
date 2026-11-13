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
                    INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc,Game,GameBuild,KnownBuild)
                    VALUES('fixture.fmsave',100,'2026-11-13T00:00:00Z','FM27','27.0.0+2486695',1);
                    SELECT last_insert_rowid();
                    """;
                var snapshotId = Convert.ToInt32(await run.ExecuteScalarAsync());

                await InsertPlayer(connection, snapshotId, "P1", "FMSAVE:1", "Low Minutes Player", "GK");
                await InsertPlayer(connection, snapshotId, "P2", "FMSAVE:2", "Qualified Player", "STC");

                await InsertFact(connection, snapshotId, "P1", "player_season_stats", 0, "2025/26", 100, "goals", 50);
                await InsertFact(connection, snapshotId, "P2", "player_season_stats", 1, "2025/26", 500, "goals", 30);
                await InsertFact(connection, snapshotId, "P2", "player_season_stats", 2, "2025/26", 900, "goals", 40);
            }

            var qualified = await database.SearchSeasonMetricAsync("goals", null, null, 5000, 450);
            Assert.AreEqual(1, qualified.Count, "A raw-total Moneyball cohort must apply the displayed minimum-minutes floor.");
            Assert.AreEqual("Qualified Player", qualified[0].Name);
            Assert.AreEqual(40d, qualified[0].Value, 0.0001,
                "Duplicate player/period rows must retain the row with the greatest minutes, not inflate the cohort.");
            Assert.AreEqual("STC", qualified[0].PrimaryPosition, "The source-backed primary position must follow the player snapshot identity.");

            var unrestricted = await database.SearchSeasonMetricAsync("goals", null, null, 5000, 0);
            Assert.AreEqual(2, unrestricted.Count);
            Assert.IsTrue(unrestricted.Any(x => x.Name == "Low Minutes Player"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task MoneyballSearch_FallsBackToFmViewCsvAndAppliesMinutesFloor()
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

                var import = connection.CreateCommand();
                import.CommandText = """
                    INSERT INTO ExternalStatImportRuns(FileName,ImportedUtc,RowsSeen,RowsImported)
                    VALUES('fixture.csv','2026-11-13T00:00:00Z',2,2);
                    SELECT last_insert_rowid();
                    """;
                var importId = Convert.ToInt32(await import.ExecuteScalarAsync());

                await InsertExternalRow(connection, importId, 0, "CSV:1", "Low Minutes CSV Player", 100, 50, "GK");
                await InsertExternalRow(connection, importId, 1, "CSV:2", "Qualified CSV Player", 900, 5, "STC");
            }

            var totals = await database.SearchSeasonMetricAsync("goals", null, null, 5000, 450);
            Assert.AreEqual(1, totals.Count);
            Assert.AreEqual("Qualified CSV Player", totals[0].Name);
            Assert.AreEqual(5d, totals[0].Value, 0.0001);
            Assert.AreEqual("STC", totals[0].PrimaryPosition, "The FM View row's imported position should be retained for cohort grouping.");

            var per90 = await database.SearchSeasonMetricPer90Async("goals", null, null, 450, 5000);
            Assert.AreEqual(1, per90.Count);
            Assert.AreEqual("Qualified CSV Player", per90[0].Name);
            Assert.AreEqual(0.5d, per90[0].Value, 0.0001);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task InsertExternalRow(
        SqliteConnection connection, int importId, int rowIndex,
        string sourceId, string name, double minutes, double goals, string? position = null)
    {
        var row = connection.CreateCommand();
        row.CommandText = """
            INSERT INTO ExternalStatRows(
                ImportId,RowIndex,PlayerIdentity,PlayerSourceId,PlayerName,Club,Season,Minutes,RawJson)
            VALUES($import,$row,$identity,$source,$name,'CSV Test FC','2025/26',$minutes,$json);
            """;
        row.Parameters.AddWithValue("$import", importId);
        row.Parameters.AddWithValue("$row", rowIndex);
        row.Parameters.AddWithValue("$identity", sourceId);
        row.Parameters.AddWithValue("$source", sourceId);
        row.Parameters.AddWithValue("$name", name);
        row.Parameters.AddWithValue("$minutes", minutes);
        var json = "{\"goals\":" + goals.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + (position is null ? "" : ",\"position\":\"" + position.Replace("\"", "\\\"") + "\"") + "}";
        row.Parameters.AddWithValue("$json", json);
        await row.ExecuteNonQueryAsync();
    }

    private static async Task InsertPlayer(
        SqliteConnection connection, int snapshotId, string uid, string sourceId, string name, string position)
    {
        var player = connection.CreateCommand();
        player.CommandText = """
            INSERT INTO Players(SourceId,Name,Club,DataOrigin,UpdatedUtc)
            VALUES($source,$name,'Test FC','Moneyball fixture','2026-11-13T00:00:00Z');
            INSERT INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson)
            VALUES($snapshot,$uid,$source,$raw);
            """;
        player.Parameters.AddWithValue("$source", sourceId);
        player.Parameters.AddWithValue("$name", name);
        player.Parameters.AddWithValue("$snapshot", snapshotId);
        player.Parameters.AddWithValue("$uid", uid);
        player.Parameters.AddWithValue("$raw", "{\"position\":\"" + position + "\"}");
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
