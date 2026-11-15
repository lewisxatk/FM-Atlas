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

    public static IReadOnlyList<AdvancedSearchProfile> LoadAll(string? folderPath = null)
    {
        try
        {
            var folder = ResolveFolder(folderPath);
            if (!Directory.Exists(folder)) return Array.Empty<AdvancedSearchProfile>();
            var profiles = new List<AdvancedSearchProfile>();
            foreach (var file in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
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

    public static void Save(AdvancedSearchProfile profile, string? folderPath = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Id)) throw new ArgumentException("A profile ID is required.", nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Name)) throw new ArgumentException("A profile name is required.", nameof(profile));
        if (profile.Criteria is null || profile.Criteria.Count > 60)
            throw new ArgumentOutOfRangeException(nameof(profile), "A saved profile can contain up to 60 general conditions.");
        if (profile.Criteria.Count == 0 && AdvancedPositionFilterRules.Normalize(profile.PositionFilter) is null)
            throw new ArgumentOutOfRangeException(nameof(profile), "A saved profile needs at least one general or position condition.");
        _ = AdvancedPositionFilterRules.Normalize(profile.PositionFilter);
        ValidateSorts(profile.Sorts);

        var folder = ResolveFolder(folderPath);
        Directory.CreateDirectory(folder);
        var file = GetPath(profile.Id, folder);
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

    public static bool Delete(string id, string? folderPath = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var file = GetPath(id, ResolveFolder(folderPath));
        if (!File.Exists(file)) return false;
        File.Delete(file);
        return true;
    }

    private static string ResolveFolder(string? folderPath)
        => Path.GetFullPath(string.IsNullOrWhiteSpace(folderPath) ? Folder : folderPath);

    private static string GetPath(string id, string folder)
    {
        var safeId = new string(id.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (safeId.Length == 0) throw new ArgumentException("The profile ID is invalid.", nameof(id));
        return Path.Combine(folder, safeId + ".json");
    }

    private static void ValidateSorts(IReadOnlyList<AdvancedSearchSort>? sorts)
    {
        if (sorts is null) return;
        if (sorts.Count > 2)
            throw new ArgumentOutOfRangeException(nameof(sorts), "An advanced search profile can store up to two sort keys.");
        var allowed = new HashSet<string>(new[]
        {
            "player:Name", "player:Age", "player:Club", "player:Position", "player:League",
            "player:Nation", "player:CurrentAbility", "player:PotentialAbility", "player:Value"
        }, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sort in sorts)
        {
            if (sort is null || string.IsNullOrWhiteSpace(sort.FieldKey) || !allowed.Contains(sort.FieldKey))
                throw new ArgumentException("A saved search sort key is not supported.", nameof(sorts));
            if (!seen.Add(sort.FieldKey))
                throw new ArgumentException("A saved search cannot sort by the same field twice.", nameof(sorts));
        }
    }
}
