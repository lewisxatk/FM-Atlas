using Microsoft.Data.Sqlite;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Infrastructure.Persistence;

/// <summary>
/// Read-only quick search over entity display names populated by the record
/// snapshot store. Search terms are parameterised and LIKE metacharacters are
/// escaped so user input is treated literally.
/// </summary>
public sealed class SqliteEntitySearchProvider : IEntitySearchProvider
{
    private readonly string _connectionString;

    public SqliteEntitySearchProvider(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("A database file path is required.", nameof(databasePath));

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        _connectionString = builder.ToString();
    }

    public async Task<IReadOnlyList<EntitySearchResult>> QuickSearchAsync(
        string searchText,
        IReadOnlyCollection<EntityKind>? entityKinds = null,
        int limit = 50,
        CancellationToken cancellationToken = default,
        bool includeWomenPlayers = false)
    {
        if (limit is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(limit), "Quick search limit must be between 1 and 200.");

        var hasSearchText = !string.IsNullOrWhiteSpace(searchText);
        var kinds = entityKinds?.Distinct().ToArray();
        // A blank global search remains empty; a blank entity-scoped search browses that directory.
        if (!hasSearchText && kinds is null)
            return Array.Empty<EntitySearchResult>();
        if (kinds is { Length: 0 })
            return Array.Empty<EntitySearchResult>();

        var kindFilter = kinds is null
            ? string.Empty
            : $" AND e.entity_kind IN ({string.Join(", ", kinds.Select((_, index) => $"$kind{index}"))})";
        var nameFilter = hasSearchText ? " AND e.display_name LIKE $pattern ESCAPE '\\'" : string.Empty;
        var womenFilter = includeWomenPlayers
            ? string.Empty
            : """
              AND (
                  e.entity_kind <> 'Player'
                  OR NOT EXISTS (
                      SELECT 1
                      FROM field_values AS gender
                      WHERE gender.entity_kind = e.entity_kind
                        AND gender.source_id = e.source_id
                        AND gender.provider_key = e.provider_key
                        AND gender.field_key = 'gender'
                        AND LOWER(TRIM(gender.text_value)) IN ('female', 'woman', 'women', 'f')
                  )
              )
              """;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT e.entity_kind, e.source_id, e.provider_key, e.display_name
            FROM entities AS e
            WHERE e.display_name IS NOT NULL
              {nameFilter} ESCAPE '\'
              {kindFilter}
              {womenFilter}
            ORDER BY e.display_name COLLATE NOCASE, e.entity_kind, e.provider_key, e.source_id
            LIMIT $limit;
            """;

        if (hasSearchText)
            command.Parameters.AddWithValue("$pattern", BuildLiteralContainsPattern(searchText.Trim()));
        command.Parameters.AddWithValue("$limit", limit);

        if (kinds is not null)
        {
            for (var index = 0; index < kinds.Length; index++)
                command.Parameters.AddWithValue($"$kind{index}", kinds[index].ToString());
        }

        var results = new List<EntitySearchResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!Enum.TryParse<EntityKind>(reader.GetString(0), ignoreCase: true, out var kind))
                continue;

            results.Add(new EntitySearchResult(
                new EntityIdentity(kind, reader.GetString(1), reader.GetString(2)),
                reader.GetString(3)));
        }

        return results.AsReadOnly();
    }

    private static string BuildLiteralContainsPattern(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return "%" + escaped + "%";
    }
}
