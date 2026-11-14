using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>
/// Produces quoted CSV text cells and protects spreadsheet applications from formula
/// execution when untrusted source text begins with a formula marker.
/// Numeric values should be formatted separately and passed with protectFormula=false.
/// </summary>
public static class CsvExportSafety
{
    public static string QuoteText(string? value, bool protectFormula = true)
    {
        var text = value ?? string.Empty;
        var first = text.TrimStart();
        if (protectFormula && first.Length > 0 && first[0] is '=' or '+' or '-' or '@')
            text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    public const string AdvancedPlayerHeader =
        "Source ID,Player,Age,Club,League,Nation,Position,CA,PA,Market Value (Source Units),Home-grown at Club,Home-grown in Nation,Preferred Foot,Left Foot,Right Foot,Source";

    public static string FormatAdvancedPlayerRow(PlayerRecord player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var cells = new[]
        {
            QuoteText(player.SourceId),
            QuoteText(player.Name),
            QuoteText(player.Age?.ToString(System.Globalization.CultureInfo.InvariantCulture), false),
            QuoteText(player.Club),
            QuoteText(player.League),
            QuoteText(player.Nation),
            QuoteText(player.Position),
            QuoteText(player.CurrentAbility?.ToString(System.Globalization.CultureInfo.InvariantCulture), false),
            QuoteText(player.PotentialAbility?.ToString(System.Globalization.CultureInfo.InvariantCulture), false),
            QuoteText(player.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture), false),
            QuoteText(player.DisplayHomeGrownClub),
            QuoteText(player.DisplayHomeGrownNation),
            QuoteText(player.PreferredFoot),
            QuoteText(player.LeftFoot?.ToString(System.Globalization.CultureInfo.InvariantCulture), false),
            QuoteText(player.RightFoot?.ToString(System.Globalization.CultureInfo.InvariantCulture), false),
            QuoteText(player.DataOrigin)
        };
        return string.Join(",", cells);
    }
}
