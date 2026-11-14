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

public sealed record AdvancedSearchProfile(
    string Id,
    string Name,
    DateTime UpdatedUtc,
    string SearchText,
    bool MatchAll,
    IReadOnlyList<AdvancedSearchCriterion> Criteria);
