namespace FmAtlas.Services;

public sealed record SquadArchitectSlotDefinition(
    string SlotId,
    string DisplayName,
    string PositionKey,
    string RoleProfileName);

public sealed record SquadArchitectScoreBreakdown(
    double RoleFitScore,
    double PositionRating,
    double CompositeScore);

public sealed record SquadArchitectResult(
    SquadSelectionResult Selection,
    IReadOnlyDictionary<string, SquadArchitectScoreBreakdown> ScoreBreakdown);

/// <summary>
/// Builds a transparent Atlas-estimated XI from source-backed position ratings and the
/// existing weighted role-fit estimates. Composite score = role-fit estimate × position
/// rating / 20. This is not Sports Interactive's hidden role formula, tactical suitability,
/// match sharpness, availability or selection logic.
/// </summary>
public static class SquadArchitectService
{
    private static readonly IReadOnlyDictionary<string, SquadArchitectSlotDefinition[]> Formations =
        new Dictionary<string, SquadArchitectSlotDefinition[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["4-2-3-1"] =
            [
                new("GK", "Goalkeeper", "gk", "Goalkeeper"),
                new("DL", "Left Back", "dl", "Full-Back"),
                new("DC-L", "Left Centre-Back", "dc", "Centre-Back"),
                new("DC-R", "Right Centre-Back", "dc", "Centre-Back"),
                new("DR", "Right Back", "dr", "Full-Back"),
                new("DM-H", "Holding Pivot", "dm", "Defensive Midfielder"),
                new("DM-P", "Playmaking Pivot", "dm", "Deep-Lying Playmaker"),
                new("AML", "Left Attacking Midfielder", "aml", "Inside Forward"),
                new("AMC", "Central Attacking Midfielder", "amc", "Attacking Midfielder"),
                new("AMR", "Right Attacking Midfielder", "amr", "Inside Forward"),
                new("STC", "Striker", "stc", "Centre Forward")
            ],
            ["4-3-3"] =
            [
                new("GK", "Goalkeeper", "gk", "Goalkeeper"),
                new("DL", "Left Back", "dl", "Full-Back"),
                new("DC-L", "Left Centre-Back", "dc", "Centre-Back"),
                new("DC-R", "Right Centre-Back", "dc", "Centre-Back"),
                new("DR", "Right Back", "dr", "Full-Back"),
                new("DM", "Defensive Midfielder", "dm", "Defensive Midfielder"),
                new("MC-L", "Left Central Midfielder", "mc", "Central Midfielder"),
                new("MC-R", "Right Central Midfielder", "mc", "Deep-Lying Playmaker"),
                new("AML", "Left Forward", "aml", "Inside Forward"),
                new("AMR", "Right Forward", "amr", "Inside Forward"),
                new("STC", "Striker", "stc", "Centre Forward")
            ],
            ["4-4-2"] =
            [
                new("GK", "Goalkeeper", "gk", "Goalkeeper"),
                new("DL", "Left Back", "dl", "Full-Back"),
                new("DC-L", "Left Centre-Back", "dc", "Centre-Back"),
                new("DC-R", "Right Centre-Back", "dc", "Centre-Back"),
                new("DR", "Right Back", "dr", "Full-Back"),
                new("ML", "Left Midfielder", "ml", "Winger"),
                new("MC-L", "Left Central Midfielder", "mc", "Central Midfielder"),
                new("MC-R", "Right Central Midfielder", "mc", "Deep-Lying Playmaker"),
                new("MR", "Right Midfielder", "mr", "Winger"),
                new("STC-L", "Left Striker", "stc", "Centre Forward"),
                new("STC-R", "Right Striker", "stc", "Centre Forward")
            ],
            ["3-4-2-1"] =
            [
                new("GK", "Goalkeeper", "gk", "Goalkeeper"),
                new("DC-L", "Left Centre-Back", "dc", "Centre-Back"),
                new("DC-C", "Central Centre-Back", "dc", "Centre-Back"),
                new("DC-R", "Right Centre-Back", "dc", "Centre-Back"),
                new("WBL", "Left Wing-Back", "wbl", "Wing-Back"),
                new("DM", "Defensive Midfielder", "dm", "Defensive Midfielder"),
                new("MC", "Central Midfielder", "mc", "Deep-Lying Playmaker"),
                new("WBR", "Right Wing-Back", "wbr", "Wing-Back"),
                new("AML", "Left Attacking Midfielder", "aml", "Inside Forward"),
                new("AMR", "Right Attacking Midfielder", "amr", "Inside Forward"),
                new("STC", "Striker", "stc", "Centre Forward")
            ]
        };

    public static IReadOnlyList<string> GetFormationNames()
        => Formations.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<SquadArchitectSlotDefinition> GetFormationSlots(string formation)
    {
        if (string.IsNullOrWhiteSpace(formation) || !Formations.TryGetValue(formation.Trim(), out var slots))
            throw new ArgumentException("Choose a supported formation.", nameof(formation));
        return slots.ToArray();
    }

    public static string GetBreakdownKey(string sourceId, string slotId)
        => sourceId + "\u001f" + slotId;

    public static SquadArchitectResult Optimize(
        string formation,
        IReadOnlyDictionary<string, IReadOnlyList<RoleFitCandidate>> roleCandidatesByProfile,
        IReadOnlyDictionary<string, Dictionary<string, double>> positionRatingsBySourceId)
    {
        ArgumentNullException.ThrowIfNull(roleCandidatesByProfile);
        ArgumentNullException.ThrowIfNull(positionRatingsBySourceId);

        var slots = GetFormationSlots(formation);
        var candidates = new Dictionary<string, (string Name, Dictionary<string, double> Scores)>(StringComparer.OrdinalIgnoreCase);
        var breakdown = new Dictionary<string, SquadArchitectScoreBreakdown>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in slots)
        {
            if (!roleCandidatesByProfile.TryGetValue(slot.RoleProfileName, out var roleCandidates))
                continue;

            foreach (var roleCandidate in roleCandidates)
            {
                if (string.IsNullOrWhiteSpace(roleCandidate.SourceId)
                    || !double.IsFinite(roleCandidate.Score)
                    || roleCandidate.Score < 0
                    || roleCandidate.Score > 20
                    || !positionRatingsBySourceId.TryGetValue(roleCandidate.SourceId, out var sourcePositions))
                    continue;

                var position = PositionSuitabilityService.Build(sourcePositions)
                    .FirstOrDefault(x => x.Key.Equals(slot.PositionKey, StringComparison.OrdinalIgnoreCase));
                if (position?.Rating is not double positionRating || positionRating <= 0)
                    continue;

                var composite = roleCandidate.Score * (positionRating / 20d);
                if (!double.IsFinite(composite) || composite <= 0)
                    continue;

                if (!candidates.TryGetValue(roleCandidate.SourceId, out var candidate))
                    candidate = (roleCandidate.Name, new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));

                candidate.Scores[slot.SlotId] = composite;
                candidates[roleCandidate.SourceId] = candidate;
                breakdown[GetBreakdownKey(roleCandidate.SourceId, slot.SlotId)] =
                    new SquadArchitectScoreBreakdown(roleCandidate.Score, positionRating, composite);
            }
        }

        var selectionCandidates = candidates.Select(x =>
            new SquadSelectionCandidate(x.Key, x.Value.Name, x.Value.Scores)).ToArray();
        var selection = SquadSelectionOptimizer.Optimize(
            slots.Select(x => new SquadSelectionSlot(x.SlotId, x.DisplayName)),
            selectionCandidates);

        return new SquadArchitectResult(selection, breakdown);
    }
}
