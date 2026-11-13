namespace FmAtlas.Services;

/// <summary>Exact FM27 Windows game_plugin.dll fingerprints and entity-array RVAs.</summary>
/// <remarks>
/// Profile data is an independently authored registry based on public build observations.
/// It selects the profile-specific module addresses. A match is necessary but not sufficient for writing; each live field has its own whitelist, identity and value preflight, and read-back checks.
/// </remarks>
public sealed record Fm27BuildProfile(
    string Id,
    string DisplayName,
    string Storefront,
    uint SizeOfImage,
    uint LinkTimestamp,
    ulong PlayersArrayRva,
    ulong StaffArrayRva,
    ulong ClubsArrayRva,
    ulong CurrentDateRva,
    ulong GameUiRva = 0)
{
    public const ulong SaveLoadCounterOffset = 0x100;
    public string Fingerprint => $"PE SizeOfImage=0x{SizeOfImage:X8}; TimeDateStamp=0x{LinkTimestamp:X8}";
}

public static class Fm27BuildProfiles
{
    // Observed Windows builds. The two 10-November 2026 retail profiles are the primary targets.
    // Earlier entries are retained to avoid breaking test installations, but they remain separately gated.
    public static IReadOnlyList<Fm27BuildProfile> All { get; } = new[]
    {
        new Fm27BuildProfile("gamepass-0.9.2435.0", "FM27 Game Pass 0.9.2435.0 · 10 Nov 2026", "Game Pass", 0x17969000, 0x6AE8CF77, 0x53B7DE0, 0x53B7E28, 0x53B7F98, 0x53597C8, 0x53B4160),
        new Fm27BuildProfile("steam-26385295", "FM27 Steam 27.0.1 build 26385295 · 10 Nov 2026", "Steam", 0x17CB9000, 0x6AE8B5A4, 0x5820810, 0x5820858, 0x58209C8, 0x57CA3B8, 0x581CB90),
        new Fm27BuildProfile("gamepass-0.9.2409.0", "FM27 Game Pass 0.9.2409.0 · 2 Nov 2026", "Game Pass", 0x175DA000, 0x6AE563A1, 0x5683DC0, 0x5683E08, 0x5683F78, 0x56257C8, 0x5680140),
        new Fm27BuildProfile("steam-26340721", "FM27 Steam 27.0.0 build 26340721 · Nov 2026", "Steam", 0x1821C000, 0x6AE565AF, 0x57057D0, 0x5705818, 0x5705988, 0x56AF378, 0x5701B50),
        new Fm27BuildProfile("gamepass-0.9.2355.0", "FM27 Game Pass 0.9.2355.0 · Launch Preview", "Game Pass", 0x19436000, 0x6ADFB161, 0x5681DC0, 0x5681E08, 0x5681F78, 0),
        new Fm27BuildProfile("steam-26227640", "FM27 Steam build 26227640 · Launch Preview", "Steam", 0x19E69000, 0x6ADFB11C, 0x57037D0, 0x5703818, 0x5703988, 0)
    };

    public static Fm27BuildProfile? Match(uint sizeOfImage, uint linkTimestamp)
        => All.FirstOrDefault(x => x.SizeOfImage == sizeOfImage && x.LinkTimestamp == linkTimestamp);
}
