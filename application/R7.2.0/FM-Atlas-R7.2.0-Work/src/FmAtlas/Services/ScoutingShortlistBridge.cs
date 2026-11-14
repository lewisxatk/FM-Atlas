using System.Globalization;
using FmAtlas.Data;
using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>Moves explicitly selected scouting-profile matches into the existing Atlas shortlist pipeline.</summary>
public static class ScoutingShortlistBridge
{
    public static async Task<int> AddMatchesAsync(
        ScoutDatabase database,
        IEnumerable<ScoutingProfileMatch> matches,
        string shortlistName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(matches);
        if (string.IsNullOrWhiteSpace(shortlistName))
            throw new ArgumentException("Choose a named shortlist.", nameof(shortlistName));

        var selected = matches
            .Where(x => !string.IsNullOrWhiteSpace(x.Player.SourceId))
            .GroupBy(x => x.Player.SourceId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
        var added = 0;
        foreach (var match in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = match.Player;
            var record = new PlayerRecord
            {
                SourceId = source.SourceId,
                Name = source.PlayerName,
                Club = source.Club,
                Position = source.Position,
                League = source.League,
                Age = source.Age,
                Value = source.Value
            };
            var notes = BuildEvidenceNote(match);
            await database.AddToShortlistAsync(record, shortlistName.Trim(), notes).ConfigureAwait(false);
            added++;
        }
        return added;
    }

    public static string BuildEvidenceNote(ScoutingProfileMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        var evidence = match.Evidence;
        var updated = evidence.SourceUpdatedUtc?.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
            ?? "timestamp unavailable";
        var missing = evidence.MissingFields.Count == 0
            ? "none reported"
            : string.Join(", ", evidence.MissingFields.Take(8));
        return string.Join(" | ",
            "Scouting profile match " + match.MatchScore.ToString("0.#", CultureInfo.InvariantCulture) + "%",
            "Source: " + evidence.SourceName,
            "Updated: " + updated,
            "Coverage: " + evidence.Coverage.ToString("P0", CultureInfo.InvariantCulture),
            "Sample: " + evidence.SampleSize.ToString(CultureInfo.InvariantCulture),
            "Minutes: " + evidence.Minutes.ToString("N0", CultureInfo.InvariantCulture),
            "Missing: " + missing);
    }
}
