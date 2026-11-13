namespace FmAtlas.Services;

public enum PositionSuitabilityBand
{
    Low,
    Limited,
    Developing,
    Accomplished,
    Natural,
    Unavailable
}

public sealed record PositionSuitabilityRating(
    string Key,
    string Label,
    double? Rating,
    PositionSuitabilityBand Band,
    string BandLabel,
    bool IsSourceValue);

/// <summary>
/// Normalises the 15 position ratings exposed by the reviewed fmsave player record.
/// Natural (18+) and Accomplished (15–17) mirror fmsave's documented classifier.
/// The 0–14 bands and 0–20 display bound are Atlas presentation assumptions, not
/// documented FM labels or a claim about the game's hidden suitability formula.
/// </summary>
public static class PositionSuitabilityService
{
    private sealed record Definition(string Key, string Label, string[] Aliases);

    private static readonly Definition[] Definitions =
    {
        new("gk", "Goalkeeper", new[] { "gk", "pos_gk", "position_gk" }),
        new("sw", "Sweeper", new[] { "sw", "pos_sw", "position_sw" }),
        new("dl", "Left Back", new[] { "dl", "pos_dl", "position_dl" }),
        new("dc", "Centre Back", new[] { "dc", "pos_dc", "position_dc" }),
        new("dr", "Right Back", new[] { "dr", "pos_dr", "position_dr" }),
        new("dm", "Defensive Midfielder", new[] { "dm", "pos_dm", "position_dm" }),
        new("ml", "Left Midfielder", new[] { "ml", "pos_ml", "position_ml" }),
        new("mc", "Central Midfielder", new[] { "mc", "pos_mc", "position_mc" }),
        new("mr", "Right Midfielder", new[] { "mr", "pos_mr", "position_mr" }),
        new("aml", "Attacking Midfielder Left", new[] { "aml", "pos_aml", "position_aml" }),
        new("amc", "Attacking Midfielder Centre", new[] { "amc", "pos_amc", "position_amc" }),
        new("amr", "Attacking Midfielder Right", new[] { "amr", "pos_amr", "position_amr" }),
        new("stc", "Striker", new[] { "stc", "st", "pos_st", "pos_stc", "position_st", "position_stc" }),
        new("wbl", "Left Wing-Back", new[] { "wbl", "pos_wbl", "position_wbl" }),
        new("wbr", "Right Wing-Back", new[] { "wbr", "pos_wbr", "position_wbr" })
    };

    public static IReadOnlyList<PositionSuitabilityRating> Build(IReadOnlyDictionary<string, double> sourceValues)
    {
        ArgumentNullException.ThrowIfNull(sourceValues);

        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in sourceValues)
        {
            var key = Normalize(pair.Key);
            if (key.Length > 0 && double.IsFinite(pair.Value) && pair.Value >= 0 && pair.Value <= 20)
                values.TryAdd(key, pair.Value);
        }

        return Definitions.Select(definition =>
        {
            double? rating = null;
            foreach (var alias in definition.Aliases)
            {
                if (values.TryGetValue(Normalize(alias), out var found))
                {
                    rating = found;
                    break;
                }
            }

            var band = GetBand(rating);
            return new PositionSuitabilityRating(
                definition.Key,
                definition.Label,
                rating,
                band,
                GetBandLabel(band),
                rating.HasValue);
        }).ToArray();
    }

    public static PositionSuitabilityBand GetBand(double? rating)
    {
        if (!rating.HasValue || !double.IsFinite(rating.Value) || rating.Value < 0 || rating.Value > 20)
            return PositionSuitabilityBand.Unavailable;
        if (rating.Value >= 18) return PositionSuitabilityBand.Natural;
        if (rating.Value >= 15) return PositionSuitabilityBand.Accomplished;
        if (rating.Value >= 10) return PositionSuitabilityBand.Developing;
        if (rating.Value >= 5) return PositionSuitabilityBand.Limited;
        return PositionSuitabilityBand.Low;
    }

    public static string GetBandLabel(PositionSuitabilityBand band) => band switch
    {
        PositionSuitabilityBand.Natural => "Natural",
        PositionSuitabilityBand.Accomplished => "Accomplished",
        PositionSuitabilityBand.Developing => "Atlas developing band",
        PositionSuitabilityBand.Limited => "Atlas limited band",
        PositionSuitabilityBand.Low => "Atlas low band",
        _ => "Unavailable"
    };

    private static string Normalize(string value)
        => new((value ?? string.Empty).Split('.', ':').LastOrDefault()!
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
}
