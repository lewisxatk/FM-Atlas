namespace FMAtlas.Domain.Search;

/// <summary>
/// A portable named set of Advanced Search criteria. Version is the preset
/// format version, not the Football Manager database version.
/// </summary>
public sealed record AdvancedSearchPreset(
    int Version,
    string Name,
    SearchEntityKind Entity,
    SearchFilterLogic Logic,
    SearchFilterLogic PositionLogic,
    IReadOnlyList<SearchFilterCondition> Conditions);
