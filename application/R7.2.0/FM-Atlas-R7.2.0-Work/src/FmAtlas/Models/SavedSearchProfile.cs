namespace FmAtlas.Models;

public enum SavedSearchKind
{
    Statistics,
    Attributes
}

/// <summary>A typed numeric condition stored in a reusable search profile.</summary>
public sealed record SavedSearchCondition(
    string Key,
    string DisplayName,
    double? Minimum,
    double? Maximum);

/// <summary>
/// Local-only reusable criteria. Profiles contain field keys and filter settings only;
/// they never store player rows, career-save content or imported source data.
/// </summary>
public sealed record SavedSearchProfile(
    string Id,
    string Name,
    SavedSearchKind Kind,
    string? TextFilter,
    bool MatchAny,
    bool Per90,
    int MinimumMinutes,
    IReadOnlyList<SavedSearchCondition> Conditions,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
