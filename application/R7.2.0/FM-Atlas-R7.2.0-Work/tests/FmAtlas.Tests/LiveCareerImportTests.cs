using FmAtlas.Data;
using FmAtlas.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FmAtlas.Tests;

public sealed class LiveCareerImportTests
{
    [Fact]
    public async Task SearchReturnsProfileMatchedProvisionalCaPaAndAttributeFilters()
    {
        using var temp = new TemporaryDirectory();
        var database = new ScoutDatabase(Path.Combine(temp.Path, "scouting.db"));
        await database.InitialiseAsync();
        await database.ImportLiveSnapshotAsync(CreatePayload());

        var all = await database.SearchLivePlayersAsync("Fixture Player");
        var player = Assert.Single(all);
        Assert.Equal(147, player.CurrentAbility);
        Assert.Equal(-8, player.PotentialAbility);
        Assert.Equal("Fixture FC", player.Club);
        Assert.Equal("England", player.Nation);
        Assert.Equal("ST", player.Position);
        Assert.Contains("provisional", player.DataOrigin, StringComparison.OrdinalIgnoreCase);

        var byAbility = await database.SearchLivePlayersAsync("Fixture Player", minCa: 140, minPa: -9, maxPa: -5);
        Assert.Single(byAbility);

        var byAttribute = await database.SearchLivePlayersAsync(
            "Fixture Player", attributeName: "finishing", minAttribute: 16, maxAttribute: 18);
        Assert.Single(byAttribute);

        var byPersonality = await database.SearchLivePlayersAsync(
            "Fixture Player", attributeName: "professionalism", minAttribute: 16, maxAttribute: 19);
        Assert.Single(byPersonality);

        var byPosition = await database.SearchLivePlayersAsync("Fixture Player", positionFilter: "ST");
        Assert.Single(byPosition);
        var wrongPosition = await database.SearchLivePlayersAsync("Fixture Player", positionFilter: "GK");
        Assert.Empty(wrongPosition);

        var advanced = await database.SearchAsync(
            "Fixture Player", null, null, "ATTACKERS", null, "England", null,
            minCa: 140, minPa: -9, maxPa: -5);
        Assert.Contains(advanced, result => result.SourceId == "LIVE:7001");

        var personality = await database.GetPlayerAttributesAsync("LIVE:7001");
        Assert.Equal(18d, personality["professionalism"]);

        var tooHigh = await database.SearchLivePlayersAsync("Fixture Player", minCa: 180);
        Assert.Empty(tooHigh);
    }

    [Fact]
    public async Task StaffSearchProfileAndCareerIdentityUseTheConnectedLiveManager()
    {
        using var temp = new TemporaryDirectory();
        var database = new ScoutDatabase(Path.Combine(temp.Path, "scouting.db"));
        await database.InitialiseAsync();
        await database.ImportLiveSnapshotAsync(CreatePayload());

        var staff = Assert.Single(await database.SearchFmsaveStaffAsync("Fixture Manager"));
        Assert.Equal("Fixture Manager", staff.Name);
        Assert.True(staff.IsHumanManager);
        Assert.Equal(132, staff.CurrentAbility);
        Assert.Equal(164, staff.PotentialAbility);
        Assert.Equal("Fixture FC", staff.Club);
        Assert.Equal("England", staff.Nation);
        Assert.Equal("Manager", staff.Job);
        Assert.Equal(41, staff.Age);

        // The directory advertises search by staff name, club or ID. Live rows must
        // honour the club part of that contract, not just save-snapshot rows.
        var byClub = Assert.Single(await database.SearchFmsaveStaffAsync("Fixture FC"));
        Assert.Equal("Fixture Manager", byClub.Name);
        Assert.Equal("Fixture FC", byClub.Club);

        var byUid = Assert.Single(await database.SearchFmsaveStaffAsync("9001"));
        Assert.Equal("Fixture Manager", byUid.Name);

        var facts = await database.GetFmsaveStaffFactsAsync(staff.SourceId);
        Assert.Contains(facts, fact => fact.Field.Contains("is_human_manager", StringComparison.OrdinalIgnoreCase)
            && fact.Value == "1");
        Assert.Contains(facts, fact => fact.Group.Contains("provisional", StringComparison.OrdinalIgnoreCase));

        var metrics = await database.GetWorkspaceMetricsAsync();
        Assert.Equal("Fixture Manager", metrics.CurrentManager);
        Assert.Equal("Fixture FC", metrics.CurrentManagerClub);
        Assert.Equal("England", metrics.CurrentManagerNation);
        Assert.Equal(42L, metrics.LiveSaveLoadCounter);
    }

    [Fact]
    public async Task StaffDirectoryDeduplicatesSamePersonAcrossOfflineAndLiveSources()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        await database.ImportLiveSnapshotAsync(CreatePayload());

