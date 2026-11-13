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

        foreach (var pair in tokens)
            app.Resources["Atlas." + pair.Key] = MakeBrush(pair.Value);

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
        var value = hex.ToUpperInvariant();
        var light = CurrentMode == AtlasThemeMode.WarmLight;
        string mapped;

        if (value is "#C6F36B" or "#D8F5A2" or "#087F91" or "#2D75B6" or "#2D8A63" or "#3988C8"
            or "#205F91" or "#237C62" or "#2A669C" or "#356C98" or "#277BB9" or "#16845D" or "#4BA6A8")
            mapped = light ? "#E87826" : "#F28A36";
        else if (value is "#182F45" or "#183B56" or "#1A3851" or "#173B55" or "#25435E" or "#334F65"
            or "#365F83" or "#3D586E" or "#486477")
            mapped = light ? "#242830" : "#F1F2F3";
        else if (value is "#63778A" or "#647A8E" or "#6F8293" or "#8293A1" or "#526B7D" or "#536C7F")
            mapped = light ? "#727780" : "#9299A3";
        else if (value is "#D7E3EA" or "#D4E1EC" or "#DDE7ED" or "#D5E0E7" or "#D8E4ED" or "#D9E5EF"
            or "#E1E9EF" or "#C9D9E8")
            mapped = light ? "#DAD9D2" : "#343A43";
        else if (value is "#FFFFFF" or "#FBFDFF" or "#F9FDFC" or "#F8FCFA" or "#F7FBFA" or "#F6FCFA"
            or "#F4F8FC" or "#F4F8FB" or "#F5F8FC" or "#EFF5FA" or "#EAF1F8" or "#EAF2F9"
            or "#E9EFF3" or "#E8EEF2" or "#E6EDF2" or "#E1F3EA" or "#D3E9E4" or "#DBF2EF")
            mapped = light ? "#FFFFFF" : "#1B1F25";
        else if (value is "#EEF4F8" or "#EEF4F9" or "#EAF1F5" or "#0D1117" or "#111820" or "#151B22")
            mapped = light ? "#F2F2EF" : "#101216";
        else if (value is "#1C242E" or "#202A35" or "#20242B" or "#171F28")
            mapped = light ? "#E9E8E2" : "#252A32";
        else if (value is "#F08C96" or "#A54E55" or "#B44B52" or "#B94A48" or "#D77B7E")
            mapped = light ? "#B54750" : "#F08C96";
        else if (value is "#176B46" or "#2F825D" or "#37875A" or "#4F9B70")
            mapped = light ? "#287A55" : "#8BCFA6";
        else if (value is "#D9A44D" or "#78591F" or "#84612B" or "#8B622C" or "#94652A" or "#A06B24"
            or "#A46E2B" or "#A56D22" or "#C47B32" or "#D18A3A")
            mapped = light ? "#A66B19" : "#E6C477";
        else if (value == "#B45B2F")
            mapped = light ? "#B65D25" : "#E6A06E";
        else
            mapped = value;

        return MakeBrush(mapped);
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

    private static SolidColorBrush MakeBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
