using System.Text.Json;

namespace FmAtlas.Services;

public sealed record RoleFitSavedTemplate(
    string Name,
    RoleFitProfile Profile,
    RoleFitSearchFilters Filters);

/// <summary>
/// Stores user-authored Role Fit templates locally. Writes are atomic and independent
/// of the active FM career; no game or scouting database records are changed.
/// </summary>
public sealed class RoleFitTemplateStore
{
    private sealed class StoreDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public List<TemplateDocument> Templates { get; set; } = new();
    }

    private sealed class TemplateDocument
    {
        public string Name { get; set; } = "";
        public string RoleName { get; set; } = "";
        public string Phase { get; set; } = "Both";
        public string PositionGroup { get; set; } = "Any";
        public string Description { get; set; } = "";
        public Dictionary<string, double> Weights { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int? MinimumAge { get; set; }
        public int? MaximumAge { get; set; }
        public int? MinimumCurrentAbility { get; set; }
        public int? MinimumPotentialAbility { get; set; }
        public string? PositionContains { get; set; }
        public double MinimumCoverage { get; set; } = 0.5;
        public int ResultLimit { get; set; } = 250;
    }

    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;

    public RoleFitTemplateStore(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas", "role-fit-templates.json")
            : Path.GetFullPath(filePath);
    }

    public List<RoleFitSavedTemplate> LoadAll()
    {
        lock (Gate)
        {
            if (!File.Exists(_filePath)) return new List<RoleFitSavedTemplate>();
            StoreDocument document;
            try
            {
                using var stream = File.OpenRead(_filePath);
                document = JsonSerializer.Deserialize<StoreDocument>(stream, JsonOptions)
                    ?? throw new InvalidDataException("The Role Fit template file is empty.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("The Role Fit template file contains invalid JSON. The original file has been left untouched.", ex);
            }

            if (document.SchemaVersion != 1)
                throw new InvalidDataException($"Unsupported Role Fit template schema version: {document.SchemaVersion}.");

            return document.Templates.Select(ToTemplate).ToList();
        }
    }

    public void Save(RoleFitSavedTemplate template)
    {
        var validated = Validate(template);
        lock (Gate)
        {
            var templates = File.Exists(_filePath) ? LoadAll() : new List<RoleFitSavedTemplate>();
            var existing = templates.FindIndex(x => x.Name.Equals(validated.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) templates[existing] = validated;
            else templates.Add(validated);
            WriteAll(templates);
        }
    }

    public bool Delete(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        lock (Gate)
        {
            if (!File.Exists(_filePath)) return false;
            var templates = LoadAll();
            var removed = templates.RemoveAll(x => x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return false;
            WriteAll(templates);
            return true;
        }
    }

    private static RoleFitSavedTemplate Validate(RoleFitSavedTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(template.Profile);
        ArgumentNullException.ThrowIfNull(template.Filters);
        var name = template.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 60) throw new ArgumentException("Template name must contain 1–60 characters.", nameof(template));
        var roleName = template.Profile.Name?.Trim() ?? "";
        if (roleName.Length is < 1 or > 80) throw new ArgumentException("Role name must contain 1–80 characters.", nameof(template));
        if (template.Profile.Weights is null || template.Profile.Weights.Count is < 1 or > 60)
            throw new ArgumentException("A template needs between 1 and 60 weighted attributes.", nameof(template));

        var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in template.Profile.Weights)
        {
            var key = pair.Key?.Trim() ?? "";
            if (key.Length is < 1 or > 80) throw new ArgumentException("Attribute names must contain 1–80 characters.", nameof(template));
            if (!double.IsFinite(pair.Value) || pair.Value <= 0 || pair.Value > 100)
                throw new ArgumentException($"Weight for '{key}' must be finite and above zero and no greater than 100.", nameof(template));
            if (weights.ContainsKey(key)) throw new ArgumentException($"Duplicate attribute weight: {key}.", nameof(template));
            weights[key] = pair.Value;
        }

        var filters = template.Filters;
        if (filters.MinimumAge is < 0 or > 120 || filters.MaximumAge is < 0 or > 120
            || (filters.MinimumAge.HasValue && filters.MaximumAge.HasValue && filters.MinimumAge > filters.MaximumAge))
            throw new ArgumentException("Template age filters are invalid.", nameof(template));
        if (filters.MinimumCurrentAbility is < 0 or > 200 || filters.MinimumPotentialAbility is < 0 or > 200)
            throw new ArgumentException("Template CA/PA filters must be between 0 and 200.", nameof(template));
        if (!double.IsFinite(filters.MinimumCoverage) || filters.MinimumCoverage is < 0 or > 1)
            throw new ArgumentException("Template coverage must be between 0 and 1.", nameof(template));
        if (filters.ResultLimit is < 1 or > 5000)
            throw new ArgumentException("Template result limit must be between 1 and 5,000.", nameof(template));

        var profile = new RoleFitProfile(
            roleName,
            template.Profile.Phase?.Trim() ?? "Both",
            template.Profile.PositionGroup?.Trim() ?? "Any",
            (template.Profile.Description ?? "").Trim(),
            weights);
        var normalizedFilters = filters with { PositionContains = string.IsNullOrWhiteSpace(filters.PositionContains) ? null : filters.PositionContains.Trim() };
        return new RoleFitSavedTemplate(name, profile, normalizedFilters);
    }

    private static TemplateDocument ToDocument(RoleFitSavedTemplate template)
        => new()
        {
            Name = template.Name,
            RoleName = template.Profile.Name,
            Phase = template.Profile.Phase,
            PositionGroup = template.Profile.PositionGroup,
            Description = template.Profile.Description,
            Weights = new Dictionary<string, double>(template.Profile.Weights, StringComparer.OrdinalIgnoreCase),
            MinimumAge = template.Filters.MinimumAge,
            MaximumAge = template.Filters.MaximumAge,
            MinimumCurrentAbility = template.Filters.MinimumCurrentAbility,
            MinimumPotentialAbility = template.Filters.MinimumPotentialAbility,
            PositionContains = template.Filters.PositionContains,
            MinimumCoverage = template.Filters.MinimumCoverage,
            ResultLimit = template.Filters.ResultLimit
        };

    private static RoleFitSavedTemplate ToTemplate(TemplateDocument template)
    {
        var profile = new RoleFitProfile(
            template.RoleName, template.Phase, template.PositionGroup, template.Description,
            new Dictionary<string, double>(template.Weights, StringComparer.OrdinalIgnoreCase));
        var filters = new RoleFitSearchFilters(
            template.MinimumAge, template.MaximumAge, template.MinimumCurrentAbility,
            template.MinimumPotentialAbility, template.PositionContains, template.MinimumCoverage, template.ResultLimit);
        return Validate(new RoleFitSavedTemplate(template.Name, profile, filters));
    }

    private void WriteAll(IReadOnlyList<RoleFitSavedTemplate> templates)
    {
        var folder = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
        var document = new StoreDocument { Templates = templates.Select(ToDocument).ToList() };
        var temporaryPath = _filePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, document, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
