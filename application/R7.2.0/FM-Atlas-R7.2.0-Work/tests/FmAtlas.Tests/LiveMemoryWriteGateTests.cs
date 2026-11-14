using FmAtlas.Services;
using Xunit;

namespace FmAtlas.Tests;

public sealed class LiveMemoryWriteGateTests
{
    [Fact]
    public void SafeModuleDiagnosticReportRedactsWitnessNamesAndAbsolutePaths()
    {
        var raw = "Manager witness 'Lewis Atkinson' MATCH; Player witness 'Bradley Barcola'; common='Ada Player' decoded='Liverpool FC' expected='Liverpool' (expected club: Chelsea); path=C:\\Users\\Lewis\\FM\\game_plugin.dll";
        var safe = GameModuleDiagnostics.RedactForSupport(raw, includePersonalDetails: false);
        Assert.DoesNotContain("Lewis Atkinson", safe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bradley Barcola", safe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ada Player", safe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Liverpool FC", safe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Liverpool", safe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Chelsea", safe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Users\Lewis", safe, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted manager]", safe);
        Assert.Contains("[redacted player]", safe);
        Assert.Contains("[path redacted]", safe);
    }

    [Fact]
    public void ModuleDiagnosticPersonalDetailsAreIncludedOnlyAfterExplicitOptIn()
    {
        const string raw = "Lewis Atkinson · C:\\Users\\Lewis\\FM";
        Assert.Equal(raw, GameModuleDiagnostics.RedactForSupport(raw, includePersonalDetails: true));
    }

    [Fact]
    public void LegacyPlayerOnlyLiveMemoryWritesRemainDisabled()
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
    public void TacticalAreaOptionDecoderMatchesTheFM27LibreGoldenFixture()
    {
        var decoded = LiveEntityMemoryEditService.DecodeTacticalAreaOptions(
            0x8261_100A_108A_1088UL, 0x8064UL);
        Assert.Equal(new[] { 5, 17, 10, 4, 20, 10 }, decoded);
    }

    [Fact]
    public void MaxTrainingContributionBuildsAtLeastEighteenEntriesPerArea()
    {
        var oldHistory = new byte[12];
        BitConverter.GetBytes((ushort)120).CopyTo(oldHistory, 0);
        BitConverter.GetBytes((ushort)0x0C8C).CopyTo(oldHistory, 2);
        oldHistory[4] = 0;
        oldHistory[6] = 12;
        oldHistory[9] = 20;
        oldHistory[10] = 20;
        oldHistory[11] = 156;

        var built = LiveEntityMemoryEditService.BuildMaxTrainingContributionHistory(
            oldHistory, new[] { 5, 17, 10, 4, 20, 10 }, 250);

        Assert.Equal(18 * 6 * 12, built.Length);
        for (var area = 0; area < 6; area++)
        {
            var count = Enumerable.Range(0, built.Length / 12)
                .Count(i => built[i * 12 + 4] == area);
            Assert.Equal(18, count);
        }
        Assert.Equal((ushort)250, BitConverter.ToUInt16(built, 0));
        Assert.Equal((ushort)0x0C8C, BitConverter.ToUInt16(built, 2));
        Assert.Equal((byte)5, built[6]);
        Assert.Equal((byte)20, built[9]);
        Assert.Equal((byte)20, built[10]);
        Assert.Equal((byte)156, built[11]);
    }

    [Fact]
    public void MaxTrainingContributionPreservesKnownShapesAndAddsBothShapeAreas()
    {
        var oldHistory = new byte[24];
        BitConverter.GetBytes((ushort)200).CopyTo(oldHistory, 0);
        BitConverter.GetBytes((ushort)0x1234).CopyTo(oldHistory, 2);
        oldHistory[4] = 6;
        oldHistory[6] = 255;
        oldHistory[8] = 1;
        oldHistory[9] = 20;
        oldHistory[10] = 20;
        oldHistory[11] = 156;
        BitConverter.GetBytes((ushort)210).CopyTo(oldHistory, 12);
        BitConverter.GetBytes((ushort)0x5678).CopyTo(oldHistory, 14);
        oldHistory[16] = 7;
        oldHistory[18] = 255;
        oldHistory[20] = 1;
        oldHistory[21] = 20;
        oldHistory[22] = 20;
        oldHistory[23] = 156;

        var built = LiveEntityMemoryEditService.BuildMaxTrainingContributionHistory(
            oldHistory, new[] { 5, 17, 10, 4, 20, 10 }, 250);

        Assert.Equal(18 * 8 * 12, built.Length);
        Assert.Equal((ushort)0x1234, BitConverter.ToUInt16(built, 2 + 6 * 12));
        Assert.Equal((byte)1, built[8 + 6 * 12]);
        Assert.Equal((ushort)0x5678, BitConverter.ToUInt16(built, 2 + 7 * 12));
        Assert.Equal((byte)1, built[8 + 7 * 12]);
    }

    [Theory]
    [InlineData(48, 48)]
    [InlineData(156, 160)]
    [InlineData(288, 288)]
    [InlineData(513, 1024)]
    [InlineData(4096, 4096)]
    public void TacticalGrowthUsesMeasuredFMAllocatorClasses(ulong capacity, ulong expected)
        => Assert.Equal((ulong?)expected, LiveEntityMemoryEditService.GetMeasuredTacticalBufferBlockSize(capacity));

