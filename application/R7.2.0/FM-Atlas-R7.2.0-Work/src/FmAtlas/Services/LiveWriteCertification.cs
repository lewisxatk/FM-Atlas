namespace FmAtlas.Services;

/// <summary>
/// A build-wide kill switch for live-game writes. A matching module fingerprint and a
/// successfully staged preview are not field-level certification. This remains false until
/// the exact FM build has independent field witnesses and disposable offline Apply/read-back/Undo
/// acceptance evidence.
/// </summary>
public static class LiveWriteCertification
{
    public static bool IsCertified => false;

    public const string BlockReason =
        "Live Football Manager memory writes are disabled in this build. The current process profile " +
        "is not the same as field-level certification. Atlas can inspect and stage a read-only preview, " +
        "but Apply and Undo remain blocked until independent field witnesses and disposable offline " +
        "career acceptance tests pass.";

    public static void EnsureWritesEnabled()
    {
        if (!IsCertified)
        {
            AtlasLog.Warning("Blocked live-memory write attempt: build-wide field certification gate is closed.");
            throw new InvalidOperationException(BlockReason);
        }
    }
}
