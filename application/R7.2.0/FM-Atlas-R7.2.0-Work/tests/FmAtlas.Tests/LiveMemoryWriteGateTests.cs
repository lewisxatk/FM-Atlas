using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class LiveMemoryWriteGateTests
{
    [Fact]
    public void LiveMemoryWritesAreEnabledOnlyForTheExplicitlyMappedFieldCatalogue()
        => Assert.True(LiveMemoryEditService.LiveWritesEnabled);

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
    [InlineData("transfer_value")]
    [InlineData("date_of_birth")]
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
    [InlineData("trait.0")]
    [InlineData("trait.31")]
    [InlineData("trait.63")]
    [InlineData("contract.wage")]
    [InlineData("contract.signed")]
    [InlineData("contract.expires")]
    [InlineData("contract.joined")]
    [InlineData("contract.loyalty_bonus")]
    [InlineData("contract.happiness")]
    [InlineData("contract.playing_time_happiness")]
    [InlineData("contract.squad_number")]
    [InlineData("contract.squad_status")]
    [InlineData("contract.transfer_listed")]
    [InlineData("contract.loan_listed")]
    public void LibreMappedPlayerFieldsHaveAValidatedLiveMapping(string field)
        => Assert.True(LiveMemoryEditService.SupportsPlayerFieldPreview(field));

    [Theory]
    [InlineData("nation")]
    [InlineData("date_of_birth")]
    [InlineData("club.finance.balance")]
    [InlineData("tactical.effectiveness")]
    [InlineData("contract.clause")]
    public void PointerBackedOrUnresolvedFieldsRemainOutsidePlayerPreview(string field)
        => Assert.False(LiveMemoryEditService.SupportsPlayerFieldPreview(field));

    [Fact]
    public void NovemberSteamAndGamePassProfilesExposeTacticalEffectiveness()
    {
        Assert.True(LiveMemoryEditService.SupportsTacticalEffectiveness(
            Fm27BuildProfiles.All.Single(x => x.Id == "steam-26385295")));
        Assert.True(LiveMemoryEditService.SupportsTacticalEffectiveness(
            Fm27BuildProfiles.All.Single(x => x.Id == "gamepass-0.9.2435.0")));
        Assert.False(LiveMemoryEditService.SupportsTacticalEffectiveness(
            Fm27BuildProfiles.All.Single(x => x.Id == "steam-26227640")));
    }

    [Fact]
    public void ExpiredPlansAreRejectedBeforeAnyProcessIsOpened()
    {
        var plan = CreatePlan() with { CreatedUtc = DateTime.UtcNow.AddMinutes(-3) };
        var exception = Assert.Throws<InvalidOperationException>(() => LiveMemoryEditService.ApplyBatch(plan));
        Assert.Contains("expired", exception.Message.ToLowerInvariant());
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
