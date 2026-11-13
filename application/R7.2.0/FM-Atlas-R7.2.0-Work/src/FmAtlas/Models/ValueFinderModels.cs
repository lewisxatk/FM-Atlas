namespace FmAtlas.Models;

public sealed record ValueFinderCandidate(
    PlayerRecord Player,
    double ValueIndex,
    double AbilityPercentile,
    double MarketValuePercentile,
    int PotentialUpside)
{
    public string DisplayValueIndex => ValueIndex.ToString("0.0");
    public string DisplayAbilityPercentile => AbilityPercentile.ToString("0");
    public string DisplayMarketPercentile => MarketValuePercentile.ToString("0");
    public string DisplayPotentialUpside => PotentialUpside.ToString("+0;-0;0");
}
