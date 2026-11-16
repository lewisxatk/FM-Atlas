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
    public async Task AdvancedSearchExcludesExplicitlyFemalePlayersUnlessPreferenceIsEnabled()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        foreach (var (id, gender) in new[]
        {
            ("male", "Male"),
            ("female", "Female"),
            ("unknown", (string?)null)
        })
        {
            var player = new PlayerRecord(new EntityIdentity(EntityKind.Player, id, "test"))
            {
                FullName = new SourcedValue<string>($"{id} Player", Evidence),
                Gender = gender is null ? null : new SourcedValue<string>(gender, Evidence)
            };
            await store.SaveSnapshotAsync(player, $"{id} Player");
        }

        var provider = new SqliteAdvancedSearchProvider(path);
        var defaultQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("name", SearchFilterOperator.Contains, TextValue: "Player")]);
        var inclusiveQuery = defaultQuery with { IncludeWomenPlayers = true };

        var defaultResults = await provider.ExecuteAsync(defaultQuery);
        var inclusiveResults = await provider.ExecuteAsync(inclusiveQuery);

        Assert.Equal(2, defaultResults.Count);
        Assert.DoesNotContain(defaultResults, result => result.Identity.SourceId == "female");
        Assert.Equal(3, inclusiveResults.Count);
        Assert.Contains(inclusiveResults, result => result.Identity.SourceId == "female");
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
    public async Task ContractStatusUsesTheCurrentInGameDate()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);
        var today = new DateOnly(2027, 1, 1);
        const string providerKey = "live-contract-test";

        async Task AddPlayer(string id, DateOnly expiry)
        {
            var identity = new EntityIdentity(EntityKind.Player, id, providerKey);
            await store.SaveSnapshotAsync(new PlayerRecord(identity)
            {
                FullName = new SourcedValue<string>($"Player {id}", Evidence),
                ContractExpires = new SourcedValue<DateOnly>(expiry, Evidence),
                SearchDateFields = new Dictionary<string, SourcedValue<DateOnly>>
                {
                    ["current_game_date"] = new(today, Evidence)
                }
            }, $"Player {id}");
        }

        await AddPlayer("expired", new DateOnly(2026, 12, 31));
        await AddPlayer("three-months", new DateOnly(2027, 3, 31));
        await AddPlayer("six-months", new DateOnly(2027, 5, 1));

        var provider = new SqliteAdvancedSearchProvider(path);
        var expired = await provider.ExecuteAsync(new AdvancedSearchQuery(
            SearchEntityKind.Player, SearchFilterLogic.All,
            [new SearchFilterCondition("contract_status", SearchFilterOperator.Equals, TextValue: "Expired")]));
        var threeMonths = await provider.ExecuteAsync(new AdvancedSearchQuery(
            SearchEntityKind.Player, SearchFilterLogic.All,
            [new SearchFilterCondition("contract_status", SearchFilterOperator.Equals, TextValue: "Expires in 3 months")]));

        Assert.Equal("expired", Assert.Single(expired).Identity.SourceId);
        Assert.Equal("three-months", Assert.Single(threeMonths).Identity.SourceId);
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
            [new SearchFilterCondition("field_that_does_not_exist", SearchFilterOperator.Contains, TextValue: "teammate")]);

        Assert.False(unsupported.CanExecute);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new SqliteAdvancedSearchProvider(path).ExecuteAsync(unsupported));

        var invalidInteger = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("age", SearchFilterOperator.Range, Minimum: 18.5m)]);

        Assert.False(AdvancedSearchQueryValidator.Validate(invalidInteger).IsValid);
    }

    [Fact]
    public async Task BooleanNewgenSearchSeparatesGeneratedOriginalAndUnknownRecords()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var generated = new EntityIdentity(EntityKind.Player, "generated", "test");
        var original = new EntityIdentity(EntityKind.Player, "original", "test");
        var unknown = new EntityIdentity(EntityKind.Player, "unknown", "test");

        await store.SaveSnapshotAsync(new PlayerRecord(generated)
        {
            FullName = new SourcedValue<string>("Generated Player", Evidence),
            IsNewgen = new SourcedValue<bool>(true, Evidence)
        }, "Generated Player");
        await store.SaveSnapshotAsync(new PlayerRecord(original)
        {
            FullName = new SourcedValue<string>("Original Player", Evidence),
            IsNewgen = new SourcedValue<bool>(false, Evidence)
        }, "Original Player");
        await store.SaveSnapshotAsync(new PlayerRecord(unknown)
        {
            FullName = new SourcedValue<string>("Unclassified Player", Evidence)
        }, "Unclassified Player");

        var provider = new SqliteAdvancedSearchProvider(path);
        var generatedQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("is_newgen", SearchFilterOperator.IsTrue)]);
        var originalQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("is_newgen", SearchFilterOperator.IsFalse)]);
        var unknownQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("is_newgen", SearchFilterOperator.IsUnknown)]);

        Assert.Equal("generated", Assert.Single(await provider.ExecuteAsync(generatedQuery)).Identity.SourceId);
        Assert.Equal("original", Assert.Single(await provider.ExecuteAsync(originalQuery)).Identity.SourceId);
        Assert.Equal("unknown", Assert.Single(await provider.ExecuteAsync(unknownQuery)).Identity.SourceId);
    }

    [Fact]
    public async Task AttributeSearchSupportsMaximumOnlyBoundary()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        foreach (var (id, pace) in new[] { ("slow", 8), ("limit", 12), ("fast", 17) })
        {
            var identity = new EntityIdentity(EntityKind.Player, id, "test");
            await store.SaveSnapshotAsync(new PlayerRecord(identity)
            {
                FullName = new SourcedValue<string>($"{id} player", Evidence),
                Attributes = new Dictionary<string, SourcedValue<int>>
                {
                    ["dribbling"] = new(pace, Evidence)
                }
            }, $"{id} player");
        }

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("attribute_dribbling", SearchFilterOperator.Range, Maximum: 12)]);

        var results = await new SqliteAdvancedSearchProvider(path).ExecuteAsync(query);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.Identity.SourceId == "slow");
        Assert.Contains(results, x => x.Identity.SourceId == "limit");
        Assert.DoesNotContain(results, x => x.Identity.SourceId == "fast");
    }

    [Fact]
    public async Task PositionAndClubFacilitySearchUseIndividualIndexedFields()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var playerId = new EntityIdentity(EntityKind.Player, "right-back", "test");
        await store.SaveSnapshotAsync(new PlayerRecord(playerId)
        {
            FullName = new SourcedValue<string>("Right Back", Evidence),
            PositionRatings = new Dictionary<string, SourcedValue<int>>
            {
                ["dr"] = new(18, Evidence)
            }
        }, "Right Back");

        var clubId = new EntityIdentity(EntityKind.Club, "club", "test");
        await store.SaveSnapshotAsync(new FMAtlas.Domain.Clubs.ClubRecord(clubId)
        {
            Name = new SourcedValue<string>("Facilities FC", Evidence),
            Facilities = new Dictionary<string, SourcedValue<int>>
            {
                ["training"] = new(17, Evidence),
                ["youth"] = new(11, Evidence)
            }
        }, "Facilities FC");

        var provider = new SqliteAdvancedSearchProvider(path);
        var positionQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("position_dr", SearchFilterOperator.Range, Minimum: 18)]);
        var clubQuery = new AdvancedSearchQuery(
            SearchEntityKind.Club,
            SearchFilterLogic.All,
            [new SearchFilterCondition("club_training_facilities", SearchFilterOperator.Range, Minimum: 16)]);

        Assert.Equal("right-back", Assert.Single(await provider.ExecuteAsync(positionQuery)).Identity.SourceId);
        Assert.Equal("club", Assert.Single(await provider.ExecuteAsync(clubQuery)).Identity.SourceId);
    }

    [Fact]
    public async Task PositionFiltersCanRequireAllOrAnySelectedPositions()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var goalkeeper = new EntityIdentity(EntityKind.Player, "goalkeeper", "test");
        var centreBack = new EntityIdentity(EntityKind.Player, "centre-back", "test");
        await store.SaveSnapshotAsync(new PlayerRecord(goalkeeper)
        {
            FullName = new SourcedValue<string>("Keeper", Evidence),
            PositionRatings = new Dictionary<string, SourcedValue<int>> { ["gk"] = new(18, Evidence) }
        }, "Keeper");
        await store.SaveSnapshotAsync(new PlayerRecord(centreBack)
        {
            FullName = new SourcedValue<string>("Defender", Evidence),
            PositionRatings = new Dictionary<string, SourcedValue<int>> { ["dc"] = new(18, Evidence) }
        }, "Defender");

        var conditions = new[]
        {
            new SearchFilterCondition("position_gk", SearchFilterOperator.Range, Minimum: 15),
            new SearchFilterCondition("position_dc", SearchFilterOperator.Range, Minimum: 15)
        };
        var allQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player, SearchFilterLogic.All, conditions, SearchFilterLogic.All);
        var anyQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player, SearchFilterLogic.All, conditions, SearchFilterLogic.Any);
        var provider = new SqliteAdvancedSearchProvider(path);

        Assert.Empty(await provider.ExecuteAsync(allQuery));
        var anyResults = await provider.ExecuteAsync(anyQuery);
        Assert.Equal(2, anyResults.Count);
    }

    [Fact]
    public async Task AbilityDifferenceSearchCalculatesPotentialMinusCurrentAbility()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var highGap = new EntityIdentity(EntityKind.Player, "high-gap", "test");
        var lowGap = new EntityIdentity(EntityKind.Player, "low-gap", "test");
        await store.SaveSnapshotAsync(new PlayerRecord(highGap)
        {
            FullName = new SourcedValue<string>("High Gap", Evidence),
            CurrentAbility = new SourcedValue<int>(120, Evidence),
            PotentialAbility = new SourcedValue<int>(160, Evidence)
        }, "High Gap");
        await store.SaveSnapshotAsync(new PlayerRecord(lowGap)
        {
            FullName = new SourcedValue<string>("Low Gap", Evidence),
            CurrentAbility = new SourcedValue<int>(140, Evidence),
            PotentialAbility = new SourcedValue<int>(155, Evidence)
        }, "Low Gap");

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("ability_difference", SearchFilterOperator.Range, Minimum: 30)]);

        Assert.Equal("high-gap", Assert.Single(await new SqliteAdvancedSearchProvider(path).ExecuteAsync(query)).Identity.SourceId);
    }

    [Fact]
    public async Task StaffRolePreferencesCanBeSearchedWithMinimumOnly()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var scoutId = new EntityIdentity(EntityKind.Staff, "scout", "test");
        var physioId = new EntityIdentity(EntityKind.Staff, "physio", "test");
        await store.SaveSnapshotAsync(new FMAtlas.Domain.Staff.StaffRecord(scoutId)
        {
            FullName = new SourcedValue<string>("Scout", Evidence),
            RolePreferences = new Dictionary<string, SourcedValue<int>>
            {
                ["scout"] = new(17, Evidence)
            }
        }, "Scout");
        await store.SaveSnapshotAsync(new FMAtlas.Domain.Staff.StaffRecord(physioId)
        {
            FullName = new SourcedValue<string>("Physio", Evidence),
            RolePreferences = new Dictionary<string, SourcedValue<int>>
            {
                ["physio"] = new(11, Evidence)
            }
        }, "Physio");

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Coach,
            SearchFilterLogic.All,
            [new SearchFilterCondition("role_preference_scout", SearchFilterOperator.Range, Minimum: 12)]);

        Assert.Equal("scout", Assert.Single(await new SqliteAdvancedSearchProvider(path).ExecuteAsync(query)).Identity.SourceId);
    }

    [Fact]
    public async Task RelationshipSearchMatchesRelationKindAndRelatedEntityInEitherDirection()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var snapshots = new SqliteEntityRecordSnapshotStore(path);

        var parent = new EntityIdentity(EntityKind.Player, "parent", "test");
        var child = new EntityIdentity(EntityKind.Player, "child", "test");
        await snapshots.SaveSnapshotAsync(new PlayerRecord(parent)
        {
            FullName = new SourcedValue<string>("Alex Parent", Evidence)
        }, "Alex Parent");
        await snapshots.SaveSnapshotAsync(new PlayerRecord(child)
        {
            FullName = new SourcedValue<string>("Sam Child", Evidence)
        }, "Sam Child");

        var relationships = new SqliteEntityRelationshipStore(path);
        await relationships.SaveRelationshipAsync(child, parent, "father", Evidence);

        var provider = new SqliteAdvancedSearchProvider(path);
        var relationKindQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("relations", SearchFilterOperator.Contains, TextValue: "father")]);
        var relatedNameQuery = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("relations", SearchFilterOperator.Contains, TextValue: "Alex")]);

        var byKind = await provider.ExecuteAsync(relationKindQuery);
        var byRelatedName = await provider.ExecuteAsync(relatedNameQuery);

        Assert.Equal(2, byKind.Count);
        Assert.Equal("child", Assert.Single(byRelatedName).Identity.SourceId);
        Assert.Contains(byKind, result => result.Identity.SourceId == "parent");
        Assert.Contains(byKind, result => result.Identity.SourceId == "child");
    }

    [Fact]
    public async Task EveryFieldMarkedReadyHasAnExecutableSqliteMapping()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var provider = new SqliteAdvancedSearchProvider(path);

        foreach (var entity in Enum.GetValues<SearchEntityKind>())
        {
            var fields = AdvancedSearchCatalog.Categories
                .SelectMany(category =>
                    AdvancedSearchCatalog.GetFields(entity, category.Kind)
                        .Concat(AdvancedSearchCatalog.GetAttributeFields(entity, category.Kind)))
                .Where(field => field.CanExecute)
                .GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();

            foreach (var field in fields)
            {
                var condition = field.ValueKind switch
                {
                    SearchFieldValueKind.IntegerRange or SearchFieldValueKind.DecimalRange =>
                        new SearchFilterCondition(field.Key, SearchFilterOperator.Range,
                            Minimum: field.MinimumAllowed ?? 0m),
                    SearchFieldValueKind.PositionRating =>
                        new SearchFilterCondition(field.Key, SearchFilterOperator.Range,
                            Minimum: field.MinimumAllowed ?? 0m, TextValue: "dr"),
                    SearchFieldValueKind.Boolean =>
                        new SearchFilterCondition(field.Key, SearchFilterOperator.IsTrue),
                    SearchFieldValueKind.DateRange =>
                        new SearchFilterCondition(field.Key, SearchFilterOperator.Range,
                            DateMinimum: new DateOnly(2026, 1, 1)),
                    SearchFieldValueKind.Enum =>
                        new SearchFilterCondition(field.Key, SearchFilterOperator.Equals,
                            TextValue: field.Key.Equals("contract_status", StringComparison.OrdinalIgnoreCase) ? "Expired" : "test"),
                    SearchFieldValueKind.TraitSelector =>
                        new SearchFilterCondition(field.Key, SearchFilterOperator.HasTrait, TextValue: "test"),
                    SearchFieldValueKind.Text or SearchFieldValueKind.EntityLookup =>
                        new SearchFilterCondition(field.Key, SearchFilterOperator.Contains, TextValue: "test"),
                    _ => throw new InvalidOperationException($"No test condition for {field.ValueKind}.")
                };

                var query = new AdvancedSearchQuery(
                    entity,
                    SearchFilterLogic.All,
                    [condition]);

                await provider.ExecuteAsync(query);
            }
        }
    }

    [Fact]
    public async Task PositionAllLogicRemainsIndependentWhenOverallLogicIsAny()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var hybrid = new EntityIdentity(EntityKind.Player, "hybrid", "test");
        var goalkeeperOnly = new EntityIdentity(EntityKind.Player, "goalkeeper-only", "test");
        var highAbilityOnly = new EntityIdentity(EntityKind.Player, "high-ability-only", "test");

        await store.SaveSnapshotAsync(new PlayerRecord(hybrid)
        {
            FullName = new SourcedValue<string>("Hybrid Player", Evidence),
            CurrentAbility = new SourcedValue<int>(130, Evidence),
            PositionRatings = new Dictionary<string, SourcedValue<int>>
            {
                ["gk"] = new(18, Evidence),
                ["dc"] = new(17, Evidence)
            }
        }, "Hybrid Player");

        await store.SaveSnapshotAsync(new PlayerRecord(goalkeeperOnly)
        {
            FullName = new SourcedValue<string>("Goalkeeper Only", Evidence),
            CurrentAbility = new SourcedValue<int>(100, Evidence),
            PositionRatings = new Dictionary<string, SourcedValue<int>>
            {
                ["gk"] = new(18, Evidence)
            }
        }, "Goalkeeper Only");

        await store.SaveSnapshotAsync(new PlayerRecord(highAbilityOnly)
        {
            FullName = new SourcedValue<string>("High Ability Only", Evidence),
            CurrentAbility = new SourcedValue<int>(160, Evidence)
        }, "High Ability Only");

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.Any,
            [
                new SearchFilterCondition("position_gk", SearchFilterOperator.Range, Minimum: 15),
                new SearchFilterCondition("position_dc", SearchFilterOperator.Range, Minimum: 15),
                new SearchFilterCondition("current_ability", SearchFilterOperator.Range, Minimum: 150)
            ],
            PositionLogic: SearchFilterLogic.All);

        var results = await new SqliteAdvancedSearchProvider(path).ExecuteAsync(query);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.Identity.SourceId == "hybrid");
        Assert.Contains(results, result => result.Identity.SourceId == "high-ability-only");
        Assert.DoesNotContain(results, result => result.Identity.SourceId == "goalkeeper-only");
    }

    [Fact]
    public async Task IndexedSeasonStatisticsAreDiscoveredAndSearchable()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(path).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(path);

        var high = new EntityIdentity(EntityKind.Player, "high-stat", "test");
        var low = new EntityIdentity(EntityKind.Player, "low-stat", "test");

        await store.SaveSnapshotAsync(new PlayerRecord(high)
        {
            FullName = new SourcedValue<string>("High Statistic", Evidence),
            SeasonStatistics = new Dictionary<string, SourcedValue<decimal>>
            {
                ["goals_per_90"] = new(0.82m, Evidence)
            }
        }, "High Statistic");

        await store.SaveSnapshotAsync(new PlayerRecord(low)
        {
            FullName = new SourcedValue<string>("Low Statistic", Evidence),
            SeasonStatistics = new Dictionary<string, SourcedValue<decimal>>
            {
                ["goals_per_90"] = new(0.21m, Evidence)
            }
        }, "Low Statistic");

        var provider = new SqliteAdvancedSearchProvider(path);
        var indexedFields = await provider.GetIndexedFieldsAsync(SearchEntityKind.Player);
        var goalsPer90 = Assert.Single(indexedFields, field => field.Key == "stat_goals_per_90");

        Assert.Equal(SearchFieldValueKind.DecimalRange, goalsPer90.ValueKind);
        Assert.Equal(AdvancedSearchCategoryKind.IndexedFields, goalsPer90.Category);
        Assert.Equal(FieldEvidenceStatus.Mapped, goalsPer90.EvidenceStatus);

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("stat_goals_per_90", SearchFilterOperator.Range, Minimum: 0.5m)],
            IndexedFields: indexedFields);

        var results = await provider.ExecuteAsync(query);

        Assert.Equal("high-stat", Assert.Single(results).Identity.SourceId);
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
