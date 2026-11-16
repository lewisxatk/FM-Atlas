using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Search;
using FMAtlas.Infrastructure.Persistence;
using Xunit;

namespace FMAtlas.Tests;

public sealed class SqliteAdvancedSearchProviderTests
{
    private static readonly FieldProvenance Evidence =
        new(DataSourceKind.CareerSave, FieldEvidenceStatus.ReadVerified);

    [Fact]
    public async Task AllLogicCombinesAgeAndAttributeRanges()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var target = new EntityIdentity(EntityKind.Player, "target", "gamepass");
        var tooYoung = new EntityIdentity(EntityKind.Player, "young", "gamepass");
        var slow = new EntityIdentity(EntityKind.Player, "slow", "gamepass");

        await store.SaveSnapshotAsync(new PlayerRecord(target)
        {
            FullName = new SourcedValue<string>("Target Player", Evidence),
            Age = new SourcedValue<int>(20, Evidence),
            Attributes = new Dictionary<string, SourcedValue<int>> { ["pace"] = new(17, Evidence) }
        }, "Target Player");

        await store.SaveSnapshotAsync(new PlayerRecord(tooYoung)
        {
            FullName = new SourcedValue<string>("Young Fast Player", Evidence),
            Age = new SourcedValue<int>(17, Evidence),
            Attributes = new Dictionary<string, SourcedValue<int>> { ["pace"] = new(18, Evidence) }
        }, "Young Fast Player");

        await store.SaveSnapshotAsync(new PlayerRecord(slow)
        {
            FullName = new SourcedValue<string>("Older Slow Player", Evidence),
            Age = new SourcedValue<int>(21, Evidence),
            Attributes = new Dictionary<string, SourcedValue<int>> { ["pace"] = new(12, Evidence) }
        }, "Older Slow Player");

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [
                new SearchFilterCondition("age", SearchFilterOperator.Range, Minimum: 18, Maximum: 23),
                new SearchFilterCondition("attribute_pace", SearchFilterOperator.Range, Minimum: 15)
            ]);

        Assert.True(query.CanExecute);
        var results = await new SqliteAdvancedSearchProvider(path).ExecuteAsync(query);

        Assert.Single(results);
        Assert.Equal(target.SourceId, results[0].Identity.SourceId);
    }

    [Fact]
    public async Task AnyLogicReturnsRecordsMatchingEitherCondition()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var spanish = new EntityIdentity(EntityKind.Player, "spanish", "test");
        var highAbility = new EntityIdentity(EntityKind.Player, "ability", "test");
        await store.SaveSnapshotAsync(new PlayerRecord(spanish)
        {
            FullName = new SourcedValue<string>("Spanish Player", Evidence),
            Nationality = new SourcedValue<string>("Spain", Evidence)
        }, "Spanish Player");
        await store.SaveSnapshotAsync(new PlayerRecord(highAbility)
        {
            FullName = new SourcedValue<string>("High Ability Player", Evidence),
            CurrentAbility = new SourcedValue<int>(160, Evidence)
        }, "High Ability Player");

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.Any,
            [
                new SearchFilterCondition("nationality", SearchFilterOperator.Contains, TextValue: "Spain"),
                new SearchFilterCondition("current_ability", SearchFilterOperator.Range, Minimum: 150)
            ]);

        var results = await new SqliteAdvancedSearchProvider(path).ExecuteAsync(query);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task PreferredMoveFilterFindsPlayerWithTrait()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var playerId = new EntityIdentity(EntityKind.Player, "trait", "test");
        await store.SaveSnapshotAsync(new PlayerRecord(playerId)
        {
            FullName = new SourcedValue<string>("Tricky Player", Evidence),
            PreferredMoves = [new SourcedValue<string>("Tries Tricks", Evidence)]
        }, "Tricky Player");

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [
                new SearchFilterCondition("preferred_moves", SearchFilterOperator.HasTrait, TextValue: "Tries Tricks")
            ]);

        var results = await new SqliteAdvancedSearchProvider(path).ExecuteAsync(query);
        Assert.Single(results);
        Assert.Equal("trait", results[0].Identity.SourceId);
    }

    [Fact]
    public async Task PositionFamiliarityUsesNamedPositionFieldKey()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var identity = new EntityIdentity(EntityKind.Player, "position", "test");
        await store.SaveSnapshotAsync(new PlayerRecord(identity)
        {
            FullName = new SourcedValue<string>("Right Back", Evidence),
            PositionRatings = new Dictionary<string, SourcedValue<int>>
            {
                ["dr"] = new(20, Evidence),
                ["dc"] = new(10, Evidence)
            }
        }, "Right Back");

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("position_familiarity", SearchFilterOperator.Range, 18, 20, "dr")]);

        var results = await new SqliteAdvancedSearchProvider(path).ExecuteAsync(query);
        Assert.Single(results);
        Assert.Equal("position", results[0].Identity.SourceId);
    }

    [Fact]
    public async Task ContractExpirySupportsDateRanges()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var identity = new EntityIdentity(EntityKind.Player, "contract", "test");
        await store.SaveSnapshotAsync(new PlayerRecord(identity)
        {
            FullName = new SourcedValue<string>("Contract Player", Evidence),
            ContractExpires = new SourcedValue<DateOnly>(new DateOnly(2028, 6, 30), Evidence)
        }, "Contract Player");

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [
                new SearchFilterCondition(
                    "contract_expiry",
                    SearchFilterOperator.Range,
                    DateMinimum: new DateOnly(2028, 1, 1),
                    DateMaximum: new DateOnly(2028, 12, 31))
            ]);

        var results = await new SqliteAdvancedSearchProvider(path).ExecuteAsync(query);
        Assert.Single(results);
    }

    [Fact]
    public async Task UnsupportedFieldsAndNonIntegralAgeBoundariesAreRejected()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();

        var unsupported = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("relations", SearchFilterOperator.Contains, TextValue: "teammate")]);

        Assert.False(unsupported.CanExecute);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new SqliteAdvancedSearchProvider(path).ExecuteAsync(unsupported));

        var invalidInteger = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("age", SearchFilterOperator.Range, Minimum: 18.5m)]);

        Assert.False(AdvancedSearchQueryValidator.Validate(invalidInteger).IsValid);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "FMAtlas.AdvancedSearchTests", Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
