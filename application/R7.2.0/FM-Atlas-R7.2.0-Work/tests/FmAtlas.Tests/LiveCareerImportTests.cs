using FmAtlas.Data;
using FmAtlas.Services;
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

        var byAbility = await database.SearchLivePlayersAsync("Fixture Player", minCa: 140, minPa: -9, maxPa: -5);
        Assert.Single(byAbility);

        var byAttribute = await database.SearchLivePlayersAsync(
            "Fixture Player", attributeName: "finishing", minAttribute: 16, maxAttribute: 18);
        Assert.Single(byAttribute);

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

    private static LiveImportPayload CreatePayload()
    {
        var club = new LiveClubRecord(55, "Fixture FC", 0, 0x100000, false);
        var player = new LivePlayerRecord(
            7001, "Fixture Player", "0x110000", club.Uid, 0, 0x120000,
            true, true, new DateTime(2002, 1, 5), "Unknown",
            "Generated-player evidence is not mapped in this regression fixture.",
            147, -8,
            new Dictionary<string, double> { ["Finishing"] = 17 },
            new Dictionary<string, double>(),
            new Dictionary<string, double> { ["ST"] = 18 });
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
