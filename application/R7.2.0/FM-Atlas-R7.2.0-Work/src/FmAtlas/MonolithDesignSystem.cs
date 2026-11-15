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
    public const string Canvas = "#0A0E13";
    public const string Surface = "#121922";
    public const string SurfaceRaised = "#18222D";
    public const string SurfaceHover = "#202D39";
    public const string Border = "#34414D";
    public const string Text = "#F1F4F6";
    public const string Muted = "#A8B3BD";
    public const string Signal = "#F08A42";
    public const string SignalSoft = "#3A291F";
    public const string Positive = "#83CDA5";
    public const string Caution = "#E7BD70";
    public const string Negative = "#EC8791";

    public static readonly FontFamily DisplayFont = new("Bahnschrift");
    public static readonly FontFamily BodyFont = new("Segoe UI");
    public static readonly FontFamily DataFont = new("Consolas");
    public static readonly FontFamily EditorialFont = new("Georgia");

    public static Brush Solid(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public static TextBlock DisplayText(string text, double size, Brush? foreground = null)
        => new()
        {
            Text = text,
            FontFamily = DisplayFont,
            FontSize = size,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground ?? Solid(Text),
            TextWrapping = TextWrapping.Wrap
        };

    public static TextBlock BodyText(string text, double size = 11, Brush? foreground = null)
        => new()
        {
            Text = text,
            FontFamily = BodyFont,
            FontSize = size,
            Foreground = foreground ?? Solid(Text),
            TextWrapping = TextWrapping.Wrap
        };

    public static TextBlock DataText(string text, double size = 10, Brush? foreground = null)
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
            FontSize = 9,
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
            CornerRadius = new CornerRadius(radius),
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
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = Solid(primary ? Canvas : Text),
            Background = Solid(primary ? Signal : SurfaceRaised),
            BorderBrush = Solid(primary ? Signal : Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(3),
            MinHeight = 34,
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
