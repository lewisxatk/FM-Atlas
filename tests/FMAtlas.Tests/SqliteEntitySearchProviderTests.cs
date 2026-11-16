using FMAtlas.Domain.Clubs;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Staff;
using FMAtlas.Infrastructure.Persistence;
using Xunit;

namespace FMAtlas.Tests;

public sealed class SqliteEntitySearchProviderTests
{
    private static readonly FieldProvenance Evidence =
        new(DataSourceKind.CareerSave, FieldEvidenceStatus.Documented);

    [Fact]
    public async Task QuickSearchReturnsPlayerStaffAndClubNamesFromTheEntityIndex()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var playerId = new EntityIdentity(EntityKind.Player, "p-1", "gamepass");
        var staffId = new EntityIdentity(EntityKind.Staff, "s-1", "gamepass");
        var clubId = new EntityIdentity(EntityKind.Club, "c-1", "gamepass");

        await store.SaveSnapshotAsync(
            new PlayerRecord(playerId) { FullName = new SourcedValue<string>("Lamine Yamal", Evidence) },
            "Lamine Yamal");
        await store.SaveSnapshotAsync(
            new StaffRecord(staffId) { FullName = new SourcedValue<string>("Maya Coach", Evidence) },
            "Maya Coach");
        await store.SaveSnapshotAsync(
            new ClubRecord(clubId) { Name = new SourcedValue<string>("Yamal United", Evidence) },
            "Yamal United");

        var provider = new SqliteEntitySearchProvider(path);
        var results = await provider.QuickSearchAsync("yAmAl");

        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.Identity == playerId);
        Assert.Contains(results, x => x.Identity == clubId);
    }

    [Fact]
    public async Task EntityKindFilterRestrictsSearchResults()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);
        var playerId = new EntityIdentity(EntityKind.Player, "1", "test");
        var clubId = new EntityIdentity(EntityKind.Club, "1", "test");

        await store.SaveSnapshotAsync(
            new PlayerRecord(playerId) { FullName = new SourcedValue<string>("Arsenal Prospect", Evidence) },
            "Arsenal Prospect");
        await store.SaveSnapshotAsync(
            new ClubRecord(clubId) { Name = new SourcedValue<string>("Arsenal FC", Evidence) },
            "Arsenal FC");

        var results = await new SqliteEntitySearchProvider(path).QuickSearchAsync(
            "Arsenal",
            [EntityKind.Player]);

        Assert.Single(results);
        Assert.Equal(EntityKind.Player, results[0].Identity.Kind);
    }

    [Fact]
    public async Task PercentAndUnderscoreAreTreatedAsLiteralSearchCharacters()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        await store.SaveSnapshotAsync(
            new PlayerRecord(new EntityIdentity(EntityKind.Player, "1", "test"))
            {
                FullName = new SourcedValue<string>("Player 100% Fit", Evidence)
            },
            "Player 100% Fit");
        await store.SaveSnapshotAsync(
            new PlayerRecord(new EntityIdentity(EntityKind.Player, "2", "test"))
            {
                FullName = new SourcedValue<string>("Player 1000X Fit", Evidence)
            },
            "Player 1000X Fit");

        var results = await new SqliteEntitySearchProvider(path).QuickSearchAsync("100%");

        Assert.Single(results);
        Assert.Equal("Player 100% Fit", results[0].DisplayName);
    }

    [Fact]
    public async Task EmptyQueryAndEmptyKindListReturnNoResults()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();

        var provider = new SqliteEntitySearchProvider(path);

        Assert.Empty(await provider.QuickSearchAsync("  "));
        Assert.Empty(await provider.QuickSearchAsync("player", Array.Empty<EntityKind>()));
    }

    [Fact]
    public async Task RejectsInvalidResultLimit()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();

        var provider = new SqliteEntitySearchProvider(path);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.QuickSearchAsync("player", limit: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.QuickSearchAsync("player", limit: 201));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "FMAtlas.SearchTests", Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
