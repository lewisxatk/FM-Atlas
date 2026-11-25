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
    public void MatchesTheDocumentedGamePass2710Build2524Fingerprint()
    {
        var fingerprint = new GameModuleFingerprint(
            0x8664, 0x6AF5C8C6, 0x179AD000, 123456, "0.9.2524.0");

        var match = GameBuildProfileCatalog.MatchReference(fingerprint);

        Assert.NotNull(match);
        Assert.Equal("fm27-gamepass-0.9.2524.0", match!.Id);
        Assert.Equal(GameBuildEvidenceStatus.ExternalReferenceOnly, match.EvidenceStatus);
        Assert.True(match.CanAttemptReadOnlyScan);
        Assert.False(match.CanReadLiveData);
        Assert.False(match.CanWriteLiveData);
    }

    [Fact]
    public void MatchesUserSuppliedGamePass2711FingerprintAndEnablesOnlyGuardedReadOnlyProbe()
    {
        var fingerprint = new GameModuleFingerprint(
            0x8664, 0x6AFF583B, 0x17624000, 391758808, "27.1.1");

        var match = GameBuildProfileCatalog.MatchReference(fingerprint);

        Assert.NotNull(match);
        Assert.Equal("fm27-gamepass-0.9.2562.0", match!.Id);
        Assert.Equal("0.9.2562.0", match.PackageVersion);
        Assert.Equal(GameBuildEvidenceStatus.ExternalReferenceOnly, match.EvidenceStatus);
        Assert.True(match.HasReadOnlyScanner);
        Assert.True(match.HasRequiredReadOnlyLayout);
        Assert.True(match.CanAttemptReadOnlyScan);
        Assert.False(match.CanReadLiveData);
        Assert.False(match.CanWriteLiveData);
        Assert.Equal(0x53F8EA8UL, match.PlayersArrayRva);
        Assert.Equal(0x53F8EF0UL, match.StaffArrayRva);
        Assert.Equal(0x53F9060UL, match.ClubsArrayRva);
        Assert.Equal(0x53F9A60UL, match.GameUiRva);
        Assert.Equal(0x539BD10UL, match.CurrentDateRva);
        Assert.Equal(0x100UL, match.GameLoadsOffset);
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
            Assert.False(profile.CanReadLiveData);
            Assert.False(profile.CanWriteLiveData);
        });
        Assert.Contains(GameBuildProfileCatalog.Profiles, profile =>
            profile.PackageVersion == "0.9.2524.0" && profile.CanAttemptReadOnlyScan);
        Assert.Contains(GameBuildProfileCatalog.Profiles, profile =>
            profile.PackageVersion == "0.9.2562.0" && profile.CanAttemptReadOnlyScan);
        Assert.Contains(GameBuildProfileCatalog.Profiles, profile =>
            profile.PackageVersion == "0.9.2435.0" && !profile.CanAttemptReadOnlyScan);
        Assert.Contains(GameBuildProfileCatalog.Profiles, profile =>
            profile.PackageVersion == "0.9.2409.0" && !profile.CanAttemptReadOnlyScan);
    }
}
