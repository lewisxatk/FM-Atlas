using System.IO;
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
        Dictionary<string, double> Weights,
        int? MinimumAge = null,
        int? MaximumAge = null,
        int? MinimumCurrentAbility = null,
        int? MinimumPotentialAbility = null,
        string? PositionContains = null,
        double? MinimumCoverage = null,
        int? ResultLimit = null);

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
            .Select(ToProfile)
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
        var filters = ValidateSavedFilters(profile.SavedFilters);

        var path = Path.GetFullPath(string.IsNullOrWhiteSpace(filePath) ? DefaultPath : filePath);
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
        var stored = LoadAll(path)
            .Where(x => !x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(ToStored)
            .ToList();
        stored.Add(new StoredRoleFitProfile(
            name, profile.Phase, profile.PositionGroup.Trim(), profile.Description ?? "",
            new Dictionary<string, double>(profile.Weights, StringComparer.OrdinalIgnoreCase),
            filters?.MinimumAge, filters?.MaximumAge, filters?.MinimumCurrentAbility,
            filters?.MinimumPotentialAbility, filters?.PositionContains,
            filters?.MinimumCoverage, filters?.ResultLimit));
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

    private static RoleFitSearchFilters? ValidateSavedFilters(RoleFitSearchFilters? filters)
    {
        if (filters is null) return null;
        if (filters.MinimumAge is < 0 or > 120 || filters.MaximumAge is < 0 or > 120
            || (filters.MinimumAge.HasValue && filters.MaximumAge.HasValue && filters.MinimumAge > filters.MaximumAge))
            throw new ArgumentException("Saved template age filters are invalid.", nameof(filters));
        if (filters.MinimumCurrentAbility is < 0 or > 200 || filters.MinimumPotentialAbility is < 0 or > 200)
            throw new ArgumentException("Saved template CA/PA filters must be between 0 and 200.", nameof(filters));
        if (!double.IsFinite(filters.MinimumCoverage) || filters.MinimumCoverage is < 0.5 or > 1)
            throw new ArgumentException("Saved template coverage must be between 50% and 100%.", nameof(filters));
        if (filters.ResultLimit is < 1 or > 5000)
            throw new ArgumentException("Saved template result limit must be between 1 and 5,000.", nameof(filters));
        var position = string.IsNullOrWhiteSpace(filters.PositionContains) ? null : filters.PositionContains.Trim();
        if (position?.Length > 120) throw new ArgumentException("Saved position filter is too long.", nameof(filters));
        return filters with { PositionContains = position };
    }

    private static RoleFitProfile ToProfile(StoredRoleFitProfile stored)
    {
        var hasSavedFilters = stored.MinimumAge.HasValue || stored.MaximumAge.HasValue
            || stored.MinimumCurrentAbility.HasValue || stored.MinimumPotentialAbility.HasValue
            || !string.IsNullOrWhiteSpace(stored.PositionContains)
            || stored.MinimumCoverage.HasValue || stored.ResultLimit.HasValue;
        RoleFitSearchFilters? filters = hasSavedFilters
            ? new RoleFitSearchFilters(stored.MinimumAge, stored.MaximumAge, stored.MinimumCurrentAbility,
                stored.MinimumPotentialAbility, stored.PositionContains, stored.MinimumCoverage ?? 0.5,
                stored.ResultLimit ?? 250)
            : null;
        return new RoleFitProfile(
            stored.Name.Trim(), stored.Phase ?? "Both", stored.PositionGroup ?? "Other",
            stored.Description ?? "", new Dictionary<string, double>(stored.Weights, StringComparer.OrdinalIgnoreCase),
            true, ValidateSavedFilters(filters));
    }

    private static StoredRoleFitProfile ToStored(RoleFitProfile profile)
    {
        var filters = ValidateSavedFilters(profile.SavedFilters);
        return new StoredRoleFitProfile(
            profile.Name, profile.Phase, profile.PositionGroup, profile.Description,
            new Dictionary<string, double>(profile.Weights, StringComparer.OrdinalIgnoreCase),
            filters?.MinimumAge, filters?.MaximumAge, filters?.MinimumCurrentAbility,
            filters?.MinimumPotentialAbility, filters?.PositionContains,
            filters?.MinimumCoverage, filters?.ResultLimit);
    }
}
