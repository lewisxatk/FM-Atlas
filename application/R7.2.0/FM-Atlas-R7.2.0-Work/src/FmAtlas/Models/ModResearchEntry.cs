namespace FmAtlas.Models;

/// <summary>
/// Research-only catalogue entry. It is not an install instruction or an executable plugin.
/// Licence text is a recorded research note and must be rechecked before any reuse.
/// </summary>
public sealed record ModResearchEntry(
    string Name,
    string Category,
    string RepositoryUrl,
    string RecordedLicence,
    string Target,
    string AtlasUse,
    string Boundary);
