using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Infrastructure.Persistence;

/// <summary>
/// Persists typed domain snapshots as versioned JSON and refreshes the normalised
/// searchable field projection in the same transaction. Each projected field keeps
/// its own source and evidence metadata.
/// </summary>
public sealed class SqliteEntityRecordSnapshotStore : IEntityRecordSnapshotStore, IEntityRecordBatchSnapshotStore
{
    private const int SnapshotSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _connectionString;
    private readonly IEntityRecordProjector _projector;

    public SqliteEntityRecordSnapshotStore(
        string databasePath,
        IEntityRecordProjector? projector = null)
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
        _projector = projector ?? new FMAtlas.Application.Projection.EntityRecordProjector();
    }

    public async Task SaveSnapshotAsync<TRecord>(
        TRecord record,
        string? displayName,
        CancellationToken cancellationToken = default)
        where TRecord : class, IEntityRecord
    {
        ArgumentNullException.ThrowIfNull(record);

        var projection = _projector.Project(record);
        var identity = projection.Identity;
        ValidateIdentity(identity);

        var recordType = GetRecordType<TRecord>();
        var payload = JsonSerializer.Serialize(record, JsonOptions);
        var savedAt = DateTimeOffset.UtcNow.ToString("O");
        var effectiveDisplayName = displayName ?? projection.DisplayName;

        foreach (var field in projection.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key) || !field.HasValidValueShape())
                throw new InvalidOperationException($"Projected field '{field.Key}' has an invalid value shape.");
        }

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
            ("$displayName", (object?)effectiveDisplayName ?? DBNull.Value),
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

        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE FROM field_values
            WHERE entity_kind = $kind AND source_id = $sourceId AND provider_key = $provider;
            """,
            cancellationToken,
            ("$kind", identity.Kind.ToString()),
            ("$sourceId", identity.SourceId),
            ("$provider", identity.ProviderKey))
            .ConfigureAwait(false);

        foreach (var field in projection.Fields)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO field_values(
                    entity_kind, source_id, provider_key, field_key, value_kind,
                    text_value, integer_value, decimal_value, date_value,
                    source_kind, evidence_status, source_version,
                    target_build_fingerprint, observed_at_utc)
                VALUES (
                    $kind, $sourceId, $provider, $fieldKey, $valueKind,
                    $textValue, $integerValue, $decimalValue, $dateValue,
                    $sourceKind, $evidenceStatus, $sourceVersion,
                    $targetBuildFingerprint, $observedAtUtc);
                """,
                cancellationToken,
                ("$kind", identity.Kind.ToString()),
                ("$sourceId", identity.SourceId),
                ("$provider", identity.ProviderKey),
                ("$fieldKey", field.Key),
                ("$valueKind", field.ValueKind.ToString()),
                ("$textValue", (object?)field.TextValue ?? DBNull.Value),
                ("$integerValue", (object?)(field.IntegerValue ?? (field.BooleanValue is bool flag ? (flag ? 1L : 0L) : null)) ?? DBNull.Value),
                ("$decimalValue", (object?)(field.DecimalValue is decimal amount ? Convert.ToDouble(amount, CultureInfo.InvariantCulture) : null) ?? DBNull.Value),
                ("$dateValue", (object?)field.DateValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value),
                ("$sourceKind", field.Provenance.Source.ToString()),
                ("$evidenceStatus", field.Provenance.EvidenceStatus.ToString()),
                ("$sourceVersion", (object?)field.Provenance.SourceVersion ?? DBNull.Value),
                ("$targetBuildFingerprint", (object?)field.Provenance.TargetBuildFingerprint ?? DBNull.Value),
                ("$observedAtUtc", (object?)field.Provenance.ObservedAtUtc?.ToString("O") ?? DBNull.Value))
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveSnapshotsAsync(
        IReadOnlyCollection<IEntityRecord> records,
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null,
        bool replaceProvider = false)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
            return;

        const int RecordBatchSize = 100;
        const int FieldBatchSize = 60; // 60 × 14 parameters stays below SQLite's historic 999-variable limit.

        string? replacementProviderKey = null;
        if (replaceProvider)
        {
            var first = records.FirstOrDefault()
                ?? throw new ArgumentException("The batch cannot contain a null record.", nameof(records));
            ValidateIdentity(first.Identity);
            replacementProviderKey = first.Identity.ProviderKey;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction();

        // A provider replacement is atomic: the old provider rows are removed inside
        // the same transaction that writes the new snapshot and index.
        if (replaceProvider)
        {
            await using var deleteProviderCommand = connection.CreateCommand();
            deleteProviderCommand.Transaction = transaction;
            deleteProviderCommand.CommandText = "DELETE FROM entities WHERE provider_key = $provider;";
            deleteProviderCommand.Parameters.AddWithValue("$provider", replacementProviderKey!);
            await deleteProviderCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string EntityInsertSql = """
            INSERT INTO entities(entity_kind, source_id, provider_key, display_name, imported_at_utc)
            """;
        const string EntityConflictSql = """
            ON CONFLICT(entity_kind, provider_key, source_id)
            DO UPDATE SET
                display_name = COALESCE(excluded.display_name, entities.display_name),
                imported_at_utc = excluded.imported_at_utc;
            """;
        const string SnapshotInsertSql = """
            INSERT INTO entity_record_snapshots(
                entity_kind, source_id, provider_key, record_type,
                snapshot_schema_version, payload_json, saved_at_utc)
            """;
        const string SnapshotConflictSql = """
            ON CONFLICT(entity_kind, provider_key, source_id, record_type)
            DO UPDATE SET
                snapshot_schema_version = excluded.snapshot_schema_version,
                payload_json = excluded.payload_json,
                saved_at_utc = excluded.saved_at_utc;
            """;
        const string FieldInsertSql = """
            INSERT INTO field_values(
                entity_kind, source_id, provider_key, field_key, value_kind,
                text_value, integer_value, decimal_value, date_value,
                source_kind, evidence_status, source_version,
                target_build_fingerprint, observed_at_utc)
            """;
        const string DeleteFieldsSql = """
            DELETE FROM field_values
            WHERE entity_kind = $kind AND source_id = $sourceId AND provider_key = $provider;
            """;

        using var entityBatchCommand = new BulkInsertCommand(
            connection, transaction, EntityInsertSql, EntityConflictSql, RecordBatchSize, 5);
        using var snapshotBatchCommand = new BulkInsertCommand(
            connection, transaction, SnapshotInsertSql, SnapshotConflictSql, RecordBatchSize, 7);
        using var fieldBatchCommand = new BulkInsertCommand(
            connection, transaction, FieldInsertSql, string.Empty, FieldBatchSize, 14);

        await using var deleteFieldsCommand = connection.CreateCommand();
        deleteFieldsCommand.Transaction = transaction;
        deleteFieldsCommand.CommandText = DeleteFieldsSql;
        deleteFieldsCommand.Parameters.Add("$kind", SqliteType.Text);
        deleteFieldsCommand.Parameters.Add("$sourceId", SqliteType.Text);
        deleteFieldsCommand.Parameters.Add("$provider", SqliteType.Text);
        deleteFieldsCommand.Prepare();

        var pendingFieldRows = new List<object?[]>(FieldBatchSize);
        using var enumerator = records.GetEnumerator();
        var processed = 0;

        while (processed < records.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batchSize = Math.Min(RecordBatchSize, records.Count - processed);
            var prepared = new List<(EntityRecordProjection Projection, string RecordType, string Payload, string SavedAt)>(batchSize);

            for (var index = 0; index < batchSize; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!enumerator.MoveNext())
                    throw new InvalidOperationException("The record collection changed while it was being imported.");

                var record = enumerator.Current
                    ?? throw new ArgumentException("The batch cannot contain a null record.", nameof(records));
                var projection = _projector.Project(record);
                ValidateIdentity(projection.Identity);

                if (replaceProvider &&
                    !string.Equals(projection.Identity.ProviderKey, replacementProviderKey, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "A provider-replacement batch must contain records from exactly one provider.",
                        nameof(records));
                }

                foreach (var field in projection.Fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Key) || !field.HasValidValueShape())
                        throw new InvalidOperationException($"Projected field '{field.Key}' has an invalid value shape.");
                }

                prepared.Add((
                    projection,
                    record.GetType().FullName ?? record.GetType().Name,
                    JsonSerializer.Serialize(record, record.GetType(), JsonOptions),
                    DateTimeOffset.UtcNow.ToString("O")));
            }

            var entityRows = new List<object?[]>(prepared.Count);
            var snapshotRows = new List<object?[]>(prepared.Count);
            foreach (var record in prepared)
            {
                var identity = record.Projection.Identity;
                entityRows.Add([
                    identity.Kind.ToString(),
                    identity.SourceId,
                    identity.ProviderKey,
                    (object?)record.Projection.DisplayName ?? DBNull.Value,
                    record.SavedAt
                ]);
                snapshotRows.Add([
                    identity.Kind.ToString(),
                    identity.SourceId,
                    identity.ProviderKey,
                    record.RecordType,
                    SnapshotSchemaVersion,
                    record.Payload,
                    record.SavedAt
                ]);

                // Normal upserts replace a record's previous projection. A provider
                // replacement has already cleared every field row once inside this transaction.
                if (!replaceProvider)
                {
                    deleteFieldsCommand.Parameters["$kind"].Value = identity.Kind.ToString();
                    deleteFieldsCommand.Parameters["$sourceId"].Value = identity.SourceId;
                    deleteFieldsCommand.Parameters["$provider"].Value = identity.ProviderKey;
                    await deleteFieldsCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

            }

            if (prepared.Count == RecordBatchSize)
            {
                entityBatchCommand.Execute(entityRows);
                snapshotBatchCommand.Execute(snapshotRows);
            }
            else
            {
                using var partialEntityCommand = new BulkInsertCommand(
                    connection, transaction, EntityInsertSql, EntityConflictSql, entityRows.Count, 5);
                using var partialSnapshotCommand = new BulkInsertCommand(
                    connection, transaction, SnapshotInsertSql, SnapshotConflictSql, snapshotRows.Count, 7);
                partialEntityCommand.Execute(entityRows);
                partialSnapshotCommand.Execute(snapshotRows);
            }

            // Entity rows must exist before field_values are inserted because the
            // field projection is protected by a foreign key to entities.
            foreach (var record in prepared)
            {
                var identity = record.Projection.Identity;
                foreach (var field in record.Projection.Fields)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pendingFieldRows.Add(BuildFieldValueRow(identity, field));
                    if (pendingFieldRows.Count == FieldBatchSize)
                    {
                        fieldBatchCommand.Execute(pendingFieldRows);
                        pendingFieldRows.Clear();
                    }
                }
            }

            processed += prepared.Count;
            // Report completed records, not the old two-stage 0–20 / 20–100 sub-scale.
            // Keep 100% for after the final field batch and transaction commit.
            progress?.Report(Math.Min(99, Math.Clamp((int)(processed * 100L / records.Count), 0, 99)));
        }

        if (pendingFieldRows.Count > 0)
        {
            using var partialFieldCommand = new BulkInsertCommand(
                connection, transaction, FieldInsertSql, string.Empty, pendingFieldRows.Count, 14);
            partialFieldCommand.Execute(pendingFieldRows);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        progress?.Report(100);
    }

    private static object?[] BuildFieldValueRow(EntityIdentity identity, EntityFieldProjection field) =>
    [
        identity.Kind.ToString(),
        identity.SourceId,
        identity.ProviderKey,
        field.Key,
        field.ValueKind.ToString(),
        (object?)field.TextValue ?? DBNull.Value,
        (object?)(field.IntegerValue ?? (field.BooleanValue is bool flag ? (flag ? 1L : 0L) : null)) ?? DBNull.Value,
        (object?)(field.DecimalValue is decimal amount ? Convert.ToDouble(amount, CultureInfo.InvariantCulture) : null) ?? DBNull.Value,
        (object?)field.DateValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value,
        field.Provenance.Source.ToString(),
        field.Provenance.EvidenceStatus.ToString(),
        (object?)field.Provenance.SourceVersion ?? DBNull.Value,
        (object?)field.Provenance.TargetBuildFingerprint ?? DBNull.Value,
        (object?)field.Provenance.ObservedAtUtc?.ToString("O") ?? DBNull.Value
    ];

    public async Task DeleteProviderAsync(
        string providerKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("A provider key is required.", nameof(providerKey));

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction();
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM entities WHERE provider_key = $provider;",
            cancellationToken,
            ("$provider", providerKey)).ConfigureAwait(false);
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
        await ExecuteAsync(connection, null, "PRAGMA foreign_keys = ON;", cancellationToken)
            .ConfigureAwait(false);

        await using var transaction = connection.BeginTransaction();
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = """
                DELETE FROM entity_record_snapshots
                WHERE entity_kind = $kind
                  AND source_id = $sourceId
                  AND provider_key = $provider
                  AND record_type = $recordType;
                """;
            deleteCommand.Parameters.AddWithValue("$kind", identity.Kind.ToString());
            deleteCommand.Parameters.AddWithValue("$sourceId", identity.SourceId);
            deleteCommand.Parameters.AddWithValue("$provider", identity.ProviderKey);
            deleteCommand.Parameters.AddWithValue("$recordType", GetRecordType<TRecord>());
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        long remainingSnapshots;
        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.Transaction = transaction;
            countCommand.CommandText = """
                SELECT COUNT(*) FROM entity_record_snapshots
                WHERE entity_kind = $kind AND source_id = $sourceId AND provider_key = $provider;
                """;
            countCommand.Parameters.AddWithValue("$kind", identity.Kind.ToString());
            countCommand.Parameters.AddWithValue("$sourceId", identity.SourceId);
            countCommand.Parameters.AddWithValue("$provider", identity.ProviderKey);
            var value = await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            remainingSnapshots = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (remainingSnapshots == 0)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                DELETE FROM field_values
                WHERE entity_kind = $kind AND source_id = $sourceId AND provider_key = $provider;
                """,
                cancellationToken,
                ("$kind", identity.Kind.ToString()),
                ("$sourceId", identity.SourceId),
                ("$provider", identity.ProviderKey))
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateIdentity(EntityIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!identity.IsUsable)
            throw new ArgumentException("A valid provider-scoped entity identity is required.", nameof(identity));
    }

    private static string GetRecordType<TRecord>() =>
        typeof(TRecord).FullName ?? typeof(TRecord).Name;

    /// <summary>
    /// Reuses one prepared multi-row INSERT statement. Batches of 60 field rows use
    /// 840 bind parameters, safely below SQLite's historical 999 parameter limit.
    /// </summary>
    private sealed class BulkInsertCommand : IDisposable
    {
        private readonly SqliteCommand _command;
        private readonly SqliteParameter[,] _parameters;
        private readonly int _rowCount;
        private readonly int _columnCount;

        public BulkInsertCommand(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string insertSql,
            string conflictSql,
            int rowCount,
            int columnCount)
        {
            if (rowCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(rowCount));
            if (columnCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(columnCount));

            _rowCount = rowCount;
            _columnCount = columnCount;
            _parameters = new SqliteParameter[rowCount, columnCount];
            _command = connection.CreateCommand();
            _command.Transaction = transaction;

            var valueRows = new string[rowCount];
            for (var row = 0; row < rowCount; row++)
            {
                var parameterNames = new string[columnCount];
                for (var column = 0; column < columnCount; column++)
                {
                    var parameter = new SqliteParameter
                    {
                        ParameterName = $"$p{row}_{column}"
                    };
                    _parameters[row, column] = parameter;
                    _command.Parameters.Add(parameter);
                    parameterNames[column] = parameter.ParameterName;
                }

                valueRows[row] = $"({string.Join(", ", parameterNames)})";
            }

            _command.CommandText = $"{insertSql} VALUES {string.Join(", ", valueRows)} {conflictSql}";
            _command.Prepare();
        }

        public void Execute(IReadOnlyList<object?[]> rows)
        {
            if (rows.Count != _rowCount)
                throw new ArgumentException($"This prepared command expects exactly {_rowCount} rows.", nameof(rows));

            for (var row = 0; row < _rowCount; row++)
            {
                if (rows[row].Length != _columnCount)
                    throw new ArgumentException($"Bulk row {row} must contain exactly {_columnCount} values.", nameof(rows));

                for (var column = 0; column < _columnCount; column++)
                    _parameters[row, column].Value = rows[row][column] ?? DBNull.Value;
            }

            _command.ExecuteNonQuery();
        }

        public void Dispose() => _command.Dispose();
    }

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
