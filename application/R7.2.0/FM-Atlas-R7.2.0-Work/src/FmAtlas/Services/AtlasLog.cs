using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FmAtlas.Services;

/// <summary>Small local rolling log. Log messages deliberately avoid player and save contents.</summary>
public static class AtlasLog
{
    private static readonly object Gate = new();
    private static bool _initialised;
    private static readonly string SessionId = Guid.NewGuid().ToString("N");
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas", "logs");
    public static string LogFolder => Folder;
    public static string CurrentLogPath => Path.Combine(Folder, "fm-atlas.log");

    public static void Initialise()
    {
        lock (Gate)
        {
            if (_initialised) return;
            Directory.CreateDirectory(Folder);
            RotateIfNeeded();
            _initialised = true;
            Write("INFO", "Application logging started.");
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warning(string message) => Write("WARN", message);
    public static void Error(string message, Exception? exception = null)
        => Write("ERROR", exception is null ? message : message + Environment.NewLine + exception);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                RotateIfNeeded();
                var safe = Redact(message);
                File.AppendAllText(CurrentLogPath, $"{DateTime.UtcNow:O}\t{SessionId}\t{level}\t{safe}{Environment.NewLine}", new UTF8Encoding(false));
            }
        }
        catch { /* Logging must never crash the application. */ }
    }

    public static string RedactForSupport(string value) => Redact(value);

    /// <summary>
    /// Runs deterministic privacy regression cases used by the Diagnostics page.
    /// These checks verify that common secrets, personal paths and save names do not
    /// survive the same redaction pipeline used for logs and support reports.
    /// </summary>
    public static IReadOnlyList<string> ValidateRedaction()
    {
        var cases = new[]
        {
            (Name: "bearer-token", Input: "Authorization: Bearer atlas-test-token-123", Forbidden: "atlas-test-token-123"),
            (Name: "api-key", Input: "api_key=atlas-test-api-key-456", Forbidden: "atlas-test-api-key-456"),
            (Name: "password", Input: "password: atlas-test-password-789", Forbidden: "atlas-test-password-789"),
            (Name: "user-path", Input: @"C:\Users\AtlasTestUser\AppData\Local\FmAtlas\scouting.db", Forbidden: "AppData"),
            (Name: "save-path", Input: @"D:\Football Manager\Career\atlas-test-career.fmf", Forbidden: "Football Manager"),
            (Name: "unc-path", Input: @"\\AtlasServer\Users\AtlasTestUser\Documents\Career\save.fmf", Forbidden: "AtlasServer"),
            (Name: "standalone-save-name", Input: "Failed to read atlas-test-career.fm", Forbidden: "atlas-test-career.fm")
        };

        var failures = new List<string>();
        foreach (var test in cases)
        {
            var redacted = Redact(test.Input);
            if (redacted.Contains(test.Forbidden, StringComparison.OrdinalIgnoreCase))
                failures.Add(test.Name);
        }

        return failures;
    }

    private static string Redact(string value)
    {
        var text = value.Replace('\r', ' ').Replace('\n', ' ');
        // Scrub common credential/token formats before a log can enter a support bundle.
        text = Regex.Replace(text, @"(?i)\b(Bearer\s+)[A-Za-z0-9._~+/=-]+", "$1[redacted]");
        text = Regex.Replace(text, @"(?i)\b(api[_-]?key|access[_-]?token|refresh[_-]?token|password|passwd|authorization)\s*[:=]\s*[^;\s,]+", "$1=[redacted]");
        text = Regex.Replace(text, @"(?i)\b[A-Z]:\\[^""<>|,;()\r\n]+", "[path redacted]");
        text = Regex.Replace(text, @"(?i)\\\\[^\\\r\n]+(?:\\[^\\\r\n]+)+", "[path redacted]");
        // and standalone Football Manager save filenames before writing to the default support log.
        text = Regex.Replace(text, @"(?i)\b[A-Z]:\\[^\s""<>|]+", "[path redacted]");
        text = Regex.Replace(text, @"(?i)\b[^\\/\s]+\.(?:fm|fmf|dat)\b", "[save-file]");
        return text.Length > 4000 ? text[..4000] + "…" : text;
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(CurrentLogPath) || new FileInfo(CurrentLogPath).Length < MaxBytes) return;
            var oldest = Path.Combine(Folder, "fm-atlas.4.log"); if (File.Exists(oldest)) File.Delete(oldest);
            for (var i = 3; i >= 1; i--)
            {
                var from = Path.Combine(Folder, $"fm-atlas.{i}.log");
                var to = Path.Combine(Folder, $"fm-atlas.{i + 1}.log");
                if (File.Exists(from)) File.Move(from, to, true);
            }
            File.Move(CurrentLogPath, Path.Combine(Folder, "fm-atlas.1.log"), true);
        }
        catch { }
    }

    public static IReadOnlyList<string> GetLogFiles()
    {
        try
        {
            if (!Directory.Exists(Folder)) return Array.Empty<string>();
            return Directory.GetFiles(Folder, "fm-atlas*.log")
                .OrderByDescending(x => string.Equals(x, CurrentLogPath, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .Take(5)
                .ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    public static string ReadFileForSupport(string path, int maxCharacters = 60000)
    {
        try
        {
            if (!File.Exists(path)) return string.Empty;
            var text = Redact(File.ReadAllText(path));
            if (text.Length > maxCharacters) text = text[^maxCharacters..];
            return text;
        }
        catch (Exception ex) { return "Log read failed: " + ex.GetType().Name; }
    }

    public static string ReadRecent(int maxCharacters = 30000)
        => File.Exists(CurrentLogPath)
            ? ReadFileForSupport(CurrentLogPath, maxCharacters)
            : "No application log has been written yet.";
}
