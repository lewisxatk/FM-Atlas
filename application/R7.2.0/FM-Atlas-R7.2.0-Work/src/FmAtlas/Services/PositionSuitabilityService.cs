namespace FmAtlas.Services;

public enum PositionSuitabilityBand
{
    Unrated,
    Low,
    Limited,
    Competent,
    Strong,
    Natural
}

/// <summary>
/// A position rating taken directly from the imported FM positions value object or an
/// explicitly verified live field. Atlas bands are a display interpretation of the source
/// 1–20 rating; they do not synthesize missing ratings.
/// </summary>
public sealed record PositionSuitabilityEntry(
    string Code,
    string Label,
    double? Rating,
    PositionSuitabilityBand Band)
{
    public string BandLabel => Band switch
    {
        PositionSuitabilityBand.Low => "Low · 1–4",
        PositionSuitabilityBand.Limited => "Limited · 5–9",
        PositionSuitabilityBand.Competent => "Competent · 10–14",
        PositionSuitabilityBand.Strong => "Strong · 15–17",
        PositionSuitabilityBand.Natural => "Natural · 18–20",
        _ => "Unrated"
    };
}

public static class PositionSuitabilityService
{
    private sealed record PositionDefinition(string Code, string Label, string[] Aliases);

    private static readonly IReadOnlyList<PositionDefinition> Positions = new[]
    {
        new PositionDefinition("GK", "Goalkeeper", new[] { "gk", "goalkeeper" }),
        new PositionDefinition("SW", "Sweeper", new[] { "sw", "sweeper" }),
        new PositionDefinition("DL", "Left Defender", new[] { "dl", "leftdefender", "defenderleft" }),
        new PositionDefinition("DC", "Centre Defender", new[] { "dc", "centredefender", "centerdefender", "centreback", "centerback" }),
        new PositionDefinition("DR", "Right Defender", new[] { "dr", "rightdefender", "defenderright" }),
        new PositionDefinition("WBL", "Left Wing-Back", new[] { "wbl", "leftwingback", "wingbackleft" }),
        new PositionDefinition("WBR", "Right Wing-Back", new[] { "wbr", "rightwingback", "wingbackright" }),
        new PositionDefinition("DM", "Defensive Midfielder", new[] { "dm", "defensivemidfielder" }),
        new PositionDefinition("ML", "Left Midfielder", new[] { "ml", "leftmidfielder", "midfielderleft" }),
        new PositionDefinition("MC", "Central Midfielder", new[] { "mc", "centralmidfielder", "centremidfielder", "centermidfielder" }),
        new PositionDefinition("MR", "Right Midfielder", new[] { "mr", "rightmidfielder", "midfielderright" }),
        new PositionDefinition("AML", "Left Attacking Midfielder", new[] { "aml", "leftattackingmidfielder", "attackingmidfielderleft" }),
        new PositionDefinition("AMC", "Central Attacking Midfielder", new[] { "amc", "centralattackingmidfielder", "attackingmidfieldercentral" }),
        new PositionDefinition("AMR", "Right Attacking Midfielder", new[] { "amr", "rightattackingmidfielder", "attackingmidfielderright" }),
        // fmsave exposes the centre-forward slot as STC. Accept legacy "ST" aliases,
        // but always display the canonical source slot.
        new PositionDefinition("STC", "Centre Forward", new[] { "stc", "st", "striker", "centreforward", "centerforward" })
    };

    /// <summary>Canonical, ordered Football Manager position slots, shared by filters and displays.</summary>
    public static IReadOnlyList<string> PositionCodes => Positions.Select(x => x.Code).ToArray();

    public static IReadOnlyList<PositionSuitabilityEntry> MapSourceRatings(
        IEnumerable<KeyValuePair<string, double?>> sourceValues)
    {
        ArgumentNullException.ThrowIfNull(sourceValues);
        var ratings = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sourceValues)
        {
            if (!source.Value.HasValue) continue;
            var value = source.Value.Value;
            if (!double.IsFinite(value) || value is < 1 or > 20) continue;
            var position = ResolvePosition(source.Key);
            if (position is null) continue;
            // Later values take precedence, allowing an explicitly verified live value
            // (added after save facts by the caller) to override a parser value.
            ratings[position.Code] = value;
        }

        return Positions.Select(position =>
        {
            double? rating = ratings.TryGetValue(position.Code, out var value) ? value : null;
            return new PositionSuitabilityEntry(position.Code, position.Label, rating, GetBand(rating));
        }).ToList();
    }

    private static PositionDefinition? ResolvePosition(string? sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey)) return null;
        var segment = sourceKey.Trim();
        var separator = Math.Max(segment.LastIndexOf('.'), segment.LastIndexOf(':'));
        if (separator >= 0 && separator < segment.Length - 1)
            segment = segment[(separator + 1)..];

        var key = new string(segment.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        if (key.StartsWith("position", StringComparison.Ordinal) && key.Length > "position".Length)
            key = key["position".Length..];
        else if (key.StartsWith("pos", StringComparison.Ordinal) && key.Length > "pos".Length)
            key = key["pos".Length..];

        return Positions.FirstOrDefault(position =>
            position.Aliases.Contains(key, StringComparer.OrdinalIgnoreCase));
    }

    private static PositionSuitabilityBand GetBand(double? rating)
    {
        if (!rating.HasValue || !double.IsFinite(rating.Value) || rating.Value is < 1 or > 20)
            return PositionSuitabilityBand.Unrated;
        return rating.Value switch
        {
            < 5 => PositionSuitabilityBand.Low,
            < 10 => PositionSuitabilityBand.Limited,
            < 15 => PositionSuitabilityBand.Competent,
            < 18 => PositionSuitabilityBand.Strong,
            _ => PositionSuitabilityBand.Natural
        };
    }
}
