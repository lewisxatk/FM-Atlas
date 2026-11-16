using FMAtlas.Domain.Connection;
using Xunit;

namespace FMAtlas.Tests;

public sealed class GameBuildProfileCatalogTests
{
    [Fact]
    public void MatchesTheDocumentedGamePass2435ReferenceFingerprint()
    {
        var fingerprint = new GameModuleFingerprint(
            0x8664, 0x6AE8CF77, 0x17969000, 123456, "0.9.2435.0");

        var match = GameBuildProfileCatalog.MatchReference(fingerprint);

        Assert.NotNull(match);
        Assert.Equal("fm27-gamepass-0.9.2435.0", match!.Id);
        Assert.Equal(GameBuildEvidenceStatus.ExternalReferenceOnly, match.EvidenceStatus);
        Assert.False(match.CanReadLiveData);
        Assert.False(match.CanWriteLiveData);
    }

    [Fact]
    public void UnknownFingerprintDoesNotMatchAReferenceProfile()
    {
        var fingerprint = new GameModuleFingerprint(
            0x8664, 0x12345678, 0x11111111, 123456, null);

        Assert.Null(GameBuildProfileCatalog.MatchReference(fingerprint));
    }

    [Fact]
    public void ReferenceProfilesRemainReadAndWriteDisabled()
    {
        Assert.NotEmpty(GameBuildProfileCatalog.Profiles);
        Assert.All(GameBuildProfileCatalog.Profiles, profile =>
        {
            Assert.Equal(GameBuildEvidenceStatus.ExternalReferenceOnly, profile.EvidenceStatus);
            Assert.True(profile.CanAttemptReadOnlyScan);
            Assert.False(profile.CanReadLiveData);
            Assert.False(profile.CanWriteLiveData);
        });
    }
}
