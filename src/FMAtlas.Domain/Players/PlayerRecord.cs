using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Domain.Players;

/// <summary>
/// Typed player record shared by save, export and live-read providers.
/// Nullable sourced fields mean unavailable/not imported; no default FM values
/// are fabricated. Attribute dictionary keys use PlayerAttributeCatalog keys.
/// </summary>
public sealed record PlayerRecord(EntityIdentity Identity)
{
    public SourcedValue<string>? FullName { get; init; }
    public SourcedValue<DateOnly>? DateOfBirth { get; init; }
    public SourcedValue<int>? Age { get; init; }
    public SourcedValue<string>? Nationality { get; init; }
    public SourcedValue<string>? SecondNationality { get; init; }
    public SourcedValue<string>? ClubName { get; init; }
    public SourcedValue<string>? TeamName { get; init; }
    public SourcedValue<string>? LeagueName { get; init; }
    public SourcedValue<string>? PrimaryPosition { get; init; }
    public SourcedValue<string>? PreferredFoot { get; init; }
    public SourcedValue<decimal>? HeightCentimetres { get; init; }
    public SourcedValue<decimal>? WeightKilograms { get; init; }
    public SourcedValue<int>? CurrentAbility { get; init; }
    public SourcedValue<int>? PotentialAbility { get; init; }
    public SourcedValue<DateOnly>? ContractExpires { get; init; }
    public SourcedValue<decimal>? Wage { get; init; }
    public SourcedValue<decimal>? TransferValue { get; init; }

    public IReadOnlyDictionary<string, SourcedValue<int>> Attributes { get; init; } =
        new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SourcedValue<int>> Personality { get; init; } =
        new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SourcedValue<int>> PositionRatings { get; init; } =
        new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<SourcedValue<string>> PreferredMoves { get; init; } =
        Array.Empty<SourcedValue<string>>();

    public IReadOnlyDictionary<string, SourcedValue<decimal>> SeasonStatistics { get; init; } =
        new Dictionary<string, SourcedValue<decimal>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SourcedValue<string>> ExtendedFields { get; init; } =
        new Dictionary<string, SourcedValue<string>>(StringComparer.OrdinalIgnoreCase);
}
