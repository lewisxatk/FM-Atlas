namespace FmAtlas.Services;

public sealed record TacticalShapeSlot(string SlotId, string DisplayName, string PositionKey, string RoleProfileName);
public sealed record TacticalShapeDefinition(string Name, string Phase, IReadOnlyList<TacticalShapeSlot> Slots);
public sealed record TacticalPhaseSquadResult(string ShapeName, string Phase, SquadSelectionResult Selection, IReadOnlyDictionary<string, SquadArchitectScoreBreakdown> ScoreBreakdown);
public sealed record TacticalSquadComparisonResult(TacticalPhaseSquadResult InPossession, TacticalPhaseSquadResult OutOfPossession, IReadOnlyList<string> SharedPlayerSourceIds);

/// <summary>
/// Produces separate in-possession and out-of-possession Atlas squad assignments from
/// supplied shapes, source-backed position ratings and transparent role-fit estimates.
/// Does not calculate FM tactical effectiveness, tactical understanding or player buy-in.
/// </summary>
public static class TacticalSquadAnalysisService
{
    public static TacticalShapeDefinition GetDefaultInPossessionShape()
        => new("4-3-3 · In Possession", "In Possession",
        [
            new("GK", "Goalkeeper", "gk", "Goalkeeper"),
            new("DL", "Left Full-Back", "dl", "Full-Back"),
            new("DC-L", "Left Centre-Back", "dc", "Ball-Playing Centre-Back"),
            new("DC-R", "Right Centre-Back", "dc", "Ball-Playing Centre-Back"),
            new("DR", "Right Full-Back", "dr", "Full-Back"),
            new("DM", "Holding Midfielder", "dm", "Defensive Midfielder"),
            new("MC-L", "Left Central Midfielder", "mc", "Central Midfielder"),
            new("MC-R", "Right Central Midfielder", "mc", "Deep-Lying Playmaker"),
            new("AML", "Left Winger", "aml", "Winger"),
            new("AMR", "Right Inside Forward", "amr", "Inside Forward"),
            new("STC", "Centre Forward", "stc", "Centre Forward")
        ]);

    public static TacticalShapeDefinition GetDefaultOutOfPossessionShape()
        => new("4-4-2 · Out of Possession", "Out of Possession",
        [
            new("GK", "Goalkeeper", "gk", "Goalkeeper"),
            new("DL", "Left Pressing Full-Back", "dl", "Pressing Full-Back"),
            new("DC-L", "Left Covering Centre-Back", "dc", "Covering Centre-Back"),
            new("DC-R", "Right Covering Centre-Back", "dc", "Covering Centre-Back"),
            new("DR", "Right Pressing Full-Back", "dr", "Pressing Full-Back"),
            new("ML", "Left Pressing Midfielder", "ml", "Pressing Midfielder"),
            new("MC-L", "Ball-Winning Midfielder", "mc", "Ball-Winning Midfielder"),
            new("MC-R", "Screening Midfielder", "mc", "Screening Midfielder"),
            new("MR", "Right Pressing Midfielder", "mr", "Pressing Midfielder"),
            new("STC-L", "Left Pressing Forward", "stc", "Pressing Forward"),
            new("STC-R", "Right Pressing Forward", "stc", "Pressing Forward")
        ]);

