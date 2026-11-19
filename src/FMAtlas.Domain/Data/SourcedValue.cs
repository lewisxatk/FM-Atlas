namespace FMAtlas.Domain.Data;

/// <summary>A value paired with the evidence describing where it came from.</summary>
public sealed record SourcedValue<T>(T Value, FieldProvenance Provenance)
{
    public bool IsReadVerified => Provenance.IsReadVerified;

    /// <summary>
    /// A field-level evidence hint only. This is never a direct write command
    /// and must not bypass the guarded live-editing application service.
    /// </summary>
    public bool IsWriteVerifiedForBuild => Provenance.IsWriteVerifiedForBuild;
}
