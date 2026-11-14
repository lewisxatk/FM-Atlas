namespace FmAtlas.Models;

public sealed record StatMetricField(string Key, string DisplayName, string Category, string SourceTables, int RecordsWithValue, bool SupportsPer90)
{
    public string PickerLabel => $"{Category} · {DisplayName}";
}

public sealed record StatFilterCriterion(string Key, string DisplayName, double? Minimum, double? Maximum);

public sealed record StatSearchResult(string SourceId, string Name, string? Club, string Period, double? Minutes, string ValuesSummary);

public sealed record AttributeSearchCriterion(string Key, string DisplayName, double? Minimum, double? Maximum);


public enum AdvancedSearchFieldType
{
    Number,
    Text,
    Boolean
}

public sealed record AdvancedSearchField(
    string Key,
    string DisplayName,
    string Category,
    AdvancedSearchFieldType FieldType,
    string Source)
{
    public string PickerLabel => $"{Category} · {DisplayName}";
}

public sealed record AdvancedSearchCriterion(
    string FieldKey,
    double? Minimum,
    double? Maximum,
    string? TextValue,
    bool? BooleanValue);

public sealed record AdvancedSearchSort(string FieldKey, bool Descending = false);

public sealed record AdvancedPositionCriterion(string PositionCode, int Rating);

public sealed record AdvancedPositionFilter(
    bool MatchAny = false,
    bool AtLeast = false,
    IReadOnlyList<AdvancedPositionCriterion>? Positions = null);

public static class AdvancedPositionFilterRules
{
    private static readonly HashSet<string> Allowed = new(PositionSuitabilityService.PositionCodes, StringComparer.OrdinalIgnoreCase);

    public static AdvancedPositionFilter? Normalize(AdvancedPositionFilter? filter)
    {
        if (filter is null || filter.Positions is null || filter.Positions.Count == 0) return null;
        var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in filter.Positions)
        {
            if (item is null) continue;
            var code = item.PositionCode?.Trim() ?? string.Empty;
            if (code.Equals("ST", StringComparison.OrdinalIgnoreCase)) code = "STC";
            if (!Allowed.Contains(code)) throw new ArgumentException($"Unsupported FM position code: {item.PositionCode}", nameof(filter));
            if (item.Rating is < 0 or > 20) throw new ArgumentOutOfRangeException(nameof(filter), "Position ratings must be whole numbers from 0 to 20.");
            if (item.Rating == 0) continue;
            normalized[code.ToUpperInvariant()] = item.Rating;
        }
        if (normalized.Count == 0) return null;
        var positions = normalized.OrderBy(x => Array.IndexOf(PositionSuitabilityService.PositionCodes.ToArray(), x.Key))
            .Select(x => new AdvancedPositionCriterion(x.Key, x.Value)).ToArray();
        return new AdvancedPositionFilter(filter.MatchAny, filter.AtLeast, positions);
    }
}

public sealed record AdvancedSearchProfile(
    string Id,
    string Name,
    DateTime UpdatedUtc,
    string SearchText,
    bool MatchAll,
    IReadOnlyList<AdvancedSearchCriterion> Criteria,
    IReadOnlyList<string>? VisibleColumns = null,
    IReadOnlyList<AdvancedSearchSort>? Sorts = null, AdvancedPositionFilter? PositionFilter = null);
