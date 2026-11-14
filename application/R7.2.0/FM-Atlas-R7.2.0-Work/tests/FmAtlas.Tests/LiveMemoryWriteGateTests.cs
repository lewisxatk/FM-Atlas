using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class LiveMemoryWriteGateTests
{
    [Fact]
    public void LiveMemoryWritesAreDisabledInV10PreTestingBuild()
        => Assert.False(LiveMemoryEditService.LiveWritesEnabled);

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
    [InlineData("contract.wage")]
    [InlineData("club.finance.balance")]
    public void UnwitnessedOrUnresolvedFieldsRemainOutsidePlayerPreview(string field)
        => Assert.False(LiveMemoryEditService.SupportsPlayerFieldPreview(field));

    [Fact]
    public void LatestSteamBuild26577676IsRegisteredForLiveEditingAndTactics()
    {
        var profile = Fm27BuildProfiles.Match(0x185FA000, 0x6AF38E5C);
        Assert.NotNull(profile);
        Assert.Equal("steam-26577676", profile!.Id);
        Assert.Equal(0x5812E20UL, profile.TacticsManagerRva);
        Assert.Equal(0x580F3F0UL, profile.TrainingManagerRva);
    }

    [Fact]
    public void CurrentRetailSteamAndGamePassProfilesHaveTacticalManagerPointers()
    {
        var steam = Fm27BuildProfiles.All.Single(x => x.Id == "steam-26385295");
        var gamePass = Fm27BuildProfiles.All.Single(x => x.Id == "gamepass-0.9.2435.0");
        Assert.NotEqual(0UL, steam.TacticsManagerRva);
        Assert.NotEqual(0UL, steam.TrainingManagerRva);
        Assert.NotEqual(0UL, gamePass.TacticsManagerRva);
        Assert.NotEqual(0UL, gamePass.TrainingManagerRva);
    }

    [Fact]
    public void LivePlayerCatalogueIncludesVisibleHiddenPositionPersonalityTraitAndContractFields()
    {
        var fields = LiveEntityMemoryEditService.GetEditableFields(LiveEntityMemoryEditService.EntityKind.Player);
        var keys = fields.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[]
        {
            "attribute.finishing", "attribute.consistency", "attribute.important_matches",
            "attribute.injury_proneness", "attribute.dirtiness", "attribute.versatility",
            "position.gk", "position.amc", "personality.professionalism",
            "left_foot", "right_foot", "trait.0", "trait.63",
            "contract.wage", "contract.expires", "contract.squad_number",
            "contract.squad_status", "contract.transfer_listed", "contract.loan_listed",
            "contract.loyalty_bonus", "contract.happiness", "contract.playing_time_happiness"
        })
            Assert.Contains(key, keys);
    }

    [Fact]
    public void LiveStaffCatalogueUsesStaffSpecificFieldsAndContracts()
    {
        var fields = LiveEntityMemoryEditService.GetEditableFields(LiveEntityMemoryEditService.EntityKind.Staff);
        var keys = fields.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[]
        {
            "ability.current_ability", "ability.potential_ability", "reputation.world",
            "attribute.goalkeeping", "attribute.judging_ability", "attribute.working_with_youngsters",
            "attribute.authority", "personality.professionalism", "contract.wage", "contract.expires"
        })
            Assert.Contains(key, keys);
    }

    [Fact]
    public void LiveClubCatalogueExposesFinanceFacilitiesAndStadiumFields()
    {
        var fields = LiveEntityMemoryEditService.GetEditableFields(LiveEntityMemoryEditService.EntityKind.Club);
        var keys = fields.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[]
        {
            "reputation", "facilities.training", "facilities.youth_facilities",
            "facilities.youth_coaching", "facilities.youth_recruitment", "facilities.corporate",
            "finance.balance", "finance.transfer_budget", "finance.wage_budget",
            "finance.total_wage_budget", "stadium.capacity", "stadium.seating",
            "stadium.used", "stadium.expansion"
        })
            Assert.Contains(key, keys);
    }

    [Theory]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Player, "attribute.consistency")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Player, "trait.63")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Player, "contract.transfer_listed")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Staff, "attribute.sports_science")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Staff, "personality.ambition")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "finance.balance")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "stadium.capacity")]
    public void SupportedLiveWriterFieldsAreWhitelisted(LiveEntityMemoryEditService.EntityKind entity, string field)
        => Assert.True(LiveEntityMemoryEditService.SupportsField(entity, field));

    [Theory]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Player, "contract.release_clause")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Staff, "attribute.potential_pa")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "ownership.sugar_daddy")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "debt.add")]
    public void DynamicOrUnverifiedFieldsAreNotMistakenForScalarWrites(LiveEntityMemoryEditService.EntityKind entity, string field)
        => Assert.False(LiveEntityMemoryEditService.SupportsField(entity, field));

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
