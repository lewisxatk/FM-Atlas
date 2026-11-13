using System.IO;
using FmAtlas.Data;
using FmAtlas.Services;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabaseSquadArchitectIntegrationTests
{
    [TestMethod]
    public async Task SquadArchitectQueries_AreClubScopedAndUseLatestSnapshotPositionFacts()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-SquadArchitect-" + Guid.NewGuid().ToString("N"));
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
                    VALUES('squad-architect-fixture.json',100,'2026-11-13T00:00:00Z','FM27','27.0.0+2486695',1,'2026-11-12');
                    SELECT last_insert_rowid();
                    """;
                var snapshotId = Convert.ToInt32(await run.ExecuteScalarAsync());

                await InsertPlayerAsync(connection, snapshotId, "1", "FMSAVE:XI-1", "Keeper One", "Club A", "GK", "gk", 20);
                await InsertPlayerAsync(connection, snapshotId, "2", "FMSAVE:XI-2", "Defender Two", "Club A", "DC", "dc", 18);
                await InsertPlayerAsync(connection, snapshotId, "3", "FMSAVE:XI-3", "Keeper Three", "Club B", "GK", "gk", 19);
            }

            var clubA = await database.GetPlayerPositionRatingsForClubAsync("Club A");
            Assert.AreEqual(2, clubA.Count);
            Assert.AreEqual(20d, clubA["FMSAVE:XI-1"]["gk"]);
            Assert.AreEqual(18d, clubA["FMSAVE:XI-2"]["dc"]);
            Assert.IsFalse(clubA.ContainsKey("FMSAVE:XI-3"));

            var profile = new RoleFitProfile(
                "Squad Architect Test",
                "Both",
                "Test",
                "Verifies club scoping and result caps.",
                new Dictionary<string, double> { ["Reflexes"] = 1, ["Handling"] = 1 });

            var filtered = await database.SearchRoleFitCandidatesAsync(profile, 5000, "Club A", resultLimit: 5000);
            Assert.AreEqual(2, filtered.Count);
            Assert.IsTrue(filtered.All(x => x.Club == "Club A"));

            var limited = await database.SearchRoleFitCandidatesAsync(profile, 5000, "Club A", resultLimit: 1);
            Assert.AreEqual(1, limited.Count);

            var allClubs = await database.SearchRoleFitCandidatesAsync(profile, 5000, null, resultLimit: 5000);
            Assert.AreEqual(3, allClubs.Count);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task InsertPlayerAsync(
        SqliteConnection connection,
        int snapshotId,
        string uid,
        string sourceId,
        string name,
        string club,
        string primaryPosition,
        string positionField,
        double positionRating)
    {
        var player = connection.CreateCommand();
        player.CommandText = """
            INSERT INTO Players(SourceId,Name,Age,Club,Position,DataOrigin,UpdatedUtc)
            VALUES($source,$name,24,$club,$position,'FMSave fixture','2026-11-13T00:00:00Z');
            INSERT INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson)
            VALUES($snapshot,$uid,$source,$raw);
            INSERT INTO FmsavePlayerFacts(SnapshotId,PlayerSourceId,FactGroup,FieldKey,NumericValue,TextValue)
            VALUES($snapshot,$source,'positions',$positionField,$rating,NULL);
            """;
        player.Parameters.AddWithValue("$source", sourceId);
        player.Parameters.AddWithValue("$name", name);
        player.Parameters.AddWithValue("$club", club);
        player.Parameters.AddWithValue("$position", primaryPosition);
        player.Parameters.AddWithValue("$snapshot", snapshotId);
        player.Parameters.AddWithValue("$uid", uid);
        player.Parameters.AddWithValue("$raw", "{\"position\":\"" + primaryPosition + "\"}");
        player.Parameters.AddWithValue("$positionField", positionField);
        player.Parameters.AddWithValue("$rating", positionRating);
        await player.ExecuteNonQueryAsync();

        foreach (var attribute in new[] { ("Reflexes", 17d), ("Handling", 16d) })
        {
            var fact = connection.CreateCommand();
            fact.CommandText = """
                INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc)
                VALUES($source,$name,'Goalkeeping',$value,'2026-11-13T00:00:00Z');
                """;
            fact.Parameters.AddWithValue("$source", sourceId);
            fact.Parameters.AddWithValue("$name", attribute.Item1);
            fact.Parameters.AddWithValue("$value", attribute.Item2);
            await fact.ExecuteNonQueryAsync();
        }
    }
}
