namespace FmAtlas.Services;

/// <summary>
/// Decodes the 15 FM27 staff preferred-job group ratings. The group layout is based on
/// independent public FM27 observations; every value is retained as a provisional candidate
/// until compared with the in-game staff search/profile UI.
/// </summary>
public sealed record StaffJobPreferenceResult(
    IReadOnlyDictionary<string, int> Ratings,
    IReadOnlyList<string> PreferredJobs,
    int HighestRating);

public static class StaffJobPreferenceDecoder
{
    public static IReadOnlyList<string> JobGroupKeys { get; } = new[]
    {
        "manager",
        "assistant_manager",
        "coach",
        "physio",
        "scout",
        "goalkeeping_coach",
        "fitness_coach",
        "job_code_22_unresolved",
        "board_ownership",
        "director_of_football",
        "head_of_youth_development",
        "loan_manager",
        "analyst_recruitment_or_performance",
        "sports_scientist",
        "technical_director"
    };

    public static StaffJobPreferenceResult? Decode(ReadOnlySpan<byte> values)
    {
        if (values.Length != JobGroupKeys.Count) return null;

        var ratings = new Dictionary<string, int>(JobGroupKeys.Count, StringComparer.OrdinalIgnoreCase);
        var highest = 0;
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            if (value > 20) return null;
            ratings[JobGroupKeys[i]] = value;
            if (value > highest) highest = value;
        }

        // FM's preferred-job label is only meaningful when a score reaches 10.
        // Preserve every rating, but do not manufacture a single winner from broad ties.
        var preferred = new List<string>();
        if (highest >= 10)
        {
            for (var i = 0; i < values.Length; i++)
                if (values[i] == highest)
                    preferred.Add(JobGroupKeys[i]);
            if (preferred.Count > 3) preferred.Clear();
        }

        return new StaffJobPreferenceResult(ratings, preferred, highest);
    }
}
