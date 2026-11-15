using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FmAtlas;

/// <summary>
/// Shared visual tokens and primitives for the V9 full interface rebuild.
/// The presentation layer is deliberately separated from game/data services.
/// </summary>
internal static class MonolithDesignSystem
{
    public const string Canvas = "#07080A";
    public const string Surface = "#0C0E11";
    public const string SurfaceRaised = "#14171C";
    public const string SurfaceHover = "#1C2027";
    public const string Border = "#292B30";
    public const string Text = "#ECE8E0";
    public const string Muted = "#A8A39A";
    public const string Signal = "#FF6A1A";
    public const string SignalSoft = "#351A0F";
    public const string Positive = "#83C99A";
    public const string Caution = "#E8C547";
    public const string Negative = "#E27B76";

    private static bool _lightAppearance;

    public static void SetAppearance(string appearance)
        => _lightAppearance = appearance.Equals("Glass Light", StringComparison.OrdinalIgnoreCase);

    public static readonly FontFamily DisplayFont = new("Bahnschrift");
    public static readonly FontFamily BodyFont = new("Segoe UI");
    public static readonly FontFamily DataFont = new("Consolas");
    public static readonly FontFamily EditorialFont = new("Georgia");

    public static Brush Solid(string hex)
    {
        if (_lightAppearance)
        {
            hex = hex.ToUpperInvariant() switch
            {
                Canvas => "#F2F0EB",
                Surface => "#FBFAF7",
                SurfaceRaised => "#F1EFE9",
                SurfaceHover => "#E7E3DA",
                Border => "#D4CEC3",
                Text => "#211F1A",
                Muted => "#68635B",
                SignalSoft => "#F9E7DC",
                Positive => "#287A52",
                Caution => "#956518",
                Negative => "#AF3E4C",
                _ => hex
            };
        }
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public static TextBox TextInput(string placeholder, double fontSize = 12)
        => new()
        {
            Text = "",
            Tag = placeholder,
            ToolTip = placeholder,
            FontFamily = BodyFont,
            FontSize = fontSize,
            Foreground = Solid(Text),
            Background = Solid(SurfaceRaised),
            BorderBrush = Solid(Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 3, 0, 8),
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center
        };

    public static TextBlock DisplayText(string text, double size, Brush? foreground = null)
        => new()
        {
            Text = text,
            FontFamily = DisplayFont,
            FontSize = size,
            FontWeight = FontWeights.SemiBold,
            FontStretch = FontStretches.Condensed,
            Foreground = foreground ?? Solid(Text),
            TextWrapping = TextWrapping.Wrap
        };

    public static TextBlock BodyText(string text, double size = 12.5, Brush? foreground = null)
        => new()
        {
            Text = text,
            FontFamily = BodyFont,
            FontSize = size,
            Foreground = foreground ?? Solid(Text),
            TextWrapping = TextWrapping.Wrap
        };

    public static TextBlock DataText(string text, double size = 11, Brush? foreground = null)
        => new()
        {
            Text = text,
            FontFamily = DataFont,
            FontSize = size,
            Foreground = foreground ?? Solid(Text),
            TextWrapping = TextWrapping.Wrap
        };

    public static TextBlock EditorialAccent(string text, double size = 18)
        => new()
        {
            Text = text,
            FontFamily = EditorialFont,
            FontSize = size,
            FontStyle = FontStyles.Italic,
            Foreground = Solid(Signal),
            TextWrapping = TextWrapping.Wrap
        };

    public static TextBlock MicroLabel(string text, bool accent = false)
        => new()
        {
            Text = text.ToUpperInvariant(),
            FontFamily = DataFont,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Solid(accent ? Signal : Muted),
            TextWrapping = TextWrapping.Wrap
        };

    public static Border Panel(
        UIElement child,
        double radius = 12,
        Thickness? margin = null,
        Thickness? padding = null,
        bool raised = false)
        => new()
        {
            Background = Solid(raised ? SurfaceRaised : Surface),
            BorderBrush = Solid(Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
            Margin = margin ?? new Thickness(0),
            Padding = padding ?? new Thickness(10),
            Child = child,
            SnapsToDevicePixels = true
        };

    public static Button ActionButton(string label, RoutedEventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Content = label,
            FontFamily = BodyFont,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Solid(primary ? Canvas : Text),
            Background = Solid(primary ? Signal : SurfaceRaised),
            BorderBrush = Solid(primary ? Signal : Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(3),
            MinHeight = 36,
            Cursor = Cursors.Hand,
            Focusable = true,
            ToolTip = label
        };
        button.Click += click;
        button.MouseEnter += (_, _) =>
        {
            button.Background = Solid(primary ? "#FF9B5B" : SurfaceHover);
            button.BorderBrush = Solid(primary ? "#FFB37F" : "#566879");
        };
        button.MouseLeave += (_, _) =>
        {
            button.Background = Solid(primary ? Signal : SurfaceRaised);
            button.BorderBrush = Solid(primary ? Signal : Border);
        };
        button.IsEnabledChanged += (_, _) =>
        {
            button.Opacity = button.IsEnabled ? 1.0 : 0.48;
        };
        return button;
    }
}
