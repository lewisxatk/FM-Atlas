using Microsoft.Data.Sqlite;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Clubs;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Staff;
using FMAtlas.Domain.Search;
using FMAtlas.Infrastructure.Persistence;
using Xunit;

namespace FMAtlas.Tests;

public sealed class LiveGameBatchImportTests
{
    private static readonly FieldProvenance LiveEvidence = new(
        DataSourceKind.LiveGameMemory,
        FieldEvidenceStatus.Mapped,
        SourceVersion: "FM27 Game Pass 0.9.2435.0",
        TargetBuildFingerprint: "PE:6AE8CF77:17969000",
        ObservedAtUtc: DateTimeOffset.UtcNow);

    [Fact]
    public async Task BatchImportProjectsPlayersStaffAndClubsIntoTheSameSearchIndex()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(databasePath).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(databasePath);
        var batch = (IEntityRecordBatchSnapshotStore)store;
        const string provider = "live-fm27-gamepass-test";

        IEntityRecord[] records =
        [
            new PlayerRecord(new EntityIdentity(EntityKind.Player, "p-1", provider))
            {
                FullName = new SourcedValue<string>("Live Player", LiveEvidence),
                Gender = new SourcedValue<string>("Male", LiveEvidence),
                CurrentAbility = new SourcedValue<int>(154, LiveEvidence),
                Attributes = new Dictionary<string, SourcedValue<int>>
                {
                    ["dribbling"] = new(17, LiveEvidence),
                    ["pace"] = new(18, LiveEvidence)
                }
            },
            new StaffRecord(new EntityIdentity(EntityKind.Staff, "s-1", provider))
            {
                FullName = new SourcedValue<string>("Live Coach", LiveEvidence),
                Gender = new SourcedValue<string>("Female", LiveEvidence),
                Attributes = new Dictionary<string, SourcedValue<int>>
                {
                    ["attribute_attacking"] = new(16, LiveEvidence)
                }
            },
            new ClubRecord(new EntityIdentity(EntityKind.Club, "c-1", provider))
            {
                Name = new SourcedValue<string>("Live Club", LiveEvidence),
                SearchIntegerFields = new Dictionary<string, SourcedValue<int>>
                {
                    ["club_training_facilities"] = new(18, LiveEvidence)
                }
            }
        ];

        await batch.SaveSnapshotsAsync(records);

        var results = await new SqliteEntitySearchProvider(databasePath).QuickSearchAsync("Live");

        Assert.Equal(3, results.Count);
        Assert.Contains(results, result => result.Identity.Kind == EntityKind.Player);
        Assert.Contains(results, result => result.Identity.Kind == EntityKind.Staff);
        Assert.Contains(results, result => result.Identity.Kind == EntityKind.Club);

