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
    public void LiveImporterHonoursCancellationBeforeOpeningTheGameProcess()
    {
        var probe = new LiveCareerProbe(
            IsGameRunning: true, IsSupportedBuild: true, IsCareerLoaded: true,
            ProcessId: 1234, ProfileId: "steam-26385295", ProfileName: "FM27 Steam",
            SaveLoadCounter: 7, PlayerSlots: 40286, StaffSlots: 28000, ClubSlots: 9000,
            Status: "Career loaded");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            LiveDataImporter.Extract(probe, cancellation.Token));
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
                    (42,'identity.is_human_manager',1,NULL,'Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z'),
                    (42,'staff_attributes.attacking',15,NULL,'Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z'),
                    (42,'staff_personality.professionalism',18,NULL,'Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z'),
                    (42,'contract.wage_per_week_gbp',2500,NULL,'Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z'),
                    (42,'contract.expiry_date',NULL,'2028-06-30','Provisional-ProfileMatched-Needs-UI-Witness',$run,'2026-11-14T00:00:05Z');
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
            Assert.Contains(facts, x => x.Field == "staff_attributes.attacking" && x.Value == "15");
            Assert.Contains(facts, x => x.Field == "staff_personality.professionalism" && x.Value == "18");
            Assert.Contains(facts, x => x.Field == "contract.wage_per_week_gbp" && x.Value == "2500");
            Assert.Contains(facts, x => x.Field == "contract.expiry_date" && x.Value == "2028-06-30");
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void StaffAttributeDecoderUsesDocumentedFm27OffsetsAndDisplayScale()
    {
        var staffObject = new byte[0x540];
        staffObject[0x32] = 73; // Attacking: (73 + 2) / 5 = 15
        staffObject[0x2C] = 92; // Judging player ability: (92 + 2) / 5 = 18
        staffObject[0x3F] = 68; // Sports science: (68 + 2) / 5 = 14
        staffObject[0x14] = 17; // Authority is stored directly as 1..20
        staffObject[0x1C] = 19; // Working with youngsters is stored directly as 1..20
        staffObject[0x33] = 101; // Out-of-range raw value must not become a fabricated rating

        var decoded = StaffAttributeDecoder.Decode(staffObject);

        Assert.Equal(15, decoded["attacking"]);
        Assert.Equal(18, decoded["judging_player_ability"]);
        Assert.Equal(14, decoded["sports_science"]);
        Assert.Equal(17, decoded["authority"]);
        Assert.Equal(19, decoded["working_with_youngsters"]);
        Assert.False(decoded.ContainsKey("defending"));
        Assert.Equal(20, decoded.Count);
    }

    [Fact]
    public void StaffAttributeDecoderLeavesMissingBytesAbsent()
    {
        var decoded = StaffAttributeDecoder.Decode(new byte[0x20]);

        Assert.Empty(decoded);
    }

    [Fact]
    public void StaffPersonalityDecoderUsesTheHumanManagerIdentityShift()
    {
        var staffObject = new byte[0x540];
        for (var i = 0; i < 8; i++)
        {
            staffObject[0x170 + i] = (byte)(i + 1);
            staffObject[0x170 + 0x340 + i] = (byte)(13 + i);
        }

        var ordinaryStaff = StaffAttributeDecoder.DecodePersonality(staffObject, 0);
        var humanManager = StaffAttributeDecoder.DecodePersonality(staffObject, 0x340);

        Assert.Equal(8, ordinaryStaff.Count);
        Assert.Equal(8, humanManager.Count);
        Assert.Equal(1, ordinaryStaff["adaptability"]);
        Assert.Equal(8, ordinaryStaff["controversy"]);
        Assert.Equal(13, humanManager["adaptability"]);
        Assert.Equal(20, humanManager["controversy"]);
        Assert.Throws<ArgumentOutOfRangeException>(() => StaffAttributeDecoder.DecodePersonality(staffObject, 0x100));
    }
}
