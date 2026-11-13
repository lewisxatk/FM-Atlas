using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>
/// Curated research references. Atlas does not download, execute or copy these projects.
/// </summary>
public static class ModResearchCatalog
{
    private static readonly IReadOnlyList<ModResearchEntry> Entries = Array.AsReadOnly(new[]
    {
        new ModResearchEntry(
            "fmsave", "Offline save parsing", "https://github.com/rhiever/fmsave",
            "MIT (recorded; verify current LICENSE)", "FM26 / FM27 claims",
            "Typed read-only career snapshots, field coverage and validation reporting.",
            "Parser support for the user's exact Game Pass save build still requires a copied-save test."),
        new ModResearchEntry(
            "fm-editor", "Live memory architecture", "https://github.com/barak14/fm-editor",
            "GPL-3.0-or-later (recorded)", "FM24 / FM26 / FM27 claims",
            "Versioned layouts, identity re-resolution, read/write abstraction and undo design.",
            "Research reference only; no source copied. FM27 build coverage is not universal."),
        new ModResearchEntry(
            "FMLibreEditor", "Game Pass compatibility", "https://github.com/tozmoz/FMLibreEditor",
            "GPL-3.0-or-later (recorded)", "Windows Game Pass / Steam claims",
            "Storefront-specific profiles, save-load generation checks and write safeguards.",
            "Module matching does not prove every field. Atlas live writes remain blocked."),
        new ModResearchEntry(
            "FMSuperScout", "Read-only scouting", "https://github.com/mavarobli/FMSuperScout",
            "MIT (recorded; verify current LICENSE)", "FM26 / FM27 claims",
            "Scanner/core/UI separation, snapshot history, filters and scouting workflows.",
            "Performance and Game Pass claims have not been independently reproduced by Atlas."),
        new ModResearchEntry(
            "gilet", "Save-format research", "https://github.com/fifteen15-labs/gilet",
            "Not assessed", "FM26 sample investigation",
            "Container and multi-frame decompression investigation methodology.",
            "FM26 observations must not be assumed to describe FM27 or later patches."),
        new ModResearchEntry(
            "FMM Editor", "Pre-game database editing", "https://github.com/nyongrand/fmm-editor",
            "Not rechecked", "Extracted database files",
            "Entity models and relationships for pre-game database workflows.",
            "Not a live-career editor; database and active-save editing remain separate."),
        new ModResearchEntry(
            "FM View Exporter", "Visible-data export", "https://github.com/ElectroHugin/FM-View-Exporter",
            "GPL-3.0 (recorded)", "FM27 Windows / Steam documentation",
            "CSV/HTML fixtures from visible in-game player views.",
            "Visible/scouted fields only; Steam documentation is not Game Pass certification."),
        new ModResearchEntry(
            "FMRTE 27", "Commercial UX benchmark", "https://www.fmrte.com/",
            "Commercial / proprietary", "FM27 product documentation",
            "Feature and workflow benchmark for search, player stats, editing and undo.",
            "Public documentation does not reveal proprietary implementation details.")
    });

    public static IReadOnlyList<ModResearchEntry> GetEntries() => Entries;
}
