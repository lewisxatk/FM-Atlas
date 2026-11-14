using System.Globalization;
using System.IO;
using System.Text.Json;

namespace FmAtlas.Services;

public sealed record UnifiedSoccerAction(
    string MatchId, string Competition, string Season, int Period, int Minute, int Second,
    string? PossessionId, string? TeamId, string Team, string? PlayerId, string Player,
    string EventType, string ActionType, bool Successful, double? StartX, double? StartY,
    double? EndX, double? EndY, bool IsShot, bool IsGoal, double? Xg,
    string Provider, string ProviderEventId, int Sequence,
    string? RecipientPlayerId = null, string? RecipientPlayer = null, string? BodyPart = null);

public sealed record ExpectedThreatModel(
    int Columns, int Rows, double[] Values, string ModelName, string TrainingSource,
    int TrainingMatches, int TrainingActions, DateTimeOffset TrainedAtUtc);

public sealed record ValuedSoccerAction(UnifiedSoccerAction Action, double? XtAdded);
public sealed record PossessionChain(
    string MatchId, string Competition, string Season, string PossessionId, string Team,
    int StartMinute, int EndMinute, int ActionCount, int Passes, int Carries, int Shots,
    int Goals, double Xg, double XtAdded, string? FinalPlayer, string Outcome);
public sealed record PassingNetworkEdge(string MatchId, string Team, string FromPlayer, string ToPlayer, int Passes, int CompletedPasses, double? AverageStartX, double? AverageStartY, double? AverageEndX, double? AverageEndY);
public sealed record PlayerEventAnalytics(
    string PlayerId, string Player, string Team, int? Minutes,
    int Passes, int CompletedPasses, int Carries, int Shots, int Goals,
    double Xg, double XtAdded, int Pressures, int Tackles, int Interceptions,
    int FinalThirdEntries, int ProgressivePasses, int ProgressiveCarries,
    double? PassCompletionPct, double? XtPer90);

/// <summary>
/// Provider-neutral event-action model inspired by SPADL. The initial adapter is deliberately
/// StatsBomb-specific; all coordinates are normalised to a 105x68 attacking-direction pitch.
/// xT is fitted from the imported sample, not copied from an external model.
/// </summary>
public static class ScoutingActionAnalyticsService
{
    public const int PitchLength = 105;
    public const int PitchWidth = 68;
    private const int Columns = 16;
    private const int Rows = 12;