        await using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString()))
        {
            await connection.OpenAsync();
            var import = connection.CreateCommand();
            import.CommandText = """
                INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc,Game,GameBuild)
                VALUES('offline-fixture.fm',0,'2026-11-14T00:00:00Z','FM27','fixture');
                SELECT last_insert_rowid();
                """;
            var snapshotId = Convert.ToInt32(await import.ExecuteScalarAsync());
            var staff = connection.CreateCommand();
            staff.CommandText = """
                INSERT INTO FmsaveStaffDirectory(SnapshotId,StaffUid,Name,ClubName,JobTitle,Age,Nation,RawJson)
                VALUES($snapshot,'OFFLINE-MANAGER-1','Fixture Manager','Fixture FC','Manager',41,'England','{}');
                """;
            staff.Parameters.AddWithValue("$snapshot", snapshotId);
            await staff.ExecuteNonQueryAsync();
        }

        var results = await database.SearchFmsaveStaffAsync("Fixture Manager");
        var staffer = Assert.Single(results);
        Assert.Equal("9001", staffer.SourceId);
        Assert.True(staffer.IsHumanManager);
        Assert.Equal(132, staffer.CurrentAbility);
        Assert.Equal(164, staffer.PotentialAbility);
        Assert.Equal("Fixture FC", staffer.Club);
    }

    [Fact]
    public async Task NewLiveIdentityRunReplacesOlderManagerAndCounter()
    {
        using var temp = new TemporaryDirectory();
        var database = new ScoutDatabase(Path.Combine(temp.Path, "scouting.db"));
        await database.InitialiseAsync();
        await database.ImportLiveSnapshotAsync(CreatePayload());
        var first = await database.GetWorkspaceMetricsAsync();
        Assert.Equal(42L, first.LiveSaveLoadCounter);

        var next = CreatePayload() with
        {
            SaveLoadCounter = 43,
            Staff = new List<LiveStaffRecord>
            {
                new(9002, "Second Manager", 0, 0x200000, true,
                    new DateTime(1988, 3, 4), 38, "Scotland", 1651,
                    "Second FC", 56, "Manager", 16, 128, 158)
            }
        };
        // A fresh live import must also carry the clubs referenced by the new manager.
        next = next with
        {
            Clubs = next.Clubs.Concat(new[] { new LiveClubRecord(56, "Second FC", 1, 0x210000, false) }).ToList()
        };
        await database.ImportLiveSnapshotAsync(next);

        var metrics = await database.GetWorkspaceMetricsAsync();
        Assert.Equal("Second Manager", metrics.CurrentManager);
        Assert.Equal("Second FC", metrics.CurrentManagerClub);
        Assert.Equal(43L, metrics.LiveSaveLoadCounter);
    }

    [Fact]
    public async Task NewLiveSnapshotReplacesOnlyActiveLiveRecordsAndKeepsOfflineLocalData()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        await database.ImportLiveSnapshotAsync(CreatePayload());

        // Seed a durable local/CSV-style player row that must survive a live career switch.
        await using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString()))
        {
            await connection.OpenAsync();
            var player = connection.CreateCommand();
            player.CommandText = """
                INSERT INTO Players(SourceId,Name,Age,Club,Nation,League,Position,DataOrigin,UpdatedUtc)
                VALUES('LOCAL:KEEP','Offline Local Prospect',23,'Local FC','England','Local League','MC','Local CSV','2026-11-14T00:00:00Z');
                INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc)
                VALUES('LOCAL:KEEP','Passing','Technical',16,'2026-11-14T00:00:00Z');
                """;
            await player.ExecuteNonQueryAsync();
        }

        var next = CreatePayload() with
        {
            SaveLoadCounter = 43,
            Players = new List<LivePlayerRecord>
            {
                new(7002, "Second Career Player", "0x220000", 55, 0, 0x230000,
                    true, true, new DateTime(2001, 6, 4), "Unknown",
                    "Second career test fixture.", 151, -6,
                    new Dictionary<string, double> { ["Finishing"] = 18 },
                    new Dictionary<string, double>(),
                    new Dictionary<string, double> { ["ST"] = 19 })
            },
            Staff = new List<LiveStaffRecord>
            {
                new(9002, "Second Career Manager", 0, 0x240000, true,
                    new DateTime(1988, 3, 4), 38, "Scotland", 1651,
                    "Fixture FC", 55, "Manager", 16, 128, 158)
            }
        };
        await database.ImportLiveSnapshotAsync(next);

        Assert.Empty(await database.SearchLivePlayersAsync("Fixture Player"));
        var current = Assert.Single(await database.SearchLivePlayersAsync("Second Career Player"));
        Assert.Equal(151, current.CurrentAbility);
        Assert.Equal(-6, current.PotentialAbility);

        var offline = Assert.Single(await database.SearchAsync(
            "Offline Local Prospect", null, null, null, null, null, null, limit: 10));
        Assert.Equal("LOCAL:KEEP", offline.SourceId);
        var offlineAttributes = await database.GetPlayerAttributesAsync("LOCAL:KEEP");
        Assert.Equal(16d, offlineAttributes["Passing"]);

        var manager = Assert.Single(await database.SearchFmsaveStaffAsync("Second Career Manager"));
        Assert.Equal("Second Career Manager", manager.Name);
        Assert.Equal(43L, (await database.GetWorkspaceMetricsAsync()).LiveSaveLoadCounter);
    }

    [Fact]
    public async Task CancelledDatabasePublicationLeavesPreviousLiveSnapshotIntact()
    {
        using var temp = new TemporaryDirectory();
        var database = new ScoutDatabase(Path.Combine(temp.Path, "scouting.db"));
        await database.InitialiseAsync();
        await database.ImportLiveSnapshotAsync(CreatePayload());

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var next = CreatePayload() with { SaveLoadCounter = 43 };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => database.ImportLiveSnapshotAsync(next, cancellation.Token));

        var current = Assert.Single(await database.SearchLivePlayersAsync("Fixture Player"));
        Assert.Equal(147, current.CurrentAbility);
        Assert.Equal(-8, current.PotentialAbility);
        Assert.Equal(42L, (await database.GetWorkspaceMetricsAsync()).LiveSaveLoadCounter);
    }

    [Fact]
    public void LiveSessionIdentityIncludesProcessBuildAndSaveGeneration()
    {
        var first = new LiveCareerProbe(
            IsGameRunning: true, IsSupportedBuild: true, IsCareerLoaded: true,
            ProcessId: 1234, ProfileId: "steam-26385295", ProfileName: "FM27 Steam",
            SaveLoadCounter: 42, PlayerSlots: 40_286, StaffSlots: 28_497,
            ClubSlots: 8_000, Status: "Test career");
        var same = first with { Status = "Re-probed career" };
        var restartedGame = first with { ProcessId = 9876, SaveLoadCounter = 1 };
        var differentBuild = first with { ProfileId = "gamepass-0.9.2435.0" };
        var differentSave = first with { SaveLoadCounter = 43 };

        Assert.True(first.MatchesSession(first.SessionKey));
        Assert.True(same.MatchesSession(first.SessionKey));
        Assert.False(restartedGame.MatchesSession(first.SessionKey));
        Assert.False(differentBuild.MatchesSession(first.SessionKey));
        Assert.False(differentSave.MatchesSession(first.SessionKey));
    }

    [Fact]
    public void LiveScannerHonoursCancellationBeforeOpeningTheGameProcess()
    {
        var probe = new LiveCareerProbe(
            IsGameRunning: true,
            IsSupportedBuild: true,
            IsCareerLoaded: true,
            ProcessId: 1234,
            ProfileId: "steam-26385295",
            ProfileName: "FM27 Steam",
            SaveLoadCounter: 42,
            PlayerSlots: 40_286,
            StaffSlots: 28_497,
            ClubSlots: 8_000,
            Status: "Test career");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            LiveDataImporter.Extract(probe, cancellation.Token));
    }

    private static LiveImportPayload CreatePayload()
    {
        var club = new LiveClubRecord(55, "Fixture FC", 0, 0x100000, false);
        var player = new LivePlayerRecord(
            7001, "Fixture Player", "0x110000", club.Uid, 0, 0x120000,
            true, true, new DateTime(2002, 1, 5), "Unknown",
            "Generated-player evidence is not mapped in this regression fixture.",
            147, -8,
            new Dictionary<string, double> { ["Finishing"] = 17 },
            new Dictionary<string, double> { ["professionalism"] = 18, ["ambition"] = 15 },
            new Dictionary<string, double> { ["ST"] = 18 },
            NationName: "England", NationUid: 1646);
        var manager = new LiveStaffRecord(
            9001, "Fixture Manager", 0, 0x130000, true,
            new DateTime(1985, 7, 8), 41, "England", 1646,
            club.Name, club.Uid, "Manager", 16, 132, 164);
        return new LiveImportPayload(
            "FM27 fixture profile", "PE fingerprint fixture",
            new List<LiveClubRecord> { club }, new List<LivePlayerRecord> { player },
            PlayerSlots: 1, UnnamedPlayers: 0, UnlinkedPlayers: 0,
            GameDate: new DateTime(2026, 11, 14),
            SaveLoadCounter: 42, StaffSlots: 1,
            Staff: new List<LiveStaffRecord> { manager });
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "FmAtlas-live-tests-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* Test cleanup must not mask an assertion or SQLite failure. */ }
        }
    }
}
