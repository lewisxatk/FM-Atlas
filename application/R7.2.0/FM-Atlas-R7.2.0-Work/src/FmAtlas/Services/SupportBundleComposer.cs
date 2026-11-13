using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FmAtlas.Services;

public sealed record SupportBundleManifestFile(string Name, long SizeBytes, string Sha256);

public sealed record SupportBundlePackage(
    IReadOnlyDictionary<string, byte[]> Files,
    IReadOnlyList<SupportBundleManifestFile> ManifestEntries,
    string PreviewText)
{
    public byte[] CreateZipBytes()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var pair in Files.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var entry = archive.CreateEntry(pair.Key, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(pair.Value, 0, pair.Value.Length);
            }
        }
        return memory.ToArray();
    }
}

/// <summary>
/// Composes the exact allow-listed support bundle payload. It deliberately accepts only
/// diagnostic text and named application logs; it has no API for adding arbitrary filesystem
/// paths, career saves, SQLite files or FM installation files.
/// </summary>
public static class SupportBundleComposer
{
    private static readonly Regex RotatedLogName = new(
        @"\Aapplication-logs/fm-atlas\.[1-4]\.log\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static SupportBundlePackage Compose(
        string bundleId,
        string appVersion,
        bool includePersonalDetails,
        string diagnosticReport,
        IEnumerable<(string Name, string Text)> logFiles,
        DateTime? generatedUtc = null)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
            throw new ArgumentException("A bundle ID is required.", nameof(bundleId));
        if (string.IsNullOrWhiteSpace(appVersion))
            throw new ArgumentException("An application version is required.", nameof(appVersion));
        ArgumentNullException.ThrowIfNull(diagnosticReport);
        ArgumentNullException.ThrowIfNull(logFiles);

        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["support-report.txt"] = new UTF8Encoding(false).GetBytes(AtlasLog.RedactForSupport(diagnosticReport)),
            ["README.txt"] = new UTF8Encoding(false).GetBytes(
                "FM Atlas support bundle\r\n" +
                "This ZIP contains only the reviewed diagnostic report and named application logs.\r\n" +
                (includePersonalDetails
                    ? "You opted in to include player names and recent edit history. Absolute paths and career save filenames remain redacted. Review before sharing.\r\n"
                    : "Player names and edit history were excluded. Absolute paths and career save filenames are always redacted.\r\n") +
                "Career saves, the SQLite database, FM installation files and arbitrary user files are not included.\r\n" +
                "No files are uploaded automatically. You choose whether and how to share this ZIP.\r\n")
        };

        foreach (var log in logFiles)
        {
            ValidateLogName(log.Name);
            if (files.ContainsKey(log.Name) || string.Equals(log.Name, "manifest.json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Support bundle contains a duplicate or reserved filename: " + log.Name, nameof(logFiles));

            var safeText = AtlasLog.RedactForSupport(log.Text ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(safeText))
                files.Add(log.Name, new UTF8Encoding(false).GetBytes(safeText));
        }

        if (!files.ContainsKey("recent-application-log.txt"))
            files.Add("recent-application-log.txt", new UTF8Encoding(false).GetBytes("No application log is available."));

        var entries = files
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new SupportBundleManifestFile(
                pair.Key,
                pair.Value.LongLength,
                Convert.ToHexString(SHA256.HashData(pair.Value))))
            .ToArray();

        var manifest = new
        {
            schema = 1,
            bundleId,
            generatedUtc = (generatedUtc ?? DateTime.UtcNow).ToUniversalTime(),
            appVersion,
            personalDetailsIncluded = includePersonalDetails,
            automaticallyUploaded = false,
            excludedByDesign = new[]
            {
                "career saves",
                "SQLite database",
                "FM installation files",
                "arbitrary user files"
            },
            alwaysRedacted = new[]
            {
                "credentials and tokens",
                "absolute file paths",
                "career save filenames"
            },
            files = entries
        };
        files.Add("manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        var preview = new StringBuilder();
        preview.AppendLine("SUPPORT BUNDLE CONTENTS");
        preview.AppendLine("Bundle ID: " + bundleId);
        preview.AppendLine("Player names/edit history: " + (includePersonalDetails ? "included because you opted in" : "excluded"));
        preview.AppendLine("Absolute paths and career save filenames: always redacted");
        preview.AppendLine();
        preview.AppendLine("Files to be exported:");
        foreach (var item in entries)
            preview.AppendLine($"• {item.Name} · {item.SizeBytes:N0} bytes · SHA-256 {item.Sha256}");
        preview.AppendLine("• manifest.json");
        preview.AppendLine();
        preview.AppendLine("Excluded: career save files, FM installation files, SQLite database and arbitrary user files.");
        preview.AppendLine("Credentials/tokens, absolute paths and career save filenames are always redacted.");
        preview.AppendLine("No automatic upload. The ZIP is saved locally and only shared if you choose.");
        preview.AppendLine();
        preview.AppendLine("DIAGNOSTIC REPORT PREVIEW");
        preview.AppendLine(Encoding.UTF8.GetString(files["support-report.txt"]));

        foreach (var pair in files.Where(x => x.Key.EndsWith(".log", StringComparison.OrdinalIgnoreCase)))
        {
            preview.AppendLine();
            preview.AppendLine("LOG CONTENT PREVIEW · " + pair.Key);
            var logText = Encoding.UTF8.GetString(pair.Value);
            preview.AppendLine(logText.Length > 5000 ? logText[^5000..] : logText);
        }

        return new SupportBundlePackage(files, entries, preview.ToString());
    }

    private static void ValidateLogName(string? name)
    {
        if (string.Equals(name, "recent-application-log.txt", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(name) && RotatedLogName.IsMatch(name)))
            return;

        throw new ArgumentException(
            "Only recent-application-log.txt and application-logs/fm-atlas.1.log through .4.log may be added.",
            nameof(name));
    }
}
