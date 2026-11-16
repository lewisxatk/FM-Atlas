using Microsoft.Data.Sqlite;
using FMAtlas.Domain.Clubs;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Staff;
using FMAtlas.Infrastructure.Persistence;
using Xunit;

namespace FMAtlas.Tests;

public sealed class EntityRecordSnapshotStoreTests
{
    [Fact]
    public async Task PlayerSnapshotRoundTripsValuesAndFieldProvenanceAndIndexesFields()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();

        var identity = new EntityIdentity(EntityKind.Player, "12345", "fm27-gamepass-test");
        var provenance = new FieldProvenance(
            DataSourceKind.CareerSave,
            FieldEvidenceStatus.ReadVerified,
            SourceVersion: "FM27 fixture",
            ObservedAtUtc: DateTimeOffset.Parse("2026-11-16T08:00:00Z"));

        var original = new PlayerRecord(identity)
        {
            FullName = new SourcedValue<string>("Example Player", provenance),
            Age = new SourcedValue<int>(21, provenance),
            CurrentAbility = new SourcedValue<int>(145, provenance),
            Attributes = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["finishing"] = new(17, provenance),
                ["pace"] = new(16, provenance)
            },
            PreferredMoves = [new SourcedValue<string>("Runs With Ball Often", provenance)]
        };

        var store = new SqliteEntityRecordSnapshotStore(path);
        await store.SaveSnapshotAsync(original, "Example Player");

        var loaded = await store.GetSnapshotAsync<PlayerRecord>(identity);

        Assert.NotNull(loaded);
        Assert.Equal("Example Player", loaded.FullName!.Value);
        Assert.Equal(21, loaded.Age!.Value);
        Assert.Equal(145, loaded.CurrentAbility!.Value);
        Assert.Equal(17, loaded.Attributes["finishing"].Value);
        Assert.Equal("CareerSave", loaded.Attributes["finishing"].Provenance.Source.ToString());
        Assert.Equal("FM27 fixture", loaded.Attributes["finishing"].Provenance.SourceVersion);
        Assert.Equal("Runs With Ball Often", Assert.Single(loaded.PreferredMoves).Value);

        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT value_kind, integer_value, source_kind, evidence_status
            FROM field_values
            WHERE entity_kind = 'Player'
              AND provider_key = 'fm27-gamepass-test'
              AND source_id = '12345'
              AND field_key = 'attribute_finishing';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Integer", reader.GetString(0));
        Assert.Equal(17L, reader.GetInt64(1));
        Assert.Equal("CareerSave", reader.GetString(2));
        Assert.Equal("ReadVerified", reader.GetString(3));
    }

    [Fact]
    public async Task SameSourceIdFromDifferentProvidersDoesNotOverwrite()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var gamePassIdentity = new EntityIdentity(EntityKind.Player, "123", "gamepass");
        var steamIdentity = new EntityIdentity(EntityKind.Player, "123", "steam");
        var provenance = new FieldProvenance(DataSourceKind.CareerSave, FieldEvidenceStatus.Documented);

        var gamePass = new PlayerRecord(gamePassIdentity)
        {
            FullName = new SourcedValue<string>("Game Pass Player", provenance)
        };
        var steam = new PlayerRecord(steamIdentity)
        {
            FullName = new SourcedValue<string>("Steam Player", provenance)
        };

        await store.SaveSnapshotAsync(gamePass, "Game Pass Player");
        await store.SaveSnapshotAsync(steam, "Steam Player");

        Assert.Equal("Game Pass Player", (await store.GetSnapshotAsync<PlayerRecord>(gamePassIdentity))!.FullName!.Value);
        Assert.Equal("Steam Player", (await store.GetSnapshotAsync<PlayerRecord>(steamIdentity))!.FullName!.Value);
    }

    [Fact]
    public async Task SupportsTypedStaffAndClubSnapshots()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);
        var provenance = new FieldProvenance(DataSourceKind.GameExport, FieldEvidenceStatus.Documented);

        var staffIdentity = new EntityIdentity(EntityKind.Staff, "s-10", "fm27-export");
        var clubIdentity = new EntityIdentity(EntityKind.Club, "c-20", "fm27-export");

        var staff = new StaffRecord(staffIdentity)
        {
            FullName = new SourcedValue<string>("Coach Example", provenance),
            Attributes = new Dictionary<string, SourcedValue<int>> { ["motivating"] = new(16, provenance) }
        };
        var club = new ClubRecord(clubIdentity)
        {
            Name = new SourcedValue<string>("Club Example", provenance),
            Facilities = new Dictionary<string, SourcedValue<int>> { ["training"] = new(18, provenance) }
        };

        await store.SaveSnapshotAsync(staff, "Coach Example");
        await store.SaveSnapshotAsync(club, "Club Example");

        Assert.Equal("Coach Example", (await store.GetSnapshotAsync<StaffRecord>(staffIdentity))!.FullName!.Value);
        Assert.Equal(18, (await store.GetSnapshotAsync<ClubRecord>(clubIdentity))!.Facilities["training"].Value);
    }

    [Fact]
    public async Task DeletingSnapshotDoesNotAccidentallyDeleteEntity()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);
        var identity = new EntityIdentity(EntityKind.Player, "9", "test");

        await store.SaveSnapshotAsync(new PlayerRecord(identity), null);
        await store.DeleteSnapshotAsync<PlayerRecord>(identity);

        Assert.Null(await store.GetSnapshotAsync<PlayerRecord>(identity));

        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM entities WHERE source_id = '9' AND provider_key = 'test';";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task RejectsUnknownEntityIdentityBeforeWriting()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);
        var identity = new EntityIdentity(EntityKind.Unknown, "1", "test");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveSnapshotAsync(new PlayerRecord(identity), "Invalid"));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "FMAtlas.SnapshotTests", Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
