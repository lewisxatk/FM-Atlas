using System.Text.Json;
using FmAtlas.Models;
using Xunit;

namespace FmAtlas.Tests;

public sealed class AdvancedPositionFilterTests
{
    [Fact]
    public void ZeroRatingsAreNoRequirementAndEmptyFilterNormalizesToNull()
    {
        Assert.Null(AdvancedPositionFilterRules.Normalize(null));
        Assert.Null(AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
            Positions: new[] { new AdvancedPositionCriterion("GK", 0), new AdvancedPositionCriterion("DC", 0) })));
    }

    [Fact]
    public void NormalizesLegacyStrikerAliasAndKeepsExactAllSemantics()
    {
        var result = AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
            MatchAny: false,
            AtLeast: false,
            Positions: new[]
            {
                new AdvancedPositionCriterion("ST", 16),
                new AdvancedPositionCriterion("DC", 14)
            }));

        Assert.NotNull(result);
        Assert.False(result.MatchAny);
        Assert.False(result.AtLeast);
        Assert.Equal(new[] { "DC", "STC" }, result.Positions!.Select(x => x.PositionCode));
        Assert.Equal(new[] { 14, 16 }, result.Positions.Select(x => x.Rating));
    }

    [Fact]
    public void PreservesAnyAndAtLeastOptionsForSelectedPositions()
    {
        var result = AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
            MatchAny: true,
            AtLeast: true,
            Positions: new[] { new AdvancedPositionCriterion("AML", 12) }));

        Assert.NotNull(result);
        Assert.True(result.MatchAny);
        Assert.True(result.AtLeast);
        Assert.Equal(12, Assert.Single(result.Positions!).Rating);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void RejectsPositionRatingsOutsideZeroToTwenty(int rating)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
                Positions: new[] { new AdvancedPositionCriterion("DC", rating) })));
    }

    [Fact]
    public void RejectsUnknownPositionCodes()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
                Positions: new[] { new AdvancedPositionCriterion("CB", 15) })));
    }
    [Fact]
    public void OlderSavedProfilesWithoutPositionFilterRemainLoadable()
    {
        const string json = """
            {
              "Id": "legacy",
              "Name": "Legacy search",
              "UpdatedUtc": "2026-11-14T00:00:00Z",
              "SearchText": "",
              "MatchAll": true,
              "Criteria": []
            }
            """;

        var profile = JsonSerializer.Deserialize<AdvancedSearchProfile>(json);

        Assert.NotNull(profile);
        Assert.Null(profile.PositionFilter);
    }

    [Fact]
    public void PositionSettingsRoundTripInsideSavedProfile()
    {
        var profile = new AdvancedSearchProfile(
            "position-only",
            "Any natural winger",
            DateTime.UtcNow,
            "",
            true,
            Array.Empty<AdvancedSearchCriterion>(),
            PositionFilter: new AdvancedPositionFilter(
                MatchAny: true,
                AtLeast: true,
                Positions: new[] { new AdvancedPositionCriterion("AML", 18), new AdvancedPositionCriterion("AMR", 18) }));

        var json = JsonSerializer.Serialize(profile);
        var loaded = JsonSerializer.Deserialize<AdvancedSearchProfile>(json);

        Assert.NotNull(loaded?.PositionFilter);
        Assert.True(loaded.PositionFilter.MatchAny);
        Assert.True(loaded.PositionFilter.AtLeast);
        Assert.Equal(new[] { "AML", "AMR" }, loaded.PositionFilter.Positions!.Select(x => x.PositionCode));
    }

}
