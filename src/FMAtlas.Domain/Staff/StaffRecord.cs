using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Domain.Staff;

/// <summary>
/// Typed staff record. Attribute and knowledge keys remain source-catalogue
/// identifiers; absent fields stay absent until a provider supplies evidence.
/// </summary>
public sealed record StaffRecord(EntityIdentity Identity)
{
    public SourcedValue<string>? FullName { get; init; }
    public SourcedValue<DateOnly>? DateOfBirth { get; init; }
    public SourcedValue<int>? Age { get; init; }
    public SourcedValue<string>? Nationality { get; init; }
    public SourcedValue<string>? ClubName { get; init; }
    public SourcedValue<string>? PrimaryRole { get; init; }
    public SourcedValue<int>? CurrentAbility { get; init; }
    public SourcedValue<int>? PotentialAbility { get; init; }
    public SourcedValue<DateOnly>? ContractExpires { get; init; }
    public SourcedValue<decimal>? Wage { get; init; }

    public IReadOnlyDictionary<string, SourcedValue<int>> Attributes { get; init; } =
        new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SourcedValue<int>> Knowledge { get; init; } =
        new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SourcedValue<string>> ExtendedFields { get; init; } =
        new Dictionary<string, SourcedValue<string>>(StringComparer.OrdinalIgnoreCase);
}