        var player = await store.GetSnapshotAsync<PlayerRecord>(new EntityIdentity(EntityKind.Player, "p-1", provider));
        Assert.Equal(154, player?.CurrentAbility?.Value);
        Assert.Equal(17, player?.Attributes["dribbling"].Value);
        Assert.Equal(FieldEvidenceStatus.Mapped, player?.Attributes["dribbling"].Provenance.EvidenceStatus);
    }

    [Fact]
    public async Task LiveBatchRecordsExposePreferredMovesAndClubLiveFieldsToAdvancedSearch()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(databasePath).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(databasePath);
        var batch = (IEntityRecordBatchSnapshotStore)store;
        const string providerKey = "live-fm27-gamepass-test";

        IEntityRecord[] records =
        [
            new PlayerRecord(new EntityIdentity(EntityKind.Player, "player-traits", providerKey))
            {
                FullName = new SourcedValue<string>("Trait Player", LiveEvidence),
                PreferredMoves = [new SourcedValue<string>("Tries tricks", LiveEvidence)],
                SecondNationality = new SourcedValue<string>("Spain", LiveEvidence),
                SearchTextFields = new Dictionary<string, SourcedValue<string>>
                {
                    ["trained_in_nation_1"] = new("England", LiveEvidence)
                },
                SearchIntegerFields = new Dictionary<string, SourcedValue<int>>
                {
                    ["condition"] = new(8_500, LiveEvidence),
                    ["morale"] = new(17, LiveEvidence)
                }
            },
            new ClubRecord(new EntityIdentity(EntityKind.Club, "club-live", providerKey))
            {
                Name = new SourcedValue<string>("Live Facilities Club", LiveEvidence),
                SearchIntegerFields = new Dictionary<string, SourcedValue<int>>
                {
                    ["club_training_facilities"] = new(18, LiveEvidence),
                    ["stadium_capacity"] = new(62_000, LiveEvidence)
                },
                SearchBooleanFields = new Dictionary<string, SourcedValue<bool>>
                {
                    ["club_is_women"] = new(false, LiveEvidence)
                },
                SearchDecimalFields = new Dictionary<string, SourcedValue<decimal>>
                {
                    ["club_ticket_price"] = new(42.5m, LiveEvidence)
                }
            }
        ];

        await batch.SaveSnapshotsAsync(records);

        var advanced = new SqliteAdvancedSearchProvider(databasePath);
        var playerQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("preferred_moves", SearchFilterOperator.HasTrait, TextValue: "Tries tricks")]);
        var players = await advanced.ExecuteAsync(playerQuery);
        Assert.Equal("player-traits", Assert.Single(players).Identity.SourceId);
        var playerSnapshot = await store.GetSnapshotAsync<PlayerRecord>(
            new EntityIdentity(EntityKind.Player, "player-traits", providerKey));
        Assert.Equal("Spain", playerSnapshot?.SecondNationality?.Value);
        Assert.Equal("England", playerSnapshot?.SearchTextFields["trained_in_nation_1"].Value);

        var clubFields = await advanced.GetIndexedFieldsAsync(SearchEntityKind.Club);
        Assert.Contains(clubFields, field => field.Key == "club_training_facilities" && field.CanExecute);
        Assert.Contains(clubFields, field => field.Key == "stadium_capacity" && field.CanExecute);
        Assert.Contains(clubFields, field => field.Key == "club_is_women" && field.ValueKind == SearchFieldValueKind.Boolean);
        Assert.Contains(clubFields, field => field.Key == "club_ticket_price" && field.ValueKind == SearchFieldValueKind.DecimalRange);

        var clubQuery = new AdvancedSearchQuery(
            SearchEntityKind.Club,
            SearchFilterLogic.All,
            [new SearchFilterCondition("club_training_facilities", SearchFilterOperator.Range, Minimum: 18)],
            IndexedFields: clubFields);
        var clubs = await advanced.ExecuteAsync(clubQuery);
        Assert.Equal("club-live", Assert.Single(clubs).Identity.SourceId);
    }

    [Fact]
    public async Task ClubProfileFindsPlayersAndStaffBySourceBackedClubUid()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(databasePath).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(databasePath);
        var batch = (IEntityRecordBatchSnapshotStore)store;
        const string provider = "live-fm27-gamepass-test";

        IEntityRecord[] records =
        [
            new PlayerRecord(new EntityIdentity(EntityKind.Player, "squad-player-1", provider))
            {
                FullName = new SourcedValue<string>("Squad Player One", LiveEvidence),
                SearchIntegerFields = new Dictionary<string, SourcedValue<int>>
                {
                    ["club_uid"] = new(77, LiveEvidence)
                }
            },
            new PlayerRecord(new EntityIdentity(EntityKind.Player, "other-club-player", provider))
            {
                FullName = new SourcedValue<string>("Other Club Player", LiveEvidence),
                SearchIntegerFields = new Dictionary<string, SourcedValue<int>>
                {
                    ["club_uid"] = new(88, LiveEvidence)
                }
            },
            new StaffRecord(new EntityIdentity(EntityKind.Staff, "club-staff-1", provider))
            {
                FullName = new SourcedValue<string>("Club Coach", LiveEvidence),
                SearchIntegerFields = new Dictionary<string, SourcedValue<int>>
                {
                    ["club_uid"] = new(77, LiveEvidence)
                }
            },
            new StaffRecord(new EntityIdentity(EntityKind.Staff, "other-club-staff", provider))
            {
                FullName = new SourcedValue<string>("Other Club Coach", LiveEvidence),
                SearchIntegerFields = new Dictionary<string, SourcedValue<int>>
                {
                    ["club_uid"] = new(88, LiveEvidence)
                }
            },
            new ClubRecord(new EntityIdentity(EntityKind.Club, "77", provider))
            {
                Name = new SourcedValue<string>("Squad Test Club", LiveEvidence)
            }
        ];

        await batch.SaveSnapshotsAsync(records);

        var advanced = new SqliteAdvancedSearchProvider(databasePath);
        var playerFields = await advanced.GetIndexedFieldsAsync(SearchEntityKind.Player);
        Assert.Contains(playerFields, field =>
            field.Key == "club_uid" &&
            field.ValueKind == SearchFieldValueKind.IntegerRange &&
            field.CanExecute);

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [
                new SearchFilterCondition(
                    "club_uid",
                    SearchFilterOperator.Range,
                    Minimum: 77,
                    Maximum: 77)
            ],
            IndexedFields: playerFields);

        var squad = await advanced.ExecuteAsync(query, limit: 200);

        var player = Assert.Single(squad);
        Assert.Equal("squad-player-1", player.Identity.SourceId);
        Assert.Equal("Squad Player One", player.DisplayName);

        var staffFields = await advanced.GetIndexedFieldsAsync(SearchEntityKind.Coach);
        Assert.Contains(staffFields, field =>
            field.Key == "club_uid" &&
            field.ValueKind == SearchFieldValueKind.IntegerRange &&
            field.CanExecute);
        var staffQuery = new AdvancedSearchQuery(
            SearchEntityKind.Coach,
            SearchFilterLogic.All,
            [
                new SearchFilterCondition(
                    "club_uid",
                    SearchFilterOperator.Range,
                    Minimum: 77,
                    Maximum: 77)
            ],
            IndexedFields: staffFields);
        var staff = await advanced.ExecuteAsync(staffQuery, limit: 200);
        var coach = Assert.Single(staff);
        Assert.Equal("club-staff-1", coach.Identity.SourceId);
        Assert.Equal("Club Coach", coach.DisplayName);
    }

    [Fact]
    public async Task DeletingLiveProviderRemovesItsRecordsButPreservesExportedRecords()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(databasePath).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(databasePath);
        var batch = (IEntityRecordBatchSnapshotStore)store;

        IEntityRecord[] records =
        [
            new PlayerRecord(new EntityIdentity(EntityKind.Player, "live-1", "live-fm27-gamepass-test"))
            {
                FullName = new SourcedValue<string>("Live Old Career", LiveEvidence)
            },
            new PlayerRecord(new EntityIdentity(EntityKind.Player, "csv-1", "fm27-view-export:0123456789abcdef"))
            {
                FullName = new SourcedValue<string>("CSV Player", new FieldProvenance(
                    DataSourceKind.GameExport, FieldEvidenceStatus.Mapped))
            }
        ];
        await batch.SaveSnapshotsAsync(records);

        await batch.DeleteProviderAsync("live-fm27-gamepass-test");

        var live = await new SqliteEntitySearchProvider(databasePath).QuickSearchAsync("Live");
        var csv = await new SqliteEntitySearchProvider(databasePath).QuickSearchAsync("CSV");

        Assert.Empty(live);
        Assert.Single(csv);
        Assert.Equal("CSV Player", csv[0].DisplayName);
    }

    [Fact]
    public async Task InvalidRecordPreventsTheEntireBatchFromBeingWritten()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(databasePath).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(databasePath);
        var batch = (IEntityRecordBatchSnapshotStore)store;

        IEntityRecord[] records =
        [
            new PlayerRecord(new EntityIdentity(EntityKind.Player, "valid", "live-fm27-gamepass-test"))
            {
                FullName = new SourcedValue<string>("Should Not Persist", LiveEvidence)
            },
            new PlayerRecord(new EntityIdentity(EntityKind.Player, "", "live-fm27-gamepass-test"))
            {
                FullName = new SourcedValue<string>("Invalid Identity", LiveEvidence)
            }
        ];

        await Assert.ThrowsAsync<ArgumentException>(() => batch.SaveSnapshotsAsync(records));

        var results = await new SqliteEntitySearchProvider(databasePath).QuickSearchAsync("Should Not Persist");
        Assert.Empty(results);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "FMAtlas.LiveBatchTests", Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
