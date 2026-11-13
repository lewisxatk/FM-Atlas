using System.IO;
using System.Text;
using System.Text.Json;
using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>
/// Atomic local storage for Moneyball composite criteria. It stores field keys and settings only,
/// never player observations, save data or exported results.
/// </summary>
public sealed class MoneyballCompositeProfileStore
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumProfiles = 100;
    public const int MaximumMetrics = 12;
    public const int MaximumNameLength = 80;

    private static readonly HashSet<string> CohortModes = new(StringComparer.Ordinal)
    {
        "All players",
        "Same primary position",
        "Same age band (5-year)",
        "Same league",
        "Same position + age band"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly object _gate = new();

    public MoneyballCompositeProfileStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FmAtlas", "moneyball-composite-profiles.json")
            : Path.GetFullPath(path);
    }

    public string StoragePath => _path;

    public IReadOnlyList<MoneyballCompositeProfile> Load()
    {
        lock (_gate)
            return ReadDocument().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public MoneyballCompositeProfile Save(MoneyballCompositeProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Validate(profile);
        lock (_gate)
        {
            var profiles = ReadDocument().ToList();
            var index = profiles.FindIndex(x => x.Id.Equals(profile.Id, StringComparison.Ordinal));
            if (index >= 0)
            {
                if (profiles.Where((_, i) => i != index).Any(x =>
                    x.Name.Equals(profile.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("A Moneyball profile with this name already exists.");

                var updated = profile with
                {
                    Name = profile.Name.Trim(),
                    CreatedUtc = profiles[index].CreatedUtc,
                    UpdatedUtc = DateTime.UtcNow,
                    MetricKeys = profile.MetricKeys.ToArray(),
                    MetricWeights = profile.MetricWeights.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
                };
                profiles[index] = updated;
                WriteDocument(profiles);
                return updated;
            }

            if (profiles.Count >= MaximumProfiles)
                throw new InvalidOperationException($"A maximum of {MaximumProfiles} Moneyball profiles is supported.");
            if (profiles.Any(x => x.Name.Equals(profile.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A Moneyball profile with this name already exists.");

            var created = profile with
            {
                Name = profile.Name.Trim(),
                CreatedUtc = profile.CreatedUtc == default ? DateTime.UtcNow : profile.CreatedUtc,
                UpdatedUtc = DateTime.UtcNow,
                MetricKeys = profile.MetricKeys.ToArray(),
                MetricWeights = profile.MetricWeights.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
            };
            profiles.Add(created);
            WriteDocument(profiles);
            return created;
        }
    }

    public MoneyballCompositeProfile Duplicate(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A profile ID is required.", nameof(id));
        lock (_gate)
        {
            var profiles = ReadDocument().ToList();
            var source = profiles.FirstOrDefault(x => x.Id.Equals(id, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException("The selected Moneyball profile no longer exists.");
            if (profiles.Count >= MaximumProfiles)
                throw new InvalidOperationException($"A maximum of {MaximumProfiles} Moneyball profiles is supported.");

            var number = 1;
            string name;
            do
            {
                var suffix = number == 1 ? " (Copy)" : $" (Copy {number})";
                var baseName = source.Name[..Math.Min(source.Name.Length, MaximumNameLength - suffix.Length)];
                name = baseName + suffix;
                number++;
            }
            while (profiles.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

            var now = DateTime.UtcNow;
            var copy = source with
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                CreatedUtc = now,
                UpdatedUtc = now,
                MetricKeys = source.MetricKeys.ToArray(),
                MetricWeights = source.MetricWeights.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
            };
            profiles.Add(copy);
            WriteDocument(profiles);
            return copy;
        }
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A profile ID is required.", nameof(id));
        lock (_gate)
        {
            var profiles = ReadDocument().ToList();
            var removed = profiles.RemoveAll(x => x.Id.Equals(id, StringComparison.Ordinal)) > 0;
            if (removed) WriteDocument(profiles);
            return removed;
        }
    }

    public string ExportJson()
    {
        lock (_gate)
            return JsonSerializer.Serialize(new ProfileDocument(CurrentSchemaVersion, ReadDocument()), JsonOptions);
    }

    public MoneyballCompositeProfileImportResult ImportJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("The imported Moneyball profile document is empty.");
        if (json.Length > 500_000)
            throw new InvalidDataException("The imported Moneyball profile document exceeds the 500 KB safety limit.");

        ProfileDocument document;
        try
        {
            document = JsonSerializer.Deserialize<ProfileDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("The imported Moneyball profile document is invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The imported Moneyball profile document is not valid JSON.", ex);
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Moneyball profile schema {document.SchemaVersion} is not supported.");
        if (document.Profiles is null || document.Profiles.Count > MaximumProfiles)
            throw new InvalidDataException($"An import may contain at most {MaximumProfiles} profiles.");
        foreach (var profile in document.Profiles) Validate(profile);
        if (document.Profiles.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1))
            throw new InvalidDataException("The imported document contains duplicate profile IDs.");
        if (document.Profiles.GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new InvalidDataException("The imported document contains duplicate profile names.");

        lock (_gate)
        {
            var profiles = ReadDocument().ToList();
            var conflicts = new List<string>();
            var added = 0;
            var skipped = 0;
            foreach (var imported in document.Profiles)
            {
                if (profiles.Any(x => x.Id.Equals(imported.Id, StringComparison.Ordinal)))
                {
                    skipped++;
                    conflicts.Add($"Skipped '{imported.Name}': the profile ID already exists.");
                    continue;
                }
                if (profiles.Any(x => x.Name.Equals(imported.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    skipped++;
                    conflicts.Add($"Skipped '{imported.Name}': the profile name already exists.");
                    continue;
                }
                if (profiles.Count >= MaximumProfiles)
                {
                    skipped++;
                    conflicts.Add($"Skipped '{imported.Name}': the local store is full.");
                    continue;
                }
                profiles.Add(imported with
                {
                    Name = imported.Name.Trim(),
                    MetricKeys = imported.MetricKeys.ToArray(),
                    MetricWeights = imported.MetricWeights.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
                });
                added++;
            }
            if (added > 0) WriteDocument(profiles);
            return new MoneyballCompositeProfileImportResult(added, skipped, conflicts);
        }
    }

    private List<MoneyballCompositeProfile> ReadDocument()
    {
        if (!File.Exists(_path)) return new List<MoneyballCompositeProfile>();
        try
        {
            var document = JsonSerializer.Deserialize<ProfileDocument>(File.ReadAllText(_path, Encoding.UTF8), JsonOptions)
                ?? throw new InvalidDataException("The Moneyball profile file is empty or invalid.");
            if (document.SchemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException($"Moneyball profile schema {document.SchemaVersion} is unsupported.");
            if (document.Profiles is null || document.Profiles.Count > MaximumProfiles)
                throw new InvalidDataException("The Moneyball profile file has an invalid profile count.");
            foreach (var profile in document.Profiles) Validate(profile);
            if (document.Profiles.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1)
                || document.Profiles.GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
                throw new InvalidDataException("The Moneyball profile file contains duplicate IDs or names.");
            return document.Profiles;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The Moneyball profile file is corrupt. The original was left untouched.", ex);
        }
    }

    private void WriteDocument(List<MoneyballCompositeProfile> profiles)
    {
        var folder = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        var payload = JsonSerializer.Serialize(new ProfileDocument(CurrentSchemaVersion, profiles), JsonOptions);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       16 * 1024, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(payload);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static void Validate(MoneyballCompositeProfile profile)
    {
        if (profile is null) throw new ArgumentNullException(nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Id) || profile.Id.Length > 80)
            throw new ArgumentException("A Moneyball profile needs a stable ID.", nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Trim().Length > MaximumNameLength)
            throw new ArgumentException($"A Moneyball profile name must contain 1–{MaximumNameLength} characters.", nameof(profile));
        if (profile.MetricKeys is null || profile.MetricKeys.Count is < 2 or > MaximumMetrics)
            throw new ArgumentException($"A Moneyball profile must contain 2–{MaximumMetrics} metrics.", nameof(profile));
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in profile.MetricKeys)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 256 || !keys.Add(key.Trim()))
                throw new ArgumentException("Metric keys must be non-empty, bounded and unique.", nameof(profile));
        }
        if (profile.MinimumMetrics < 2 || profile.MinimumMetrics > profile.MetricKeys.Count)
            throw new ArgumentOutOfRangeException(nameof(profile), "Minimum metric coverage must be between 2 and the selected metric count.");
        if (!CohortModes.Contains(profile.CohortMode ?? string.Empty))
            throw new ArgumentException("The selected Moneyball cohort mode is not supported.", nameof(profile));
        if (profile.MinimumMinutes is < 1 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(profile), "Minimum minutes must be between 1 and 10,000.");
        if (profile.MinimumSampleSize is < 1 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(profile), "Minimum cohort size must be between 1 and 100,000.");
        if (profile.MetricWeights is null)
            throw new ArgumentException("Metric weights cannot be null.", nameof(profile));
        foreach (var pair in profile.MetricWeights)
        {
            if (!profile.MetricKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"Weight key '{pair.Key}' is not one of the selected metrics.", nameof(profile));
            if (string.IsNullOrWhiteSpace(pair.Key) || !double.IsFinite(pair.Value) || pair.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(profile), "Metric weights must be finite positive numbers.");
        }
    }

    private sealed record ProfileDocument(int SchemaVersion, List<MoneyballCompositeProfile> Profiles);
}
