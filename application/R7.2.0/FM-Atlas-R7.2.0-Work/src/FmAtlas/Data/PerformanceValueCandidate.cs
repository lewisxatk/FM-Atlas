using FmAtlas.Models;

namespace FmAtlas.Data;

public sealed record PerformanceValueCandidate(
    PlayerRecord Player,
    string MetricKey,
    string MetricName,
    double PerformanceValue,
    bool Per90,
    double TotalMinutes,
    int SampleSize,
    string LatestPeriod,
    double PerformancePercentile,
    double ValuePercentile,
    double? PositionPerformancePercentile,
    int PositionCohortSize,
    double? LeaguePerformancePercentile,
    int LeagueCohortSize,
    double ValueGap,
    double DataCoverage,
    string? PositionRatingCode = null,
    int? PositionRating = null)
{
    public string DisplayPerformanceValue => PerformanceValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    public string DisplayPerformancePercentile => PerformancePercentile.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    public string DisplayValuePercentile => ValuePercentile.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    public string DisplayPositionPercentile => PositionPerformancePercentile?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "—";
    public string DisplayLeaguePercentile => LeaguePerformancePercentile?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "—";
    public string DisplayValueGap => ValueGap.ToString("+0.0;-0.0;0.0", System.Globalization.CultureInfo.InvariantCulture);
    public string DisplayMinutes => TotalMinutes.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    public string DisplayCoverage => DataCoverage.ToString("P0", System.Globalization.CultureInfo.InvariantCulture);
    public string DisplayPositionRating => PositionRating.HasValue && PositionRatingCode is not null
        ? $"{PositionRatingCode} {PositionRating.Value}/20"
        : "—";
}
