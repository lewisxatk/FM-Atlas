using System.IO;
using FmAtlas.Data;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabasePositionRatingsIntegrationTests
{
    [TestMethod]
    public async Task GetPlayerPositionRatingsAsync_ReturnsLatestPerPlayerSnapshotAndRejectsInvalidValues()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-PositionRatings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");

        try
        {
            var database = new ScoutDatabase(databasePath, pooling: false);
            await database.InitialiseAsync();
            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
            await connection.OpenAsync();

            var first = await InsertSnapshotAsync(connection, "week-1.json", "FMSAVE:player-42");
            var second = await InsertSnapshotAsync(connection, "week-2.json", "FMSAVE:player-42");
            var other = await InsertSnapshotAsync(connection, "other.json", "FMSAVE:player-99");

            await InsertRatingAsync(connection, first, "FMSAVE:player-42", "gk", 18);
            await InsertRatingAsync(connection, first, "FMSAVE:player-42", "dc", 11);
            await InsertRatingAsync(connection, second, "FMSAVE:player-42", "gk", 19);
            await InsertRatingAsync(connection, second, "FMSAVE:player-42", "dc", 14);
            await InsertRatingAsync(connection, second, "FMSAVE:player-42", "amc", 21);
            await InsertRatingAsync(connection, other, "FMSAVE:player-99", "gk", 20);

            var results = await database.GetPlayerPositionRatingsAsync(new[]
            {
                "FMSAVE:player-42", "FMSAVE:player-99", "FMSAVE:missing", "FMSAVE:player-42"
            });

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(19d, results["FMSAVE:player-42"]["gk"]);
            Assert.AreEqual(14d, results["FMSAVE:player-42"]["dc"]);
            Assert.IsFalse(results["FMSAVE:player-42"].ContainsKey("amc"));
            Assert.AreEqual(20d, results["FMSAVE:player-99"]["gk"]);
            Assert.AreEqual(0, (await database.GetPlayerPositionRatingsAsync(Array.Empty<string>())).Count);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task<int> InsertSnapshotAsync(SqliteConnection connection, string file, string source)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc)
            VALUES($file,100,$utc);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$file", file);
        command.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task InsertRatingAsync(
        SqliteConnection connection, int snapshot, string source, string field, double value)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FmsavePlayerFacts(SnapshotId,PlayerSourceId,FactGroup,FieldKey,NumericValue,TextValue)
            VALUES($snapshot,$source,'positions',$field,$value,NULL);
            """;
        command.Parameters.AddWithValue("$snapshot", snapshot);
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$field", field);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }
}
