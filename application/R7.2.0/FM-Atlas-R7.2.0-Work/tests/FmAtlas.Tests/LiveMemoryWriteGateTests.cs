using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class LiveMemoryWriteGateTests
{
    [Fact]
    public void LiveMemoryWritesAreDisabledInV10PreTestingBuild()
        => Assert.False(LiveMemoryEditService.LiveWritesEnabled);

    [Theory]
    [InlineData("position.gk")]
    [InlineData("positions.dc")]
    [InlineData("personality.professionalism")]
    [InlineData("left_foot")]
    [InlineData("right_foot")]
    [InlineData("height")]
    [InlineData("condition")]
    [InlineData("match_sharpness")]
    [InlineData("jadedness")]
    [InlineData("morale")]
    [InlineData("reputation.world")]
    [InlineData("asking_price")]
    [InlineData("international.caps")]
    [InlineData("international.u21_goals")]
    public void ExtendedPlayerFieldsHaveAValidatedPreviewMapping(string field)
        => Assert.True(LiveMemoryEditService.SupportsPlayerFieldPreview(field));

    [Theory]
    [InlineData("consistency")]
    [InlineData("important_matches")]
    [InlineData("injury_proneness")]
    [InlineData("dirtiness")]
    [InlineData("versatility")]
    [InlineData("contract.wage")]
    [InlineData("club.finance.balance")]
    public void UnwitnessedOrUnresolvedFieldsRemainOutsidePlayerPreview(string field)
        => Assert.False(LiveMemoryEditService.SupportsPlayerFieldPreview(field));

    [Fact]
    public void ApplyBatchIsBlockedBeforeAttemptingToOpenTheGameProcess()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => LiveMemoryEditService.ApplyBatch(CreatePlan()));

        Assert.Contains("hard-disabled", exception.Message.ToLowerInvariant());
    }

    [Fact]
    public void UndoBatchIsBlockedBeforeAttemptingToOpenTheGameProcess()
    {
        var receipt = new LiveMemoryEditService.BatchReceipt(
            CreatePlan(), DateTime.UtcNow, ReadBackVerified: true);

        var exception = Assert.Throws<InvalidOperationException>(
            () => LiveMemoryEditService.UndoBatch(receipt));

        Assert.Contains("hard-disabled", exception.Message.ToLowerInvariant());
    }

    private static LiveMemoryEditService.BatchPlan CreatePlan()
        => new(
            ProcessId: -1,
            PlayerUid: 1,
            PlayerName: "Test Player",
            ProfileId: "test-profile",
            ProfileFingerprint: "test-fingerprint",
            SaveLoadCounter: 1,
            CreatedUtc: DateTime.UtcNow,
            Changes: Array.Empty<LiveMemoryEditService.PlannedChange>());
}
