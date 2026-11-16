using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Infrastructure.Persistence;

/// <summary>
/// Persists typed domain snapshots as versioned JSON while preserving the
/// original SourcedValue provenance on each field. The normalised field_values
/// projection is populated by a later indexing service.
/// </summary>
public sealed class SqliteEntityRecordSnapshotStore : IEntityRecordSnapshotStore
{
    private const int SnapshotSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _connectionString;

    public SqliteEntityRecordSnapshotStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("A database file path is required.", nameof(databasePath));

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        _connectionString = builder.ToString();
    }

    public async Task SaveSnapshotAsync<TRecord>(
        TRecord record,
        string? displayName,
        CancellationToken cancellationToken = default)
        where TRecord : class, IEntityRecord
    {
        ArgumentNullException.ThrowIfNull(record);
        var identity = record.Identity;
        ValidateIdentity(identity);

        var recordType = GetRecordType<TRecord>();
        var payload = JsonSerializer.Serialize(record, JsonOptions);
        var savedAt = DateTimeOffset.UtcNow.ToString("O");

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "PRAGMA foreign_keys = ON;", cancellationToken)
            .ConfigureAwait(false);

        await using var transaction = connection.BeginTransaction();

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO entities(entity_kind, source_id, provider_key, display_name, imported_at_utc)
            VALUES ($kind, $sourceId, $provider, $displayName, $savedAt)
            ON CONFLICT(entity_kind, provider_key, source_id)
            DO UPDATE SET
                display_name = COALESCE(excluded.display_name, entities.display_name),
                imported_at_utc = excluded.imported_at_utc;
            """,
            cancellationToken,
            ("$kind", identity.Kind.ToString()),
            ("$sourceId", identity.SourceId),
            ("$provider", identity.ProviderKey),
            ("$displayName", (object?)displayName ?? DBNull.Value),
            ("$savedAt", savedAt))
            .ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO entity_record_snapshots(
                entity_kind, source_id, provider_key, record_type,
                snapshot_schema_version, payload_json, saved_at_utc)
            VALUES ($kind, $sourceId, $provider, $recordType, $schemaVersion, $payload, $savedAt)
            ON CONFLICT(entity_kind, provider_key, source_id, record_type)
            DO UPDATE SET
                snapshot_schema_version = excluded.snapshot_schema_version,
                payload_json = excluded.payload_json,
                saved_at_utc = excluded.saved_at_utc;
            """,
            cancellationToken,
            ("$kind", identity.Kind.ToString()),
            ("$sourceId", identity.SourceId),
            ("$provider", identity.ProviderKey),
            ("$recordType", recordType),
            ("$schemaVersion", SnapshotSchemaVersion),
            ("$payload", payload),
            ("$savedAt", savedAt))
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TRecord?> GetSnapshotAsync<TRecord>(
        EntityIdentity identity,
        CancellationToken cancellationToken = default)
        where TRecord : class, IEntityRecord
    {
        ValidateIdentity(identity);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT payload_json, snapshot_schema_version
            FROM entity_record_snapshots
            WHERE entity_kind = $kind
              AND source_id = $sourceId
              AND provider_key = $provider
              AND record_type = $recordType;
            """;
        command.Parameters.AddWithValue("$kind", identity.Kind.ToString());
        command.Parameters.AddWithValue("$sourceId", identity.SourceId);
        command.Parameters.AddWithValue("$provider", identity.ProviderKey);
        command.Parameters.AddWithValue("$recordType", GetRecordType<TRecord>());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        var schemaVersion = reader.GetInt32(1);
        if (schemaVersion != SnapshotSchemaVersion)
            throw new InvalidDataException(
                $"Snapshot schema {schemaVersion} is not supported by this application ({SnapshotSchemaVersion}).");

        var payload = reader.GetString(0);
        return JsonSerializer.Deserialize<TRecord>(payload, JsonOptions);
    }

    public async Task DeleteSnapshotAsync<TRecord>(
        EntityIdentity identity,
        CancellationToken cancellationToken = default)
        where TRecord : class, IEntityRecord
    {
        ValidateIdentity(identity);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM entity_record_snapshots
            WHERE entity_kind = $kind
              AND source_id = $sourceId
              AND provider_key = $provider
              AND record_type = $recordType;
            """;
        command.Parameters.AddWithValue("$kind", identity.Kind.ToString());
        command.Parameters.AddWithValue("$sourceId", identity.SourceId);
        command.Parameters.AddWithValue("$provider", identity.ProviderKey);
        command.Parameters.AddWithValue("$recordType", GetRecordType<TRecord>());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateIdentity(EntityIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!identity.IsUsable)
            throw new ArgumentException("A valid provider-scoped entity identity is required.", nameof(identity));
    }

    private static string GetRecordType<TRecord>() =>
        typeof(TRecord).FullName ?? typeof(TRecord).Name;

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
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
