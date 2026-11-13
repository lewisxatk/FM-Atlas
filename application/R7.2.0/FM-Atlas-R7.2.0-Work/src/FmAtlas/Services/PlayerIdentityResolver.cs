using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>
/// Safe, deliberately conservative identity handling for the scouting workspace.
/// Provider-specific IDs are not interchangeable: LIVE and FMSAVE remain distinct
/// until a future, verified crosswalk explicitly links them.
/// </summary>
public static class PlayerIdentityResolver
{
    public static IReadOnlyList<PlayerRecord> DeduplicateExactSourceIds(IEnumerable<PlayerRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var indexed = records.Select((record, index) => new
        {
            Record = record ?? throw new ArgumentException("A player record cannot be null.", nameof(records)),
            Index = index,
            SourceKey = string.IsNullOrWhiteSpace(record.SourceId)
                ? $"UNRESOLVED:{index:D10}"
                : record.SourceId.Trim()
        });

        return indexed
            .GroupBy(x => x.SourceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => Completeness(x.Record))
                .ThenBy(x => x.Index)
                .First().Record)
            .OrderBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int Completeness(PlayerRecord player) =>
        (player.Age.HasValue ? 1 : 0)
        + (player.DateOfBirth.HasValue ? 1 : 0)
        + (!string.IsNullOrWhiteSpace(player.Nation) ? 1 : 0)
        + (!string.IsNullOrWhiteSpace(player.Position) ? 1 : 0)
        + (player.CurrentAbility.HasValue ? 1 : 0)
        + (player.PotentialAbility.HasValue ? 1 : 0)
        + (player.Value.HasValue ? 1 : 0)
        + (!string.IsNullOrWhiteSpace(player.League) ? 1 : 0)
        + (!string.IsNullOrWhiteSpace(player.PreferredFoot) ? 1 : 0);
}
