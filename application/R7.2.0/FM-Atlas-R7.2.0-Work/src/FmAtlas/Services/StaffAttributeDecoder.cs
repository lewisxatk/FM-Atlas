namespace FmAtlas.Services;

/// <summary>
/// Decodes FM27 staff coaching attributes using the public FMLibreEditor layout.
/// Staff coaching attributes and CA/PA stay at their base offsets for the human
/// manager; only the staff identity/person block is shifted by 0x340.
/// Raw coaching values are stored 0..100 and displayed by FM as (raw + 2) / 5.
/// </summary>
public static class StaffAttributeDecoder
{
    private static readonly (string Key, int Offset)[] CoachingFields =
    {
        ("goalkeeping", 0x2B),
        ("judging_player_ability", 0x2C),
        ("judging_player_potential", 0x2D),
        ("people_management", 0x2E),
        ("motivating", 0x2F),
        ("physiotherapy", 0x30),
        ("tactical_knowledge", 0x31),
        ("attacking", 0x32),
        ("defending", 0x33),
        ("fitness", 0x34),
        ("possession", 0x35),
        ("technical", 0x36),
        ("tactical", 0x37),
        ("sports_science", 0x3F),
        ("negotiating", 0x41),
        ("judging_staff_ability", 0x42),
        ("set_pieces", 0x43),
        ("data_analysis", 0x3C),
        ("determination", 0x1D)
    };

    private static readonly (string Key, int Offset)[] DirectRatings =
    {
        ("authority", 0x14),
        ("working_with_youngsters", 0x1C)
    };

    public static IReadOnlyDictionary<string, double> Decode(ReadOnlySpan<byte> staffObject)
    {
        var values = new Dictionary<string, double>(CoachingFields.Length + DirectRatings.Length,
            StringComparer.OrdinalIgnoreCase);

        foreach (var field in CoachingFields)
        {
            if ((uint)field.Offset >= (uint)staffObject.Length) continue;
            var raw = staffObject[field.Offset];
            if (raw > 100) continue;
            values[field.Key] = (raw + 2) / 5;
        }

        foreach (var field in DirectRatings)
        {
            if ((uint)field.Offset >= (uint)staffObject.Length) continue;
            var rating = staffObject[field.Offset];
            if (rating is < 1 or > 20) continue;
            values[field.Key] = rating;
        }

        return values;
    }
}
