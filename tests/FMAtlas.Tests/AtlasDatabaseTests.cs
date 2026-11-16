using Microsoft.Data.Sqlite;
using FMAtlas.Infrastructure.Persistence;
using Xunit;

namespace FMAtlas.Tests;

public sealed class AtlasDatabaseTests
{
    [Fact]
    public async Task InitialisationCreatesSchemaVersionOneAndCoreTables()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "data", "atlas.db");
        var database = new AtlasDatabase(path);

        await database.InitializeAsync();

        Assert.Equal(1, await database.GetSchemaVersionAsync());
        Assert.True(File.Exists(path));

        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        var tables = await ReadNamesAsync(connection,
            "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;");

        Assert.Contains("entities", tables);
        Assert.Contains("field_values", tables);
        Assert.Contains("data_sources", tables);
        Assert.Contains("entity_relationships", tables);
        Assert.Contains("graphics_mappings", tables);
        Assert.Contains("atlas_schema_migrations", tables);
    }

    [Fact]
    public async Task InitialisationIsIdempotent()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        var database = new AtlasDatabase(path);

        await database.InitializeAsync();
        await database.InitializeAsync();

        Assert.Equal(1, await database.GetSchemaVersionAsync());

        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        var migrations = await ReadCountAsync(connection,
            "SELECT COUNT(*) FROM atlas_schema_migrations WHERE version = 1;");
        Assert.Equal(1, migrations);
    }

    [Fact]
    public async Task DatabaseEnablesForeignKeyEnforcementPerConnection()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Root, "atlas.db");
        var database = new AtlasDatabase(path);
        await database.InitializeAsync();

        await using var connection = new SqliteConnection($"Data Source={path};Foreign Keys=True");
        await connection.OpenAsync();
        var result = await ReadCountAsync(connection, "PRAGMA foreign_keys;");
        Assert.Equal(1, result);
    }

    private static async Task<List<string>> ReadNamesAsync(SqliteConnection connection, string sql)
    {
        var result = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));
        return result;
    }

    private static async Task<long> ReadCountAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "FMAtlas.DatabaseTests", Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
