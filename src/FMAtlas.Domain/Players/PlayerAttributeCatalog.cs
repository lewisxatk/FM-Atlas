namespace FMAtlas.Domain.Players;

/// <summary>
/// Football Manager player attribute groups. The order in each group follows the
/// public FMLibreEditor FM profile catalogue. FM Atlas must still visually verify
/// this order against the user's exact FM27 build before marking it verified.
/// </summary>
public enum PlayerAttributeGroupKind
{
    Technical,
    Goalkeeping,
    Mental,
    Physical,
    Hidden,
    Personality,
    Feet
}

/// <summary>
/// Evidence level for a catalogue definition. Documented does not mean that the
/// field has been read or written successfully on the user's game build.
/// </summary>
public enum FieldEvidenceStatus
{
    Documented,
    Mapped,
    ReadVerified,
    WriteVerified,
    ReadOnly,
    Unknown,
    Unsupported
}

public sealed record PlayerAttributeDefinition(
    string Key,
    string DisplayName,
    PlayerAttributeGroupKind Group,
    int GroupOrder,
    FieldEvidenceStatus EvidenceStatus = FieldEvidenceStatus.Documented,
    bool IsHidden = false);

public sealed record PlayerAttributeGroupDefinition(
    PlayerAttributeGroupKind Kind,
    string DisplayName,
    IReadOnlyList<PlayerAttributeDefinition> Attributes);

/// <summary>
/// Source-documented FM player profile catalogue. The keys are stable Atlas domain
/// identifiers, not memory offsets. Memory layout and write permission belong to
/// version-specific infrastructure profiles and must never be inferred here.
/// </summary>
public static class PlayerAttributeCatalog
{
    private static readonly IReadOnlyList<PlayerAttributeDefinition> Technical =
    [
        A("corners", "Corners", PlayerAttributeGroupKind.Technical, 0),
        A("crossing", "Crossing", PlayerAttributeGroupKind.Technical, 1),
        A("dribbling", "Dribbling", PlayerAttributeGroupKind.Technical, 2),
        A("finishing", "Finishing", PlayerAttributeGroupKind.Technical, 3),
        A("first_touch", "First Touch", PlayerAttributeGroupKind.Technical, 4),
        A("free_kicks", "Free Kicks", PlayerAttributeGroupKind.Technical, 5),
        A("heading", "Heading", PlayerAttributeGroupKind.Technical, 6),
        A("long_shots", "Long Shots", PlayerAttributeGroupKind.Technical, 7),
        A("long_throws", "Long Throws", PlayerAttributeGroupKind.Technical, 8),
        A("marking", "Marking", PlayerAttributeGroupKind.Technical, 9),
        A("passing", "Passing", PlayerAttributeGroupKind.Technical, 10),
        A("penalty_taking", "Penalty Taking", PlayerAttributeGroupKind.Technical, 11),
        A("tackling", "Tackling", PlayerAttributeGroupKind.Technical, 12),
        A("technique", "Technique", PlayerAttributeGroupKind.Technical, 13)
    ];

    private static readonly IReadOnlyList<PlayerAttributeDefinition> Goalkeeping =
    [
        A("aerial_reach", "Aerial Reach", PlayerAttributeGroupKind.Goalkeeping, 0),
        A("command_of_area", "Command of Area", PlayerAttributeGroupKind.Goalkeeping, 1),
        A("communication", "Communication", PlayerAttributeGroupKind.Goalkeeping, 2),
        A("eccentricity", "Eccentricity", PlayerAttributeGroupKind.Goalkeeping, 3),
        A("first_touch", "First Touch", PlayerAttributeGroupKind.Goalkeeping, 4),
        A("handling", "Handling", PlayerAttributeGroupKind.Goalkeeping, 5),
        A("kicking", "Kicking", PlayerAttributeGroupKind.Goalkeeping, 6),
        A("one_on_ones", "One on Ones", PlayerAttributeGroupKind.Goalkeeping, 7),
        A("passing", "Passing", PlayerAttributeGroupKind.Goalkeeping, 8),
        A("punching", "Punching", PlayerAttributeGroupKind.Goalkeeping, 9),
        A("reflexes", "Reflexes", PlayerAttributeGroupKind.Goalkeeping, 10),
        A("rushing_out", "Rushing Out (Tendency)", PlayerAttributeGroupKind.Goalkeeping, 11),
        A("throwing", "Throwing", PlayerAttributeGroupKind.Goalkeeping, 12)
    ];

    private static readonly IReadOnlyList<PlayerAttributeDefinition> Mental =
    [
        A("aggression", "Aggression", PlayerAttributeGroupKind.Mental, 0),
        A("anticipation", "Anticipation", PlayerAttributeGroupKind.Mental, 1),
        A("bravery", "Bravery", PlayerAttributeGroupKind.Mental, 2),
        A("composure", "Composure", PlayerAttributeGroupKind.Mental, 3),
        A("concentration", "Concentration", PlayerAttributeGroupKind.Mental, 4),
        A("decisions", "Decisions", PlayerAttributeGroupKind.Mental, 5),
        A("determination", "Determination", PlayerAttributeGroupKind.Mental, 6),
        A("flair", "Flair", PlayerAttributeGroupKind.Mental, 7),
        A("leadership", "Leadership", PlayerAttributeGroupKind.Mental, 8),
        A("off_the_ball", "Off the Ball", PlayerAttributeGroupKind.Mental, 9),
        A("positioning", "Positioning", PlayerAttributeGroupKind.Mental, 10),
        A("teamwork", "Teamwork", PlayerAttributeGroupKind.Mental, 11),
        A("vision", "Vision", PlayerAttributeGroupKind.Mental, 12),
        A("work_rate", "Work Rate", PlayerAttributeGroupKind.Mental, 13)
    ];

