using Microsoft.Data.Sqlite;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Infrastructure.Persistence;

/// <summary>
/// Inserts and removes source-backed relationships in the normalized relationship
/// table. Both endpoint entities must already exist in the local entity index.
/// </summary>
public sealed class SqliteEntityRelationshipStore : IEntityRelationshipStore
{
    private readonly string _connectionString;

    public SqliteEntityRelationshipStore(string databasePath)
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

    public async Task SaveRelationshipAsync(
        EntityIdentity from,
        EntityIdentity to,
        string relationKind,
        FieldProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        ValidateEndpoint(from, nameof(from));
        ValidateEndpoint(to, nameof(to));
        ArgumentNullException.ThrowIfNull(provenance);

        var relation = NormalizeRelationKind(relationKind);
        var observedAt = provenance.ObservedAtUtc?.ToString("O");

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (var foreignKeys = connection.CreateCommand())
        {
            foreignKeys.CommandText = "PRAGMA foreign_keys = ON;";
            await foreignKeys.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO entity_relationships(
                from_entity_kind, from_source_id, from_provider_key,
                to_entity_kind, to_source_id, to_provider_key,
                relation_kind, source_kind, evidence_status, observed_at_utc)
            VALUES (
                $fromKind, $fromId, $fromProvider,
                $toKind, $toId, $toProvider,
                $relationKind, $sourceKind, $evidenceStatus, $observedAt)
            ON CONFLICT(
                from_entity_kind, from_provider_key, from_source_id,
                to_entity_kind, to_provider_key, to_source_id, relation_kind)
            DO UPDATE SET
                source_kind = excluded.source_kind,
                evidence_status = excluded.evidence_status,
                observed_at_utc = excluded.observed_at_utc;
            """;
        AddEndpoint(command, "$from", from);
        AddEndpoint(command, "$to", to);
        command.Parameters.AddWithValue("$relationKind", relation);
        command.Parameters.AddWithValue("$sourceKind", provenance.Source.ToString());
        command.Parameters.AddWithValue("$evidenceStatus", provenance.EvidenceStatus.ToString());
        command.Parameters.AddWithValue("$observedAt", (object?)observedAt ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRelationshipAsync(
        EntityIdentity from,
        EntityIdentity to,
        string relationKind,
        CancellationToken cancellationToken = default)
    {
        ValidateEndpoint(from, nameof(from));
        ValidateEndpoint(to, nameof(to));
        var relation = NormalizeRelationKind(relationKind);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM entity_relationships
            WHERE from_entity_kind = $fromKind
              AND from_source_id = $fromId
              AND from_provider_key = $fromProvider
              AND to_entity_kind = $toKind
              AND to_source_id = $toId
              AND to_provider_key = $toProvider
              AND relation_kind = $relationKind;
            """;
        AddEndpoint(command, "$from", from);
        AddEndpoint(command, "$to", to);
        command.Parameters.AddWithValue("$relationKind", relation);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateEndpoint(EntityIdentity identity, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(identity, parameterName);
        if (!identity.IsUsable)
            throw new ArgumentException("A valid provider-scoped entity identity is required.", parameterName);
    }

    private static string NormalizeRelationKind(string relationKind)
    {
        if (string.IsNullOrWhiteSpace(relationKind))
            throw new ArgumentException("A relationship kind is required.", nameof(relationKind));

        var value = relationKind.Trim();
        if (value.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(relationKind), "Relationship kinds are limited to 128 characters.");

        return value;
    }

    private static void AddEndpoint(SqliteCommand command, string prefix, EntityIdentity identity)
    {
        command.Parameters.AddWithValue(prefix + "Kind", identity.Kind.ToString());
        command.Parameters.AddWithValue(prefix + "Id", identity.SourceId);
        command.Parameters.AddWithValue(prefix + "Provider", identity.ProviderKey);
    }
}
