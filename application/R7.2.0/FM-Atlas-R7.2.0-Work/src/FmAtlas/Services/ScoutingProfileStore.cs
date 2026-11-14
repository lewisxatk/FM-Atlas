using System.IO;
using System.Text.Json;
using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>Atomic local persistence for reusable multi-stat scouting profiles.</summary>
public static class ScoutingProfileStore
{
    private static readonly string DefaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas", "scouting-profiles");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<ScoutingProfile> LoadAll(string? folderPath = null)
    {
        var folder = Resolve(folderPath);
        if (!Directory.Exists(folder)) return Array.Empty<ScoutingProfile>();
        var result = new List<ScoutingProfile>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<ScoutingProfile>(File.ReadAllText(file), Options);
                if (profile is not null && !string.IsNullOrWhiteSpace(profile.Id)
                    && !string.IsNullOrWhiteSpace(profile.Name) && profile.Requirements is { Count: > 0 })
                    result.Add(profile);
            }
            catch (JsonException) { /* A damaged profile must not block other profiles. */ }
            catch (IOException) { /* Skip an unreadable individual profile. */ }
        }
        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static void Save(ScoutingProfile profile, string? folderPath = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Validate(profile);
        var folder = Resolve(folderPath);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, SafeId(profile.Id) + ".json");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(profile, Options));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static bool Delete(string id, string? folderPath = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var path = Path.Combine(Resolve(folderPath), SafeId(id) + ".json");
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public static void Validate(ScoutingProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Trim().Length > 80)
            throw new ArgumentException("A profile ID and a name of up to 80 characters are required.", nameof(profile));
        if (profile.Requirements is null || profile.Requirements.Count is < 1 or > 40)
            throw new ArgumentOutOfRangeException(nameof(profile), "A scouting profile needs 1–40 metric requirements.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var requirement in profile.Requirements)
        {
            if (string.IsNullOrWhiteSpace(requirement.MetricKey) || !seen.Add(requirement.MetricKey))
                throw new ArgumentException("Metric keys must be present and unique.", nameof(profile));
            if (requirement.Minimum.HasValue && !double.IsFinite(requirement.Minimum.Value)
                || requirement.Maximum.HasValue && !double.IsFinite(requirement.Maximum.Value)
                || requirement.Minimum.HasValue && requirement.Maximum.HasValue && requirement.Minimum > requirement.Maximum)
                throw new ArgumentException($"Invalid range for {requirement.DisplayName}.", nameof(profile));
        }
        if (profile.MinimumAge is < 0 or > 120 || profile.MaximumAge is < 0 or > 120
            || profile.MinimumAge.HasValue && profile.MaximumAge.HasValue && profile.MinimumAge > profile.MaximumAge)
            throw new ArgumentException("Age bounds are invalid.", nameof(profile));
        if (!double.IsFinite(profile.MinimumMinutes) || profile.MinimumMinutes < 0
            || profile.MinimumSampleSize < 1 || !double.IsFinite(profile.MinimumDataCoverage)
            || profile.MinimumDataCoverage is < 0 or > 1 || profile.ResultLimit is < 1 or > 5000
            || profile.MaximumValue < 0)
            throw new ArgumentException("Minutes, sample size, coverage, value and result limit are invalid.", nameof(profile));
        if (profile.PositionCode?.Length > 12 || profile.League?.Length > 120)
            throw new ArgumentException("Position or league filter is too long.", nameof(profile));
    }

    private static string Resolve(string? path) => Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? DefaultFolder : path);
    private static string SafeId(string id)
    {
        var safe = new string(id.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (safe.Length == 0) throw new ArgumentException("Profile ID is invalid.", nameof(id));
        return safe;
    }
}
