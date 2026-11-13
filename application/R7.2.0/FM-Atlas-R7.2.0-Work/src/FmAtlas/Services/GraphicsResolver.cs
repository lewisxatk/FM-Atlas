using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FmAtlas.Services;

public sealed record GraphicsScanResult(
    string? GraphicsRoot,
    int ConfigFiles,
    int FaceMappings,
    int ClubMappings,
    int CompetitionMappings,
    int FlagMappings,
    int MissingFiles,
    int ParseErrors,
    DateTime ScannedUtc,
    string Status);

/// <summary>
/// Resolves player faces and other graphics from the user's existing FM27 graphics packs.
/// It does not bundle or copy any image assets. XML paths are constrained to the selected
/// graphics tree before being opened.
/// </summary>
public sealed class GraphicsResolver
{
    private static readonly Lazy<GraphicsResolver> LazyShared = new(() => new GraphicsResolver());
    public static GraphicsResolver Shared => LazyShared.Value;

    private readonly object _gate = new();
    private readonly Dictionary<string, string> _faces = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _clubs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _competitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _flags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _imageFilesByStem = new(StringComparer.OrdinalIgnoreCase);
    private GraphicsScanResult _last = new(null, 0, 0, 0, 0, 0, 0, 0, DateTime.MinValue, "Not scanned");
    private bool _scanned;

    private GraphicsResolver() { }

