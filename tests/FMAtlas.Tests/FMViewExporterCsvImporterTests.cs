using Microsoft.Data.Sqlite;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Search;
using FMAtlas.Infrastructure.Import;
using FMAtlas.Infrastructure.Persistence;
using Xunit;

namespace FMAtlas.Tests;

public sealed class FMViewExporterCsvImporterTests
{
    [Fact]
    public async Task ImportsPlayerIdentityAttributesAndDynamicColumnsFromEnglishCsv()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(databasePath).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(databasePath);
        var csvPath = Path.Combine(temp.Root, "squad.csv");
        await File.WriteAllTextAsync(csvPath,
            "Player,Unique ID,Age,Nation,Club,Position,Preferred Foot,Height,Current Ability,Potential Ability,Dribbling,Consistency,Professionalism,Left Foot,Right Foot,Scouting Notes\n" +
            "\"Smith, Jane\",101,19,England,Arsenal,MC,Right,180,122,165,15,14,17,14,10,\"High potential, versatile\"\n");

        var result = await new FMViewExporterCsvImporter(store).ImportAsync(csvPath);

        Assert.Equal(1, result.RowsRead);
        Assert.Equal(1, result.ImportedPlayers);
        Assert.Equal(0, result.SkippedRows);
        Assert.Equal(1, result.DynamicFieldsIndexed);

        var identity = new EntityIdentity(EntityKind.Player, "101", result.ProviderKey);
        var player = await store.GetSnapshotAsync<PlayerRecord>(identity);

        Assert.NotNull(player);
        Assert.Equal("Smith, Jane", player.FullName?.Value);
        Assert.Equal("England", player.Nationality?.Value);
        Assert.Equal("Arsenal", player.ClubName?.Value);
        Assert.Equal(122, player.CurrentAbility?.Value);
        Assert.Equal(165, player.PotentialAbility?.Value);
        Assert.Equal(15, player.Attributes["dribbling"].Value);
        Assert.Equal(14, player.HiddenAttributes["consistency"].Value);
        Assert.Equal(17, player.Personality["professionalism"].Value);
        Assert.Equal(14, player.FootRatings["left_foot"].Value);
        Assert.Equal(FieldEvidenceStatus.Mapped, player.Attributes["dribbling"].Provenance.EvidenceStatus);
        Assert.Equal(DataSourceKind.GameExport, player.Attributes["dribbling"].Provenance.Source);
        Assert.Equal("High potential, versatile", player.SearchTextFields["export_scouting_notes"].Value);

        var quickResults = await new SqliteEntitySearchProvider(databasePath).QuickSearchAsync("Smith");
        Assert.Single(quickResults);
        Assert.Equal("101", quickResults[0].Identity.SourceId);

        var provider = new SqliteAdvancedSearchProvider(databasePath);
        var indexed = await provider.GetIndexedFieldsAsync(SearchEntityKind.Player);
        Assert.Contains(indexed, field => field.Key == "export_scouting_notes" && field.CanExecute);

        var query = new AdvancedSearchQuery(
            SearchEntityKind.Player,
            SearchFilterLogic.All,
            [new SearchFilterCondition("export_scouting_notes", SearchFilterOperator.Contains, TextValue: "versatile")],
            IndexedFields: indexed);
        var advancedResults = await provider.ExecuteAsync(query);

        Assert.Single(advancedResults);
        Assert.Equal("101", advancedResults[0].Identity.SourceId);
    }

    [Fact]
    public async Task SupportsSemicolonDelimitedGermanDecimalCommaCsv()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(databasePath).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(databasePath);
        var csvPath = Path.Combine(temp.Root, "german-style.csv");
        await File.WriteAllTextAsync(csvPath,
            "Player;Unique ID;Age;Height\n" +
            "\"Müller, Jana\";700;22;181,5 cm\n");

        var result = await new FMViewExporterCsvImporter(store).ImportAsync(csvPath);
        var player = await store.GetSnapshotAsync<PlayerRecord>(
            new EntityIdentity(EntityKind.Player, "700", result.ProviderKey));

        Assert.Equal(1, result.ImportedPlayers);
        Assert.Equal(181.5m, player?.HeightCentimetres?.Value);
        Assert.Equal("Müller, Jana", player?.FullName?.Value);
    }

    [Fact]
    public async Task DuplicateIdsAreSkippedAndReimportingSameFileIsIdempotent()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(databasePath).InitializeAsync();
        var store = new SqliteEntityRecordSnapshotStore(databasePath);
        var csvPath = Path.Combine(temp.Root, "duplicates.csv");
        await File.WriteAllTextAsync(csvPath,
            "Player,Unique ID,Age\n" +
            "First Player,77,20\n" +
            "Duplicate Player,77,21\n");

        var importer = new FMViewExporterCsvImporter(store);
        var first = await importer.ImportAsync(csvPath);
        var second = await importer.ImportAsync(csvPath);

        Assert.Equal(first.ProviderKey, second.ProviderKey);
        Assert.Equal(1, first.ImportedPlayers);
        Assert.Equal(1, first.DuplicateIds);
        Assert.Equal(1, second.ImportedPlayers);

        var results = await new SqliteEntitySearchProvider(databasePath).QuickSearchAsync("Player");
        Assert.Single(results);
        Assert.Equal("First Player", results[0].DisplayName);
    }

    [Fact]
    public async Task RejectsFilesWithoutAStableUniqueIdColumnBeforeImporting()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(databasePath).InitializeAsync();
        var csvPath = Path.Combine(temp.Root, "no-id.csv");
        await File.WriteAllTextAsync(csvPath, "Player,Age\nA Player,20\n");

        var importer = new FMViewExporterCsvImporter(new SqliteEntityRecordSnapshotStore(databasePath));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(csvPath));
        Assert.Contains("Unique ID", exception.Message);
    }

    [Fact]
    public async Task RejectsMalformedQuotedCsv()
    {
        using var temp = new TemporaryDirectory();
        var databasePath = Path.Combine(temp.Root, "atlas.db");
        await new AtlasDatabase(databasePath).InitializeAsync();
        var csvPath = Path.Combine(temp.Root, "malformed.csv");
        await File.WriteAllTextAsync(csvPath, "Player,Unique ID\n\"Unclosed Player,101\n");

        var importer = new FMViewExporterCsvImporter(new SqliteEntityRecordSnapshotStore(databasePath));

        await Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(csvPath));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "FMAtlas.CsvImportTests", Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
