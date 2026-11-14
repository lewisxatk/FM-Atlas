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
}
