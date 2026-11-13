namespace FmAtlas;

/// <summary>
/// Temporary no-op source marker used only to trigger the Windows integration
/// workflow against the current overhaul branch. Do not merge this validation branch.
/// </summary>
internal static class IntegratedStagesValidationProbe
{
    internal const string Scope = "FM Atlas integrated stages 3-7 validation";
}
// Re-trigger validation against the corrected cohort-specific DTOs and current base.
// Refresh validation after WPF captured-control initialization fixes.
