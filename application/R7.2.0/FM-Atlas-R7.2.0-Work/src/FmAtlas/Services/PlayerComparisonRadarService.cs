namespace FmAtlas.Services;

public sealed record PlayerComparisonRadarSeries(
    string PlayerName,
    IReadOnlyList<double> Values,
    IReadOnlyList<int> AttributeCounts);

public sealed record PlayerComparisonRadarData(
    IReadOnlyList<string> Categories,
    IReadOnlyList<PlayerComparisonRadarSeries> Series,
    string Message)
{
    public bool CanRender => Categories.Count >= 3 && Series.Count >= 2;
}

/// <summary>
/// Builds fair per-player attribute-group averages for a comparison radar.
/// A category is included only when every compared player has at least one valid
/// source-backed 1–20 rating in that category. Hidden and personality fields are excluded.
/// </summary>
public static class PlayerComparisonRadarService
{
    private static readonly string[] CategoryOrder =
        { "Technical", "Set Pieces", "Mental", "Physical", "Goalkeeping" };

    public static PlayerComparisonRadarData Build(
        IReadOnlyList<string> playerNames,
        IReadOnlyList<Dictionary<string, double>> attributeSets)
    {
        ArgumentNullException.ThrowIfNull(playerNames);
        ArgumentNullException.ThrowIfNull(attributeSets);
        if (playerNames.Count != attributeSets.Count)
            throw new ArgumentException("Each player needs exactly one attribute dictionary.", nameof(attributeSets));
        if (playerNames.Count is < 2 or > 4)
            throw new ArgumentOutOfRangeException(nameof(playerNames), "Compare two to four players.");

        var normalizedSets = attributeSets.Select(NormalizeValues).ToList();
        var categories = new List<string>();
        var perPlayerValues = Enumerable.Range(0, playerNames.Count)
            .Select(_ => new List<double>())
            .ToList();
        var perPlayerCounts = Enumerable.Range(0, playerNames.Count)
            .Select(_ => new List<int>())
            .ToList();

        foreach (var category in CategoryOrder)
        {
            var fields = PlayerAttributeSchema.Fields
                .Where(field => field.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (fields.Count == 0) continue;

            var categoryValues = new List<(double Average, int Count)>();
            var common = true;
            for (var playerIndex = 0; playerIndex < normalizedSets.Count; playerIndex++)
            {
                var available = fields
                    .Select(field => normalizedSets[playerIndex].TryGetValue(Normalize(field.Label), out var value)
                        ? (double?)value : null)
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .ToList();
                if (available.Count == 0)
                {
                    common = false;
                    break;
                }
                categoryValues.Add((available.Average(), available.Count));
            }

            if (!common) continue;
            categories.Add(category);
            for (var playerIndex = 0; playerIndex < normalizedSets.Count; playerIndex++)
            {
                perPlayerValues[playerIndex].Add(categoryValues[playerIndex].Average);
                perPlayerCounts[playerIndex].Add(categoryValues[playerIndex].Count);
            }
        }

        var series = playerNames.Select((name, index) =>
            new PlayerComparisonRadarSeries(name, perPlayerValues[index], perPlayerCounts[index])).ToList();
        var message = categories.Count >= 3
            ? "Only shared source-backed categories are plotted."
            : "At least three attribute groups must contain valid imported ratings for every compared player. Missing categories are not scored as zero.";
        return new PlayerComparisonRadarData(categories, series, message);
    }

    private static Dictionary<string, double> NormalizeValues(IReadOnlyDictionary<string, double> source)
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            if (!double.IsFinite(pair.Value) || pair.Value is < 1 or > 20) continue;
            var key = Normalize(pair.Key);
            if (key.Length > 0) values[key] = pair.Value;
        }
        return values;
    }

    private static string Normalize(string value)
    {
        var separator = value.LastIndexOf(" · ", StringComparison.Ordinal);
        var label = separator >= 0 ? value[(separator + 3)..] : value;
        return new string(label.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
