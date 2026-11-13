using System.IO;
using FmAtlas.Data;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabaseDevelopmentHistoryIntegrationTests
{
    [TestMethod]
    public async Task DevelopmentHistory_TracksOnlyConsecutiveSourceBackedSnapshotsForTheSamePlayer()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-Development-" + Guid.NewGuid().ToString("N"));
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

                var firstSnapshot = await InsertSnapshotAsync(
                    connection,
                    "2026-11-01T12:00:00Z",
                    "2026-10-31",
                    "FM27-test-build",
                    "career-week-1.json",
                    "player-42",
                    """
                    {
                      "uid":"42",
                      "unique_id":"player-42",
                      "name":"Development Test",
                      "age":20,
                      "current_ability":150,
                      "potential_ability":180,
                      "attributes":{"technical":{"passing":12,"finishing":13},"physical":{"pace":14}}
                    }
                    """);

                var secondSnapshot = await InsertSnapshotAsync(
                    connection,
                    "2026-11-08T12:00:00Z",
                    "2026-11-07",
                    "FM27-test-build",
                    "career-week-2.json",
                    "player-42",
                    """
                    {
                      "uid":"420",
                      "unique_id":"player-42",
                      "name":"Development Test",
                      "age":21,
                      "current_ability":155,
                      "potential_ability":180,
                      "attributes":{"technical":{"passing":14,"finishing":12},"physical":{"pace":15}}
                    }
                    """);

                // Another player's snapshot between the two imports must not become part
                // of this player's history or break the persistent-identity join.
                await InsertSnapshotAsync(
                    connection,
                    "2026-11-05T12:00:00Z",
                    "2026-11-04",
                    "FM27-test-build",
                    "other-player.json",
                    "player-99",
                    """
                    {"uid":"99","unique_id":"player-99","name":"Other Player","age":30,"current_ability":100,
                     "potential_ability":100,"attributes":{"technical":{"passing":5}}}
                    """);

                var rows = await database.GetPlayerDevelopmentHistoryAsync("FMSAVE:player-42");
                Assert.IsTrue(rows.Count >= 10, "Both snapshots should expose CA, PA, age and all supplied technical/physical attributes.");
                Assert.IsTrue(rows.All(x => x.SnapshotId == firstSnapshot || x.SnapshotId == secondSnapshot));
                Assert.AreEqual(2, rows.Select(x => x.SnapshotId).Distinct().Count());

                var firstPassing = rows.Single(x => x.SnapshotId == firstSnapshot && x.FieldName == "technical.passing");
                Assert.AreEqual(12d, firstPassing.Value);
                Assert.IsNull(firstPassing.PreviousValue);
                Assert.IsNull(firstPassing.Delta);

                var secondPassing = rows.Single(x => x.SnapshotId == secondSnapshot && x.FieldName == "technical.passing");
                Assert.AreEqual(14d, secondPassing.Value);
                Assert.AreEqual(12d, secondPassing.PreviousValue);
                Assert.AreEqual(2d, secondPassing.Delta);

                var secondCa = rows.Single(x => x.SnapshotId == secondSnapshot && x.FieldName == "Current Ability");
                Assert.AreEqual(5d, secondCa.Delta);
                var secondPa = rows.Single(x => x.SnapshotId == secondSnapshot && x.FieldName == "Potential Ability");
                Assert.AreEqual(0d, secondPa.Delta);
                var secondAge = rows.Single(x => x.SnapshotId == secondSnapshot && x.FieldName == "Age");
                Assert.AreEqual(1d, secondAge.Delta);

                Assert.AreEqual("2026-11-07", secondPassing.GameDate);
                Assert.AreEqual("FM27-test-build", secondPassing.GameBuild);
                Assert.AreEqual("career-week-2.json", secondPassing.SourceFileName);

                Assert.AreEqual(0, (await database.GetPlayerDevelopmentHistoryAsync("FMSAVE:missing-player")).Count);
            }
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task<int> InsertSnapshotAsync(
        SqliteConnection connection,
        string importedUtc,
        string gameDate,
        string gameBuild,
        string sourceFileName,
        string persistentPlayerId,
        string rawPlayerJson)
    {
        var run = connection.CreateCommand();
        run.CommandText = """
            INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc,Game,GameBuild,KnownBuild,GameDate)
            VALUES($file,100,$utc,'FM27',$build,1,$date);
            SELECT last_insert_rowid();
            """;
        run.Parameters.AddWithValue("$file", sourceFileName);
        run.Parameters.AddWithValue("$utc", importedUtc);
        run.Parameters.AddWithValue("$build", gameBuild);
        run.Parameters.AddWithValue("$date", gameDate);
        var snapshotId = Convert.ToInt32(await run.ExecuteScalarAsync());

        var player = connection.CreateCommand();
        player.CommandText = """
            INSERT INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson)
            VALUES($snapshot,$uid,$source,$raw);
            """;
        player.Parameters.AddWithValue("$snapshot", snapshotId);
        player.Parameters.AddWithValue("$uid", persistentPlayerId + "-save-" + snapshotId);
        player.Parameters.AddWithValue("$source", "FMSAVE:" + persistentPlayerId);
        player.Parameters.AddWithValue("$raw", rawPlayerJson);
        await player.ExecuteNonQueryAsync();
        return snapshotId;
    }
}
