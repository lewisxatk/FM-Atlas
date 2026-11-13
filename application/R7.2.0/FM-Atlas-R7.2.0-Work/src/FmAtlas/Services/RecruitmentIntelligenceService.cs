namespace FmAtlas.Services;

public enum RecruitmentBudgetAssessment
{
    WithinBudget,
    OverBudget,
    DataUnavailable,
    BudgetNotConfigured
}

public sealed record RecruitmentSlotNeed(
    string SlotId,
    string DisplayName,
    double? IncumbentAtlasScore);

public sealed record RecruitmentTargetCandidate(
    string SourceId,
    string Name,
    IReadOnlyDictionary<string, double> SlotScores,
    decimal? MarketValue = null,
    decimal? AskingPrice = null,
    decimal? WeeklyWage = null);

public sealed record RecruitmentBudgetLimit(
    decimal? TransferBudget,
    decimal? WeeklyWageBudget);

public sealed record RecruitmentOpportunity(
    string SourceId,
    string Name,
    string SlotId,
    string SlotName,
    double CandidateAtlasScore,
    double? IncumbentAtlasScore,
    double? Improvement,
    decimal? MarketValue,
    decimal? AskingPrice,
    decimal? WeeklyWage,
    RecruitmentBudgetAssessment TransferBudgetAssessment,
    RecruitmentBudgetAssessment WageBudgetAssessment);

/// <summary>
/// Compares source-backed Atlas role-fit scores with a slot incumbent and evaluates only
/// explicitly supplied asking-price and wage data against their separate budgets.
/// Market value is informational and is never substituted for a fee or asking price.
/// </summary>
public static class RecruitmentIntelligenceService
{
    public static IReadOnlyList<RecruitmentOpportunity> RankOpportunities(
        IEnumerable<RecruitmentSlotNeed> slotNeeds,
        IEnumerable<RecruitmentTargetCandidate> candidates,
        RecruitmentBudgetLimit budget)
    {
        ArgumentNullException.ThrowIfNull(slotNeeds);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(budget);

        ValidateMoney(budget.TransferBudget, nameof(budget.TransferBudget));
        ValidateMoney(budget.WeeklyWageBudget, nameof(budget.WeeklyWageBudget));

        var slots = slotNeeds.ToArray();
        var targets = candidates.ToArray();

        if (slots.Any(x => x is null || string.IsNullOrWhiteSpace(x.SlotId)))
            throw new ArgumentException("Every recruitment slot must have a stable ID.", nameof(slotNeeds));
        if (slots.GroupBy(x => x.SlotId, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new ArgumentException("Recruitment slot IDs must be unique.", nameof(slotNeeds));
        foreach (var slot in slots)
            ValidateScore(slot.IncumbentAtlasScore, "incumbent " + slot.SlotId);

        if (targets.Any(x => x is null || string.IsNullOrWhiteSpace(x.SourceId) || string.IsNullOrWhiteSpace(x.Name)))
            throw new ArgumentException("Every target must have a source identity and name.", nameof(candidates));
        if (targets.GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new ArgumentException("Target SourceIds must be unique.", nameof(candidates));

        var results = new List<RecruitmentOpportunity>();
        foreach (var target in targets)
        {
            if (target.SlotScores is null)
                throw new ArgumentException("Target slot scores cannot be null.", nameof(candidates));

            ValidateMoney(target.MarketValue, nameof(target.MarketValue));
            ValidateMoney(target.AskingPrice, nameof(target.AskingPrice));
            ValidateMoney(target.WeeklyWage, nameof(target.WeeklyWage));

            foreach (var pair in target.SlotScores)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Target slot score keys cannot be blank.", nameof(candidates));
                ValidateScore(pair.Value, target.SourceId + "/" + pair.Key);
            }

            foreach (var slot in slots)
            {
                if (!target.SlotScores.TryGetValue(slot.SlotId, out var score))
                    continue;

                results.Add(new RecruitmentOpportunity(
                    target.SourceId,
                    target.Name,
                    slot.SlotId,
                    slot.DisplayName,
                    score,
                    slot.IncumbentAtlasScore,
                    slot.IncumbentAtlasScore.HasValue ? score - slot.IncumbentAtlasScore.Value : null,
                    target.MarketValue,
                    target.AskingPrice,
                    target.WeeklyWage,
                    Assess(target.AskingPrice, budget.TransferBudget),
                    Assess(target.WeeklyWage, budget.WeeklyWageBudget)));
            }
        }

        return results
            .OrderByDescending(x => x.Improvement ?? double.NegativeInfinity)
            .ThenByDescending(x => x.CandidateAtlasScore)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SlotName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static RecruitmentBudgetAssessment Assess(decimal? cost, decimal? budget)
    {
        if (!budget.HasValue) return RecruitmentBudgetAssessment.BudgetNotConfigured;
        if (!cost.HasValue) return RecruitmentBudgetAssessment.DataUnavailable;
        return cost.Value <= budget.Value
            ? RecruitmentBudgetAssessment.WithinBudget
            : RecruitmentBudgetAssessment.OverBudget;
    }

    private static void ValidateMoney(decimal? value, string field)
    {
        if (value.HasValue && value.Value < 0)
            throw new ArgumentOutOfRangeException(field, "Financial values cannot be negative.");
    }

    private static void ValidateScore(double? score, string field)
    {
        if (score.HasValue && (!double.IsFinite(score.Value) || score.Value < 0 || score.Value > 20))
            throw new ArgumentOutOfRangeException(field, "Atlas role-fit scores must be finite values from 0 to 20.");
    }
}