    public static TacticalSquadComparisonResult Compare(
        TacticalShapeDefinition inPossessionShape,
        TacticalShapeDefinition outOfPossessionShape,
        IReadOnlyDictionary<string, IReadOnlyList<RoleFitCandidate>> roleCandidatesByProfile,
        IReadOnlyDictionary<string, Dictionary<string, double>> positionRatingsBySourceId)
    {
        ArgumentNullException.ThrowIfNull(inPossessionShape);
        ArgumentNullException.ThrowIfNull(outOfPossessionShape);
        ArgumentNullException.ThrowIfNull(roleCandidatesByProfile);
        ArgumentNullException.ThrowIfNull(positionRatingsBySourceId);

        var inPossession = Optimize(inPossessionShape, "In Possession", roleCandidatesByProfile, positionRatingsBySourceId);
        var outOfPossession = Optimize(outOfPossessionShape, "Out of Possession", roleCandidatesByProfile, positionRatingsBySourceId);

        var inIds = inPossession.Selection.SelectedCandidates.Select(x => x.SourceId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shared = outOfPossession.Selection.SelectedCandidates.Select(x => x.SourceId)
            .Where(inIds.Contains).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

        return new TacticalSquadComparisonResult(inPossession, outOfPossession, shared);
    }

    public static TacticalPhaseSquadResult Optimize(
        TacticalShapeDefinition shape,
        string requiredPhase,
        IReadOnlyDictionary<string, IReadOnlyList<RoleFitCandidate>> roleCandidatesByProfile,
        IReadOnlyDictionary<string, Dictionary<string, double>> positionRatingsBySourceId)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(roleCandidatesByProfile);
        ArgumentNullException.ThrowIfNull(positionRatingsBySourceId);

        if (string.IsNullOrWhiteSpace(shape.Name) || shape.Slots is null || shape.Slots.Count == 0)
            throw new ArgumentException("A named tactical shape must contain at least one slot.", nameof(shape));
        if (!string.Equals(shape.Phase, requiredPhase, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The tactical shape phase does not match the requested analysis phase.", nameof(shape));
        if (!requiredPhase.Equals("In Possession", StringComparison.OrdinalIgnoreCase)
            && !requiredPhase.Equals("Out of Possession", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose In Possession or Out of Possession.", nameof(requiredPhase));

        var profiles = RoleFitService.GetProfiles().ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var candidates = new Dictionary<string, (string Name, Dictionary<string, double> Scores)>(StringComparer.OrdinalIgnoreCase);
        var breakdown = new Dictionary<string, SquadArchitectScoreBreakdown>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in shape.Slots)
        {
            if (slot is null || string.IsNullOrWhiteSpace(slot.SlotId)
                || string.IsNullOrWhiteSpace(slot.PositionKey) || string.IsNullOrWhiteSpace(slot.RoleProfileName))
                throw new ArgumentException("Every tactical slot must have an ID, position key and role profile.", nameof(shape));
            if (!profiles.TryGetValue(slot.RoleProfileName, out var profile))
                throw new ArgumentException("Unknown role profile: " + slot.RoleProfileName, nameof(shape));
            if (!profile.Phase.Equals("Both", StringComparison.OrdinalIgnoreCase)
                && !profile.Phase.Equals(requiredPhase, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Role '" + profile.Name + "' belongs to " + profile.Phase + ", not " + requiredPhase + ".", nameof(shape));
            if (!roleCandidatesByProfile.TryGetValue(profile.Name, out var roleCandidates))
                continue;

            foreach (var candidate in roleCandidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.SourceId)
                    || !double.IsFinite(candidate.Score)
                    || candidate.Score < 0 || candidate.Score > 20
                    || !positionRatingsBySourceId.TryGetValue(candidate.SourceId, out var positions))
                    continue;

                var position = PositionSuitabilityService.Build(positions)
                    .FirstOrDefault(x => x.Key.Equals(slot.PositionKey, StringComparison.OrdinalIgnoreCase));
                if (position?.Rating is not double positionRating || positionRating <= 0)
                    continue;

                var composite = candidate.Score * (positionRating / 20d);
                if (!double.IsFinite(composite) || composite <= 0) continue;

                if (!candidates.TryGetValue(candidate.SourceId, out var entry))
                    entry = (candidate.Name, new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));
                entry.Scores[slot.SlotId] = composite;
                candidates[candidate.SourceId] = entry;
                breakdown[SquadArchitectService.GetBreakdownKey(candidate.SourceId, slot.SlotId)] =
                    new SquadArchitectScoreBreakdown(candidate.Score, positionRating, composite);
            }
        }

        var selection = SquadSelectionOptimizer.Optimize(
            shape.Slots.Select(x => new SquadSelectionSlot(x.SlotId, x.DisplayName)),
            candidates.Select(x => new SquadSelectionCandidate(x.Key, x.Value.Name, x.Value.Scores)));
        return new TacticalPhaseSquadResult(shape.Name, requiredPhase, selection, breakdown);
    }
}