    public static async Task<IReadOnlyList<UnifiedSoccerAction>> ImportStatsBombActionsAsync(
        string selectedPath, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        var root = File.Exists(selectedPath) ? Path.GetDirectoryName(Path.GetFullPath(selectedPath))! : Path.GetFullPath(selectedPath);
        var matchesRoot = Path.Combine(root, "matches");
        var eventsRoot = Path.Combine(root, "events");
        if (!Directory.Exists(matchesRoot) || !Directory.Exists(eventsRoot))
            throw new InvalidDataException("The StatsBomb action importer needs the matches/ and events/ folders.");

        var competitions = new Dictionary<(int, int), (string, string)>();
        var competitionPath = Path.Combine(root, "competitions.json");
        if (File.Exists(competitionPath))
        {
            using var compDoc = JsonDocument.Parse(await File.ReadAllTextAsync(competitionPath, cancellationToken));
            foreach (var c in compDoc.RootElement.EnumerateArray())
            {
                var comp = Int(c, "competition_id"); var season = Int(c, "season_id");
                if (comp.HasValue && season.HasValue)
                    competitions[(comp.Value, season.Value)] = (Str(c, "competition_name") ?? "Unknown competition", Str(c, "season_name") ?? "Unknown season");
            }
        }

        var output = new List<UnifiedSoccerAction>();
        var matchFiles = Directory.GetFiles(matchesRoot, "*.json", SearchOption.AllDirectories);
        var matchCount = 0;
        foreach (var file in matchFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var matchDoc = JsonDocument.Parse(await File.ReadAllTextAsync(file, cancellationToken));
            foreach (var match in matchDoc.RootElement.EnumerateArray())
            {
                var matchId = Str(match, "match_id");
                if (string.IsNullOrWhiteSpace(matchId)) continue;
                var eventPath = Path.Combine(eventsRoot, matchId + ".json");
                if (!File.Exists(eventPath)) continue;
                var compId = Int(match, "competition", "competition_id");
                var seasonId = Int(match, "season", "season_id");
                var cohort = compId.HasValue && seasonId.HasValue && competitions.TryGetValue((compId.Value, seasonId.Value), out var found)
                    ? found : ("Unknown competition", "Unknown season");
                var homeTeamId = Str(match, "home_team", "home_team_id");
                using var eventDoc = JsonDocument.Parse(await File.ReadAllTextAsync(eventPath, cancellationToken));
                var sequence = 0;
                foreach (var ev in eventDoc.RootElement.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var eventType = Str(ev, "type", "name") ?? "";
                    var actionType = eventType switch
                    {
                        "Pass" => "pass", "Carry" => "carry", "Shot" => "shot",
                        "Dribble" => "dribble", "Pressure" => "pressure", "Tackle" => "tackle",
                        "Duel" when Str(ev, "duel", "type", "name") == "Tackle" => "tackle",
                        "Interception" => "interception", "Ball Recovery" => "recovery",
                        "Clearance" => "clearance", "Block" => "block", "Duel" => "duel",
                        "Foul Committed" => "foul", "Foul Won" => "foul_won",
                        _ => ""
                    };
                    if (actionType.Length == 0) { sequence++; continue; }
                    var location = Pair(ev, "location");
                    var detail = eventType switch
                    {
                        "Pass" => Prop(ev, "pass"),
                        "Carry" => Prop(ev, "carry"),
                        "Shot" => Prop(ev, "shot"),
                        _ => default
                    };
                    var end = eventType switch
                    {
                        "Pass" => Pair(detail, "end_location"),
                        "Carry" => Pair(detail, "end_location"),
                        "Shot" => Pair(detail, "end_location"),
                        _ => (X: (double?)null, Y: (double?)null)
                    };
                    var period = Int(ev, "period") ?? 1;
                    var teamId = Str(ev, "team", "id");
                    var reverseDirection = !string.IsNullOrWhiteSpace(homeTeamId) && ((teamId == homeTeamId && period % 2 == 0) || (teamId != homeTeamId && period % 2 == 1));
                    if (reverseDirection)
                    {
                        location = (location.X.HasValue ? 120 - location.X.Value : null, location.Y.HasValue ? 80 - location.Y.Value : null);
                        end = (end.X.HasValue ? 120 - end.X.Value : null, end.Y.HasValue ? 80 - end.Y.Value : null);
                    }
                    location = (location.X.HasValue ? location.X.Value * PitchLength / 120d : null, location.Y.HasValue ? PitchWidth - location.Y.Value * PitchWidth / 80d : null);
                    end = (end.X.HasValue ? end.X.Value * PitchLength / 120d : null, end.Y.HasValue ? PitchWidth - end.Y.Value * PitchWidth / 80d : null);
                    var outcome = eventType switch
                    {
                        "Pass" => Str(detail, "outcome", "name") is null,
                        "Dribble" => Str(Prop(ev, "dribble"), "outcome", "name") == "Complete",
                        "Shot" => Str(detail, "outcome", "name") is "Goal" or "Saved" or "Saved to Post",
                        "Tackle" => Str(Prop(ev, "duel"), "outcome", "name") == "Won",
                        "Duel" when Str(ev, "duel", "type", "name") == "Tackle" => Str(ev, "duel", "outcome", "name") == "Won",
                        _ => true
                    };
                    var shotOutcome = Str(detail, "outcome", "name");
                    var xg = Number(detail, "statsbomb_xg");
                    output.Add(new UnifiedSoccerAction(
                        matchId, cohort.Item1, cohort.Item2, Int(ev, "period") ?? 1,
                        Int(ev, "minute") ?? 0, Int(ev, "second") ?? 0,
                        Str(ev, "possession"), teamId, Str(ev, "team", "name") ?? "Unknown team",
                        Str(ev, "player", "id"), Str(ev, "player", "name") ?? "Unknown player",
                        eventType, actionType, outcome, location.X, location.Y, end.X, end.Y,
                        eventType == "Shot", shotOutcome == "Goal", xg,
                        "StatsBomb Open Data", Str(ev, "id") ?? sequence.ToString(CultureInfo.InvariantCulture), sequence++,
                        Str(detail, "recipient", "id"), Str(detail, "recipient", "name"), Str(detail, "body_part", "name")));
                }
                matchCount++;
                if (matchCount % 25 == 0) progress?.Report($"Unified {matchCount:N0} matches · {output.Count:N0} actions");
            }
        }
        return output.OrderBy(x => x.MatchId).ThenBy(x => x.Period).ThenBy(x => x.Minute).ThenBy(x => x.Second).ThenBy(x => x.Sequence).ToList();
    }

