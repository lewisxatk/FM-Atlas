using System.IO;
using FmAtlas.Data;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabaseSquadPlannerIntegrationTests
{
    [TestMethod]
    public async Task SquadPlanner_ResolvesSameSnapshotPlayersAndPreservesUnknownSlotSemantics()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-SquadPlanner-" + Guid.NewGuid().ToString("N"));
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
                    INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc,Game,GameBuild,KnownBuild,GameDate)
                    VALUES('planner-fixture.json',100,'2026-11-13T00:00:00Z','FM27','test-build',1,'2026-11-12');
                    SELECT last_insert_rowid();
                    """;
                var snapshotId = Convert.ToInt32(await run.ExecuteScalarAsync());

                await InsertPlayerAsync(connection, snapshotId, "100", "FMSAVE:100", "Planner Player One");
                await InsertPlayerAsync(connection, snapshotId, "200", "FMSAVE:200", "Planner Player Two");

                await InsertPlannerRowAsync(connection, snapshotId, 0, """
                    {"season_offset":0,"slot":1,"player_uids":[100,999,null]}
                    """);
                await InsertPlannerRowAsync(connection, snapshotId, 1, """
                    {"season_offset":0,"slot":2,"player_uids":[]}
                    """);
                await InsertPlannerRowAsync(connection, snapshotId, 2, """
                    {"season_offset":1,"slot":1,"player_uids":[200]}
                    """);
                await InsertPlannerRowAsync(connection, snapshotId, 3, """
                    {"season_offset":1,"slot":28,"player_uids":[100]}
                    """);
            }

            var slots = await database.GetLatestSquadPlannerSlotsAsync();
            Assert.AreEqual(3, slots.Count);

            var currentFirst = slots.Single(x => x.SeasonOffset == 0 && x.Slot == 1);
            Assert.AreEqual("Planner Player One, Unresolved UID 999", currentFirst.PlayerNames);
            Assert.AreEqual(1, currentFirst.ResolvedPlayers);
            Assert.AreEqual(2, currentFirst.UnresolvedPlayers);

            var currentSecond = slots.Single(x => x.SeasonOffset == 0 && x.Slot == 2);
            Assert.AreEqual("Empty slot", currentSecond.PlayerNames);
            Assert.AreEqual(0, currentSecond.ResolvedPlayers);
            Assert.AreEqual(0, currentSecond.UnresolvedPlayers);

            var nextSeason = slots.Single(x => x.SeasonOffset == 1 && x.Slot == 1);
            Assert.AreEqual("Planner Player Two", nextSeason.PlayerNames);
            Assert.AreEqual(1, nextSeason.ResolvedPlayers);
            Assert.AreEqual(0, nextSeason.UnresolvedPlayers);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task InsertPlayerAsync(
        SqliteConnection connection, int snapshotId, string uid, string sourceId, string name)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Players(SourceId,Name,Club,DataOrigin,UpdatedUtc)
            VALUES($source,$name,'Planner FC','fixture','2026-11-13T00:00:00Z');
            INSERT INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson)
            VALUES($snapshot,$uid,$source,'{}');
            """;
        command.Parameters.AddWithValue("$source", sourceId);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$snapshot", snapshotId);
        command.Parameters.AddWithValue("$uid", uid);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertPlannerRowAsync(
        SqliteConnection connection, int snapshotId, int rowIndex, string rawJson)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FmsaveExtraTables(SnapshotId,TableName,RowIndex,RawJson)
            VALUES($snapshot,'squad_planner',$row,$json);
            """;
        command.Parameters.AddWithValue("$snapshot", snapshotId);
        command.Parameters.AddWithValue("$row", rowIndex);
        command.Parameters.AddWithValue("$json", rawJson);
        await command.ExecuteNonQueryAsync();
    }
}
