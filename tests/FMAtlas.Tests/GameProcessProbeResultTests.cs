using FMAtlas.Domain.Connection;
using Xunit;

namespace FMAtlas.Tests;

public sealed class GameProcessProbeResultTests
{
    [Fact]
    public void NotFoundResultContainsNoCandidates()
    {
        var result = GameProcessProbeResult.NotFound();

        Assert.Equal(GameProcessProbeState.NotFound, result.State);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void UnsupportedPlatformResultContainsNoCandidates()
    {
        var result = GameProcessProbeResult.Unsupported();

        Assert.Equal(GameProcessProbeState.UnsupportedPlatform, result.State);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void CandidateCanRepresentUnverifiedModuleWithoutClaimingBuildSupport()
    {
        var candidate = new GameProcessCandidate(
            1234,
            "fm",
            null,
            true,
            @"C:\Games\Football Manager 27\game_plugin.dll",
            null);

        Assert.True(candidate.HasGamePluginModule);
        Assert.Equal(1234, candidate.ProcessId);
    }
}
