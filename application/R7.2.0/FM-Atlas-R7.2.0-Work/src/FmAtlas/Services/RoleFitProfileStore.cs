using System.Text.Json;

namespace FmAtlas.Services;

/// <summary>
/// Stores reusable, user-defined Role Fit templates separately from the built-in Atlas role library.
/// Files are local to this PC; templates never change save data or game memory.
/// </summary>
public static class RoleFitProfileStore
{
    private sealed record StoredRoleFitProfile(
        string Name,
        string Phase,
        string PositionGroup,
        string Description,
        Dictionary<string, double> Weights);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FmAtlas", "role-fit-templates.json");

    public static IReadOnlyList<RoleFitProfile> LoadAll(string? filePath = null)
    {
        var path = Path.GetFullPath(string.IsNullOrWhiteSpace(filePath) ? DefaultPath : filePath);
        if (!File.Exists(path)) return Array.Empty<RoleFitProfile>();
        var stored = JsonSerializer.Deserialize<List<StoredRoleFitProfile>>(File.ReadAllText(path), JsonOptions)
            ?? new List<StoredRoleFitProfile>();
        return stored
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && x.Weights is { Count: > 0 })
            .Select(x => new RoleFitProfile(
                x.Name.Trim(), x.Phase ?? "Both", x.PositionGroup ?? "Other",
                x.Description ?? "", new Dictionary<string, double>(x.Weights, StringComparer.OrdinalIgnoreCase), true))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void Save(RoleFitProfile profile, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var name = (profile.Name ?? string.Empty).Trim();
        if (name.Length is < 2 or > 80)
            throw new ArgumentException("Template names must contain between 2 and 80 characters.", nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.PositionGroup) || profile.PositionGroup.Length > 40)
            throw new ArgumentException("Enter a position group of up to 40 characters.", nameof(profile));
        if (profile.Weights is null || profile.Weights.Count is < 2 or > 40)
            throw new ArgumentException("A custom role needs between 2 and 40 weighted attributes.", nameof(profile));
        if (profile.Weights.Any(x => string.IsNullOrWhiteSpace(x.Key) || !double.IsFinite(x.Value) || x.Value is <= 0 or > 20))
            throw new ArgumentException("Every selected attribute needs a finite weight greater than 0 and no greater than 20.", nameof(profile));

        var path = Path.GetFullPath(string.IsNullOrWhiteSpace(filePath) ? DefaultPath : filePath);
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
        var stored = LoadAll(path)
            .Where(x => !x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(ToStored)
            .ToList();
        stored.Add(new StoredRoleFitProfile(
            name, profile.Phase, profile.PositionGroup.Trim(), profile.Description ?? "",
            new Dictionary<string, double>(profile.Weights, StringComparer.OrdinalIgnoreCase)));
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(stored, JsonOptions));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static bool Delete(string name, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var path = Path.GetFullPath(string.IsNullOrWhiteSpace(filePath) ? DefaultPath : filePath);
        if (!File.Exists(path)) return false;
        var existing = LoadAll(path);
        var remaining = existing.Where(x => !x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (remaining.Count == existing.Count) return false;
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var stored = remaining.Select(ToStored).ToList();
            File.WriteAllText(temporary, JsonSerializer.Serialize(stored, JsonOptions));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return true;
    }

    private static StoredRoleFitProfile ToStored(RoleFitProfile profile)
        => new(profile.Name, profile.Phase, profile.PositionGroup, profile.Description,
            new Dictionary<string, double>(profile.Weights, StringComparer.OrdinalIgnoreCase));
}
