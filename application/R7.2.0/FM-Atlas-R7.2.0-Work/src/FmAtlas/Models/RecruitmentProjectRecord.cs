namespace FmAtlas.Models;

public sealed record RecruitmentProjectRecord(
    string Name,
    string Status,
    string? Owner,
    IReadOnlyList<string> Tags,
    decimal? TransferBudget,
    decimal? WeeklyWageBudget,
    DateTime CreatedUtc,
    int CandidateCount);
