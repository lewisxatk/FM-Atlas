using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace FmAtlas.Services;

public enum AtlasThemeMode
{
    Obsidian,
    WarmLight
}

/// <summary>
/// Central MONOLITH design tokens. Page helpers resolve their legacy palette colours through this
/// service, allowing the application to move from the R7 palette without duplicating page styles.
/// </summary>
public static class MonolithTheme
{
    private static readonly object Gate = new();
    private sealed record LegacyBrushEntry(WeakReference<SolidColorBrush> Brush, string OriginalHex);
    private static readonly List<LegacyBrushEntry> LegacyBrushes = new();
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FmAtlas", "theme.json");

    private static AtlasThemeMode _mode = LoadMode();

    public static AtlasThemeMode CurrentMode
    {
        get { lock (Gate) return _mode; }
    }

    public static string FontFamilyName =>
        Fonts.SystemFontFamilies.Any(f => string.Equals(f.Source, "Inter", StringComparison.OrdinalIgnoreCase))
            ? "Inter"
            : "Segoe UI";

    public static void Apply(Application? application = null)
    {
        var app = application ?? Application.Current;
        if (app is null) return;

        var light = CurrentMode == AtlasThemeMode.WarmLight;
        var tokens = light
            ? new Dictionary<string, string>
            {
                ["Canvas"] = "#F2F2EF",
                ["Surface"] = "#FFFFFF",
                ["SurfaceRaised"] = "#FFFFFF",
                ["SurfaceSubtle"] = "#E9E8E2",
                ["Border"] = "#DAD9D2",
                ["TextPrimary"] = "#242830",
                ["TextMuted"] = "#727780",
                ["Accent"] = "#E87826",
                ["AccentSoft"] = "#FBE7D8",
                ["Success"] = "#287A55",
                ["Warning"] = "#A66B19",
                ["Danger"] = "#B54750"
            }
            : new Dictionary<string, string>
            {
                ["Canvas"] = "#101216",
                ["Surface"] = "#1B1F25",
                ["SurfaceRaised"] = "#252A32",
                ["SurfaceSubtle"] = "#20242B",
                ["Border"] = "#343A43",
                ["TextPrimary"] = "#F1F2F3",
                ["TextMuted"] = "#9299A3",
                ["Accent"] = "#F28A36",
                ["AccentSoft"] = "#54351F",
                ["Success"] = "#8BCFA6",
                ["Warning"] = "#E6C477",
                ["Danger"] = "#F08C96"
            };

        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(() => Apply(app));
            return;
        }

        // Keep token brush instances stable so controls already using them update in place.
        foreach (var pair in tokens)
        {
            var key = "Atlas." + pair.Key;
            if (app.Resources[key] is SolidColorBrush existing && !existing.IsFrozen)
                existing.Color = ParseColor(pair.Value);
            else
                app.Resources[key] = MakeBrush(pair.Value);
        }

        // Legacy page helpers also receive tracked mutable brushes. Re-map them when the
        // theme changes so existing pages do not need to be destroyed/recreated.
        lock (Gate)
        {
            for (var i = LegacyBrushes.Count - 1; i >= 0; i--)
            {
                var entry = LegacyBrushes[i];
                if (!entry.Brush.TryGetTarget(out var brush))
                {
                    LegacyBrushes.RemoveAt(i);
                    continue;
                }

                if (!brush.IsFrozen)
                    brush.Color = ParseColor(MapLegacy(entry.OriginalHex, light));
            }
        }