    private static readonly IReadOnlyList<PlayerAttributeDefinition> Physical =
    [
        A("acceleration", "Acceleration", PlayerAttributeGroupKind.Physical, 0),
        A("agility", "Agility", PlayerAttributeGroupKind.Physical, 1),
        A("balance", "Balance", PlayerAttributeGroupKind.Physical, 2),
        A("jumping_reach", "Jumping Reach", PlayerAttributeGroupKind.Physical, 3),
        A("natural_fitness", "Natural Fitness", PlayerAttributeGroupKind.Physical, 4),
        A("pace", "Pace", PlayerAttributeGroupKind.Physical, 5),
        A("stamina", "Stamina", PlayerAttributeGroupKind.Physical, 6),
        A("strength", "Strength", PlayerAttributeGroupKind.Physical, 7)
    ];

    private static readonly IReadOnlyList<PlayerAttributeDefinition> Hidden =
    [
        H("consistency", "Consistency", 0),
        H("dirtiness", "Dirtiness", 1),
        H("important_matches", "Important Matches", 2),
        H("injury_proneness", "Injury Proneness", 3),
        H("versatility", "Versatility", 4)
    ];

    private static readonly IReadOnlyList<PlayerAttributeDefinition> Personality =
    [
        P("adaptability", "Adaptability", 0),
        P("ambition", "Ambition", 1),
        P("loyalty", "Loyalty", 2),
        P("pressure", "Pressure", 3),
        P("professionalism", "Professionalism", 4),
        P("sportsmanship", "Sportsmanship", 5),
        P("temperament", "Temperament", 6),
        P("controversy", "Controversy", 7)
    ];

    private static readonly IReadOnlyList<PlayerAttributeDefinition> Feet =
    [
        A("left_foot", "Left Foot", PlayerAttributeGroupKind.Feet, 0),
        A("right_foot", "Right Foot", PlayerAttributeGroupKind.Feet, 1)
    ];

    private static readonly IReadOnlyList<PlayerAttributeGroupDefinition> OutfieldGroups =
    [
        G(PlayerAttributeGroupKind.Technical, "Technical", Technical),
        G(PlayerAttributeGroupKind.Mental, "Mental", Mental),
        G(PlayerAttributeGroupKind.Physical, "Physical", Physical),
        G(PlayerAttributeGroupKind.Hidden, "Hidden", Hidden),
        G(PlayerAttributeGroupKind.Personality, "Personality", Personality),
        G(PlayerAttributeGroupKind.Feet, "Feet", Feet)
    ];

    private static readonly IReadOnlyList<PlayerAttributeGroupDefinition> GoalkeeperGroups =
    [
        G(PlayerAttributeGroupKind.Goalkeeping, "Goalkeeping", Goalkeeping),
        G(PlayerAttributeGroupKind.Mental, "Mental", Mental),
        G(PlayerAttributeGroupKind.Physical, "Physical", Physical),
        G(PlayerAttributeGroupKind.Hidden, "Hidden", Hidden),
        G(PlayerAttributeGroupKind.Personality, "Personality", Personality),
        G(PlayerAttributeGroupKind.Feet, "Feet", Feet)
    ];

    /// <summary>Unique non-foot fields across all player profile groups (52).</summary>
    public static IReadOnlyList<PlayerAttributeDefinition> AllUniqueNonFootAttributes { get; } =
        BuildUnique(Technical, Goalkeeping, Mental, Physical, Hidden);

    /// <summary>Eight personality ratings, separate from the 52 standard/hidden attributes.</summary>
    public static IReadOnlyList<PlayerAttributeDefinition> PersonalityAttributes => Personality;

    /// <summary>Left and right foot strength are separate from the 52 attributes.</summary>
    public static IReadOnlyList<PlayerAttributeDefinition> FootAttributes => Feet;

    /// <summary>
    /// Returns the profile groups. Goalkeepers show Goalkeeping instead of Technical;
    /// First Touch and Passing are intentionally shared definitions across those groups.
    /// </summary>
    public static IReadOnlyList<PlayerAttributeGroupDefinition> GetProfileGroups(bool isGoalkeeper) =>
        isGoalkeeper ? GoalkeeperGroups : OutfieldGroups;

    public static bool TryGetByKey(string key, out PlayerAttributeDefinition? attribute)
    {
        attribute = AllUniqueNonFootAttributes.FirstOrDefault(
            x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

        if (attribute is null)
            attribute = Personality.FirstOrDefault(
                x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

        if (attribute is null)
            attribute = Feet.FirstOrDefault(
                x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

        return attribute is not null;
    }

    private static PlayerAttributeDefinition A(
        string key, string name, PlayerAttributeGroupKind group, int order) =>
        new(key, name, group, order);

    private static PlayerAttributeDefinition H(string key, string name, int order) =>
        new(key, name, PlayerAttributeGroupKind.Hidden, order, FieldEvidenceStatus.Documented, true);

    private static PlayerAttributeDefinition P(string key, string name, int order) =>
        new(key, name, PlayerAttributeGroupKind.Personality, order);

    private static PlayerAttributeGroupDefinition G(
        PlayerAttributeGroupKind kind,
        string name,
        IReadOnlyList<PlayerAttributeDefinition> attributes) =>
        new(kind, name, attributes);

    private static IReadOnlyList<PlayerAttributeDefinition> BuildUnique(
        params IReadOnlyList<PlayerAttributeDefinition>[] groups)
    {
        var result = new List<PlayerAttributeDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        foreach (var attribute in group)
        {
            if (seen.Add(attribute.Key))
                result.Add(attribute);
        }

        return result.AsReadOnly();
    }
}
