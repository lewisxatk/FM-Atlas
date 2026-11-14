using System.Globalization;
using FmAtlas.Models;

namespace FmAtlas.Services;

/// <summary>Builds provenance-rich, spreadsheet-safe CSV for similar-player candidates.</summary>
public static class SimilarPlayerCsvExporter
{
    public static string Build(PlayerRecord reference, IReadOnlyList<SimilarPlayerCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(candidates);

        var lines = new List<string>
        {
            "ReferencePlayer,ReferenceSourceId,Player,SourceId,Club,League,Nation,Age,Position,CurrentAbility,PotentialAbility,TransferValueSourceUnits,HomeGrownClub,HomeGrownNation,SimilarityScore,SharedAttributes,ReferenceAttributes,AttributeCoverage,Evidence"
        };
        foreach (var candidate in candidates)
        {
            var player = candidate.Player;
            var cells = new[]
            {
                Q(reference.Name), Q(reference.SourceId),
                Q(player.Name), Q(player.SourceId), Q(player.Club), Q(player.League), Q(player.Nation),
                Q(player.Age?.ToString(CultureInfo.InvariantCulture), false),
                Q(player.Position),
                Q(player.CurrentAbility?.ToString(CultureInfo.InvariantCulture), false),
                Q(player.PotentialAbility?.ToString(CultureInfo.InvariantCulture), false),
                Q(player.Value?.ToString("0.################", CultureInfo.InvariantCulture), false),
                Q(player.DisplayHomeGrownClub), Q(player.DisplayHomeGrownNation),
                Q(candidate.SimilarityScore.ToString("0.0", CultureInfo.InvariantCulture), false),
                Q(candidate.AttributesCompared.ToString(CultureInfo.InvariantCulture), false),
                Q(candidate.ReferenceAttributes.ToString(CultureInfo.InvariantCulture), false),
                Q(candidate.AttributeCoverage.ToString("P1", CultureInfo.InvariantCulture), false),
                Q(candidate.EvidenceSummary)
            };
            lines.Add(string.Join(",", cells));
        }
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string Q(string? value, bool protectFormula = true)
        => CsvExportSafety.QuoteText(value, protectFormula);
}
