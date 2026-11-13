using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class SupportBundleComposerTests
{
    [TestMethod]
    public void Compose_DefaultBundle_UsesOnlyAllowListedFilesAndAccuratePrivacyCopy()
    {
        var package = SupportBundleComposer.Compose(
            "bundle-test-001",
            "1.2.3.4",
            includePersonalDetails: false,
            "FM Atlas diagnostics · no names",
            new[]
            {
                (Name: "recent-application-log.txt", Text: "FM Atlas started"),
                (Name: "application-logs/fm-atlas.1.log", Text: "Prior session ended")
            },
            new DateTime(2026, 11, 13, 17, 0, 0, DateTimeKind.Utc));

        CollectionAssert.AreEquivalent(
            new[]
            {
                "support-report.txt",
                "README.txt",
                "recent-application-log.txt",
                "application-logs/fm-atlas.1.log",
                "manifest.json"
            },
            package.Files.Keys.ToArray());

        var readme = Encoding.UTF8.GetString(package.Files["README.txt"]);
        StringAssert.Contains(readme, "Player names and edit history were excluded");
        StringAssert.Contains(readme, "Absolute paths and career save filenames are always redacted");
        StringAssert.Contains(readme, "No files are uploaded automatically");

        StringAssert.Contains(package.PreviewText, "Player names/edit history: excluded");
        StringAssert.Contains(package.PreviewText, "Absolute paths and career save filenames: always redacted");
        StringAssert.Contains(package.PreviewText, "SHA-256");
    }

    [TestMethod]
    public void Compose_ManifestHashesAndSizesMatchEveryPayloadEntry()
    {
        var package = SupportBundleComposer.Compose(
            "bundle-test-002",
            "1.2.3.4",
            false,
            "Report text",
            new[] { (Name: "recent-application-log.txt", Text: "Log text") });

        using var manifest = JsonDocument.Parse(package.Files["manifest.json"]);
        var root = manifest.RootElement;
        Assert.AreEqual(1, root.GetProperty("schema").GetInt32());
        Assert.AreEqual("bundle-test-002", root.GetProperty("bundleId").GetString());
        Assert.IsFalse(root.GetProperty("automaticallyUploaded").GetBoolean());
        Assert.IsFalse(root.GetProperty("personalDetailsIncluded").GetBoolean());

        var listed = root.GetProperty("files").EnumerateArray().ToArray();
        Assert.AreEqual(package.ManifestEntries.Count, listed.Length);
        foreach (var entry in listed)
        {
            var name = entry.GetProperty("name").GetString()!;
            var bytes = package.Files[name];
            Assert.AreEqual(bytes.LongLength, entry.GetProperty("sizeBytes").GetInt64());
            Assert.AreEqual(
                Convert.ToHexString(SHA256.HashData(bytes)),
                entry.GetProperty("sha256").GetString());
        }

        Assert.IsFalse(listed.Any(x => x.GetProperty("name").GetString() == "manifest.json"),
            "The manifest describes payload entries; it does not claim to hash itself.");
    }

    [TestMethod]
    public void CreateZipBytes_ProducesReadableArchiveWithExactPackageContents()
    {
        var package = SupportBundleComposer.Compose(
            "bundle-test-003",
            "1.2.3.4",
            true,
            "Player: Example Player · Edit: CA 120 -> 130",
            new[] { (Name: "recent-application-log.txt", Text: "Normal log entry") });

        using var memory = new MemoryStream(package.CreateZipBytes());
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        CollectionAssert.AreEquivalent(
            package.Files.Keys.ToArray(),
            archive.Entries.Select(x => x.FullName).ToArray());

        foreach (var expected in package.Files)
        {
            var entry = archive.GetEntry(expected.Key);
            Assert.IsNotNull(entry);
            using var stream = entry!.Open();
            using var output = new MemoryStream();
            stream.CopyTo(output);
            CollectionAssert.AreEqual(expected.Value, output.ToArray(), expected.Key);
        }
    }

    [TestMethod]
    public void Compose_RejectsArbitraryPathsAndUnrecognisedFiles()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            SupportBundleComposer.Compose(
                "bundle-test-004", "1.2.3.4", false, "report",
                new[] { (Name: "../career.fmf", Text: "not allowed") }));

        Assert.ThrowsExactly<ArgumentException>(() =>
            SupportBundleComposer.Compose(
                "bundle-test-005", "1.2.3.4", false, "report",
                new[] { (Name: "C:/Users/Player/save.fmf", Text: "not allowed") }));

        Assert.ThrowsExactly<ArgumentException>(() =>
            SupportBundleComposer.Compose(
                "bundle-test-006", "1.2.3.4", false, "report",
                new[] { (Name: "arbitrary.txt", Text: "not allowed") }));
    }

    [TestMethod]
    public void Compose_RedactsSecretsPathsAndSaveNamesFromReportsAndLogs()
    {
        var package = SupportBundleComposer.Compose(
            "bundle-test-007",
            "1.2.3.4",
            false,
            "password: atlas-secret-001 · C:\\Users\\AtlasUser\\Career\\Career.fmf",
            new[] { (Name: "recent-application-log.txt", Text: "Authorization: Bearer atlas-token-001") });

        var report = Encoding.UTF8.GetString(package.Files["support-report.txt"]);
        var log = Encoding.UTF8.GetString(package.Files["recent-application-log.txt"]);
        Assert.IsFalse(report.Contains("atlas-secret-001", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains("AtlasUser", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains("Career.fmf", StringComparison.Ordinal));
        Assert.IsFalse(log.Contains("atlas-token-001", StringComparison.Ordinal));
    }
}
