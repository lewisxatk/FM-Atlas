using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>Evaluates multi-stat recruitment profiles without imputing missing statistics.</summary>
public static class ScoutingProfileEngine
{
    public static IReadOnlyList<ScoutingProfileMatch> FindMatches(
        ScoutingProfile profile, IEnumerable<ScoutingMetricObservation> observations)
    {
        ScoutingProfileStore.Validate(profile);
        ArgumentNullException.ThrowIfNull(observations);
        var matches = new List<ScoutingProfileMatch>();
        foreach (var player in observations)
        {
            if (string.IsNullOrWhiteSpace(player.SourceId) || player.Minutes < profile.MinimumMinutes
                || player.SampleSize < profile.MinimumSampleSize || player.DataCoverage < profile.MinimumDataCoverage
                || player.DataCoverage is < 0 or > 1 || !double.IsFinite(player.DataCoverage)
                || player.Age.HasValue && (profile.MinimumAge.HasValue && player.Age < profile.MinimumAge
                    || profile.MaximumAge.HasValue && player.Age > profile.MaximumAge)
                || profile.MaximumValue.HasValue && (!player.Value.HasValue || player.Value > profile.MaximumValue))
                continue;
            if (!MatchesCohort(profile, player)) continue;

            var failures = new List<string>();
            var evaluated = 0;
            foreach (var requirement in profile.Requirements)
            {
                if (!player.Metrics.TryGetValue(requirement.MetricKey, out var raw) || !raw.HasValue || !double.IsFinite(raw.Value))
                {
                    failures.Add(requirement.DisplayName + ": missing source value");
                    continue;
                }
                var value = raw.Value;
                if (requirement.UsePer90)
                {
                    if (player.Minutes <= 0)
                    {
                        failures.Add(requirement.DisplayName + ": minutes unavailable for per-90 calculation");
                        continue;
                    }
                    value = value * 90d / player.Minutes;
                }
                evaluated++;
                if (requirement.Minimum.HasValue && value < requirement.Minimum.Value
                    || requirement.Maximum.HasValue && value > requirement.Maximum.Value)
                    failures.Add(requirement.DisplayName + ": outside required range");
            }
            if (failures.Count > 0 || evaluated != profile.Requirements.Count) continue;
            var score = profile.Requirements.Count == 0 ? 0 : 100d * evaluated / profile.Requirements.Count;
            matches.Add(new ScoutingProfileMatch(player, score,
                new ScoutingEvidence(player.SourceName, player.SourceUpdatedUtc, player.DataCoverage,
                    player.SampleSize, player.Minutes, player.MissingFields ?? Array.Empty<string>()), failures));
        }
        return matches.OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.Evidence.Coverage)
            .ThenBy(x => x.Player.Value ?? decimal.MaxValue)
            .Take(profile.ResultLimit).ToArray();
    }

    private static bool MatchesCohort(ScoutingProfile profile, ScoutingMetricObservation player)
    {
        var positionMatches = string.IsNullOrWhiteSpace(profile.PositionCode)
            || string.Equals(profile.PositionCode, player.Position, StringComparison.OrdinalIgnoreCase);
        var leagueMatches = string.IsNullOrWhiteSpace(profile.League)
            || string.Equals(profile.League, player.League, StringComparison.OrdinalIgnoreCase);
        return profile.CohortBasis switch
        {
            ScoutingCohortBasis.Overall => true,
            ScoutingCohortBasis.Position => positionMatches,
            ScoutingCohortBasis.League => leagueMatches,
            ScoutingCohortBasis.PositionAndLeague => positionMatches && leagueMatches,
            _ => false
        };
    }
}
