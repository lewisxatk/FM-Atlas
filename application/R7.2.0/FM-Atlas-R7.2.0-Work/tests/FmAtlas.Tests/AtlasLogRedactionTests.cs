using FmAtlas.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class AtlasLogRedactionTests
{
    [TestMethod]
    public void ValidateRedaction_ShouldPassEveryBuiltInPrivacyCase()
    {
        var failures = AtlasLog.ValidateRedaction();

        Assert.AreEqual(
            0,
            failures.Count,
            "Redaction regression failures: " + string.Join(", ", failures));
    }

    [TestMethod]
    [DataRow("Authorization: Bearer atlas-test-token-123", "atlas-test-token-123")]
    [DataRow("api_key=atlas-test-api-key-456", "atlas-test-api-key-456")]
    [DataRow("password: atlas-test-password-789", "atlas-test-password-789")]
    [DataRow(@"C:\Users\AtlasTestUser\AppData\Local\FmAtlas\scouting.db", "AtlasTestUser")]
    [DataRow(@"D:\Football Manager\Career\atlas-test-career.fmf", "Football Manager")]
    [DataRow(@"\\AtlasServer\Users\AtlasTestUser\Documents\Career\save.fmf", "AtlasServer")]
    [DataRow("Failed to read atlas-test-career.fm", "atlas-test-career.fm")]
    [DataRow("\"api_key\": \"atlas secret with spaces\"", "atlas secret with spaces")]
    [DataRow("Authorization: Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==", "QWxhZGRpbjpvcGVuIHNlc2FtZQ==")]
    [DataRow("client_secret=atlas-client-secret-456", "atlas-client-secret-456")]
    [DataRow("https://example.invalid/callback?access_token=atlas-query-token-123", "atlas-query-token-123")]
    public void RedactForSupport_ShouldRemoveSensitiveValues(string input, string forbidden)
    {
        var sanitized = AtlasLog.RedactForSupport(input);

        Assert.IsFalse(
            sanitized.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
            "Sensitive content remained after redaction: " + forbidden);
    }

    [TestMethod]
    public void RedactForSupport_ShouldFlattenLineBreaksAndCapOutput()
    {
        var multiline = AtlasLog.RedactForSupport("first line\r\nsecond line");
        Assert.IsFalse(multiline.Contains('\r'));
        Assert.IsFalse(multiline.Contains('\n'));

        var longMessage = AtlasLog.RedactForSupport(new string('x', 5_000));
        Assert.AreEqual(4_001, longMessage.Length);
        Assert.IsTrue(longMessage.EndsWith("…", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RedactForSupport_ShouldPreserveOrdinaryNonSensitiveText()
    {
        const string input = "FM Atlas connected · 42 players scanned · Diagnostics ready";

        Assert.AreEqual(input, AtlasLog.RedactForSupport(input));
    }
}
