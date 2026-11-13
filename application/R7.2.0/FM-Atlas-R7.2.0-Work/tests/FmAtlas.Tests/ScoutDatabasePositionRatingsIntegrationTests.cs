using System.IO;
using FmAtlas.Data;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabasePositionRatingsIntegrationTests
{
    [TestMethod]
    public async Task GetPlayerPositionRatingsAsync_UsesLatestRegisteredSnapshotAndRejectsInvalidValues()
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

            var first = await InsertKnownSnapshotAsync(connection, "week-1.json", knownBuild: true);
            var second = await InsertKnownSnapshotAsync(connection, "week-2.json", knownBuild: true);
            var latest = await InsertKnownSnapshotAsync(connection, "latest-complete-save.json", knownBuild: true);

            await InsertRatingAsync(connection, first, "FMSAVE:player-42", "gk", 18);
            await InsertRatingAsync(connection, first, "FMSAVE:player-42", "dc", 11);
            await InsertRatingAsync(connection, second, "FMSAVE:player-42", "gk", 19);
            await InsertRatingAsync(connection, second, "FMSAVE:player-42", "dc", 14);
            await InsertRatingAsync(connection, latest, "FMSAVE:player-42", "gk", 20);
            await InsertRatingAsync(connection, latest, "FMSAVE:player-42", "dc", 16);
            await InsertRatingAsync(connection, latest, "FMSAVE:player-42", "amc", 21);
            await InsertRatingAsync(connection, latest, "FMSAVE:player-99", "gk", 20);

            var results = await database.GetPlayerPositionRatingsAsync(new[]
            {
                "FMSAVE:player-42", "FMSAVE:player-99", "FMSAVE:missing", "FMSAVE:player-42"
            });

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(20d, results["FMSAVE:player-42"]["gk"]);
            Assert.AreEqual(16d, results["FMSAVE:player-42"]["dc"]);
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

    [TestMethod]
    public async Task GetPlayerPositionRatingsForClubAsync_UsesLatestRegisteredSnapshotForClub()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-ClubPositionRatings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");

        try
        {
            var database = new ScoutDatabase(databasePath, pooling: false);
            await database.InitialiseAsync();
            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
            await connection.OpenAsync();

            await InsertPlayerAsync(connection, "FMSAVE:player-42", "Player 42", "Test FC");
            await InsertPlayerAsync(connection, "FMSAVE:player-99", "Player 99", "Test FC");

            var first = await InsertKnownSnapshotAsync(connection, "week-1.json", knownBuild: true);
            var second = await InsertKnownSnapshotAsync(connection, "week-2.json", knownBuild: true);
            var latest = await InsertKnownSnapshotAsync(connection, "latest-complete-save.json", knownBuild: true);

            await InsertRatingAsync(connection, first, "FMSAVE:player-42", "gk", 18);
            await InsertRatingAsync(connection, second, "FMSAVE:player-42", "gk", 19);
            await InsertRatingAsync(connection, second, "FMSAVE:player-42", "dc", 14);
            await InsertRatingAsync(connection, latest, "FMSAVE:player-42", "gk", 20);
            await InsertRatingAsync(connection, latest, "FMSAVE:player-42", "dc", 15);
            await InsertRatingAsync(connection, latest, "FMSAVE:player-99", "gk", 18);

            var results = await database.GetPlayerPositionRatingsForClubAsync("Test FC");

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(20d, results["FMSAVE:player-42"]["gk"]);
            Assert.AreEqual(15d, results["FMSAVE:player-42"]["dc"]);
            Assert.AreEqual(18d, results["FMSAVE:player-99"]["gk"]);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task PositionFacts_ReturnEmptyWhenLatestSnapshotBuildIsUnregistered()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-UnknownPositionBuild-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");

        try
        {
            var database = new ScoutDatabase(databasePath, pooling: false);
            await database.InitialiseAsync();
            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
            await connection.OpenAsync();
            await InsertPlayerAsync(connection, "FMSAVE:player-42", "Player 42", "Test FC");

            var known = await InsertKnownSnapshotAsync(connection, "known.json", knownBuild: true);
            var unknown = await InsertKnownSnapshotAsync(connection, "unknown.json", knownBuild: false);
            await InsertRatingAsync(connection, known, "FMSAVE:player-42", "gk", 18);
            await InsertRatingAsync(connection, unknown, "FMSAVE:player-42", "gk", 20);

            Assert.AreEqual(0, (await database.GetPlayerPositionRatingsForClubAsync("Test FC")).Count);
            Assert.AreEqual(0, (await database.GetPlayerPositionRatingsAsync(new[] { "FMSAVE:player-42" })).Count);
            Assert.AreEqual(0, (await database.GetFmsavePlayerFactsAsync("FMSAVE:player-42", "positions")).Count);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task<int> InsertKnownSnapshotAsync(SqliteConnection connection, string file, bool knownBuild)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc,Game,GameBuild,KnownBuild)
            VALUES($file,100,$utc,'FM27',$build,$known);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$file", file);
        command.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$build", knownBuild ? "27.0.0+2486695" : "unregistered-build");
        command.Parameters.AddWithValue("$known", knownBuild ? 1 : 0);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task InsertPlayerAsync(SqliteConnection connection, string sourceId, string name, string club)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Players(SourceId,Name,Club,DataOrigin,UpdatedUtc)
            VALUES($source,$name,$club,'FMSave fixture',$utc);
            """;
        command.Parameters.AddWithValue("$source", sourceId);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$club", club);
        command.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
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
