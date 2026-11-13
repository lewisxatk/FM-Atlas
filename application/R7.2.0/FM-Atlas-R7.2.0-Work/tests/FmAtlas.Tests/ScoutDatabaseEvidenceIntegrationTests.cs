using FmAtlas.Data;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabaseEvidenceIntegrationTests
{
    [TestMethod]
    public async Task LivePlayerQueries_ShouldExcludeUnverifiedAndProvisionalFacts()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");

        try
        {
            var database = new ScoutDatabase(databasePath);
            await database.InitialiseAsync();

            await using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()))
            {
                await connection.OpenAsync();

                var run = connection.CreateCommand();
                run.CommandText = """
                    INSERT INTO LiveImportRuns(GameVersion,ModuleSha256,StartedUtc,Status)
                    VALUES('FM27-test','test-hash', '2026-11-13T00:00:00Z','Completed');
                    """;
                await run.ExecuteNonQueryAsync();

                var lastRun = connection.CreateCommand();
                lastRun.CommandText = "SELECT last_insert_rowid();";
                var runId = Convert.ToInt64(await lastRun.ExecuteScalarAsync());

                var club = connection.CreateCommand();
                club.CommandText = """
                    INSERT INTO LiveClubs(ClubUid,ClubName,SourceRunId,ValidationStatus,UpdatedUtc)
                    VALUES(77,'Evidence Test FC',$run,'Verified','2026-11-13T00:00:00Z');
                    """;
                club.Parameters.AddWithValue("$run", runId);
                await club.ExecuteNonQueryAsync();

                var players = new[]
                {
                    (Id: 100L, Name: "Verified Player"),
                    (Id: 101L, Name: "Readback Player"),
                    (Id: 102L, Name: "Unverified Player"),
                    (Id: 103L, Name: "Provisional Player"),
                    (Id: 104L, Name: "Mixed Status Player")
                };

                foreach (var player in players)
                {
                    var insert = connection.CreateCommand();
                    insert.CommandText = """
                        INSERT INTO LivePlayers(
                            PlayerUid,PlayerName,ClubUid,SourceRunId,NameValidation,RelationshipValidation,UpdatedUtc)
                        VALUES($uid,$name,77,$run,'Verified','Verified','2026-11-13T00:00:00Z');
                        """;
                    insert.Parameters.AddWithValue("$uid", player.Id);
                    insert.Parameters.AddWithValue("$name", player.Name);
                    insert.Parameters.AddWithValue("$run", runId);
                    await insert.ExecuteNonQueryAsync();
                }

                await InsertFact(connection, runId, 100, "Ability.Current Ability", 180, "Verified");
                await InsertFact(connection, runId, 100, "Ability.Potential Ability", 195, "FieldVerified");
                await InsertFact(connection, runId, 100, "Technical.Passing", 16, "Verified");

                await InsertFact(connection, runId, 101, "Ability.Current Ability", 160, "Verified-UserUIWitness-ReadBack");
                await InsertFact(connection, runId, 101, "Ability.Potential Ability", 180, "FieldVerified-UserUIWitness-ReadBack");
                await InsertFact(connection, runId, 101, "Technical.Passing", 18, "FieldVerified");

                await InsertFact(connection, runId, 102, "Ability.Current Ability", 199, "Unverified");
                await InsertFact(connection, runId, 102, "Ability.Potential Ability", 200, "Unverified");
                await InsertFact(connection, runId, 102, "Technical.Passing", 20, "Unverified");

                await InsertFact(connection, runId, 103, "Ability.Current Ability", 198, "ProfileMatched-Provisional");
                await InsertFact(connection, runId, 103, "Ability.Potential Ability", 200, "Verified-Provisional");
                await InsertFact(connection, runId, 103, "Technical.Passing", 20, "Verified-Needs-UI-Witness");

                await InsertFact(connection, runId, 104, "Ability.Current Ability", 197, "Unverified-Verified");
                await InsertFact(connection, runId, 104, "Ability.Potential Ability", 200, "Candidate-Verified");
                await InsertFact(connection, runId, 104, "Technical.Passing", 20, "Unverified-Verified");
            }

            var all = await database.SearchLivePlayersAsync(null);
            Assert.AreEqual(5, all.Count, "The live player identities should remain visible even when their ability facts are unverified.");

            var byId = all.ToDictionary(x => x.SourceId);
            Assert.AreEqual(180, byId["LIVE:100"].CurrentAbility);
            Assert.AreEqual(195, byId["LIVE:100"].PotentialAbility);
            Assert.AreEqual(160, byId["LIVE:101"].CurrentAbility);
            Assert.AreEqual(180, byId["LIVE:101"].PotentialAbility);

            foreach (var playerId in new[] { 102, 103, 104 })
            {
                Assert.IsNull(byId[$"LIVE:{playerId}"].CurrentAbility, $"Untrusted CA leaked for player {playerId}.");
                Assert.IsNull(byId[$"LIVE:{playerId}"].PotentialAbility, $"Untrusted PA leaked for player {playerId}.");
            }

            var caFiltered = await database.SearchLivePlayersAsync(null, minCa: 170);
            CollectionAssert.AreEqual(
                new[] { "LIVE:100" },
                caFiltered.Select(x => x.SourceId).ToArray(),
                "The CA filter must not use higher unverified/provisional values.");

            var attributeFiltered = await database.SearchLivePlayersAsync(
                null, attributeName: "Passing", minAttribute: 15);
            CollectionAssert.AreEquivalent(
                new[] { "LIVE:100", "LIVE:101" },
                attributeFiltered.Select(x => x.SourceId).ToArray(),
                "Attribute search must not include facts without a canonical verified status.");

            var clubRows = await database.GetLivePlayersForClubAsync(77);
            var clubById = clubRows.ToDictionary(x => x.SourceId);
            Assert.IsNull(clubById["LIVE:102"].CurrentAbility);
            Assert.IsNull(clubById["LIVE:103"].PotentialAbility);
            Assert.AreEqual(180, clubById["LIVE:100"].CurrentAbility);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task InsertFact(
        SqliteConnection connection,
        long runId,
        long playerUid,
        string fieldKey,
        double value,
        string validationStatus)
    {
        var fact = connection.CreateCommand();
        fact.CommandText = """
            INSERT INTO LivePlayerFacts(PlayerUid,FieldKey,NumericValue,ValidationStatus,SourceRunId,UpdatedUtc)
            VALUES($uid,$field,$value,$status,$run,'2026-11-13T00:00:00Z');
            """;
        fact.Parameters.AddWithValue("$uid", playerUid);
        fact.Parameters.AddWithValue("$field", fieldKey);
        fact.Parameters.AddWithValue("$value", value);
        fact.Parameters.AddWithValue("$status", validationStatus);
        fact.Parameters.AddWithValue("$run", runId);
        await fact.ExecuteNonQueryAsync();
    }
}