    public static ExpectedThreatModel FitExpectedThreat(
        IReadOnlyList<UnifiedSoccerAction> actions, int minimumTrainingMatches = 1, int iterations = 100)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var matches = actions.Select(x => x.MatchId).Distinct().Count();
        if (matches < minimumTrainingMatches) throw new InvalidOperationException($"xT needs at least {minimumTrainingMatches} training matches; found {matches}.");
        var shots = new double[Columns, Rows];
        var goals = new double[Columns, Rows];
        var moves = new double[Columns, Rows];
        var transitions = Enumerable.Range(0, Columns * Rows).Select(_ => new double[Columns * Rows]).ToArray();
        foreach (var a in actions)
        {
            if (!a.StartX.HasValue || !a.StartY.HasValue) continue;
            var start = Zone(a.StartX.Value, a.StartY.Value);
            if (a.IsShot)
            {
                shots[start.X, start.Y]++;
                if (a.IsGoal) goals[start.X, start.Y]++;
            }
            else if ((a.ActionType is "pass" or "carry") && a.Successful &&
                     a.EndX.HasValue && a.EndY.HasValue)
            {
                var end = Zone(a.EndX.Value, a.EndY.Value);
                moves[start.X, start.Y]++;
                transitions[Index(start)][Index(end)]++;
            }
        }

        var values = new double[Columns * Rows];
        var scoring = new double[values.Length];
        var moveProbability = new double[values.Length];
        for (var x = 0; x < Columns; x++)
        for (var y = 0; y < Rows; y++)
        {
            var i = x * Rows + y;
            var total = shots[x, y] + moves[x, y];
            scoring[i] = shots[x, y] == 0 ? 0 : goals[x, y] / shots[x, y];
            moveProbability[i] = total == 0 ? 0 : moves[x, y] / total;
            var rowTotal = transitions[i].Sum();
            if (rowTotal > 0)
                for (var j = 0; j < values.Length; j++) transitions[i][j] /= rowTotal;
        }

