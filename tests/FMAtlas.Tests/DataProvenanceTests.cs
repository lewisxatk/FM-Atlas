using FMAtlas.Domain.Data;
using FMAtlas.Domain.Players;
using Xunit;

namespace FMAtlas.Tests;

public sealed class DataProvenanceTests
{
    [Fact]
    public void DocumentedFieldIsNotReadOrWriteVerified()
    {
        var provenance = new FieldProvenance(
            DataSourceKind.GameExport,
            FieldEvidenceStatus.Documented,
            SourceVersion: "FM27");

        var value = new SourcedValue<int>(14, provenance);

        Assert.False(value.IsReadVerified);
        Assert.False(value.IsWriteVerifiedForBuild);
    }

    [Fact]
    public void ReadVerifiedLiveValueIsNotAutomaticallyWritable()
    {
        var provenance = new FieldProvenance(
            DataSourceKind.LiveGameMemory,
            FieldEvidenceStatus.ReadVerified,
            TargetBuildFingerprint: "test-build");

        var value = new SourcedValue<int>(14, provenance);

        Assert.True(value.IsReadVerified);
        Assert.False(value.IsWriteVerifiedForBuild);
    }

    [Fact]
    public void WriteVerifiedValueRequiresLiveMemoryAndSpecificBuildFingerprint()
    {
        var valid = new FieldProvenance(
            DataSourceKind.LiveGameMemory,
            FieldEvidenceStatus.WriteVerified,
            TargetBuildFingerprint: "sha256:example-build");

        var missingFingerprint = new FieldProvenance(
            DataSourceKind.LiveGameMemory,
            FieldEvidenceStatus.WriteVerified);

        var nonLiveSource = new FieldProvenance(
            DataSourceKind.CareerSave,
            FieldEvidenceStatus.WriteVerified,
            TargetBuildFingerprint: "sha256:example-build");

        Assert.True(valid.IsWriteVerifiedForBuild);
        Assert.False(missingFingerprint.IsWriteVerifiedForBuild);
        Assert.False(nonLiveSource.IsWriteVerifiedForBuild);
    }
}
