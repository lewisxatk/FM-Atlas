using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace FmAtlas.Services;

public sealed record GraphicsPackMapping(
    string RecordFrom,
    string TargetPath,
    string ConfigPath,
    string PackName,
    bool TargetExists);

public sealed record GraphicsLibraryIndexResult(
    string RootPath,
    int DirectoriesVisited,
    int ConfigFilesScanned,
    int MappingsFound,
    int DuplicateIds,
    int MissingTargets,
    int RejectedPaths,
    IReadOnlyList<GraphicsPackMapping> Mappings,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Read-only indexer for user-owned Football Manager graphics folders.
/// It parses config.xml records but never changes packs, config files or game data.
/// </summary>
public sealed class GraphicsLibraryIndexer
{
    public const int MaximumDirectories = 15000;
    public const int MaximumConfigFiles = 5000;
    private const long MaximumConfigBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff"
    };

    public GraphicsLibraryIndexResult Scan(string rootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Choose a graphics folder first.", nameof(rootPath));

        var root = Path.GetFullPath(rootPath.Trim());
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("The selected graphics folder does not exist.");

        var rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        var pending = new Stack<string>();
        pending.Push(root);
        var visited = 0;
        var configs = 0;
        var rejected = 0;
        var missing = 0;
        var warnings = new List<string>();
        var mappings = new List<GraphicsPackMapping>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Warn(string message)
        {
            if (warnings.Count < 40)
                warnings.Add(message);
        }

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (visited >= MaximumDirectories)
            {
                Warn($"Directory limit reached ({MaximumDirectories:N0}); remaining folders were not scanned.");
                break;
            }

            var directory = pending.Pop();
            visited++;
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FileAttributes attributes;
                    try { attributes = File.GetAttributes(entry); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Warn($"Skipped unreadable entry: {Path.GetFileName(entry)}");
                        continue;
                    }

                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        continue;

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push(entry);
                        continue;
                    }

                    if (!Path.GetFileName(entry).Equals("config.xml", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (configs >= MaximumConfigFiles)
                    {
                        Warn($"Config file limit reached ({MaximumConfigFiles:N0}).");
                        pending.Clear();
                        break;
                    }

                    configs++;
                    ParseConfig(entry);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Warn($"Could not inspect folder '{Path.GetFileName(directory)}': {ex.GetType().Name}.");
            }
        }

        return new GraphicsLibraryIndexResult(
            root, visited, configs, mappings.Count, duplicates.Count, missing, rejected,
            mappings.OrderBy(x => x.RecordFrom, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.PackName, StringComparer.OrdinalIgnoreCase).ToArray(),
            warnings);

        void ParseConfig(string configPath)
        {
            try
            {
                var info = new FileInfo(configPath);
                if (info.Length > MaximumConfigBytes)
                {
                    Warn($"Skipped oversized config.xml: {Path.GetFileName(Path.GetDirectoryName(configPath))}");
                    return;
                }

                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaximumConfigBytes,
                    IgnoreComments = true
                };
                using var stream = File.OpenRead(configPath);
                using var reader = XmlReader.Create(stream, settings);
                var document = XDocument.Load(reader, LoadOptions.None);
                var configDirectory = Path.GetDirectoryName(configPath)!;

                foreach (var record in document.Descendants().Where(x => x.Name.LocalName.Equals("record", StringComparison.OrdinalIgnoreCase)))
                {
                    var from = (string?)record.Attribute("from");
                    var to = (string?)record.Attribute("to");
                    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                        continue;
                    if (from.Length > 256 || to.Length > 1024)
                    {
                        rejected++;
                        continue;
                    }

                    var relative = to.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    if (Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar).Any(part => part == ".."))
                    {
                        rejected++;
                        continue;
                    }

                    var extension = Path.GetExtension(relative);
                    if (!ImageExtensions.Contains(extension))
                        continue;

                    var target = Path.GetFullPath(Path.Combine(configDirectory, relative));
                    if (!(target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(target, root, StringComparison.OrdinalIgnoreCase)))
                    {
                        rejected++;
                        continue;
                    }

                    var exists = File.Exists(target);
                    if (!exists) missing++;

                    if (!ids.Add(from.Trim()))
                        duplicates.Add(from.Trim());

                    mappings.Add(new GraphicsPackMapping(
                        from.Trim(),
                        target,
                        configPath,
                        Path.GetFileName(Path.GetDirectoryName(configPath) ?? root),
                        exists));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException)
            {
                Warn($"Could not parse '{Path.GetFileName(Path.GetDirectoryName(configPath))}/config.xml': {ex.GetType().Name}.");
            }
        }
    }
}
