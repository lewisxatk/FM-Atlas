using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Domain.Clubs;

/// <summary>
/// Typed club record. Finance, facility and other field keys are provider-defined
/// until their FM27 meanings and mappings have been individually verified.
/// </summary>
public sealed record ClubRecord(EntityIdentity Identity) : IEntityRecord
{
    public SourcedValue<string>? Name { get; init; }
    public SourcedValue<string>? ShortName { get; init; }
    public SourcedValue<string>? NationName { get; init; }
    public SourcedValue<string>? CompetitionName { get; init; }
    public SourcedValue<string>? StadiumName { get; init; }
    public SourcedValue<int>? FoundedYear { get; init; }
    public SourcedValue<int>? Reputation { get; init; }

    public IReadOnlyDictionary<string, SourcedValue<decimal>> Finances { get; init; } =
        new Dictionary<string, SourcedValue<decimal>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SourcedValue<int>> Facilities { get; init; } =
        new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Additional typed fields supplied by a verified importer.</summary>
    public IReadOnlyDictionary<string, SourcedValue<int>> SearchIntegerFields { get; init; } =
        new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, SourcedValue<decimal>> SearchDecimalFields { get; init; } =
        new Dictionary<string, SourcedValue<decimal>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, SourcedValue<bool>> SearchBooleanFields { get; init; } =
        new Dictionary<string, SourcedValue<bool>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, SourcedValue<DateOnly>> SearchDateFields { get; init; } =
        new Dictionary<string, SourcedValue<DateOnly>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, SourcedValue<string>> SearchTextFields { get; init; } =
        new Dictionary<string, SourcedValue<string>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SourcedValue<string>> ExtendedFields { get; init; } =
        new Dictionary<string, SourcedValue<string>>(StringComparer.OrdinalIgnoreCase);
}
