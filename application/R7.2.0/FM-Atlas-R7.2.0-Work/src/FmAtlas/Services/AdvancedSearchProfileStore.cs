using System.IO;
using System.Text.Json;
using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>
/// Stores named advanced-search profiles locally, separately from the career database.
/// Writes are atomic so an interrupted save cannot corrupt the previous profile.
/// </summary>
public static class AdvancedSearchProfileStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FmAtlas",
        "search-profiles");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<AdvancedSearchProfile> LoadAll()
    {
        try
        {
            if (!Directory.Exists(Folder)) return Array.Empty<AdvancedSearchProfile>();
            var profiles = new List<AdvancedSearchProfile>();
            foreach (var file in Directory.EnumerateFiles(Folder, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize<AdvancedSearchProfile>(json, JsonOptions);
                    if (profile is null || string.IsNullOrWhiteSpace(profile.Id)
                        || string.IsNullOrWhiteSpace(profile.Name) || profile.Criteria is null)
                        continue;
                    profiles.Add(profile);
                }
                catch
                {
                    // An individual damaged profile must not prevent the remaining profiles loading.
                }
            }
            return profiles
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(x => x.UpdatedUtc)
                .ToList();
        }
        catch
        {
            return Array.Empty<AdvancedSearchProfile>();
        }
    }

    public static void Save(AdvancedSearchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Id)) throw new ArgumentException("A profile ID is required.", nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Name)) throw new ArgumentException("A profile name is required.", nameof(profile));
        if (profile.Criteria is null || profile.Criteria.Count is < 1 or > 60)
            throw new ArgumentOutOfRangeException(nameof(profile), "A saved profile must contain between 1 and 60 conditions.");

        Directory.CreateDirectory(Folder);
        var file = GetPath(profile.Id);
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(temporary, json);
            File.Move(temporary, file, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    public static bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var file = GetPath(id);
        if (!File.Exists(file)) return false;
        File.Delete(file);
        return true;
    }

    private static string GetPath(string id)
    {
        var safeId = new string(id.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (safeId.Length == 0) throw new ArgumentException("The profile ID is invalid.", nameof(id));
        return Path.Combine(Folder, safeId + ".json");
    }
}
