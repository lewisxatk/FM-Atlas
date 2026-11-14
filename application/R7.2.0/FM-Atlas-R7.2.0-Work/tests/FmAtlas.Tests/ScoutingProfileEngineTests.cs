using FmAtlas.Models;
using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class ScoutingProfileEngineTests
{
    [Fact]
    public void MultiMetricProfileRequiresEveryMetricAndHonoursMinutesAndCoverage()
    {
        var profile = new ScoutingProfile("wide-forward", "Wide forward", DateTime.UtcNow,
            new[] {
                new ScoutingMetricRequirement("goals", "Goals", Minimum: 0.25, UsePer90: true),
                new ScoutingMetricRequirement("key_passes", "Key passes", Minimum: 1.5, UsePer90: true)
            }, ScoutingCohortBasis.Position, "AMR", MinimumMinutes: 900,
            MinimumSampleSize: 2, MinimumDataCoverage: .8);
        var valid = Observation("1", "Player One", "AMR", 900, 4, 20, .95);
        var missing = Observation("2", "Player Two", "AMR", 900, 4, null, .95);
        var shortSample = Observation("3", "Player Three", "AMR", 400, 4, 20, .95);
        var lowCoverage = Observation("4", "Player Four", "AMR", 900, 4, 20, .5);
        var matches = ScoutingProfileEngine.FindMatches(profile, new[] { valid, missing, shortSample, lowCoverage });
        var match = Assert.Single(matches);
        Assert.Equal("1", match.Player.SourceId);
        Assert.Equal("fmsave import", match.Evidence.SourceName);
        Assert.Equal(.95, match.Evidence.Coverage);
    }

    [Fact]
    public void ProfileStorePersistsAndDeletesProfiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "fmatlas-profile-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var profile = new ScoutingProfile("id_1", "Pressing forward", DateTime.UtcNow,
                new[] { new ScoutingMetricRequirement("pressures", "Pressures", Minimum: 10) });
            ScoutingProfileStore.Save(profile, folder);
            Assert.Equal(profile.Name, Assert.Single(ScoutingProfileStore.LoadAll(folder)).Name);
            Assert.True(ScoutingProfileStore.Delete(profile.Id, folder));
            Assert.Empty(ScoutingProfileStore.LoadAll(folder));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void ComparisonUsesTieAwarePercentilesAndLeavesMissingValuesUnranked()
    {
        var population = new[] {
            Sample("1", "A", "STC", "Premier", 1),
            Sample("2", "B", "STC", "Premier", 2),
            Sample("3", "C", "STC", "Premier", 2),
            Sample("4", "D", "STC", "Premier", 4)
        };
        var target = new[] { Sample("2", "B", "STC", "Premier", 2), Sample("5", "E", "STC", "Premier", null) };
        var rows = PlayerStatComparisonService.Compare(target, population, new[] { "xg" }, ScoutingCohortBasis.Position);
        Assert.Equal(62.5, rows[0].Percentile);
        Assert.Null(rows[1].Percentile);
        Assert.Contains(rows[1].Warnings, x => x.Contains("missing", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(4, rows[0].CohortSize);
    }

    private static ScoutingMetricObservation Observation(string id, string name, string position,
        double minutes, double goals, double? keyPasses, double coverage)
        => new(id, name, "Club", position, "League", 23, 1000000m, minutes, 3, coverage,
            "fmsave import", DateTime.UtcNow,
            new Dictionary<string, double?> { ["goals"] = goals, ["key_passes"] = keyPasses });

    private static PlayerStatSample Sample(string id, string name, string position, string league, double? value)
        => new(id, name, position, league, new Dictionary<string, double?> { ["xg"] = value }, "fmsave import", DateTime.UtcNow);
}