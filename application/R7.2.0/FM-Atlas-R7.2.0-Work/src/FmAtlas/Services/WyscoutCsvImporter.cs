using System.Globalization;
using System.Text;

namespace FmAtlas.Services;

/// <summary>
/// Imports user-owned Wyscout player-stat CSV exports. It does not scrape Wyscout or bypass
/// subscription access. Headers are retained as metric keys and source attribution is explicit.
/// </summary>
public static class WyscoutCsvImporter
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Player"] = new[] { "player", "player name", "name" },
        ["PlayerId"] = new[] { "player id", "player_id", "wyscout id", "id" },
        ["Team"] = new[] { "team", "club", "team name" },
        ["Competition"] = new[] { "competition", "league", "tournament" },
        ["Season"] = new[] { "season", "year" },
        ["Position"] = new[] { "position", "role" },
        ["Minutes"] = new[] { "minutes played", "minutes", "playing time" },
        ["Age"] = new[] { "age" },
        ["Value"] = new[] { "market value", "value", "transfer value" }
    };

    public static async Task<IReadOnlyList<ScoutingPlayerSeason>> ImportAsync(
        string path, CancellationToken cancellationToken = default)
    {
        var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8, cancellationToken);
        if (lines.Length < 2) throw new InvalidDataException("The Wyscout CSV must contain a header and at least one player row.");
        var delimiter = lines[0].Count(c => c == ';') > lines[0].Count(c => c == ',') ? ';' : ',';
        var headers = Parse(lines[0], delimiter).Select(x => x.Trim().TrimStart('\uFEFF')).ToArray();
        string? Find(string[] row, string key)
        {
            var aliases = Aliases[key];
            for (var i = 0; i < headers.Length && i < row.Length; i++)
                if (aliases.Contains(headers[i].Trim(), StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(row[i]))
                    return row[i].Trim();
            return null;
        }
        int? Index(string key)
        {
            var aliases = Aliases[key];
            for (var i = 0; i < headers.Length; i++)
                if (aliases.Contains(headers[i].Trim(), StringComparer.OrdinalIgnoreCase)) return i;
            return null;
        }
        var playerIndex = Index("Player");
        var minutesIndex = Index("Minutes");
        if (playerIndex is null || minutesIndex is null)
            throw new InvalidDataException("Wyscout CSV needs Player and Minutes played columns. Export player-level season statistics, not match/team totals.");

        var metricColumns = headers.Select((h, i) => (Header: h.Trim(), Index: i))
            .Where(x => !string.IsNullOrWhiteSpace(x.Header) &&
                !Aliases.Values.SelectMany(v => v).Contains(x.Header, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var result = new List<ScoutingPlayerSeason>();
        for (var line = 1; line < lines.Length; line++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(lines[line])) continue;
            var row = Parse(lines[line], delimiter);
            if (playerIndex.Value >= row.Length || string.IsNullOrWhiteSpace(row[playerIndex.Value])) continue;
            var name = Find(row, "Player")!;
            var id = Find(row, "PlayerId");
            var minutes = ParseNumber(Find(row, "Minutes"));
            if (!minutes.HasValue || minutes.Value <= 0) continue;
            var metrics = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in metricColumns)
            {
                if (col.Index >= row.Length) continue;
                var value = ParseNumber(row[col.Index]);
                if (value.HasValue) metrics[MetricKey(col.Header)] = value.Value;
            }
            metrics["minutes_played"] = minutes.Value;
            if (ParseNumber(Find(row, "Age")) is { } age) metrics["age"] = age;
            if (ParseNumber(Find(row, "Value")) is { } value) metrics["market_value"] = value;
            var season = Find(row, "Season") ?? "Unspecified season";
            result.Add(new ScoutingPlayerSeason(
                "WY:" + (id ?? name) + ":" + Find(row, "Team") + ":" + Find(row, "Competition") + ":" + season, name,
                Find(row, "Team"), Find(row, "Competition"), season,
                PositionGroup(Find(row, "Position")), minutes, metrics,
                "Wyscout user CSV", "User export CSV", false));
        }
        if (result.Count == 0) throw new InvalidDataException("No valid player-season rows found. Check that names and minutes are populated.");
        return result;
    }

    private static string MetricKey(string header)
    {
        var key = new string(header.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        while (key.Contains("__", StringComparison.Ordinal)) key = key.Replace("__", "_", StringComparison.Ordinal);
        return key.Trim('_');
    }
    private static double? ParseNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim().Replace("%", "").Replace("€", "").Replace("£", "").Replace("$", "")
            .Replace(" ", "").Replace("\u00A0", "");
        var multiplier = 1d;
        if (cleaned.EndsWith("bn", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_000_000_000d; cleaned = cleaned[..^2]; }
        else if (cleaned.EndsWith("m", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_000_000d; cleaned = cleaned[..^1]; }
        else if (cleaned.EndsWith("k", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_000d; cleaned = cleaned[..^1]; }
        if (cleaned.Contains('.') && cleaned.Contains(',')) cleaned = cleaned.Replace(",", "");
        else if (cleaned.Contains(','))
        {
            var commaParts = cleaned.Split(',');
            cleaned = commaParts.Length > 2 || (commaParts.Length == 2 && commaParts[1].Length == 3 && commaParts[0].Length <= 3)
                ? cleaned.Replace(",", "") : cleaned.Replace(",", ".");
        }
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)
            ? n * multiplier : null;
    }
    private static string? PositionGroup(string? position)
    {
        if (string.IsNullOrWhiteSpace(position)) return null;
        var p = position.ToLowerInvariant();
        if (p.Contains("goalkeeper") || p == "gk") return "Goalkeeper";
        if (p.Contains("def") || p.Contains("back") || p is "cb" or "lb" or "rb") return "Defender";
        if (p.Contains("mid") || p is "dm" or "cm" or "am") return "Midfielder";
        if (p.Contains("wing") || p.Contains("forward") || p.Contains("striker") || p == "fw") return "Attacker";
        return null;
    }
    private static string[] Parse(string line, char delimiter)
    {
        var fields = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == delimiter && !quoted) { fields.Add(value.ToString()); value.Clear(); }
            else value.Append(c);
        }
        fields.Add(value.ToString());
        return fields.ToArray();
    }
}
