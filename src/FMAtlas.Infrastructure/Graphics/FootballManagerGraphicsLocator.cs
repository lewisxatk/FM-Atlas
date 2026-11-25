using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Graphics;

namespace FMAtlas.Infrastructure.Graphics;

/// <summary>
/// Independent, read-only Football Manager graphics config indexer.
/// It resolves the standard config.xml mapping forms for person portraits,
/// club logos and nation logos. It does not load image bytes or change assets.
/// </summary>
public sealed class FootballManagerGraphicsLocator : IGraphicsLocator
{
    private static readonly string[] ImageExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff"];

    private sealed record Snapshot(
        string RootPath,
        IReadOnlyDictionary<(GraphicsAssetKind Kind, uint Id), GraphicsAssetReference> Assets,
        GraphicsIndexSummary Summary);

    private Snapshot _snapshot = new(
        string.Empty,
        new Dictionary<(GraphicsAssetKind, uint), GraphicsAssetReference>(),
        GraphicsIndexSummary.Off);

    public GraphicsIndexSummary Status => Volatile.Read(ref _snapshot).Summary;

    public async Task<GraphicsIndexSummary> IndexAsync(
        string graphicsFolder,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await Task.Run(
            () => BuildSnapshot(graphicsFolder, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        Interlocked.Exchange(ref _snapshot, snapshot);
        return snapshot.Summary;
    }

    public GraphicsAssetReference? FindPortrait(string playerUid)
    {
        if (!TryParseGameId(playerUid, out var id))
            return null;

        return Find(GraphicsAssetKind.Portrait, id);
    }

    public GraphicsAssetReference? FindClubLogo(uint clubId) =>
        clubId == 0 ? null : Find(GraphicsAssetKind.ClubLogo, clubId);

    public GraphicsAssetReference? FindNationLogo(uint nationId) =>
        nationId == 0 ? null : Find(GraphicsAssetKind.NationLogo, nationId);

    public void Clear()
    {
        Interlocked.Exchange(
            ref _snapshot,
            new Snapshot(
                string.Empty,
                new Dictionary<(GraphicsAssetKind, uint), GraphicsAssetReference>(),
                GraphicsIndexSummary.Off));
    }

    private GraphicsAssetReference? Find(GraphicsAssetKind kind, uint id)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot.Assets.TryGetValue((kind, id), out var asset) ? asset : null;
    }

    private static Snapshot BuildSnapshot(string graphicsFolder, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(graphicsFolder))
            return EmptySnapshot(GraphicsIndexState.Missing, "No graphics folder was supplied.");

        string root;
        try
        {
            root = Path.GetFullPath(graphicsFolder);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return EmptySnapshot(GraphicsIndexState.Failed, "The graphics folder path is invalid.");
        }

        if (!Directory.Exists(root))
            return EmptySnapshot(GraphicsIndexState.Missing, "The graphics folder does not exist.");

        var warnings = new List<string>();
        string[] configPaths;

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            configPaths = Directory
                .EnumerateFiles(root, "config.xml", options)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return EmptySnapshot(GraphicsIndexState.Failed, "The graphics folder could not be enumerated.");
        }

        var assets = new Dictionary<(GraphicsAssetKind Kind, uint Id), GraphicsAssetReference>();
        var readableConfigFiles = 0;

        foreach (var configPath in configPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            XDocument document;
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                    MaxCharactersInDocument = 10_000_000,
                    MaxCharactersFromEntities = 0
                };

                using var stream = new FileStream(
                    configPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                using var reader = XmlReader.Create(stream, settings);
                document = XDocument.Load(reader, LoadOptions.None);
                readableConfigFiles++;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException)
            {
                AddWarning(warnings, "A config.xml file could not be read and was skipped.");
                continue;
            }

            var configDirectory = Path.GetDirectoryName(configPath) ?? root;

