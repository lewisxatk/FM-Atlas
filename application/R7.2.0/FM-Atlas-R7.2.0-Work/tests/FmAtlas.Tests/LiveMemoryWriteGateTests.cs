using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class LiveMemoryWriteGateTests
{
    [Fact]
    public void LiveMemoryWritesAreDisabledInV10PreTestingBuild()
        => Assert.False(LiveMemoryEditService.LiveWritesEnabled);

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
