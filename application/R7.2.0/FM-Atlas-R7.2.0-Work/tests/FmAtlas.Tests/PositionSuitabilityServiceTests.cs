using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class PositionSuitabilityServiceTests
{
    [Fact]
    public void ExposesAllFifteenCanonicalFmPositionsInStableOrder()
    {
        Assert.Equal(
            new[] { "GK", "SW", "DL", "DC", "DR", "WBL", "WBR", "DM", "ML", "MC", "MR", "AML", "AMC", "AMR", "STC" },
            PositionSuitabilityService.PositionCodes);
    }

    [Theory]
    [InlineData(1, PositionSuitabilityBand.Low)]
    [InlineData(4, PositionSuitabilityBand.Low)]
    [InlineData(5, PositionSuitabilityBand.Limited)]
    [InlineData(9, PositionSuitabilityBand.Limited)]
    [InlineData(10, PositionSuitabilityBand.Competent)]
    [InlineData(14, PositionSuitabilityBand.Competent)]
    [InlineData(15, PositionSuitabilityBand.Strong)]
    [InlineData(17, PositionSuitabilityBand.Strong)]
    [InlineData(18, PositionSuitabilityBand.Natural)]
    [InlineData(20, PositionSuitabilityBand.Natural)]
    public void MapsDocumentedDisplayBandsWithoutChangingSourceRating(int value, PositionSuitabilityBand expected)
    {
        var result = PositionSuitabilityService.MapSourceRatings(
            new[] { new KeyValuePair<string, double?>("positions.DC", value) });

        var centreDefender = Assert.Single(result.Where(x => x.Code == "DC"));
        Assert.Equal((double)value, centreDefender.Rating);
        Assert.Equal(expected, centreDefender.Band);
    }

    [Fact]
    public void KeepsMissingZeroAndOutOfRangeValuesUnrated()
    {
        var result = PositionSuitabilityService.MapSourceRatings(new[]
        {
            new KeyValuePair<string, double?>("GK", 0),
            new KeyValuePair<string, double?>("DC", 21),
            new KeyValuePair<string, double?>("STC", null)
        });

        Assert.Equal(15, result.Count);
        Assert.All(result, item =>
        {
            Assert.Null(item.Rating);
            Assert.Equal(PositionSuitabilityBand.Unrated, item.Band);
        });
    }

    [Fact]
    public void MapsLegacyStrikerAliasToCanonicalCentreForwardSlot()
    {
        var result = PositionSuitabilityService.MapSourceRatings(
            new[] { new KeyValuePair<string, double?>("position.ST", 16) });

        var striker = Assert.Single(result.Where(x => x.Code == "STC"));
        Assert.Equal(16, striker.Rating);
        Assert.Equal(PositionSuitabilityBand.Strong, striker.Band);
    }

    [Fact]
    public void LaterExplicitSourceValueOverridesEarlierParserValue()
    {
        var result = PositionSuitabilityService.MapSourceRatings(new[]
        {
            new KeyValuePair<string, double?>("positions.DC", 12),
            new KeyValuePair<string, double?>("positions.DC", 18)
        });

        Assert.Equal(18, Assert.Single(result.Where(x => x.Code == "DC")).Rating);
    }
}
