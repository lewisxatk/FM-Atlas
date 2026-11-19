namespace FMAtlas.Domain.Entities;

/// <summary>Implemented by the typed Player, Staff and Club record models.</summary>
public interface IEntityRecord
{
    EntityIdentity Identity { get; }
}
