namespace FmAtlas.Services;

public sealed record SquadSelectionSlot(string SlotId, string DisplayName);

/// <summary>
/// Candidate scores are Atlas role-fit scores supplied per slot. A missing SlotScores key means
/// the player is not eligible for that slot; eligibility is never guessed from a display position.
/// Scores must be finite and in the 0..20 Atlas role-fit scale.
/// </summary>
public sealed record SquadSelectionCandidate(
    string SourceId,
    string Name,
    IReadOnlyDictionary<string, double> SlotScores);

public sealed record SquadSlotAssignment(
    SquadSelectionSlot Slot,
    SquadSelectionCandidate? Candidate,
    double? Score);

public sealed record SquadSelectionResult(
    IReadOnlyList<SquadSlotAssignment> Assignments,
    IReadOnlyList<SquadSelectionSlot> UnfilledSlots,
    IReadOnlyList<SquadSelectionCandidate> SelectedCandidates,
    double TotalScore)
{
    public int FilledSlots => Assignments.Count(x => x.Candidate is not null);
    public int TotalSlots => Assignments.Count;
}

/// <summary>
/// Maximum-weight one-to-one squad assignment using the rectangular Hungarian algorithm.
/// This is a global assignment: a candidate can occupy at most one slot. Missing score entries
/// are ineligible edges, and dummy columns allow slots to remain unfilled. It does not infer
/// tactical eligibility, availability or FM's internal role rating.
/// </summary>
public static class SquadSelectionOptimizer
{
    private const double UnavailableCost = 1_000_000_000_000d;

    public static SquadSelectionResult Optimize(
        IEnumerable<SquadSelectionSlot> slots,
        IEnumerable<SquadSelectionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(candidates);

        var slotList = slots.ToArray();
        var candidateList = candidates.ToArray();

        if (slotList.Length == 0)
            return new SquadSelectionResult(Array.Empty<SquadSlotAssignment>(),
                Array.Empty<SquadSelectionSlot>(), Array.Empty<SquadSelectionCandidate>(), 0);

        if (slotList.Any(x => x is null || string.IsNullOrWhiteSpace(x.SlotId)))
            throw new ArgumentException("Every squad slot must have a non-empty stable SlotId.", nameof(slots));

        var duplicateSlot = slotList.GroupBy(x => x.SlotId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateSlot is not null)
            throw new ArgumentException("Squad slot IDs must be unique: " + duplicateSlot.Key, nameof(slots));

        if (candidateList.Any(x => x is null || string.IsNullOrWhiteSpace(x.SourceId)))
            throw new ArgumentException("Every candidate must have a non-empty provider-specific SourceId.", nameof(candidates));

        var duplicateCandidate = candidateList.GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateCandidate is not null)
            throw new ArgumentException("Candidate SourceIds must be unique: " + duplicateCandidate.Key, nameof(candidates));

        foreach (var candidate in candidateList)
        {
            if (candidate.SlotScores is null)
                throw new ArgumentException("Candidate slot scores cannot be null.", nameof(candidates));

            foreach (var pair in candidate.SlotScores)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)
                    || !double.IsFinite(pair.Value)
                    || pair.Value < 0
                    || pair.Value > 20)
                {
                    throw new ArgumentOutOfRangeException(nameof(candidates),
                        $"Candidate {candidate.SourceId} has an invalid score for slot '{pair.Key}'. Expected a finite value from 0 to 20.");
                }
            }
        }

        // Stable ordering makes ties reproducible while preserving the caller's slot order.
        candidateList = candidateList
            .OrderBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SourceId, StringComparer.Ordinal)
            .ToArray();

        var maximumScore = candidateList
            .SelectMany(x => x.SlotScores.Values)
            .DefaultIfEmpty(0)
            .Max();

        var rowCount = slotList.Length;
        // One dummy column per row permits every slot to be left unfilled.
        var candidateColumnCount = candidateList.Length;
        var columnCount = candidateColumnCount + rowCount;
        var costs = new double[rowCount + 1, columnCount + 1];

        for (var row = 1; row <= rowCount; row++)
        {
            var slotId = slotList[row - 1].SlotId;
            for (var column = 1; column <= candidateColumnCount; column++)
            {
                var candidate = candidateList[column - 1];
                costs[row, column] = candidate.SlotScores.TryGetValue(slotId, out var score)
                    ? maximumScore - score
                    : UnavailableCost;
            }

            for (var column = candidateColumnCount + 1; column <= columnCount; column++)
                costs[row, column] = maximumScore;
        }

        var rowPotential = new double[rowCount + 1];
        var columnPotential = new double[columnCount + 1];
        var matchedRowForColumn = new int[columnCount + 1];
        var previousColumn = new int[columnCount + 1];

        for (var row = 1; row <= rowCount; row++)
        {
            matchedRowForColumn[0] = row;
            var currentColumn = 0;
            var minimumReducedCost = Enumerable.Repeat(double.PositiveInfinity, columnCount + 1).ToArray();
            var used = new bool[columnCount + 1];

            do
            {
                used[currentColumn] = true;
                var currentRow = matchedRowForColumn[currentColumn];
                var delta = double.PositiveInfinity;
                var nextColumn = 0;

                for (var column = 1; column <= columnCount; column++)
                {
                    if (used[column]) continue;
                    var reducedCost = costs[currentRow, column]
                        - rowPotential[currentRow] - columnPotential[column];

                    if (reducedCost < minimumReducedCost[column])
                    {
                        minimumReducedCost[column] = reducedCost;
                        previousColumn[column] = currentColumn;
                    }

                    if (minimumReducedCost[column] < delta)
                    {
                        delta = minimumReducedCost[column];
                        nextColumn = column;
                    }
                }

                if (!double.IsFinite(delta) || nextColumn == 0)
                    throw new InvalidOperationException("Squad assignment solver could not find a complete slot mapping.");

                for (var column = 0; column <= columnCount; column++)
                {
                    if (used[column])
                    {
                        rowPotential[matchedRowForColumn[column]] += delta;
                        columnPotential[column] -= delta;
                    }
                    else
                    {
                        minimumReducedCost[column] -= delta;
                    }
                }

                currentColumn = nextColumn;
            }
            while (matchedRowForColumn[currentColumn] != 0);

            do
            {
                var nextColumn = previousColumn[currentColumn];
                matchedRowForColumn[currentColumn] = matchedRowForColumn[nextColumn];
                currentColumn = nextColumn;
            }
            while (currentColumn != 0);
        }

        var selectedColumnForRow = new int[rowCount + 1];
        for (var column = 1; column <= columnCount; column++)
        {
            var row = matchedRowForColumn[column];
            if (row != 0) selectedColumnForRow[row] = column;
        }

        var assignments = new List<SquadSlotAssignment>(rowCount);
        var unfilled = new List<SquadSelectionSlot>();
        var selected = new List<SquadSelectionCandidate>();
        var total = 0d;

        for (var row = 1; row <= rowCount; row++)
        {
            var slot = slotList[row - 1];
            var column = selectedColumnForRow[row];
            if (column >= 1 && column <= candidateColumnCount
                && candidateList[column - 1].SlotScores.TryGetValue(slot.SlotId, out var score))
            {
                var candidate = candidateList[column - 1];
                assignments.Add(new SquadSlotAssignment(slot, candidate, score));
                selected.Add(candidate);
                total += score;
            }
            else
            {
                assignments.Add(new SquadSlotAssignment(slot, null, null));
                unfilled.Add(slot);
            }
        }

        return new SquadSelectionResult(assignments, unfilled, selected, total);
    }
}
