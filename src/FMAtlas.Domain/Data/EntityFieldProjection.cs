using FMAtlas.Domain.Entities;

namespace FMAtlas.Domain.Data;

public enum EntityFieldValueKind
{
    Text,
    Integer,
    Decimal,
    Date,
    Boolean
}

/// <summary>
/// One non-null typed field and its evidence. Exactly one value slot must match
/// ValueKind. Boolean values are represented explicitly here and stored as 0/1.
/// </summary>
public sealed record EntityFieldProjection(
    string Key,
    EntityFieldValueKind ValueKind,
    string? TextValue,
    long? IntegerValue,
    decimal? DecimalValue,
    DateOnly? DateValue,
    bool? BooleanValue,
    FieldProvenance Provenance)
{
    public bool HasValidValueShape()
    {
        var presentCount =
            (TextValue is not null ? 1 : 0) +
            (IntegerValue is not null ? 1 : 0) +
            (DecimalValue is not null ? 1 : 0) +
            (DateValue is not null ? 1 : 0) +
            (BooleanValue is not null ? 1 : 0);

        if (presentCount != 1)
            return false;

        return ValueKind switch
        {
            EntityFieldValueKind.Text => TextValue is not null,
            EntityFieldValueKind.Integer => IntegerValue is not null,
            EntityFieldValueKind.Decimal => DecimalValue is not null,
            EntityFieldValueKind.Date => DateValue is not null,
            EntityFieldValueKind.Boolean => BooleanValue is not null,
            _ => false
        };
    }
}

public sealed record EntityRecordProjection(
    EntityIdentity Identity,
    string? DisplayName,
    IReadOnlyList<EntityFieldProjection> Fields);
