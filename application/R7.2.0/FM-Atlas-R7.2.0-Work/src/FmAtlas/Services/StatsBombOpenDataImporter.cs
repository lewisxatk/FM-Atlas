using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FmAtlas.Services;

public sealed record EventImportSummary(
    int MatchesImported, int EventsRead, int PlayersAggregated,
    string Provider, string DatasetVersion, IReadOnlyList<string> Warnings);

/// <summary>
/// Imports the public StatsBomb Open Data folder layout (competitions, matches, lineups,
/// events). Event definitions are based on provider event types and fields; no commercial
/// Wyscout data is fetched or bundled.
/// </summary>
public static class StatsBombOpenDataImporter
{
    private sealed class Aggregate
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public string? Team { get; set; }
        public string? Position { get; set; }
        public string? Competition { get; set; }
        public string? Season { get; set; }
        public double Minutes { get; set; }
        public Dictionary<string, double> M { get; } = new(StringComparer.OrdinalIgnoreCase);
        public void Add(string key, double value = 1) => M[key] = M.GetValueOrDefault(key) + value;
    }

    public static async Task<(IReadOnlyList<ScoutingPlayerSeason> Players, EventImportSummary Summary)> ImportAsync(
        string selectedPath, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        var root = File.Exists(selectedPath) ? Path.GetDirectoryName(Path.GetFullPath(selectedPath))! : Path.GetFullPath(selectedPath);
        var competitionsPath = Path.Combine(root, "competitions.json");
        if (!File.Exists(competitionsPath))
            throw new InvalidDataException("Select the StatsBomb Open Data folder or its competitions.json file. Expected competitions.json, matches/, events/ and lineups/.");

        using var competitionsDoc = JsonDocument.Parse(await File.ReadAllTextAsync(competitionsPath, cancellationToken));
        var competitionNames = new Dictionary<(int Comp, int Season), (string Name, string Season)>();
        foreach (var c in competitionsDoc.RootElement.EnumerateArray())
        {
            var compId = Int(c, "competition_id");
            var seasonId = Int(c, "season_id");
            if (compId is null || seasonId is null) continue;
            competitionNames[(compId.Value, seasonId.Value)] =
                (Str(c, "competition_name") ?? "Competition " + compId, Str(c, "season_name") ?? "Season " + seasonId);
        }

        var matchesRoot = Path.Combine(root, "matches");
        var eventsRoot = Path.Combine(root, "events");
        var lineupsRoot = Path.Combine(root, "lineups");
        if (!Directory.Exists(matchesRoot) || !Directory.Exists(eventsRoot) || !Directory.Exists(lineupsRoot))
            throw new InvalidDataException("StatsBomb dataset is incomplete. Required directories: matches/, events/ and lineups/.");

        var totals = new Dictionary<string, Aggregate>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var matchesImported = 0;
        var eventsRead = 0;
        var matchFiles = Directory.GetFiles(matchesRoot, "*.json", SearchOption.AllDirectories);
        foreach (var matchFile in matchFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var matchesDoc = JsonDocument.Parse(await File.ReadAllTextAsync(matchFile, cancellationToken));
            foreach (var match in matchesDoc.RootElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matchId = Str(match, "match_id");
                if (string.IsNullOrWhiteSpace(matchId)) continue;
                var compId = Int(match, "competition", "competition_id");
                var seasonId = Int(match, "season", "season_id");
                var competition = compId.HasValue && seasonId.HasValue && competitionNames.TryGetValue((compId.Value, seasonId.Value), out var cn) ? cn.Name : "Unknown competition";
                var season = compId.HasValue && seasonId.HasValue && competitionNames.TryGetValue((compId.Value, seasonId.Value), out cn) ? cn.Season : "Unknown season";
                var eventsFile = Path.Combine(eventsRoot, matchId + ".json");
                var lineupsFile = Path.Combine(lineupsRoot, matchId + ".json");
                if (!File.Exists(eventsFile) || !File.Exists(lineupsFile))
                {
                    warnings.Add($"Match {matchId}: missing events or lineups file; skipped.");
                    continue;
                }

                var matchPlayers = new Dictionary<string, Aggregate>(StringComparer.Ordinal);
                using var lineupsDoc = JsonDocument.Parse(await File.ReadAllTextAsync(lineupsFile, cancellationToken));
                foreach (var team in lineupsDoc.RootElement.EnumerateArray())
                {
                    var teamName = Str(team, "team_name") ?? "Unknown team";
                    if (!team.TryGetProperty("lineup", out var lineup) || lineup.ValueKind != JsonValueKind.Array) continue;
                    foreach (var p in lineup.EnumerateArray())
                    {
                        var id = Str(p, "player_id"); var name = Str(p, "player_name");
                        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
                        var agg = Get(matchPlayers, id, name, teamName);
                        if (p.TryGetProperty("positions", out var positions) && positions.ValueKind == JsonValueKind.Array)
                        {
                            double minutes = 0;
                            foreach (var pos in positions.EnumerateArray())
                            {
                                var from = Minute(Str(pos, "from"));
                                var to = Minute(Str(pos, "to")) ?? 90;
                                if (from.HasValue && to >= from.Value) minutes += to.Value - from.Value;
                                agg.Position ??= Str(pos, "position");
                            }
                            agg.Minutes = Math.Max(agg.Minutes, minutes);
                        }
                    }
                }

                using var eventsDoc = JsonDocument.Parse(await File.ReadAllTextAsync(eventsFile, cancellationToken));
                foreach (var ev in eventsDoc.RootElement.EnumerateArray())
                {
                    eventsRead++;
                    var playerId = Str(ev, "player", "id");
                    var playerName = Str(ev, "player", "name");
                    if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(playerName)) continue;
                    var teamName = Str(ev, "team", "name");
                    var a = Get(matchPlayers, playerId, playerName, teamName);
                    var type = Str(ev, "type", "name") ?? "";
                    var minute = Number(ev, "minute") ?? 0;
                    if (type.Equals("Substitution", StringComparison.OrdinalIgnoreCase))
                    {
                        var offId = Str(ev, "player", "id");
                        var replacement = Str(ev, "substitution", "replacement", "id");
                        if (replacement is not null && Str(ev, "substitution", "replacement", "name") is { } replacementName)
                            Get(matchPlayers, replacement, replacementName, teamName);
                        continue;
                    }

                    switch (type)
                    {
                        case "Pass":
                            a.Add("passes_attempted");
                            var pass = Prop(ev, "pass");
                            var completed = !pass.TryGetProperty("outcome", out var outcome) || outcome.ValueKind == JsonValueKind.Null;
                            if (completed) a.Add("passes_completed");
                            if (Bool(pass, "shot_assist")) a.Add("key_passes");
                            if (Bool(pass, "goal_assist")) a.Add("assists");
                            if (pass.TryGetProperty("length", out var len) && len.ValueKind == JsonValueKind.Number && len.GetDouble() >= 30) a.Add("long_passes");
                            break;
                        case "Shot":
                            a.Add("shots");
                            var shot = Prop(ev, "shot");
                            if (Str(shot, "outcome", "name") == "Goal") a.Add("goals");
                            if (Number(shot, "statsbomb_xg") is { } xg) a.Add("xg", xg);
                            if (Str(shot, "type", "name") == "Penalty") a.Add("penalty_shots");
                            break;
                        case "Carry": a.Add("carries"); break;
                        case "Dribble":
                            a.Add("dribbles_attempted");
                            if (Str(ev, "dribble", "outcome", "name") == "Complete") a.Add("dribbles_completed");
                            break;
                        case "Pressure": a.Add("pressures"); break;
                        case "Duel":
                            a.Add("duels");
                            if (Str(ev, "duel", "type", "name") == "Aerial Lost" || Str(ev, "duel", "type", "name") == "Aerial Won") a.Add("aerial_duels");
                            if (Str(ev, "duel", "type", "name") == "Aerial Won") a.Add("aerial_duels_won");
                            break;
                        case "Interception": a.Add("interceptions"); break;
                        case "Ball Recovery": a.Add("recoveries"); break;
                        case "Clearance": a.Add("clearances"); break;
                        case "Block": a.Add("blocks"); break;
                        case "Tackle": a.Add("tackles"); break;
                        case "Foul Committed": a.Add("fouls_committed"); break;
                        case "Foul Won": a.Add("fouls_won"); break;
                        case "Miscontrol": a.Add("miscontrols"); break;
                        case "Dispossessed": a.Add("dispossessions"); break;
                        case "Offside": a.Add("offsides"); break;
                        case "Bad Behaviour":
                            if (Str(ev, "bad_behaviour", "card", "name") == "Yellow Card") a.Add("yellow_cards");
                            if (Str(ev, "bad_behaviour", "card", "name") == "Red Card") a.Add("red_cards");
                            break;
                    }
                }

                foreach (var p in matchPlayers.Values)
                {
                    var aggregateKey = p.Id + "|" + competition + "|" + season;
                    if (!totals.TryGetValue(aggregateKey, out var total))
                    {
                        total = new Aggregate { Id = p.Id, Name = p.Name, Team = p.Team, Position = p.Position, Competition = competition, Season = season };
                        totals[aggregateKey] = total;
                    }
                    total.Team = p.Team ?? total.Team;
                    total.Position ??= p.Position;
                    total.Competition = competition;
                    total.Season = season;
                    total.Minutes += p.Minutes > 0 ? p.Minutes : 0;
                    foreach (var metric in p.M) total.Add(metric.Key, metric.Value);
                }
                matchesImported++;
                if (matchesImported % 25 == 0) progress?.Report($"Imported {matchesImported:N0} matches · {eventsRead:N0} events");
            }
        }

        var result = totals.Values.Select(p =>
        {
            var metrics = p.M.ToDictionary(x => x.Key, x => (double?)x.Value, StringComparer.OrdinalIgnoreCase);
            metrics["minutes_played"] = p.Minutes;
            return new ScoutingPlayerSeason("SB:" + p.Id + ":" + p.Competition + ":" + p.Season, p.Name, p.Team, p.Competition, p.Season ?? "Unknown season", PositionGroup(p.Position), p.Minutes,
                metrics, "StatsBomb Open Data", "open-data JSON", true);
        }).ToList();

        // Competition and season aggregates are tracked per dataset import. The first iteration
        // is deliberately match-pooled; multi-competition splits are added by the caller's filters.
        if (result.Count == 0) warnings.Add("No player event rows were found. Check that the selected dataset contains valid lineups and event JSON.");
        return (result, new EventImportSummary(matchesImported, eventsRead, result.Count, "StatsBomb Open Data", "Open Data JSON", warnings));
    }

    private static Aggregate Get(Dictionary<string, Aggregate> players, string id, string name, string? team)
    {
        if (!players.TryGetValue(id, out var p))
        {
            p = new Aggregate { Id = id, Name = name, Team = team };
            players[id] = p;
        }
        p.Team ??= team;
        return p;
    }

    private static string? PositionGroup(string? position)
    {
        if (string.IsNullOrWhiteSpace(position)) return null;
        var value = position.ToLowerInvariant();
        if (value.Contains("goalkeeper")) return "Goalkeeper";
        if (value.Contains("back") || value.Contains("defender")) return "Defender";
        if (value.Contains("midfield")) return "Midfielder";
        if (value.Contains("forward") || value.Contains("wing") || value.Contains("striker")) return "Attacker";
        return null;
    }

    private static JsonElement Prop(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) ? p : default;
    private static string? Str(JsonElement e, params string[] path)
    {
        foreach (var part in path) { if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(part, out e)) return null; }
        return e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.GetRawText(),
            _ => null
        };
    }
    private static int? Int(JsonElement e, params string[] path) => Number(e, path) is { } n ? (int)n : null;
    private static double? Number(JsonElement e, params string[] path)
    {
        foreach (var part in path) { if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(part, out e)) return null; }
        return e.ValueKind == JsonValueKind.Number ? e.GetDouble() : null;
    }
    private static bool Bool(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
    private static double? Minute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split(':');
        if (parts.Length >= 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return minutes + seconds / 60d;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct) ? direct : null;
    }
}
