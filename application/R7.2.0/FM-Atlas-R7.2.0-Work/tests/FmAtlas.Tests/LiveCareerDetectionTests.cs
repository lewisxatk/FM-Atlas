using FmAtlas.Data;
using FmAtlas.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FmAtlas.Tests;

public sealed class LiveCareerDetectionTests
{
    [Fact]
    public void SessionKeyIdentifiesOneLoadedCareerGeneration()
    {
        var probe = new LiveCareerProbe(
            IsGameRunning: true,
            IsSupportedBuild: true,
            IsCareerLoaded: true,
            ProcessId: 1234,
            ProfileId: "steam-26385295",
            ProfileName: "FM27 Steam",
            SaveLoadCounter: 7,
            PlayerSlots: 40286,
            StaffSlots: 28000,
            ClubSlots: 9000,
            Status: "Career loaded");

        Assert.Equal("1234:steam-26385295:7", probe.SessionKey);
    }

    [Fact]
    public void SessionKeyIsDifferentWhenProcessChangesEvenIfProfileAndCounterMatch()
    {
        var imported = new LiveCareerProbe(
            IsGameRunning: true, IsSupportedBuild: true, IsCareerLoaded: true,
            ProcessId: 1234, ProfileId: "steam-26385295", ProfileName: "FM27 Steam",
            SaveLoadCounter: 7, PlayerSlots: 40286, StaffSlots: 28000, ClubSlots: 9000,
            Status: "Career loaded");
        var current = imported with { ProcessId = 5678 };

        Assert.Equal(imported.SaveLoadCounter, current.SaveLoadCounter);
        Assert.True(imported.MatchesSession(imported.SessionKey));
        Assert.False(current.MatchesSession(imported.SessionKey));
    }

    [Fact]
    public void SessionKeyIsEmptyUntilCareerIsLoaded()
    {
        var probe = new LiveCareerProbe(
            IsGameRunning: true,
            IsSupportedBuild: true,
            IsCareerLoaded: false,
            ProcessId: 1234,
            ProfileId: "steam-26385295",
            ProfileName: "FM27 Steam",
            SaveLoadCounter: 0,
            PlayerSlots: 0,
            StaffSlots: 0,
            ClubSlots: 0,
            Status: "Load a career");

        Assert.Equal(string.Empty, probe.SessionKey);
    }

    [Fact]
    public async Task LiveHumanManagerAndStaffFactsAreAvailableWithoutOfflineSaveImport()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas.LiveManager-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");

        try
        {
            var database = new ScoutDatabase(databasePath);
            await database.InitialiseAsync();

            await using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString()))
            {
                await connection.OpenAsync();

                var run = connection.CreateCommand();
                run.CommandText = """
                    INSERT INTO LiveImportRuns(GameVersion,ModuleSha256,StartedUtc,CompletedUtc,Status,PlayersSeen,PlayersImported,ClubsSeen,ClubsImported,StaffSeen,StaffImported)
                    VALUES('FM27 Steam','profile-fingerprint','2026-11-14T00:00:00Z','2026-11-14T00:00:05Z','Completed',40286,40286,9000,9000,28000,28000);
                    SELECT last_insert_rowid();
                    """;
                var runId = Convert.ToInt64(await run.ExecuteScalarAsync());

                var staff = connection.CreateCommand();
                staff.CommandText = """
                    INSERT INTO LiveStaff(StaffUid,StaffName,ArrayIndex,ObjectAddress,SourceRunId,ValidationStatus,UpdatedUtc)
                    VALUES(42,'Current Manager',0,'0x10000',$run,'HumanManager-LayoutMatched-Provisional','2026-11-14T00:00:05Z');
                    """;
                staff.Parameters.AddWithValue("$run", runId);
                await staff.ExecuteNonQueryAsync();

                var fact = connection.CreateCommand();
                fact.CommandText = """
                    INSERT INTO LiveStaffFacts(StaffUid,FieldKey,NumericValue,TextValue,ValidationStatus,SourceRunId,UpdatedUtc) VALUES
                    (42,'ability.current_ability',145,NULL,'Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z'),
                    (42,'ability.potential_ability',-8,NULL,'Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z'),
                    (42,'identity.age',44,NULL,'Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z'),
                    (42,'identity.nation_name',NULL,'England','Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z'),
                    (42,'identity.club_name',NULL,'Example FC','Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z'),
                    (42,'identity.job_title',NULL,'Manager','Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z'),
                    (42,'identity.is_human_manager',1,NULL,'Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z');
                    """;
                fact.Parameters.AddWithValue("$run", runId);
                await fact.ExecuteNonQueryAsync();

                var identity = connection.CreateCommand();
                identity.CommandText = """
                    INSERT INTO LiveCareerIdentity(RunId,SaveLoadCounter,ManagerUid,ManagerName,ManagerNation,ManagerClubUid,ManagerClubName,DetectedUtc)
                    VALUES($run,7,42,'Current Manager','England',9,'Example FC','2026-11-14T00:00:05Z');
                    """;
                identity.Parameters.AddWithValue("$run", runId);
                await identity.ExecuteNonQuery();
            }

            var metrics = await database.GetWorkspaceMetricsAsync();
            Assert.Equal("Current Manager", metrics.CurrentManager);
            Assert.Equal("Example FC", metrics.CurrentManagerClub);
            Assert.Equal("England", metrics.CurrentManagerNation);

            var staffRows = await database.SearchFmsaveStaffAsync("Current Manager");
            var manager = Assert.Single(staffRows);
            Assert.Equal("LIVE:42", manager.SourceId);
            Assert.Equal("Example FC", manager.Club);
            Assert.Equal("England", manager.Nation);
            Assert.Equal(44, manager.Age);
            Assert.Equal(145, manager.CurrentAbility);
            Assert.Equal(-8, manager.PotentialAbility);
            Assert.True(manager.IsHumanManager);

            var facts = await database.GetFmsaveStaffFactsAsync(manager.SourceId);
            Assert.Contains(facts, x => x.Field == "ability.current_ability" && x.Value == "145");
            Assert.Contains(facts, x => x.Field == "ability.potential_ability" && x.Value == "-8");
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