        for (var iteration = 0; iteration < Math.Clamp(iterations, 1, 1000); iteration++)
        {
            var next = new double[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var continuation = 0d;
                for (var j = 0; j < values.Length; j++) continuation += transitions[i][j] * values[j];
                next[i] = scoring[i] + moveProbability[i] * continuation;
            }
            values = next;
        }
        return new ExpectedThreatModel(Columns, Rows, values, "Atlas xT · empirical grid",
            "StatsBomb Open Data imported action sample", matches, actions.Count, DateTimeOffset.UtcNow);
    }

    public static IReadOnlyList<ValuedSoccerAction> ValueActions(
        IReadOnlyList<UnifiedSoccerAction> actions, ExpectedThreatModel model)
    {
        return actions.Select(a =>
        {
            double? value = null;
            if ((a.ActionType is "pass" or "carry") && a.Successful &&
                a.StartX.HasValue && a.StartY.HasValue && a.EndX.HasValue && a.EndY.HasValue)
            {
                var start = Zone(a.StartX.Value, a.StartY.Value);
                var end = Zone(a.EndX.Value, a.EndY.Value);
                value = model.Values[Index(end)] - model.Values[Index(start)];
            }
            return new ValuedSoccerAction(a, value);
        }).ToList();
    }

    public static IReadOnlyList<PossessionChain> BuildPossessionChains(IReadOnlyList<ValuedSoccerAction> actions)
    {
        var chains = new List<PossessionChain>();
        foreach (var group in actions.Where(x => !string.IsNullOrWhiteSpace(x.Action.PossessionId))
                     .GroupBy(x => (x.Action.MatchId, x.Action.PossessionId, x.Action.TeamId, x.Action.Team)))
        {
            var rows = group.OrderBy(x => x.Action.Period).ThenBy(x => x.Action.Minute).ThenBy(x => x.Action.Second).ThenBy(x => x.Action.Sequence).ToList();
            if (rows.Count == 0) continue;
            var last = rows[^1].Action;
            var shots = rows.Where(x => x.Action.IsShot).ToList();
            var outcome = shots.Any(x => x.Action.IsGoal) ? "Goal" : shots.Count > 0 ? "Shot" : "No shot";
            chains.Add(new PossessionChain(last.MatchId, last.Competition, last.Season, group.Key.PossessionId!,
                last.Team, rows[0].Action.Minute, last.Minute, rows.Count,
                rows.Count(x => x.Action.ActionType == "pass"), rows.Count(x => x.Action.ActionType == "carry"),
                shots.Count, shots.Count(x => x.Action.IsGoal), shots.Sum(x => x.Action.Xg ?? 0),
                rows.Sum(x => x.XtAdded ?? 0), last.Player, outcome));
        }
        return chains.OrderBy(x => x.MatchId).ThenBy(x => x.StartMinute).ToList();
    }

    public static IReadOnlyList<PassingNetworkEdge> BuildPassingNetwork(IReadOnlyList<ValuedSoccerAction> actions)
    {
        return actions.Where(x => x.Action.ActionType == "pass" &&
                !string.IsNullOrWhiteSpace(x.Action.PlayerId) &&
                !string.IsNullOrWhiteSpace(x.Action.RecipientPlayerId))
            .GroupBy(x => (x.Action.MatchId, x.Action.Team, From: x.Action.Player, To: x.Action.RecipientPlayer!))
            .Select(g =>
            {
                var rows = g.ToList();
                return new PassingNetworkEdge(g.Key.MatchId, g.Key.Team, g.Key.From, g.Key.To,
                    rows.Count, rows.Count(x => x.Action.Successful),
                    Mean(rows.Select(x => x.Action.StartX)), Mean(rows.Select(x => x.Action.StartY)),
                    Mean(rows.Select(x => x.Action.EndX)), Mean(rows.Select(x => x.Action.EndY)));
            }).OrderByDescending(x => x.CompletedPasses).ThenBy(x => x.FromPlayer).ToList();
    }

    private static double? Mean(IEnumerable<double?> values)
    {
        var present = values.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return present.Count == 0 ? null : present.Average();
    }

    public static IReadOnlyList<PlayerEventAnalytics> AggregatePlayers(
        IReadOnlyList<ValuedSoccerAction> actions)
    {
        return actions.GroupBy(x => (x.Action.PlayerId, x.Action.Player, x.Action.Team))
            .Select(g =>
            {
                var rows = g.ToList();
                var passes = rows.Count(x => x.Action.ActionType == "pass");
                var completed = rows.Count(x => x.Action.ActionType == "pass" && x.Action.Successful);
                var xt = rows.Sum(x => x.XtAdded ?? 0);
                return new PlayerEventAnalytics(g.Key.PlayerId ?? "", g.Key.Player, g.Key.Team, null,
                    passes, completed, rows.Count(x => x.Action.ActionType == "carry"),
                    rows.Count(x => x.Action.IsShot), rows.Count(x => x.Action.IsGoal),
                    rows.Sum(x => x.Action.Xg ?? 0), xt,
                    rows.Count(x => x.Action.ActionType == "pressure"),
                    rows.Count(x => x.Action.ActionType == "tackle"),
                    rows.Count(x => x.Action.ActionType == "interception"),
                    rows.Count(x => (x.Action.ActionType is "pass" or "carry") && x.Action.EndX >= 80 && x.Action.StartX < 80),
                    rows.Count(x => x.Action.ActionType == "pass" && IsProgressive(x.Action)),
                    rows.Count(x => x.Action.ActionType == "carry" && IsProgressive(x.Action)),
                    passes == 0 ? null : completed * 100d / passes, null);
            }).ToList();
    }

    private static bool IsProgressive(UnifiedSoccerAction a)
    {
        if (!a.StartX.HasValue || !a.EndX.HasValue) return false;
        var gain = a.EndX.Value - a.StartX.Value;
        var distanceToGoal = PitchLength - a.StartX.Value;
        var threshold = distanceToGoal >= 60 ? 30 : distanceToGoal >= 40 ? 15 : 10;
        return gain >= threshold;
    }

    private static (int X, int Y) Zone(double x, double y) =>
        (Math.Clamp((int)(Math.Clamp(x, 0, 119.999) / PitchLength * Columns), 0, Columns - 1),
         Math.Clamp((int)(Math.Clamp(y, 0, 79.999) / PitchWidth * Rows), 0, Rows - 1));
    private static int Index((int X, int Y) zone) => zone.X * Rows + zone.Y;
    private static JsonElement Prop(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var value) ? value : default;
    private static (double? X, double? Y) Pair(JsonElement e, string key)
    {
        var v = Prop(e, key);
        return v.ValueKind == JsonValueKind.Array && v.GetArrayLength() >= 2
            ? (v[0].ValueKind == JsonValueKind.Number ? v[0].GetDouble() : null,
               v[1].ValueKind == JsonValueKind.Number ? v[1].GetDouble() : null)
            : (null, null);
    }
    private static string? Str(JsonElement e, params string[] path)
    {
        foreach (var p in path) { if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(p, out e)) return null; }
        return e.ValueKind switch { JsonValueKind.String => e.GetString(), JsonValueKind.Number => e.GetRawText(), _ => null };
    }
    private static int? Int(JsonElement e, params string[] path)
    {
        foreach (var p in path) { if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(p, out e)) return null; }
        return e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n) ? n : null;
    }
    private static double? Number(JsonElement e, params string[] path)
    {
        foreach (var p in path) { if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(p, out e)) return null; }
        return e.ValueKind == JsonValueKind.Number ? e.GetDouble() : null;
    }
}