    [Fact]
    public void TacticalGrowthBeyondLargestMeasuredBlockIsRefused()
        => Assert.Null(LiveEntityMemoryEditService.GetMeasuredTacticalBufferBlockSize(4097));

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
            "date_of_birth", "contract.unhappiness", "loan.expires", "loan.option_fee",
            "position.gk", "position.amc", "personality.professionalism",
            "left_foot", "right_foot", "trait.0", "trait.63",
            "contract.wage", "contract.signed", "contract.expires", "contract.joined", "contract.squad_number",
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
            "ability.current_ability", "ability.potential_ability", "reputation.world", "date_of_birth",
            "attribute.goalkeeping", "attribute.judging_ability", "attribute.working_with_youngsters",
            "attribute.authority", "personality.professionalism", "contract.wage"
        })
            Assert.Contains(key, keys);
        Assert.DoesNotContain("contract.expires", keys);
        Assert.DoesNotContain("contract.signed", keys);
        Assert.DoesNotContain("contract.joined", keys);
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
            "club.season_tickets", "club.social_media",
            "supporters.mix.hardcore", "supporters.mix.fair_weather",
            "supporters.trait.loyalty", "supporters.trait.expectations",
            "finance.balance", "finance.transfer_budget", "finance.wage_budget",
            "finance.total_wage_budget", "finance.ticket_price_pence",
            "stadium.capacity", "stadium.seating", "stadium.used", "stadium.expansion"
        })
            Assert.Contains(key, keys);
    }

    [Theory]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Player, "contract.signed")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Player, "contract.joined")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "club.season_tickets")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "club.social_media")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "supporters.mix.core")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "supporters.trait.passion")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "finance.ticket_price_pence")]
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
    [InlineData(LiveEntityMemoryEditService.EntityKind.Player, "transfer_value")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Player, "contract.type")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Player, "contract.release_clause")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Staff, "attribute.potential_pa")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "ownership.sugar_daddy")]
    [InlineData(LiveEntityMemoryEditService.EntityKind.Club, "debt.add")]
    public void DynamicOrUnverifiedFieldsAreNotMistakenForScalarWrites(LiveEntityMemoryEditService.EntityKind entity, string field)
        => Assert.False(LiveEntityMemoryEditService.SupportsField(entity, field));

    [Fact]
    public void LiveEntityWriterRequiresExplicitSessionOptIn()
    {
        LiveEntityMemoryEditService.SetLiveEditingEnabled(false);
        try
        {
            Assert.False(LiveEntityMemoryEditService.LiveEditingEnabled);
            var exception = Assert.Throws<InvalidOperationException>(
                () => LiveEntityMemoryEditService.ApplyBatch(CreateEntityPlan()));
            Assert.Contains("OFF", exception.Message);
        }
        finally
        {
            LiveEntityMemoryEditService.SetLiveEditingEnabled(false);
        }
    }

    [Fact]
    public void LiveEntityUndoRequiresExplicitSessionOptIn()
    {
        LiveEntityMemoryEditService.SetLiveEditingEnabled(false);
        var receipt = new LiveEntityMemoryEditService.BatchReceipt(
            CreateEntityPlan(), DateTime.UtcNow, ReadBackVerified: true);
        var exception = Assert.Throws<InvalidOperationException>(
            () => LiveEntityMemoryEditService.UndoBatch(receipt));
        Assert.Contains("OFF", exception.Message);
    }

    [Fact]
    public void TacticalLiveApplyRequiresExplicitSessionOptIn()
    {
        LiveEntityMemoryEditService.SetLiveEditingEnabled(false);
        var plan = CreateTacticalPlan();
        var exception = Assert.Throws<InvalidOperationException>(
            () => LiveEntityMemoryEditService.ApplyTacticalExposureBatch(plan));
        Assert.Contains("OFF", exception.Message);
    }

    [Fact]
    public void TacticalLiveUndoRequiresExplicitSessionOptIn()
    {
        LiveEntityMemoryEditService.SetLiveEditingEnabled(false);
        var plan = CreateTacticalPlan();
        var receipt = new LiveEntityMemoryEditService.TacticalBatchReceipt(
            plan, DateTime.UtcNow, Array.Empty<LiveEntityMemoryEditService.AppliedTacticalListChange>());
        var exception = Assert.Throws<InvalidOperationException>(
            () => LiveEntityMemoryEditService.UndoTacticalExposureBatch(receipt));
        Assert.Contains("OFF", exception.Message);
    }

    private static LiveEntityMemoryEditService.BatchPlan CreateEntityPlan()
        => new(
            ProcessId: -1,
            Entity: LiveEntityMemoryEditService.EntityKind.Player,
            Uid: 1,
            Name: "Test Player",
            ObjectAddress: 0,
            IdentityShift: 0,
            ProfileId: "test-profile",
            ProfileFingerprint: "test-fingerprint",
            SaveLoadCounter: 1,
            CreatedUtc: DateTime.UtcNow,
            Values: Array.Empty<LiveEntityMemoryEditService.PlannedValue>());

    private static LiveEntityMemoryEditService.TacticalBatchPlan CreateTacticalPlan()
        => new(
            ProcessId: -1,
            ClubUid: 1,
            ClubName: "Test Club",
            ClubAddress: 0,
            SeniorTeamAddress: 0,
            TeamUid: 1,
            TacticAddress: 0,
            TacticInstructions: Array.Empty<byte>(),
            ProfileId: "test-profile",
            ProfileFingerprint: "test-fingerprint",
            SaveLoadCounter: 1,
            CreatedUtc: DateTime.UtcNow,
            PlayersVisited: 0,
            Changes: Array.Empty<LiveEntityMemoryEditService.TacticalListChange>());

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
