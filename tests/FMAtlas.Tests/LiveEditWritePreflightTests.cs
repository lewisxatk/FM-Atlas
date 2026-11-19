using FMAtlas.Application.LiveEditing;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Players;
using Xunit;

namespace FMAtlas.Tests;

public sealed class LiveEditWritePreflightTests
{
    private const string Fingerprint = "PE:6AE8CF77:17969000";

    private static FieldProvenance VerifiedEvidence(FieldEvidenceStatus status = FieldEvidenceStatus.WriteVerified) =>
        new(
            DataSourceKind.LiveGameMemory,
            status,
            SourceVersion: "FM27 Game Pass 0.9.2435.0",
            TargetBuildFingerprint: Fingerprint);

    private static LiveEditWriteContext ValidContext() =>
        new(
            SessionConnected: true,
            ExpectedProcessId: 1234,
            ActualProcessId: 1234,
            ExpectedBuildFingerprint: Fingerprint,
            ActualBuildFingerprint: Fingerprint,
            ExpectedSaveGeneration: 3,
            ActualSaveGeneration: 3,
            TargetIdentityVerified: true,
            FieldKey: "attribute.finishing",
            FieldProvenance: VerifiedEvidence(),
            ExpectedCurrentValue: 15,
            ActualCurrentValue: 15,
            ProposedValue: 18,
            MinimumValue: 1,
            MaximumValue: 20,
            TargetMemoryRegionWritable: true);

    [Fact]
    public void AllowsOnlyACompletelyVerifiedWriteContext()
    {
        var result = LiveEditWritePreflight.Evaluate(ValidContext());

        Assert.True(result.CanWrite);
        Assert.Equal(LiveEditBlockReason.None, result.BlockReason);
    }

    [Fact]
    public void MappedOrReadVerifiedFieldsAreNotWritable()
    {
        foreach (var status in new[] { FieldEvidenceStatus.Mapped, FieldEvidenceStatus.ReadVerified })
        {
            var result = LiveEditWritePreflight.Evaluate(
                ValidContext() with { FieldProvenance = VerifiedEvidence(status) });

            Assert.False(result.CanWrite);
            Assert.Equal(LiveEditBlockReason.FieldNotWriteVerified, result.BlockReason);
        }
    }

    [Fact]
    public void RefusesAChangedProcessOrBuild()
    {
        Assert.Equal(
            LiveEditBlockReason.ProcessChanged,
            LiveEditWritePreflight.Evaluate(ValidContext() with { ActualProcessId = 4321 }).BlockReason);

        Assert.Equal(
            LiveEditBlockReason.BuildNotMatched,
            LiveEditWritePreflight.Evaluate(ValidContext() with { ActualBuildFingerprint = "PE:OTHER" }).BlockReason);
    }

    [Fact]
    public void RefusesAChangedSaveGeneration()
    {
        var result = LiveEditWritePreflight.Evaluate(
            ValidContext() with { ActualSaveGeneration = 4 });

        Assert.False(result.CanWrite);
        Assert.Equal(LiveEditBlockReason.SaveChanged, result.BlockReason);
    }

    [Fact]
    public void RefusesUnverifiedTargetIdentity()
    {
        var result = LiveEditWritePreflight.Evaluate(
            ValidContext() with { TargetIdentityVerified = false });

        Assert.Equal(LiveEditBlockReason.TargetIdentityNotVerified, result.BlockReason);
    }

    [Fact]
    public void RefusesFieldEvidenceFromAnotherBuild()
    {
        var evidence = VerifiedEvidence() with { TargetBuildFingerprint = "PE:OTHER" };
        var result = LiveEditWritePreflight.Evaluate(
            ValidContext() with { FieldProvenance = evidence });

        Assert.Equal(LiveEditBlockReason.FieldBuildMismatch, result.BlockReason);
    }

    [Fact]
    public void RefusesOptimisticConcurrencyConflicts()
    {
        var result = LiveEditWritePreflight.Evaluate(
            ValidContext() with { ActualCurrentValue = 16 });

        Assert.Equal(LiveEditBlockReason.CurrentValueConflict, result.BlockReason);
    }

    [Fact]
    public void RefusesInvalidRangesAndValuesOutsideTheVerifiedRange()
    {
        Assert.Equal(
            LiveEditBlockReason.InvalidFieldRange,
            LiveEditWritePreflight.Evaluate(ValidContext() with
            {
                MinimumValue = 20,
                MaximumValue = 1
            }).BlockReason);

        Assert.Equal(
            LiveEditBlockReason.ProposedValueOutOfRange,
            LiveEditWritePreflight.Evaluate(ValidContext() with
            {
                ProposedValue = 21
            }).BlockReason);
    }

    [Fact]
    public void RefusesUnverifiedMemoryRegionsAndDisconnectedSessions()
    {
        Assert.Equal(
            LiveEditBlockReason.MemoryRegionNotWritable,
            LiveEditWritePreflight.Evaluate(ValidContext() with
            {
                TargetMemoryRegionWritable = false
            }).BlockReason);

        Assert.Equal(
            LiveEditBlockReason.NoLiveSession,
            LiveEditWritePreflight.Evaluate(ValidContext() with
            {
                SessionConnected = false
            }).BlockReason);
    }
}
