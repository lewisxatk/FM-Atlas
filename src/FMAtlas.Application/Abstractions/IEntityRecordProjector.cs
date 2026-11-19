using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Application.Abstractions;

public interface IEntityRecordProjector
{
    EntityRecordProjection Project(IEntityRecord record);
}
