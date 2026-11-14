namespace FmAtlas.Services;

public sealed record RoleFitProfile(string Name, string Phase, string PositionGroup, string Description, IReadOnlyDictionary<string, double> Weights, bool IsCustom = false, RoleFitSearchFilters? SavedFilters = null);
public sealed record RoleFitAttributeContribution(string Attribute, double Value, double Weight, double WeightedPoints, double ShareOfTotal);

public sealed record RoleFitCandidate(string SourceId, string Name, string? Club, int? Age, string? Position, int? CurrentAbility, int? PotentialAbility, double Score, int AttributesAvailable, int AttributesExpected, double Coverage, IReadOnlyList<RoleFitAttributeContribution>? Contributions = null)
{
    public string EvidenceSummary => Contributions is null || Contributions.Count == 0
        ? "No attribute evidence"
        : string.Join(" · ", Contributions.Take(4).Select(x => $"{x.Attribute} {x.Value:0.#}"));
}
public sealed record RoleFitSearchFilters(
    int? MinimumAge = null,
    int? MaximumAge = null,
    int? MinimumCurrentAbility = null,
    int? MinimumPotentialAbility = null,
    string? PositionContains = null,
    double MinimumCoverage = 0.5,
    int ResultLimit = 250);
public sealed record RoleFitScore(double Score, int AttributesAvailable, int AttributesExpected, double Coverage, IReadOnlyList<RoleFitAttributeContribution> Contributions);