    public static string? DiscoverDefaultRoot()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents)) return null;
        var candidates = new[]
        {
            Path.Combine(documents, "Sports Interactive", "Football Manager 27", "graphics"),
            Path.Combine(documents, "Sports Interactive", "Football Manager 27", "Graphics"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Documents", "Sports Interactive", "Football Manager 27", "graphics")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    public static string? GetConfiguredRoot()
    {
        try
        {
            var config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas", "graphics-root.txt");
            if (File.Exists(config))
            {
                var value = File.ReadAllText(config).Trim();
                if (Directory.Exists(value)) return Path.GetFullPath(value);
            }
        }
        catch { }
        return DiscoverDefaultRoot();
    }

    public void SetRootFromConfigFile(string configFile)
    {
        var full = Path.GetFullPath(configFile);
        if (!File.Exists(full) || !Path.GetFileName(full).Equals("config.xml", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select a graphics pack config.xml file.", nameof(configFile));
        var directory = new DirectoryInfo(Path.GetDirectoryName(full)!);
        while (directory is not null && !directory.Name.Equals("graphics", StringComparison.OrdinalIgnoreCase)) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("The selected config.xml is not inside a folder named Graphics. Select a config.xml within the Football Manager 27 graphics directory.");
        var root = directory.FullName;
        var settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas");
        Directory.CreateDirectory(settings);
        File.WriteAllText(Path.Combine(settings, "graphics-root.txt"), root);
        lock (_gate) { _scanned = false; ClearMaps(); }
    }

    public GraphicsScanResult GetLastScan()
    {
        lock (_gate) return _last;
    }

    public GraphicsScanResult Scan(bool force = false)
    {
        lock (_gate)
        {
            if (_scanned && !force) return _last;
            ClearMaps();
            var root = GetConfiguredRoot();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                _last = new GraphicsScanResult(root, 0, 0, 0, 0, 0, 0, 0, DateTime.UtcNow, "Graphics folder not found. Set it in Settings to use installed face and logo packs.");
                _scanned = true;
                return _last;
            }

            var configCount = 0; var missing = 0; var errors = 0;
            // Build one filename index up front. This avoids a recursive disk walk on every
            // player-profile open when a facepack contains tens of thousands of images.
            foreach (var imageFile in EnumerateImageFiles(root))
            {
                var stem = Path.GetFileNameWithoutExtension(imageFile);
                if (string.IsNullOrWhiteSpace(stem)) continue;
                if (!_imageFilesByStem.TryGetValue(stem, out var list)) _imageFilesByStem[stem] = list = new List<string>();
                list.Add(imageFile);
            }
            foreach (var config in EnumerateConfigFiles(root))
            {
                configCount++;
                try
                {
                    var document = XDocument.Load(config, LoadOptions.None);
                    var configDirectory = Path.GetDirectoryName(config)!;
                    foreach (var record in document.Descendants().Where(x => x.Name.LocalName.Equals("record", StringComparison.OrdinalIgnoreCase)))
                    {
                        var from = ((string?)record.Attribute("from"))?.Trim();
                        var to = ((string?)record.Attribute("to"))?.Trim();
                        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) continue;
                        var category = ClassifyTarget(to);
                        if (category is null) continue;
                        var uid = ExtractTargetUid(to) ?? ExtractTargetUid(from);
                        if (string.IsNullOrWhiteSpace(uid)) continue;
                        var imagePath = ResolveImage(root, configDirectory, from);
                        if (imagePath is null) { missing++; continue; }
                        var map = category switch { "person" => _faces, "club" => _clubs, "competition" => _competitions, "flag" => _flags, _ => null };
                        if (map is not null && !map.ContainsKey(uid)) map[uid] = imagePath;
                    }
                }
                catch { errors++; }
            }

            _last = new GraphicsScanResult(root, configCount, _faces.Count, _clubs.Count, _competitions.Count, _flags.Count, missing, errors, DateTime.UtcNow,
                configCount == 0 ? "No config.xml files found in the selected graphics directory." : "Graphics mappings indexed from installed config.xml files.");
            _scanned = true;
            AtlasLog.Info($"Graphics resolver scan: root={root}; configs={configCount}; faces={_faces.Count}; clubs={_clubs.Count}; competitions={_competitions.Count}; flags={_flags.Count}; missing={missing}; errors={errors}.");
            return _last;
        }
    }

    public string? ResolveFace(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return null;
        if (!_scanned) Scan();
        lock (_gate)
        {
            if (_faces.TryGetValue(uid, out var path) && File.Exists(path)) return path;
            return FindDirectImage(uid, "person");
        }
    }

    public string? ResolveClubLogo(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return null;
        if (!_scanned) Scan();
        lock (_gate) return _clubs.TryGetValue(uid, out var path) && File.Exists(path) ? path : FindDirectImage(uid, "club");
    }

    public string? ResolveCompetitionLogo(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return null;
        if (!_scanned) Scan();
        lock (_gate) return _competitions.TryGetValue(uid, out var path) && File.Exists(path) ? path : FindDirectImage(uid, "competition");
    }

    public string? ResolveFlag(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return null;
        if (!_scanned) Scan();
        lock (_gate) return _flags.TryGetValue(uid, out var path) && File.Exists(path) ? path : FindDirectImage(uid, "flag");
    }

    private void ClearMaps() { _faces.Clear(); _clubs.Clear(); _competitions.Clear(); _flags.Clear(); _imageFilesByStem.Clear(); }

    private static IEnumerable<string> EnumerateImageFiles(string root)
    {
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] files; string[] directories;
            try { files = Directory.GetFiles(current, "*.*", SearchOption.TopDirectoryOnly); } catch { files = Array.Empty<string>(); }
            foreach (var file in files) if (IsImage(file)) yield return file;
            try { directories = Directory.GetDirectories(current); } catch { directories = Array.Empty<string>(); }
            foreach (var directory in directories)
            {
                try { if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0) pending.Push(directory); } catch { }
            }
        }
    }

    private static IEnumerable<string> EnumerateConfigFiles(string root)
    {
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] files; string[] directories;
            try { files = Directory.GetFiles(current, "config.xml", SearchOption.TopDirectoryOnly); } catch { files = Array.Empty<string>(); }
            foreach (var file in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) yield return file;
            try { directories = Directory.GetDirectories(current); } catch { directories = Array.Empty<string>(); }
            foreach (var directory in directories.OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase))
            {
                try { var attributes = File.GetAttributes(directory); if ((attributes & FileAttributes.ReparsePoint) == 0) pending.Push(directory); } catch { }
            }
        }
    }

    private static string? ResolveImage(string root, string configDirectory, string from)
    {
        var safeFrom = from.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var candidates = new List<string>();
        if (Path.IsPathRooted(safeFrom)) return null;
        candidates.Add(Path.Combine(configDirectory, safeFrom));
        candidates.Add(Path.Combine(root, safeFrom));
        var clean = safeFrom.TrimStart(Path.DirectorySeparatorChar);
        if (clean.StartsWith("graphics" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) clean = clean[("graphics" + Path.DirectorySeparatorChar).Length..];
        candidates.Add(Path.Combine(root, clean));
        foreach (var candidate in candidates)
        {
            var safe = SafePath(root, candidate);
            if (safe is null) continue;
            if (File.Exists(safe) && IsImage(safe)) return safe;
            if (Path.GetExtension(safe).Length == 0)
                foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" })
                    if (File.Exists(safe + extension)) return safe + extension;
        }
        return null;
    }

    private static string? SafePath(string root, string candidate)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(candidate);
            return full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ? full : null;
        }
        catch { return null; }
    }

    private string? FindDirectImage(string uid, string kind)
    {
        if (!_imageFilesByStem.TryGetValue(uid, out var files)) return null;
        var tokens = kind switch
        {
            "person" => new[] { "faces", "person", "players" },
            "club" => new[] { "logos", "clubs", "club" },
            "competition" => new[] { "logos", "competition", "competitions", "leagues" },
            "flag" => new[] { "flags", "nation", "nations" },
            _ => Array.Empty<string>()
        };
        return files.FirstOrDefault(file => tokens.Any(token => file.Contains(token, StringComparison.OrdinalIgnoreCase)))
            ?? files.FirstOrDefault();
    }

    private static bool IsImage(string path) => new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static string? ClassifyTarget(string to)
    {
        var value = to.Replace('\\', '/').ToLowerInvariant();
        if (value.Contains("/person/") || value.Contains("/player/")) return "person";
        if (value.Contains("/club/") || value.Contains("/team/")) return "club";
        if (value.Contains("/competition/") || value.Contains("/comp/") || value.Contains("/league/")) return "competition";
        if (value.Contains("/nation/") || value.Contains("/flag/")) return "flag";
        return null;
    }

    private static string? ExtractTargetUid(string value)
    {
        var leaf = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? value;
        var match = Regex.Match(leaf, @"\d{3,}");
        return match.Success ? match.Value : null;
    }
}
