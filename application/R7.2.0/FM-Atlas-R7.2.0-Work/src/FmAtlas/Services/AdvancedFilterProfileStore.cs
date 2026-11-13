using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FmAtlas.Models;

namespace FmAtlas.Services;

public sealed record AdvancedFilterProfile(
    string Id,
    string Name,
    AdvancedFilterGroup Root,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record AdvancedFilterProfileImportResult(int Added, int Skipped, IReadOnlyList<string> Conflicts);

/// <summary>Versioned, local-only persistence for nested universal-search profiles.</summary>
public sealed class AdvancedFilterProfileStore
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumProfiles = 100;
    private const int MaximumDocumentCharacters = 1024 * 1024;

    private sealed record Document(int SchemaVersion, List<AdvancedFilterProfile> Profiles);

    private static readonly JsonSerializerOptions Options = CreateOptions();
    private readonly string _path;
    private readonly object _gate = new();

    public AdvancedFilterProfileStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FmAtlas", "advanced-search-profiles.json")
            : Path.GetFullPath(path);
    }

    public IReadOnlyList<AdvancedFilterProfile> Load()
    {
        lock (_gate)
            return Read().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public AdvancedFilterProfile Save(AdvancedFilterProfile profile)
    {
        Validate(profile);
        lock (_gate)
        {
            var profiles = Read().ToList();
            var index = profiles.FindIndex(x => x.Id == profile.Id);
            if (profiles.Where((_, i) => i != index).Any(x =>
                    x.Name.Equals(profile.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("An advanced-search profile with this name already exists.");

            var now = DateTime.UtcNow;
            var stored = profile with
            {
                Name = profile.Name.Trim(),
                CreatedUtc = index >= 0 ? profiles[index].CreatedUtc : profile.CreatedUtc == default ? now : profile.CreatedUtc,
                UpdatedUtc = now
            };
            if (index >= 0) profiles[index] = stored;
            else
            {
                if (profiles.Count >= MaximumProfiles)
                    throw new InvalidOperationException($"A maximum of {MaximumProfiles} profiles is supported.");
                profiles.Add(stored);
            }
            Write(profiles);
            return stored;
        }
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_gate)
        {
            var profiles = Read().ToList();
            var removed = profiles.RemoveAll(x => x.Id == id) > 0;
            if (removed) Write(profiles);
            return removed;
        }
    }

    public AdvancedFilterProfile Duplicate(string id)
    {
        lock (_gate)
        {
            var profiles = Read().ToList();
            var source = profiles.FirstOrDefault(x => x.Id == id)
                ?? throw new KeyNotFoundException("The selected advanced-search profile no longer exists.");
            if (profiles.Count >= MaximumProfiles)
                throw new InvalidOperationException($"A maximum of {MaximumProfiles} profiles is supported.");
            var name = source.Name + " copy";
            var suffix = 2;
            while (profiles.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                name = source.Name + " copy " + suffix++;
            var now = DateTime.UtcNow;
            var duplicate = source with { Id = Guid.NewGuid().ToString("N"), Name = name, CreatedUtc = now, UpdatedUtc = now };
            profiles.Add(duplicate);
            Write(profiles);
            return duplicate;
        }
    }

    public string ExportJson()
    {
        lock (_gate)
            return JsonSerializer.Serialize(new Document(CurrentSchemaVersion, Read().ToList()), Options);
    }

    public AdvancedFilterProfileImportResult ImportJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumDocumentCharacters)
            throw new ArgumentException("The advanced-search profile document is empty or too large.", nameof(json));
        Document? document;
        try { document = JsonSerializer.Deserialize<Document>(json, Options); }
        catch (JsonException ex) { throw new InvalidDataException("The advanced-search profile document is invalid.", ex); }
        if (document is null || document.SchemaVersion != CurrentSchemaVersion || document.Profiles is null)
            throw new InvalidDataException("The advanced-search profile schema is not supported.");

        foreach (var profile in document.Profiles) Validate(profile);
        lock (_gate)
        {
            var existing = Read().ToList();
            var conflicts = new List<string>();
            var added = 0;
            foreach (var profile in document.Profiles)
            {
                if (existing.Count >= MaximumProfiles) { conflicts.Add(profile.Name + " (profile limit)"); continue; }
                if (existing.Any(x => x.Id == profile.Id
                    || x.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    conflicts.Add(profile.Name + " (ID or name conflict)");
                    continue;
                }
                existing.Add(profile);
                added++;
            }
            if (added > 0) Write(existing);
            return new AdvancedFilterProfileImportResult(added, conflicts.Count, conflicts);
        }
    }

    private List<AdvancedFilterProfile> Read()
    {
        if (!File.Exists(_path)) return new List<AdvancedFilterProfile>();
        var json = File.ReadAllText(_path);
        if (json.Length > MaximumDocumentCharacters)
            throw new InvalidDataException("The local advanced-search profile file exceeds the size limit.");
        Document? document;
        try { document = JsonSerializer.Deserialize<Document>(json, Options); }
        catch (JsonException ex) { throw new InvalidDataException("The local advanced-search profile file is corrupt; it has been left unchanged.", ex); }
        if (document is null || document.SchemaVersion != CurrentSchemaVersion || document.Profiles is null)
            throw new InvalidDataException("The local advanced-search profile schema is unsupported; the file has been left unchanged.");
        foreach (var profile in document.Profiles) Validate(profile);
        return document.Profiles;
    }

    private void Write(List<AdvancedFilterProfile> profiles)
    {
        var folder = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(folder);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(new Document(CurrentSchemaVersion, profiles), Options));
            if (File.Exists(_path)) File.Move(temp, _path, overwrite: true);
            else File.Move(temp, _path);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static void Validate(AdvancedFilterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Id) || profile.Id.Length > 128)
            throw new ArgumentException("An advanced-search profile needs a valid ID.");
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Trim().Length > 80)
            throw new ArgumentException("A profile name must contain 1–80 characters.");
        if (profile.Root is null) throw new ArgumentException("A profile must have a filter group.");
        AdvancedFilterEvaluator.Validate(profile.Root);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
