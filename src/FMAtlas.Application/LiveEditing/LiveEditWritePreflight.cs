using FMAtlas.Domain.Data;

namespace FMAtlas.Application.LiveEditing;

/// <summary>
/// Facts that must be revalidated immediately before a live memory write.
/// This policy intentionally fails closed: mapped/read-verified fields are not
/// writable until the exact field and build have passed FM Atlas write verification.
/// </summary>
public sealed record LiveEditWriteContext(
    bool SessionConnected,
    int ExpectedProcessId,
    int ActualProcessId,
    string ExpectedBuildFingerprint,
    string ActualBuildFingerprint,
    long ExpectedSaveGeneration,
    long ActualSaveGeneration,
    bool TargetIdentityVerified,
    string FieldKey,
    FieldProvenance FieldProvenance,
    decimal ExpectedCurrentValue,
    decimal ActualCurrentValue,
    decimal ProposedValue,
    decimal MinimumValue,
    decimal MaximumValue,
    bool TargetMemoryRegionWritable);

public enum LiveEditBlockReason
{
    None,
    NoLiveSession,
    ProcessChanged,
    BuildNotMatched,
    SaveChanged,
    TargetIdentityNotVerified,
    FieldNotWriteVerified,
    FieldBuildMismatch,
    CurrentValueConflict,
    InvalidFieldRange,
    ProposedValueOutOfRange,
    MemoryRegionNotWritable
}

public sealed record LiveEditWritePreflightResult(
    bool CanWrite,
    LiveEditBlockReason BlockReason,
    string Message)
{
    public static LiveEditWritePreflightResult Allow() =>
        new(true, LiveEditBlockReason.None, "All live-write preconditions passed.");

    public static LiveEditWritePreflightResult Block(LiveEditBlockReason reason, string message) =>
        new(false, reason, message);
}

/// <summary>
/// Pure preflight policy for the future live writer. It performs no process access
/// and never writes memory; the writer must independently repeat these checks at
/// the point of use and verify the value by reading it back.
/// </summary>
public static class LiveEditWritePreflight
{
    public static LiveEditWritePreflightResult Evaluate(LiveEditWriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.SessionConnected)
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.NoLiveSession,
                "Load and verify an active FM career before editing.");

        if (context.ExpectedProcessId <= 0 || context.ActualProcessId != context.ExpectedProcessId)
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.ProcessChanged,
                "The connected Football Manager process changed. Reload the career.");

        if (string.IsNullOrWhiteSpace(context.ExpectedBuildFingerprint) ||
            !string.Equals(context.ActualBuildFingerprint, context.ExpectedBuildFingerprint, StringComparison.Ordinal))
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.BuildNotMatched,
                "The active game build does not match the exact build profile.");

        if (context.ExpectedSaveGeneration <= 0 ||
            context.ActualSaveGeneration != context.ExpectedSaveGeneration)
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.SaveChanged,
                "The active career changed after it was loaded. Reload before editing.");

        if (!context.TargetIdentityVerified)
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.TargetIdentityNotVerified,
                "The target object could not be verified against its player, staff or club identity.");

        if (!context.FieldProvenance.IsWriteVerifiedForBuild)
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.FieldNotWriteVerified,
                $"Field '{context.FieldKey}' has not passed FM Atlas write verification.");

        if (!string.Equals(
                context.FieldProvenance.TargetBuildFingerprint,
                context.ExpectedBuildFingerprint,
                StringComparison.Ordinal))
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.FieldBuildMismatch,
                $"Field '{context.FieldKey}' was not write-verified for the active game build.");

        if (context.ActualCurrentValue != context.ExpectedCurrentValue)
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.CurrentValueConflict,
                $"Field '{context.FieldKey}' changed since it was read. Refresh the record before applying the edit.");

        if (context.MinimumValue > context.MaximumValue)
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.InvalidFieldRange,
                $"Field '{context.FieldKey}' has an invalid verified range.");

        if (context.ProposedValue < context.MinimumValue || context.ProposedValue > context.MaximumValue)
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.ProposedValueOutOfRange,
                $"The new value for '{context.FieldKey}' is outside its verified range.");

        if (!context.TargetMemoryRegionWritable)
            return LiveEditWritePreflightResult.Block(
                LiveEditBlockReason.MemoryRegionNotWritable,
                "The target memory region is not confirmed as a safe writable data region.");

        return LiveEditWritePreflightResult.Allow();
    }
}
