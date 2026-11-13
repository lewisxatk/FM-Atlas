using System.IO;
using System.Text;
using System.Text.Json;
using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>
/// Atomic, local-first storage for reusable Stats Search and Attribute Search profiles.
/// Only query criteria are persisted; player records and save data are never copied here.
/// </summary>
public sealed class SavedSearchProfileStore
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumProfiles = 200;
    public const int MaximumConditions = 60;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly object _gate = new();

    public SavedSearchProfileStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FmAtlas",
                "search-profiles.json")
            : Path.GetFullPath(path);
    }

    public string StoragePath => _path;

    public IReadOnlyList<SavedSearchProfile> Load()
    {
        lock (_gate)
        {
            var profiles = ReadDocument();
            return profiles
                .OrderBy(x => x.Kind)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public SavedSearchProfile Save(SavedSearchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Validate(profile);

        lock (_gate)
        {
            var profiles = ReadDocument().ToList();
            var existingIndex = profiles.FindIndex(x =>
                string.Equals(x.Id, profile.Id, StringComparison.Ordinal));

            if (existingIndex >= 0)
            {
                var existing = profiles[existingIndex];
                if (profiles.Where((x, index) => index != existingIndex).Any(x =>
                        x.Kind == profile.Kind
                        && string.Equals(x.Name, profile.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("A profile with this name already exists in the same search category.");
                }

                var updated = profile with
                {
                    Name = profile.Name.Trim(),
                    CreatedUtc = existing.CreatedUtc,
                    UpdatedUtc = DateTime.UtcNow
                };
                profiles[existingIndex] = updated;
                WriteDocument(profiles);
                return updated;
            }

            if (profiles.Count >= MaximumProfiles)
                throw new InvalidOperationException($"A maximum of {MaximumProfiles} saved search profiles is supported.");

            if (profiles.Any(x =>
                    x.Kind == profile.Kind
                    && string.Equals(x.Name, profile.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A profile with this name already exists in the same search category.");
            }

            var created = profile with
            {
                Name = profile.Name.Trim(),
                CreatedUtc = profile.CreatedUtc == default ? DateTime.UtcNow : profile.CreatedUtc,
                UpdatedUtc = DateTime.UtcNow
            };
            profiles.Add(created);
            WriteDocument(profiles);
            return created;
        }
    }

    /// <summary>Create a new independent profile using a selected profile's criteria.</summary>
    public SavedSearchProfile Duplicate(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A saved profile ID is required.", nameof(id));

        lock (_gate)
        {
            var profiles = ReadDocument().ToList();
            var source = profiles.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException("The selected saved-search profile no longer exists.");

            if (profiles.Count >= MaximumProfiles)
                throw new InvalidOperationException($"A maximum of {MaximumProfiles} saved search profiles is supported.");

            var copyNumber = 1;
            string copyName;
            do
            {
                var suffix = copyNumber == 1 ? " (Copy)" : $" (Copy {copyNumber})";
                var maximumBaseLength = Math.Max(1, 80 - suffix.Length);
                var baseName = source.Name.Trim();
                if (baseName.Length > maximumBaseLength)
                    baseName = baseName[..maximumBaseLength];
                copyName = baseName + suffix;
                copyNumber++;
            }
            while (profiles.Any(x => x.Kind == source.Kind
                && string.Equals(x.Name, copyName, StringComparison.OrdinalIgnoreCase)));

            var now = DateTime.UtcNow;
            var copy = source with
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = copyName,
                CreatedUtc = now,
                UpdatedUtc = now,
                Conditions = source.Conditions.ToArray()
            };

            profiles.Add(copy);
            WriteDocument(profiles);
            return copy;
        }
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A saved profile ID is required.", nameof(id));

        lock (_gate)
        {
            var profiles = ReadDocument().ToList();
            var removed = profiles.RemoveAll(x => string.Equals(x.Id, id, StringComparison.Ordinal)) > 0;
            if (removed)
                WriteDocument(profiles);
            return removed;
        }
    }

    /// <summary>Export all profiles, or one category, as a versioned portable JSON document.</summary>
    public string ExportJson(SavedSearchKind? kind = null)
    {
        lock (_gate)
        {
            var profiles = ReadDocument()
                .Where(x => !kind.HasValue || x.Kind == kind.Value)
                .OrderBy(x => x.Kind)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .ToList();

            return JsonSerializer.Serialize(
                new SavedSearchProfileDocument(CurrentSchemaVersion, profiles),
                JsonOptions);
        }
    }

    /// <summary>
    /// Merge a portable profile document into the local store. Existing IDs and names are never
    /// overwritten; conflicts are returned to the UI for transparent reporting.
    /// </summary>
    public SavedSearchProfileImportResult ImportJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("The imported saved-search profile document is empty.");
        if (json.Length > 1_000_000)
            throw new InvalidDataException("The imported saved-search profile document exceeds the 1 MB safety limit.");

        SavedSearchProfileDocument document;
        try
        {
            document = JsonSerializer.Deserialize<SavedSearchProfileDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("The imported profile document is empty or invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The imported profile document is not valid JSON.", ex);
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Imported profile schema {document.SchemaVersion} is not supported.");

        if (document.Profiles is null || document.Profiles.Count > MaximumProfiles)
            throw new InvalidDataException($"An import may contain at most {MaximumProfiles} profiles.");

        foreach (var profile in document.Profiles)
            Validate(profile);

        var duplicateId = document.Profiles
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateId is not null)
            throw new InvalidDataException("The imported document contains duplicate profile IDs.");

        var duplicateName = document.Profiles
            .GroupBy(x => (x.Kind, NormalizedName: x.Name.Trim()), new ProfileNameComparer())
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateName is not null)
            throw new InvalidDataException("The imported document contains duplicate names in one category.");

        lock (_gate)
        {
            var profiles = ReadDocument().ToList();
            var conflicts = new List<string>();
            var added = 0;
            var skipped = 0;

            foreach (var imported in document.Profiles)
            {
                if (profiles.Any(x => string.Equals(x.Id, imported.Id, StringComparison.Ordinal)))
                {
                    skipped++;
                    conflicts.Add($"Skipped '{imported.Name}': a profile with the same ID already exists.");
                    continue;
                }

                if (profiles.Any(x => x.Kind == imported.Kind
                    && string.Equals(x.Name, imported.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    skipped++;
                    conflicts.Add($"Skipped '{imported.Name}': the same name already exists in {imported.Kind}.");
                    continue;
                }

                if (profiles.Count >= MaximumProfiles)
                {
                    skipped++;
                    conflicts.Add($"Skipped '{imported.Name}': the local store is at its {MaximumProfiles}-profile limit.");
                    continue;
                }

                profiles.Add(imported with { Name = imported.Name.Trim() });
                added++;
            }

            if (added > 0)
                WriteDocument(profiles);

            return new SavedSearchProfileImportResult(added, skipped, conflicts);
        }
    }

    private List<SavedSearchProfile> ReadDocument()
    {
        if (!File.Exists(_path))
            return new List<SavedSearchProfile>();

        try
        {
            var json = File.ReadAllText(_path, Encoding.UTF8);
            var document = JsonSerializer.Deserialize<SavedSearchProfileDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("The saved-search profile file is empty or invalid.");

            if (document.SchemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException(
                    $"Saved-search profile schema {document.SchemaVersion} is not supported by this FM Atlas version.");

            if (document.Profiles is null || document.Profiles.Count > MaximumProfiles)
                throw new InvalidDataException("The saved-search profile file contains an invalid profile count.");

            foreach (var profile in document.Profiles)
                Validate(profile);

            var duplicateId = document.Profiles
                .GroupBy(x => x.Id, StringComparer.Ordinal)
                .FirstOrDefault(x => x.Count() > 1);
            if (duplicateId is not null)
                throw new InvalidDataException("The saved-search profile file contains duplicate profile IDs.");

            var duplicateName = document.Profiles
                .GroupBy(x => (x.Kind, NormalizedName: x.Name.Trim()), new ProfileNameComparer())
                .FirstOrDefault(x => x.Count() > 1);
            if (duplicateName is not null)
                throw new InvalidDataException("The saved-search profile file contains duplicate names in one category.");

            return document.Profiles;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The saved-search profile file could not be read. The original file was left untouched.", ex);
        }
    }

    private void WriteDocument(List<SavedSearchProfile> profiles)
    {
        var folder = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        var payload = JsonSerializer.Serialize(
            new SavedSearchProfileDocument(CurrentSchemaVersion, profiles),
            JsonOptions);
        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       16 * 1024,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(payload);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch { /* Preserve the original profile file if temporary cleanup fails. */ }
        }
    }

    private static void Validate(SavedSearchProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id) || profile.Id.Length > 80)
            throw new ArgumentException("A saved profile needs a valid stable ID.", nameof(profile));

        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Trim().Length > 80)
            throw new ArgumentException("A saved profile name must contain 1–80 characters.", nameof(profile));

        if (!Enum.IsDefined(profile.Kind))
            throw new ArgumentOutOfRangeException(nameof(profile), "Unknown saved search category.");

        if (profile.Conditions is null || profile.Conditions.Count is < 1 or > MaximumConditions)
            throw new ArgumentException($"A saved profile must contain 1–{MaximumConditions} conditions.", nameof(profile));

        if (profile.Kind == SavedSearchKind.Attributes && (profile.MatchAny || profile.Per90))
            throw new ArgumentException("Attribute profiles use ALL matching and do not support per-90.", nameof(profile));

        if (profile.Kind == SavedSearchKind.Statistics
            && (profile.MinimumMinutes is < 1 or > 10000))
            throw new ArgumentOutOfRangeException(nameof(profile), "Minimum minutes must be from 1 to 10,000.");

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var condition in profile.Conditions)
        {
            if (condition is null
                || string.IsNullOrWhiteSpace(condition.Key)
                || condition.Key.Length > 256
                || string.IsNullOrWhiteSpace(condition.DisplayName)
                || condition.DisplayName.Length > 128)
            {
                throw new ArgumentException("Every saved-search condition needs a valid field key and display name.", nameof(profile));
            }

            if (!keys.Add(condition.Key))
                throw new ArgumentException("A saved-search profile cannot contain the same field more than once.", nameof(profile));

            if (!condition.Minimum.HasValue && !condition.Maximum.HasValue)
                throw new ArgumentException($"Condition '{condition.DisplayName}' needs a minimum or maximum.", nameof(profile));

            if ((condition.Minimum.HasValue && !double.IsFinite(condition.Minimum.Value))
                || (condition.Maximum.HasValue && !double.IsFinite(condition.Maximum.Value)))
            {
                throw new ArgumentOutOfRangeException(nameof(profile), "Saved-search boundaries must be finite numbers.");
            }

            if (condition.Minimum.HasValue && condition.Maximum.HasValue
                && condition.Minimum.Value > condition.Maximum.Value)
            {
                throw new ArgumentException($"Minimum cannot exceed maximum for '{condition.DisplayName}'.", nameof(profile));
            }

            if (profile.Kind == SavedSearchKind.Attributes
                && ((condition.Minimum.HasValue && (condition.Minimum.Value is < 1 or > 20 || condition.Minimum.Value % 1 != 0))
                    || (condition.Maximum.HasValue && (condition.Maximum.Value is < 1 or > 20 || condition.Maximum.Value % 1 != 0))))
            {
                throw new ArgumentOutOfRangeException(nameof(profile), "Attribute bounds must be whole numbers from 1 to 20.");
            }
        }
    }

    private sealed record SavedSearchProfileDocument(
        int SchemaVersion,
        List<SavedSearchProfile> Profiles);

    private sealed class ProfileNameComparer : IEqualityComparer<(SavedSearchKind Kind, string NormalizedName)>
    {
        public bool Equals(
            (SavedSearchKind Kind, string NormalizedName) x,
            (SavedSearchKind Kind, string NormalizedName) y)
            => x.Kind == y.Kind
               && string.Equals(x.NormalizedName, y.NormalizedName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((SavedSearchKind Kind, string NormalizedName) value)
            => HashCode.Combine(value.Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(value.NormalizedName));
    }
}
