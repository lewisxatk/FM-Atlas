namespace FmAtlas.Services;

public sealed record TacticalFormationSlot(
    string SlotId,
    string DisplayName,
    string RoleProfileName,
    IReadOnlyList<string> AllowedPrimaryPositions);

/// <summary>
/// Transparent formation templates for Atlas's role-fit optimiser. Eligibility is limited to
/// the imported primary position; secondary positions and FM's tactical familiarity are not inferred.
/// </summary>
public static class TacticalFormationService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<TacticalFormationSlot>> Templates =
        new Dictionary<string, IReadOnlyList<TacticalFormationSlot>>(StringComparer.OrdinalIgnoreCase)
        {
            ["4-3-3"] = Array.AsReadOnly(new[]
            {
                Slot("GK","Goalkeeper","Goalkeeper","GK"),
                Slot("RB","Right Back","Full-Back","DR","WBR"),
                Slot("RCB","Right Centre-Back","Centre-Back","DC","SW"),
                Slot("LCB","Left Centre-Back","Centre-Back","DC","SW"),
                Slot("LB","Left Back","Full-Back","DL","WBL"),
                Slot("DM","Defensive Midfielder","Defensive Midfielder","DM"),
                Slot("RCM","Right Central Midfielder","Central Midfielder","MC"),
                Slot("LCM","Left Central Midfielder","Central Midfielder","MC"),
                Slot("RW","Right Winger","Winger","AMR","MR"),
                Slot("LW","Left Winger","Winger","AML","ML"),
                Slot("ST","Striker","Centre Forward","ST","STC")
            }),
            ["4-2-3-1"] = Array.AsReadOnly(new[]
            {
                Slot("GK","Goalkeeper","Goalkeeper","GK"),
                Slot("RB","Right Back","Full-Back","DR","WBR"),
                Slot("RCB","Right Centre-Back","Centre-Back","DC","SW"),
                Slot("LCB","Left Centre-Back","Centre-Back","DC","SW"),
                Slot("LB","Left Back","Full-Back","DL","WBL"),
                Slot("RDM","Right Defensive Midfielder","Defensive Midfielder","DM"),
                Slot("LDM","Left Defensive Midfielder","Defensive Midfielder","DM"),
                Slot("RW","Right Winger","Winger","AMR","MR"),
                Slot("AMC","Attacking Midfielder","Attacking Midfielder","AMC"),
                Slot("LW","Left Winger","Winger","AML","ML"),
                Slot("ST","Striker","Centre Forward","ST","STC")
            }),
            ["4-4-2"] = Array.AsReadOnly(new[]
            {
                Slot("GK","Goalkeeper","Goalkeeper","GK"),
                Slot("RB","Right Back","Full-Back","DR","WBR"),
                Slot("RCB","Right Centre-Back","Centre-Back","DC","SW"),
                Slot("LCB","Left Centre-Back","Centre-Back","DC","SW"),
                Slot("LB","Left Back","Full-Back","DL","WBL"),
                Slot("RM","Right Midfielder","Winger","MR","AMR"),
                Slot("RCM","Right Central Midfielder","Central Midfielder","MC"),
                Slot("LCM","Left Central Midfielder","Central Midfielder","MC"),
                Slot("LM","Left Midfielder","Winger","ML","AML"),
                Slot("RST","Right Striker","Centre Forward","ST","STC"),
                Slot("LST","Left Striker","Centre Forward","ST","STC")
            }),
            ["3-5-2"] = Array.AsReadOnly(new[]
            {
                Slot("GK","Goalkeeper","Goalkeeper","GK"),
                Slot("RCB","Right Centre-Back","Centre-Back","DC","SW"),
                Slot("CB","Centre-Back","Centre-Back","DC","SW"),
                Slot("LCB","Left Centre-Back","Centre-Back","DC","SW"),
                Slot("RWB","Right Wing-Back","Wing-Back","WBR","DR"),
                Slot("LWB","Left Wing-Back","Wing-Back","WBL","DL"),
                Slot("DM","Defensive Midfielder","Defensive Midfielder","DM"),
                Slot("RCM","Right Central Midfielder","Central Midfielder","MC"),
                Slot("LCM","Left Central Midfielder","Central Midfielder","MC"),
                Slot("RST","Right Striker","Centre Forward","ST","STC"),
                Slot("LST","Left Striker","Centre Forward","ST","STC")
            })
        };

    public static IReadOnlyList<string> GetFormations()
        => Templates.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<TacticalFormationSlot> GetSlots(string formation)
    {
        if (string.IsNullOrWhiteSpace(formation) || !Templates.TryGetValue(formation.Trim(), out var slots))
            throw new ArgumentException("Choose a supported Atlas formation.", nameof(formation));
        return slots;
    }

    private static TacticalFormationSlot Slot(string id, string label, string role, params string[] positions)
        => new(id, label, role, Array.AsReadOnly(positions));
}