        app.Resources["Atlas.FontFamily"] = new FontFamily(FontFamilyName);
        app.Resources["Atlas.ThemeName"] = light ? "Warm Light" : "Obsidian";
    }

    public static void SetMode(AtlasThemeMode mode)
    {
        lock (Gate)
        {
            _mode = mode;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { theme = mode.ToString() }));
            }
            catch (Exception ex)
            {
                AtlasLog.Warning("Could not persist theme preference: " + ex.GetType().Name);
            }
        }

        Apply();
        AtlasLog.Info("MONOLITH theme changed to " + mode + ".");
    }

    public static Brush GetBrush(string token)
    {
        var key = "Atlas." + token;
        if (Application.Current?.Resources[key] is Brush brush) return brush;
        var fallback = token switch
        {
            "Canvas" => "#101216",
            "Surface" => "#1B1F25",
            "SurfaceRaised" => "#252A32",
            "Border" => "#343A43",
            "TextPrimary" => "#F1F2F3",
            "TextMuted" => "#9299A3",
            "Accent" => "#F28A36",
            "AccentSoft" => "#54351F",
            "Success" => "#8BCFA6",
            "Warning" => "#E6C477",
            "Danger" => "#F08C96",
            _ => "#F1F2F3"
        };
        return MakeBrush(fallback);
    }

    /// <summary>Maps old R7 page colours to MONOLITH semantic roles.</summary>
    public static Brush ResolveLegacy(string hex)
    {
        var original = (hex ?? string.Empty).Trim();
        var brush = MakeBrush(MapLegacy(original, CurrentMode == AtlasThemeMode.WarmLight));
        lock (Gate)
        {
            LegacyBrushes.RemoveAll(entry => !entry.Brush.TryGetTarget(out _));
            LegacyBrushes.Add(new LegacyBrushEntry(new WeakReference<SolidColorBrush>(brush), original));
        }
        return brush;
    }

    private static string MapLegacy(string hex, bool light)
    {

        var value = (hex ?? string.Empty).Trim().ToUpperInvariant();

        static bool Is(string candidate, params string[] values)
            => values.Contains(candidate, StringComparer.OrdinalIgnoreCase);

        string mapped;
        if (Is(value,
            "#C6F36B", "#D8F5A2", "#087F91", "#2D75B6", "#2D8A63", "#3988C8",
            "#205F91", "#237C62", "#2A669C", "#356C98", "#277BB9", "#16845D", "#4BA6A8", "#2BA578"))
            mapped = light ? "#E87826" : "#F28A36";
        else if (Is(value,
            "#63778A", "#647A8E", "#6F8293", "#8293A1", "#526B7D", "#536C7F",
            "#9EADB9", "#B9C5D0", "#485562", "#35424E"))
            mapped = light ? "#727780" : "#9299A3";
        else if (Is(value,
            "#D7E3EA", "#D4E1EC", "#DDE7ED", "#D5E0E7", "#D8E4ED",
            "#E1E9EF", "#C9D9E8", "#D9E7F1", "#DCE8F2", "#C4CED8"))
            mapped = light ? "#DAD9D2" : "#343A43";
        else if (Is(value, "#FFFFFF", "#FBFDFF", "#F9FDFC", "#F8FCFA", "#F7FBFA", "#F6FCFA",
            "#F4F8FC", "#F4F8FB", "#F5F8FC", "#F4F8FB", "#EEF4F8", "#EEF4F9",
            "#EAF1F5", "#EAF1F8", "#EAF2F9", "#E9EFF3", "#E8EEF2", "#E6EDF2"))
            mapped = light ? "#FFFFFF" : "#1B1F25";
        else if (Is(value, "#0D1117", "#111820", "#151B22", "#171F28", "#182F45",
            "#183B56", "#1A3851", "#173B55", "#20342C", "#2A3427", "#30291D",
            "#35252A", "#342C1C"))
            mapped = light ? "#F2F2EF" : "#101216";
        else if (Is(value, "#1C242E", "#202A35", "#20242B", "#66563A", "#66502C"))
            mapped = light ? "#E9E8E2" : "#252A32";
        else if (Is(value, "#F08C96", "#A54E55", "#B44B52", "#B94A48", "#D77B7E", "#C7797B", "#F8E8E9"))
            mapped = light ? "#B54750" : "#F08C96";
        else if (Is(value, "#176B46", "#2F825D", "#37875A", "#4F9B70", "#8BCFA6"))
            mapped = light ? "#287A55" : "#8BCFA6";
        else if (Is(value, "#D9A44D", "#78591F", "#84612B", "#8B622C", "#94652A", "#A06B24",
            "#A46E2B", "#A56D22", "#C47B32", "#D18A3A", "#D8A34A", "#E5A65D",
            "#EBD5A7", "#EED8AA", "#F8F0DF", "#FFF4DE", "#FFF5E4", "#66563A"))
            mapped = light ? "#A66B19" : "#E6C477";
        else if (Is(value, "#B45B2F"))
            mapped = light ? "#B65D25" : "#E6A06E";
        else if (Is(value, "#E8EEF4", "#DCE5ED", "#B7E5E0"))
            mapped = light ? "#242830" : "#F1F2F3";
        else
            mapped = value;

        return mapped;
    }

    private static AtlasThemeMode LoadMode()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
                if (document.RootElement.TryGetProperty("theme", out var element)
                    && Enum.TryParse<AtlasThemeMode>(element.GetString(), true, out var mode))
                    return mode;
            }
        }
        catch { }
        return AtlasThemeMode.Obsidian;
    }

    private static Color ParseColor(string hex)
        => (Color)ColorConverter.ConvertFromString(hex);

    private static SolidColorBrush MakeBrush(string hex)
        => new(ParseColor(hex));
}
