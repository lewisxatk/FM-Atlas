namespace FmAtlas.Services;

internal sealed record PlayerAttributeField(string Key, string Label, string Category, int Offset);
internal static class PlayerAttributeSchema
{
    // Source: FMLibreEditor profiles/fm27/layout.toml [player.attributes], cross-checked with
    // crates/fmlibre-core/src/details.rs ATTRIBUTE_GROUPS and the independently registered FM27
    // Game Pass profile fingerprint. The 47 standard plus five hidden fields (52 total) map to
    // this FM27 player layout. Live writes still require exact-build, UID/name, raw-byte preflight,
    // explicit session opt-in, read-back verification and rollback. Feet are mapped separately.
    internal static readonly PlayerAttributeField[] Fields = new PlayerAttributeField[] {
        new("crossing", "Crossing", "Technical", 0x15F),
        new("dribbling", "Dribbling", "Technical", 0x160),
        new("finishing", "Finishing", "Technical", 0x161),
        new("heading", "Heading", "Technical", 0x162),
        new("long_shots", "Long Shots", "Technical", 0x163),
        new("marking", "Marking", "Technical", 0x164),
        new("passing", "Passing", "Technical", 0x166),
        new("tackling", "Tackling", "Technical", 0x168),
        new("technique", "Technique", "Technical", 0x176),
        new("first_touch", "First Touch", "Technical", 0x175),
        new("corners", "Corners", "Set Pieces", 0x17A),
        new("free_kicks", "Free Kicks", "Set Pieces", 0x182),
        new("long_throws", "Long Throws", "Set Pieces", 0x17D),
        new("penalty_taking", "Penalty Taking", "Set Pieces", 0x167),
        new("aggression", "Aggression", "Mental", 0x18C),
        new("anticipation", "Anticipation", "Mental", 0x170),
        new("bravery", "Bravery", "Mental", 0x18A),
        new("composure", "Composure", "Mental", 0x193),
        new("concentration", "Concentration", "Mental", 0x194),
        new("decisions", "Decisions", "Mental", 0x171),
        new("flair", "Flair", "Mental", 0x179),
        new("leadership", "Leadership", "Mental", 0x187),
        new("off_the_ball", "Off The Ball", "Mental", 0x165),
        new("positioning", "Positioning", "Mental", 0x173),
        new("teamwork", "Teamwork", "Mental", 0x17B),
        new("vision", "Vision", "Mental", 0x169),
        new("work_rate", "Work Rate", "Mental", 0x17C),
        new("determination", "Determination", "Mental", 0x192),
        new("acceleration", "Acceleration", "Physical", 0x181),
        new("agility", "Agility", "Physical", 0x18D),
        new("balance", "Balance", "Physical", 0x189),
        new("jumping_reach", "Jumping Reach", "Physical", 0x186),
        new("natural_fitness", "Natural Fitness", "Physical", 0x191),
        new("pace", "Pace", "Physical", 0x185),
        new("stamina", "Stamina", "Physical", 0x184),
        new("strength", "Strength", "Physical", 0x183),
        new("handling", "Handling", "Goalkeeping", 0x16A),
        new("aerial_reach", "Aerial Reach", "Goalkeeping", 0x16B),
        new("command_of_area", "Command of Area", "Goalkeeping", 0x16C),
        new("communication", "Communication", "Goalkeeping", 0x16D),
        new("kicking", "Kicking", "Goalkeeping", 0x16E),
        new("throwing", "Throwing", "Goalkeeping", 0x16F),
        new("one_on_ones", "One on Ones", "Goalkeeping", 0x172),
        new("reflexes", "Reflexes", "Goalkeeping", 0x174),
        new("eccentricity", "Eccentricity", "Goalkeeping", 0x17E),
        new("rushing_out", "Rushing Out", "Goalkeeping", 0x17F),
        new("punching", "Punching", "Goalkeeping", 0x180),
        // Hidden attributes: corroborated against the independent FM27 profile at +0x188..+0x190.
        new("consistency", "Consistency", "Hidden", 0x18B),
        new("important_matches", "Important Matches", "Hidden", 0x18E),
        new("injury_proneness", "Injury Proneness", "Hidden", 0x18F),
        new("dirtiness", "Dirtiness", "Hidden", 0x188),
        new("versatility", "Versatility", "Hidden", 0x190),
    };

    // FM27 player layout cross-reference: eight 1–20 personality bytes at +0x2F8..+0x2FF.
    // Player-coach records can carry a shifted person block; until that class path is validated,
    // these offsets are exposed for the standard player record only.
    internal static readonly PlayerAttributeField[] PersonalityFields = new PlayerAttributeField[]
    {
        new("adaptability", "Adaptability", "Personality", 0x2F8),
        new("ambition", "Ambition", "Personality", 0x2F9),
        new("loyalty", "Loyalty", "Personality", 0x2FA),
        new("pressure", "Pressure", "Personality", 0x2FB),
        new("professionalism", "Professionalism", "Personality", 0x2FC),
        new("sportsmanship", "Sportsmanship", "Personality", 0x2FD),
        new("temperament", "Temperament", "Personality", 0x2FE),
        new("controversy", "Controversy", "Personality", 0x2FF)
    };
}
