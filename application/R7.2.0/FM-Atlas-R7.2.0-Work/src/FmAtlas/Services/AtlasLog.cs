using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FmAtlas.Services;

/// <summary>Small local rolling log. Log messages deliberately avoid player and save contents.</summary>
public static class AtlasLog
{
    private static readonly object Gate = new();
    private static bool _initialised;
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
    {
        if (exception is null) { Write("ERROR", message); return; }

        // Preserve the exception chain and WPF XAML line/position details. The old blanket
        // quote-redaction hid the exact dependency-property name needed to diagnose startup.
        var details = new StringBuilder(message);
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            details.Append(" | ").Append(current.GetType().FullName)
                .Append(" (0x").Append(current.HResult.ToString("X8")).Append("): ")
                .Append(current.Message);
            if (current is System.Windows.Markup.XamlParseException xaml)
                details.Append(" [XAML line ").Append(xaml.LineNumber)
                    .Append(", position ").Append(xaml.LinePosition).Append(']');
        }
        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
            details.Append(" | stack: ").Append(exception.StackTrace);
        Write("ERROR", details.ToString());
    }

    /// <summary>Applies the same privacy rules used by the rolling support log.</summary>
    public static string RedactForSupport(string value) => Redact(value);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                RotateIfNeeded();
                var safe = Redact(message);
                File.AppendAllText(CurrentLogPath, $"{DateTime.UtcNow:O}\t{level}\t{safe}{Environment.NewLine}", new UTF8Encoding(false));
            }
        }
        catch { /* Logging must never crash the application. */ }
    }

    private static string Redact(string value)
    {
        var text = value.Replace('\r', ' ').Replace('\n', ' ');
        text = Regex.Replace(text, @"(?i)[A-Z]:\\Users\\[^\\\s]+", "%USERPROFILE%");
        text = Regex.Replace(text, @"(?i)\\\\[^\\]+\\Users\\[^\\\s]+", "%USERPROFILE%");
        // Import exceptions can mention saves stored outside the user profile. Redact absolute Windows paths
        // and standalone Football Manager save filenames before writing to the default support log.
        text = Regex.Replace(text, @"(?i)\b[A-Z]:\\[^\s""<>|]+", "[path redacted]");
        text = Regex.Replace(text, @"(?i)\b[^\\/\s]+\.(?:fm|fmf|dat)\b", "[save-file]");
        return text.Length > 12000 ? text[..12000] + "…" : text;
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

    public static string ReadRecent(int maxCharacters = 30000)
    {
        try
        {
            if (!File.Exists(CurrentLogPath)) return "No application log has been written yet.";
            var text = File.ReadAllText(CurrentLogPath);
            if (text.Length > maxCharacters) text = text[^maxCharacters..];
            return text;
        }
        catch (Exception ex) { return "Log read failed: " + ex.GetType().Name; }
    }
}
