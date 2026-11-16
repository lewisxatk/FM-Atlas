namespace FMAtlas.Application.Models;

public sealed record LiveGameLoadProgress(
    int Percent,
    string Stage,
    string Detail);