            foreach (var element in document.Descendants().Where(x => x.Name.LocalName == "record"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var from = ((string?)element.Attribute("from"))?.Trim();
                var to = ((string?)element.Attribute("to"))?.Trim();

                if (string.IsNullOrWhiteSpace(from) ||
                    !TryParseTarget(to, out var kind, out var gameId))
                    continue;

                var imagePath = ResolveMappedImage(root, configDirectory, from);
                if (imagePath is null)
                    continue;

                var key = (kind, gameId);
                // Config files are processed in deterministic sorted order.
                // The first valid mapping wins when packs contain duplicate IDs.
                assets.TryAdd(key, new GraphicsAssetReference(kind, gameId, imagePath, configPath));
            }
        }

        var summary = new GraphicsIndexSummary(
            GraphicsIndexState.Ready,
            root,
            readableConfigFiles,
            assets.Keys.Count(key => key.Kind == GraphicsAssetKind.Portrait),
            assets.Keys.Count(key => key.Kind == GraphicsAssetKind.ClubLogo),
            assets.Keys.Count(key => key.Kind == GraphicsAssetKind.NationLogo),
            warnings.AsReadOnly());

        return new Snapshot(root, assets, summary);
    }

    private static Snapshot EmptySnapshot(GraphicsIndexState state, string warning)
    {
        var summary = new GraphicsIndexSummary(
            state, string.Empty, 0, 0, 0, 0, new[] { warning });

        return new Snapshot(
            string.Empty,
            new Dictionary<(GraphicsAssetKind, uint), GraphicsAssetReference>(),
            summary);
    }

    private static void AddWarning(ICollection<string> warnings, string warning)
    {
        if (warnings.Count < 50)
            warnings.Add(warning);
    }

    private static bool TryParseTarget(
        string? target,
        out GraphicsAssetKind kind,
        out uint gameId)
    {
        kind = default;
        gameId = 0;

        const string prefix = "graphics/pictures/";
        if (string.IsNullOrWhiteSpace(target) ||
            !target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = target[prefix.Length..].Split(
            '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 3 ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out gameId) ||
            gameId == 0)
            return false;

        if (parts[0].Equals("person", StringComparison.OrdinalIgnoreCase) &&
            parts[2].Equals("portrait", StringComparison.OrdinalIgnoreCase))
        {
            kind = GraphicsAssetKind.Portrait;
            return true;
        }

        if (parts[0].Equals("club", StringComparison.OrdinalIgnoreCase) &&
            parts[2].Equals("logo", StringComparison.OrdinalIgnoreCase))
        {
            kind = GraphicsAssetKind.ClubLogo;
            return true;
        }

        if (parts[0].Equals("nation", StringComparison.OrdinalIgnoreCase) &&
            parts[2].Equals("logo", StringComparison.OrdinalIgnoreCase))
        {
            kind = GraphicsAssetKind.NationLogo;
            return true;
        }

        return false;
    }

    private static string? ResolveMappedImage(string root, string configDirectory, string from)
    {
        if (Path.IsPathRooted(from) || from.IndexOfAny(['*', '?']) >= 0)
            return null;

        var relativePath = from
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        string basePath;
        try
        {
            basePath = Path.GetFullPath(Path.Combine(configDirectory, relativePath));
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }

        if (!IsWithinRoot(root, basePath))
            return null;

        if (Path.HasExtension(basePath))
            return File.Exists(basePath) ? basePath : null;

        foreach (var extension in ImageExtensions)
        {
            var candidate = basePath + extension;
            if (File.Exists(candidate))
                return candidate;
        }

        return File.Exists(basePath) ? basePath : null;
    }

    private static bool IsWithinRoot(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        if (Path.IsPathRooted(relative))
            return false;

        return relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static bool TryParseGameId(string? uid, out uint id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(uid))
            return false;

        var value = uid.Trim();
        if (value.StartsWith("r-", StringComparison.OrdinalIgnoreCase))
            value = value[2..];

        return uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id != 0;
    }
}
