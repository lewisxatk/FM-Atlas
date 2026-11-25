using Microsoft.Data.Sqlite;
using FMAtlas.Application.Abstractions;

namespace FMAtlas.Infrastructure.Persistence;

public sealed class AtlasDatabase : IAtlasDatabase
{
    public const int SchemaVersion = 2;

    private const string MigrationOneSql = """
        CREATE TABLE atlas_schema_migrations
        (
            version INTEGER PRIMARY KEY,
            applied_at_utc TEXT NOT NULL,
            description TEXT NOT NULL
        );

        CREATE TABLE data_sources
        (
            source_id TEXT PRIMARY KEY,
            source_kind TEXT NOT NULL,
            source_version TEXT,
            target_build_fingerprint TEXT,
            save_generation TEXT,
            imported_at_utc TEXT NOT NULL
        );

        CREATE TABLE entities
        (
            entity_kind TEXT NOT NULL CHECK (entity_kind IN ('Player','Staff','Club','Nation','Competition','Unknown')),
            source_id TEXT NOT NULL,
            provider_key TEXT NOT NULL,
            display_name TEXT,
            data_source_id TEXT,
            imported_at_utc TEXT NOT NULL,
            PRIMARY KEY (entity_kind, provider_key, source_id),
            FOREIGN KEY (data_source_id) REFERENCES data_sources(source_id) ON DELETE SET NULL
        );

        CREATE INDEX idx_entities_name ON entities(entity_kind, display_name COLLATE NOCASE);
        CREATE INDEX idx_entities_provider ON entities(provider_key, entity_kind);

        CREATE TABLE field_values
        (
            entity_kind TEXT NOT NULL,
            source_id TEXT NOT NULL,
            provider_key TEXT NOT NULL,
            field_key TEXT NOT NULL,
            value_kind TEXT NOT NULL CHECK (value_kind IN ('Text','Integer','Decimal','Date','Boolean')),
            text_value TEXT,
            integer_value INTEGER,
            decimal_value REAL,
            date_value TEXT,
            source_kind TEXT NOT NULL,
            evidence_status TEXT NOT NULL,
            source_version TEXT,
            target_build_fingerprint TEXT,
            observed_at_utc TEXT,
            PRIMARY KEY (entity_kind, provider_key, source_id, field_key),
            FOREIGN KEY (entity_kind, provider_key, source_id)
                REFERENCES entities(entity_kind, provider_key, source_id) ON DELETE CASCADE,
            CHECK (
                (CASE WHEN text_value IS NOT NULL THEN 1 ELSE 0 END) +
                (CASE WHEN integer_value IS NOT NULL THEN 1 ELSE 0 END) +
                (CASE WHEN decimal_value IS NOT NULL THEN 1 ELSE 0 END) +
                (CASE WHEN date_value IS NOT NULL THEN 1 ELSE 0 END) <= 1
            )
        );

        CREATE INDEX idx_field_values_key_integer ON field_values(field_key, integer_value);
        CREATE INDEX idx_field_values_key_decimal ON field_values(field_key, decimal_value);

        CREATE TABLE entity_relationships
        (
            from_entity_kind TEXT NOT NULL,
            from_source_id TEXT NOT NULL,
            from_provider_key TEXT NOT NULL,
            to_entity_kind TEXT NOT NULL,
            to_source_id TEXT NOT NULL,
            to_provider_key TEXT NOT NULL,
            relation_kind TEXT NOT NULL,
            source_kind TEXT NOT NULL,
            evidence_status TEXT NOT NULL,
            observed_at_utc TEXT,
            PRIMARY KEY (
                from_entity_kind, from_provider_key, from_source_id,
                to_entity_kind, to_provider_key, to_source_id, relation_kind
            ),
            FOREIGN KEY (from_entity_kind, from_provider_key, from_source_id)
                REFERENCES entities(entity_kind, provider_key, source_id) ON DELETE CASCADE,
            FOREIGN KEY (to_entity_kind, to_provider_key, to_source_id)
                REFERENCES entities(entity_kind, provider_key, source_id) ON DELETE CASCADE
        );

        CREATE INDEX idx_relationships_target ON entity_relationships(to_entity_kind, to_provider_key, to_source_id);

        CREATE TABLE graphics_mappings
        (
            asset_kind TEXT NOT NULL CHECK (asset_kind IN ('Portrait','ClubLogo','NationLogo')),
            game_id INTEGER NOT NULL CHECK (game_id > 0),
            image_path TEXT NOT NULL,
            config_path TEXT NOT NULL,
            graphics_root TEXT NOT NULL,
            indexed_at_utc TEXT NOT NULL,
            PRIMARY KEY (asset_kind, game_id)
        );

        INSERT INTO atlas_schema_migrations(version, applied_at_utc, description)
        VALUES (1, $appliedAt, 'Initial provenance-aware entity and graphics schema');
        """;

    private const string MigrationTwoSql = """
        CREATE TABLE entity_record_snapshots
        (
            entity_kind             TEXT NOT NULL,
            source_id               TEXT NOT NULL,
            provider_key            TEXT NOT NULL,
            record_type             TEXT NOT NULL,
            snapshot_schema_version INTEGER NOT NULL,
            payload_json            TEXT NOT NULL,
            saved_at_utc            TEXT NOT NULL,
            PRIMARY KEY (entity_kind, provider_key, source_id, record_type),
            FOREIGN KEY (entity_kind, provider_key, source_id)
                REFERENCES entities(entity_kind, provider_key, source_id) ON DELETE CASCADE
        );

        CREATE INDEX idx_record_snapshots_saved
            ON entity_record_snapshots(saved_at_utc);
        """;

    private readonly string _databasePath;
    private readonly string _connectionString;

    public AtlasDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("A database file path is required.", nameof(databasePath));

        _databasePath = Path.GetFullPath(databasePath);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        _connectionString = builder.ToString();
    }

    public int CurrentSchemaVersion => SchemaVersion;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, null, "PRAGMA foreign_keys = ON;", cancellationToken)
            .ConfigureAwait(false);
        await ExecuteAsync(connection, null, "PRAGMA journal_mode = WAL;", cancellationToken)
            .ConfigureAwait(false);

        await using var transaction = connection.BeginTransaction();
        var version = await ReadUserVersionAsync(connection, transaction, cancellationToken)
            .ConfigureAwait(false);

        if (version > CurrentSchemaVersion)
            throw new InvalidOperationException(
                $"The Atlas database schema version {version} is newer than this application supports ({CurrentSchemaVersion}).");

        if (version < 1)
        {
            await ExecuteAsync(
                connection, transaction, MigrationOneSql, cancellationToken,
                ("$appliedAt", DateTimeOffset.UtcNow.ToString("O")))
                .ConfigureAwait(false);

            await ExecuteAsync(connection, transaction, "PRAGMA user_version = 1;", cancellationToken)
                .ConfigureAwait(false);
            version = 1;
        }

        if (version < 2)
        {
            await ExecuteAsync(connection, transaction, MigrationTwoSql, cancellationToken)
                .ConfigureAwait(false);

            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO atlas_schema_migrations(version, applied_at_utc, description) VALUES (2, $appliedAt, $description);",
                cancellationToken,
                ("$appliedAt", DateTimeOffset.UtcNow.ToString("O")),
                ("$description", "Versioned JSON entity record snapshots"))
                .ConfigureAwait(false);

            await ExecuteAsync(connection, transaction, "PRAGMA user_version = 2;", cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadUserVersionAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    internal string ConnectionString => _connectionString;

    private static async Task<int> ReadUserVersionAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version;";
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
