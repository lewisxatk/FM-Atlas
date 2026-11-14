using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class ScoutingEngineImportTests
{
    [Fact]
    public async Task StatsBombFolderImportsEventsAndLineupMinutes()
    {
        var root = Path.Combine(Path.GetTempPath(), "fmatlas-sb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "matches", "1"));
        Directory.CreateDirectory(Path.Combine(root, "events"));
        Directory.CreateDirectory(Path.Combine(root, "lineups"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "competitions.json"),
                """[{"competition_id":1,"season_id":2,"competition_name":"Test League","season_name":"2025/26"}]""");
            await File.WriteAllTextAsync(Path.Combine(root, "matches", "1", "2.json"),
                """[{"match_id":100,"competition":{"competition_id":1},"season":{"season_id":2}}]""");
            await File.WriteAllTextAsync(Path.Combine(root, "lineups", "100.json"),
                """[{"team_name":"Atlas FC","lineup":[{"player_id":10,"player_name":"Alex Player","positions":[{"position":"Centre Back","from":"00:00:00","to":"90:00:00"}]}]}]""");
            await File.WriteAllTextAsync(Path.Combine(root, "events", "100.json"),
                """[
                  {"type":{"name":"Pass"},"player":{"id":10,"name":"Alex Player"},"team":{"name":"Atlas FC"},"pass":{"outcome":null,"shot_assist":true}},
                  {"type":{"name":"Shot"},"player":{"id":10,"name":"Alex Player"},"team":{"name":"Atlas FC"},"shot":{"statsbomb_xg":0.25,"outcome":{"name":"Goal"}}},
                  {"type":{"name":"Pressure"},"player":{"id":10,"name":"Alex Player"},"team":{"name":"Atlas FC"}}
                ]""");

            var (players, summary) = await StatsBombOpenDataImporter.ImportAsync(root);
            var player = Assert.Single(players);
            Assert.Equal("Alex Player", player.PlayerName);
            Assert.Equal("Test League", player.Competition);
            Assert.Equal("2025/26", player.Season);
            Assert.Equal(90d, player.MinutesPlayed!.Value);
            Assert.Equal(1d, player.Metrics["passes_attempted"]!.Value);
            Assert.Equal(1d, player.Metrics["passes_completed"]!.Value);
            Assert.Equal(1d, player.Metrics["key_passes"]!.Value);
            Assert.Equal(1d, player.Metrics["goals"]!.Value);
            Assert.Equal(.25d, player.Metrics["xg"]!.Value);
            Assert.Equal(1d, player.Metrics["pressures"]!.Value);
            Assert.Equal(1, summary.MatchesImported);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task WyscoutCsvPreservesPer90FieldsAndSource()
    {
        var path = Path.Combine(Path.GetTempPath(), "fmatlas-wy-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            await File.WriteAllTextAsync(path,
                "Player,Player ID,Team,Competition,Season,Position,Minutes played,Age,Market value,Progressive passes / 90,Goals / 90\n" +
                "Alex Player,77,Atlas FC,Test League,2025/26,CB,1800,24,5000000,4.1,0.2\n");
            var player = Assert.Single(await WyscoutCsvImporter.ImportAsync(path));
            Assert.Equal("Alex Player", player.PlayerName);
            Assert.Equal("Wyscout user CSV", player.SourceName);
            Assert.Equal(4.1d, player.Metrics["progressive_passes_90"]!.Value);
            Assert.Equal(5_000_000d, player.Metrics["market_value"]!.Value);
            Assert.Equal(24d, player.Metrics["age"]!.Value);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void SimilarityNormalizesMetricsWithDifferentScales()
    {
        var target = Make("target", 10, 1000);
        var close = Make("close", 11, 1100);
        var distant = Make("distant", 90, 9000);
        var definitions = new[]
        {
            new ScoutingMetricDefinition("a", "Metric A", "count"),
            new ScoutingMetricDefinition("b", "Metric B", "count")
        };
        var result = ScoutingRecruitmentService.FindSimilar(target, new[] { close, distant }, definitions, 10, .5);
        Assert.Equal("close", result[0].Player.PlayerName);
        Assert.True(result[0].Similarity > result[1].Similarity);
    }

    [Fact]
    public void UndervaluedDiscoveryRequiresAgeValueAndMinutes()
    {
        var players = Enumerable.Range(0, 6).Select(i =>
        {
            var p = Make("p" + i, i * 10, 900);
            var metrics = p.Metrics.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
            {
                ["market_value"] = (i + 1) * 100_000,
                ["age"] = 20 + i
            };
            return p with { Metrics = metrics };
        }).ToList();
        var found = ScoutingRecruitmentService.FindUndervalued(players,
            new ScoutingMetricDefinition("a", "Metric A", "count"), minimumCohortSize: 5);
        Assert.Equal(6, found.Count);
        Assert.All(found, x => Assert.Contains("65%", x.Explanation));
    }

    private static ScoutingPlayerSeason Make(string name, double a, double b) =>
        new(name, name, "Atlas FC", "Test League", "2025/26", "Defender", 900,
            new Dictionary<string, double?> { ["a"] = a, ["b"] = b },
            "Fixture", "1", true);
}