/// <summary>
/// Transparent coaching-style role estimates. These are not Sports Interactive's hidden role formula
/// or the game's star rating. Only source-provided 1–20 attributes contribute to each score.
/// </summary>
public static class RoleFitService
{
    private static IReadOnlyDictionary<string, double> W(params (string Name, double Weight)[] fields)
        => fields.ToDictionary(x => x.Name, x => x.Weight, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<RoleFitProfile> Profiles = new List<RoleFitProfile>
    {
        new("Goalkeeper", "Both", "Goalkeeper", "Shot-stopping, handling and positioning.", W(("Reflexes",3),("Handling",2.5),("One on Ones",2),("Agility",2),("Positioning",1.5),("Concentration",1),("Aerial Reach",1))),
        new("Ball-Playing Goalkeeper", "In Possession", "Goalkeeper", "Goalkeeping plus composed distribution and decisions.", W(("Reflexes",2),("Handling",1.5),("Passing",2.5),("Kicking",2),("Decisions",1.5),("Composure",1),("First Touch",1),("Positioning",1))),
        new("Centre-Back", "Both", "Defender", "Defensive duels, aerial ability and reading danger.", W(("Anticipation",2),("Positioning",2),("Marking",1.5),("Tackling",1.5),("Heading",1.5),("Jumping Reach",1.5),("Strength",1.5),("Decisions",1),("Pace",1))),
        new("Ball-Playing Centre-Back", "In Possession", "Defender", "Centre-back defending with first touch and progressive distribution.", W(("Anticipation",1.5),("Positioning",1.5),("Marking",1),("Tackling",1),("Heading",1),("Jumping Reach",1),("Strength",1),("Passing",2.5),("Technique",1.5),("Composure",1.5),("Decisions",1.5),("Vision",1))),
        new("Full-Back", "Both", "Defender", "Balanced wide defending, recovery and support.", W(("Pace",1.5),("Acceleration",1.5),("Marking",1.5),("Tackling",1.5),("Positioning",1.5),("Stamina",1.5),("Work Rate",1),("Teamwork",1),("Crossing",1))),
        new("Wing-Back", "In Possession", "Defender", "High-energy wide progression and crossing.", W(("Pace",2),("Acceleration",2),("Stamina",2),("Work Rate",1.5),("Crossing",1.5),("Dribbling",1.5),("Tackling",1),("Teamwork",1),("Off The Ball",1))),
        new("Defensive Midfielder", "Both", "Midfielder", "Screening, positioning and regaining possession.", W(("Positioning",2.5),("Anticipation",2),("Tackling",2),("Teamwork",1.5),("Work Rate",1.5),("Decisions",1.5),("Stamina",1),("Passing",1))),
        new("Deep-Lying Playmaker", "In Possession", "Midfielder", "Build-up passing, vision and composure from deep.", W(("Passing",2.5),("Vision",2.5),("Decisions",2),("Composure",1.5),("First Touch",1.5),("Technique",1),("Teamwork",1),("Positioning",1))),
        new("Central Midfielder", "Both", "Midfielder", "Well-rounded central contribution and engine.", W(("Passing",1.5),("First Touch",1.5),("Decisions",1.5),("Teamwork",1.5),("Work Rate",1.5),("Stamina",1.5),("Tackling",1),("Off The Ball",1),("Technique",1))),
        new("Midfield Playmaker", "In Possession", "Midfielder", "Creative central passing and progression.", W(("Passing",2.5),("Vision",2.5),("Technique",1.5),("First Touch",1.5),("Decisions",1.5),("Flair",1),("Composure",1.5),("Teamwork",.5))),
        new("Ball-Winning Midfielder", "Out of Possession", "Midfielder", "Aggressive ball recovery, stamina and work rate.", W(("Tackling",2.5),("Work Rate",2),("Stamina",2),("Aggression",1.5),("Teamwork",1.5),("Bravery",1),("Positioning",1),("Anticipation",1))),
        new("Attacking Midfielder", "In Possession", "Attacker", "Between-the-lines creation, touch and movement.", W(("Vision",2),("Passing",2),("Technique",1.5),("First Touch",1.5),("Flair",1.5),("Decisions",1.5),("Off The Ball",1),("Dribbling",1),("Composure",1))),
        new("Winger", "In Possession", "Attacker", "Wide one-versus-one threat and delivery.", W(("Acceleration",2),("Pace",2),("Dribbling",2),("Crossing",1.5),("Technique",1),("Flair",1),("Agility",1),("Off The Ball",1),("Stamina",.5))),
        new("Inside Forward", "In Possession", "Attacker", "Wide attacker who threatens goal from inside channels.", W(("Acceleration",2),("Pace",2),("Dribbling",2),("Finishing",2),("Off The Ball",1.5),("Technique",1.5),("Composure",1),("Agility",1),("Flair",.5))),
        new("Wide Forward", "In Possession", "Attacker", "Direct wide scoring threat with pace and movement.", W(("Pace",2),("Acceleration",2),("Off The Ball",2),("Finishing",1.5),("Dribbling",1.5),("Anticipation",1),("Composure",1),("Technique",1))),
        new("Centre Forward", "Both", "Attacker", "All-round striker with movement, finishing and link play.", W(("Finishing",2.5),("Off The Ball",2),("Composure",1.5),("Anticipation",1.5),("First Touch",1),("Technique",1),("Acceleration",1),("Pace",1),("Heading",.5))),
        new("Target Forward", "In Possession", "Attacker", "Physical focal point, aerial threat and hold-up play.", W(("Strength",2.5),("Jumping Reach",2),("Heading",2),("Balance",1.5),("First Touch",1.5),("Teamwork",1),("Finishing",1),("Bravery",1))),
        new("Pressing Forward", "Out of Possession", "Attacker", "Lead the press while retaining a credible goal threat.", W(("Work Rate",2),("Stamina",2),("Aggression",1.5),("Teamwork",1.5),("Acceleration",1.5),("Pace",1.5),("Anticipation",1),("Finishing",1),("Off The Ball",1))),
        new("Poacher", "In Possession", "Attacker", "Penalty-area movement and decisive finishing.", W(("Finishing",3),("Off The Ball",2.5),("Anticipation",2),("Composure",1.5),("Acceleration",1),("Pace",1),("First Touch",.5))),
        new("Pressing Full-Back", "Out of Possession", "Defender", "Wide defensive pressure, recovery and repeat effort.", W(("Work Rate",2),("Stamina",2),("Acceleration",1.5),("Pace",1.5),("Tackling",1.5),("Marking",1),("Aggression",1),("Teamwork",1.5),("Anticipation",1))),
        new("Covering Centre-Back", "Out of Possession", "Defender", "Recovery pace, anticipation and defensive positioning.", W(("Pace",2),("Acceleration",2),("Anticipation",2),("Positioning",2),("Concentration",1.5),("Decisions",1.5),("Tackling",1),("Marking",1))),
        new("Pressing Midfielder", "Out of Possession", "Midfielder", "Front-foot pressure, ball recovery and repeat running.", W(("Work Rate",2),("Stamina",2),("Aggression",1.5),("Tackling",1.5),("Anticipation",1.5),("Teamwork",1.5),("Bravery",1),("Positioning",1))),
        new("Screening Midfielder", "Out of Possession", "Midfielder", "Protect the central space and disrupt passing lanes.", W(("Positioning",2.5),("Anticipation",2),("Concentration",1.5),("Decisions",1.5),("Teamwork",1.5),("Tackling",1.5),("Work Rate",1),("Strength",.5)))
    };

    /// <summary>
    /// Computes an explainable weighted mean from source-provided 1–20 attributes only.
    /// Missing, non-finite and out-of-range values are excluded; coverage is reported separately.
    /// This is an Atlas estimate, not the game's hidden role formula or star rating.
    /// </summary>
    public static RoleFitScore? CalculateScore(
        RoleFitProfile profile,
        IReadOnlyDictionary<string, double> attributes,
        double minimumCoverage = 0.5)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(attributes);
        if (!double.IsFinite(minimumCoverage) || minimumCoverage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(minimumCoverage), "Coverage must be between 0 and 1.");

        var weights = profile.Weights
            .Where(x => double.IsFinite(x.Value) && x.Value > 0)
            .GroupBy(x => NormalizeAttributeKey(x.Key), StringComparer.OrdinalIgnoreCase)
            .Select(x => new { Key = x.Key, Label = x.First().Key, Weight = x.Sum(v => v.Value) })
            .Where(x => x.Key.Length > 0 && x.Weight > 0)
            .ToList();
        if (weights.Count == 0) return null;

        var values = attributes
            .Where(x => double.IsFinite(x.Value) && x.Value is >= 1 and <= 20)
            .GroupBy(x => NormalizeAttributeKey(x.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.OrdinalIgnoreCase);

        double weightedTotal = 0;
        double availableWeight = 0;
        var available = 0;
        var evidence = new List<(string Attribute, double Value, double Weight, double WeightedPoints)>();
        foreach (var weight in weights)
        {
            if (!values.TryGetValue(weight.Key, out var value)) continue;
            var points = value * weight.Weight;
            weightedTotal += points;
            availableWeight += weight.Weight;
            available++;
            evidence.Add((weight.Label, value, weight.Weight, points));
        }

        var coverage = available / (double)weights.Count;
        if (available == 0 || availableWeight <= 0 || coverage < minimumCoverage) return null;
        var contributions = evidence
            .Select(x => new RoleFitAttributeContribution(x.Attribute, x.Value, x.Weight, x.WeightedPoints,
                weightedTotal > 0 ? x.WeightedPoints / weightedTotal : 0))
            .OrderByDescending(x => x.WeightedPoints)
            .ThenBy(x => x.Attribute, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new RoleFitScore(weightedTotal / availableWeight, available, weights.Count, coverage, contributions);
    }

    private static string NormalizeAttributeKey(string value)
    {
        var separator = value.LastIndexOf(" · ", StringComparison.Ordinal);
        var attribute = separator >= 0 ? value[(separator + 3)..] : value;
        return new(attribute.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    public static IReadOnlyList<RoleFitProfile> GetProfiles() => Profiles;
}
