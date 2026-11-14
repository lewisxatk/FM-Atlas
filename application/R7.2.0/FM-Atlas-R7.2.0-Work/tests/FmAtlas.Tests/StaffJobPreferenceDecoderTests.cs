using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class StaffJobPreferenceDecoderTests
{
    [Fact]
    public void DecodesAllFifteenGroupsAndKeepsTiedTopPreferences()
    {
        var values = new byte[15];
        values[0] = 12;
        values[4] = 18;
        values[12] = 18;

        var result = StaffJobPreferenceDecoder.Decode(values);

        Assert.NotNull(result);
        Assert.Equal(15, result.Ratings.Count);
        Assert.Equal(18, result.HighestRating);
        Assert.Equal(18, result.Ratings["scout"]);
        Assert.Equal(18, result.Ratings["analyst_recruitment_or_performance"]);
        Assert.Equal(new[] { "scout", "analyst_recruitment_or_performance" }, result.PreferredJobs);
    }

    [Fact]
    public void DoesNotInventPreferredJobWhenHighestScoreIsBelowTen()
    {
        var values = Enumerable.Repeat((byte)8, 15).ToArray();

        var result = StaffJobPreferenceDecoder.Decode(values);

        Assert.NotNull(result);
        Assert.Equal(8, result.HighestRating);
        Assert.Empty(result.PreferredJobs);
        Assert.Equal(15, result.Ratings.Count);
    }

    [Fact]
    public void DoesNotChooseAWinnerWhenMoreThanThreeGroupsTie()
    {
        var values = Enumerable.Repeat((byte)12, 15).ToArray();

        var result = StaffJobPreferenceDecoder.Decode(values);

        Assert.NotNull(result);
        Assert.Equal(12, result.HighestRating);
        Assert.Empty(result.PreferredJobs);
        Assert.All(result.Ratings.Values, score => Assert.Equal(12, score));
    }

    [Fact]
    public void RejectsWrongLengthAndOutOfRangeScores()
    {
        Assert.Null(StaffJobPreferenceDecoder.Decode(new byte[14]));
        var invalid = new byte[15];
        invalid[7] = 21;
        Assert.Null(StaffJobPreferenceDecoder.Decode(invalid));
    }
}
