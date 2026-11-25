using FMAtlas.Domain.Clubs;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Staff;
using FMAtlas.Infrastructure.Connection;
using Xunit;

namespace FMAtlas.Tests;

public sealed class FM27LiveScanValidatorTests
{
    private static readonly FieldProvenance Evidence = new(
        DataSourceKind.LiveGameMemory,
        FieldEvidenceStatus.Mapped,
        SourceVersion: "0.9.2524.0",
        TargetBuildFingerprint: "PE:6AF5C8C6:179AD000");

    [Fact]
    public void StreamingValidatorConsumesTheSnapshotOnceWithoutMaterialisingRecordArrays()
    {
        var enumerationCount = 0;
        IEnumerable<FMAtlas.Domain.Entities.IEntityRecord> SinglePass()
        {
            if (++enumerationCount > 1)
                throw new InvalidOperationException("The validator must support a single-pass source.");

            yield return Player("1001", 150);
            yield return Player("1002", 110);
            yield return Player("1003", 80);
            yield return Player("1004", 90);
            yield return Staff("2001", 140);
            yield return Staff("2002", 90);
            yield return Club("3001");
        }

        var result = FM27LiveScanValidator.Validate(SinglePass(), WindowsFm27LiveGameConnector.LiveProviderKey);

        Assert.Equal(1, enumerationCount);
        Assert.Equal(4, result.Players);
        Assert.Equal(2, result.Staff);
        Assert.Equal(1, result.Clubs);
    }

    [Fact]
    public void AcceptsBuildScopedProviderKeyForFm2711GamePassPackage()
    {
        const string providerKey = "live-fm27-gamepass-0.9.2562.0";
        var accumulator = new FM27LiveScanValidator.StreamingAccumulator(providerKey);

        accumulator.Add(Player("1001", 150, providerKey: providerKey));
        accumulator.Add(Player("1002", 110, providerKey: providerKey));
        accumulator.Add(Player("1003", 80, providerKey: providerKey));
        accumulator.Add(Player("1004", 90, providerKey: providerKey));
        accumulator.Add(Staff("2001", 140, providerKey));
        accumulator.Add(Staff("2002", 90, providerKey));
        accumulator.Add(Club("3001", providerKey));

        var result = accumulator.Complete();

        Assert.Equal(4, result.Players);
        Assert.Equal(2, result.Staff);
        Assert.Equal(1, result.Clubs);
    }

    [Fact]
    public void AcceptsAConsistentPlayerStaffAndClubSnapshot()
    {
        var records = new FMAtlas.Domain.Entities.IEntityRecord[]
        {
            Player("1001", 150, potentialAbility: -95),
            Player("1002", 110),
            Player("1003", 80),
            Player("1004", 90),
            Staff("2001", 140),
            Staff("2002", 90),
            Club("3001")
        };

        var result = FM27LiveScanValidator.Validate(records, WindowsFm27LiveGameConnector.LiveProviderKey);

        Assert.Equal(4, result.Players);
        Assert.Equal(2, result.Staff);
        Assert.Equal(1, result.Clubs);
        Assert.Equal(4, result.PlayersWithCurrentAbility);
        Assert.Equal(4, result.PlayersWithNaturalPosition);
        Assert.Equal(4, result.PlayersWithTwentyInAtLeastOnePosition);
    }

    [Fact]
    public void AcceptsSignedNegativeRandomPotentialAbilityMarker()
    {
        var records = new FMAtlas.Domain.Entities.IEntityRecord[]
        {
            Player("1001", 150, potentialAbility: -95),
            Player("1002", 110),
            Player("1003", 80),
            Player("1004", 90),
            Staff("2001", 140),
            Staff("2002", 90),
            Club("3001")
        };

        var result = FM27LiveScanValidator.Validate(records, WindowsFm27LiveGameConnector.LiveProviderKey);

        Assert.Equal(4, result.Players);
    }

    [Fact]
    public void RejectsPotentialAbilityOutsideSignedFmRange()
    {
        var records = new FMAtlas.Domain.Entities.IEntityRecord[]
        {
            Player("1001", 150, potentialAbility: 201),
            Player("1002", 110),
            Player("1003", 80),
            Player("1004", 90),
            Staff("2001", 140),
            Staff("2002", 90),
            Club("3001")
        };

        var error = Assert.Throws<InvalidDataException>(() => FM27LiveScanValidator.Validate(records, WindowsFm27LiveGameConnector.LiveProviderKey));

        Assert.Contains("player value", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsDuplicatePlayerUids()
    {
        var records = new FMAtlas.Domain.Entities.IEntityRecord[]
        {
            Player("1001", 150),
            Player("1001", 110),
            Staff("2001", 140),
            Club("3001")
        };

        var error = Assert.Throws<InvalidDataException>(() => FM27LiveScanValidator.Validate(records, WindowsFm27LiveGameConnector.LiveProviderKey));

        Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsLayoutWithNoTwentyRatedNaturalPositions()
    {
        var records = new FMAtlas.Domain.Entities.IEntityRecord[]
        {
            Player("1001", 150, naturalPositionTwenty: false),
            Player("1002", 110, naturalPositionTwenty: false),
            Player("1003", 80, naturalPositionTwenty: false),
            Player("1004", 90, naturalPositionTwenty: false),
            Staff("2001", 140),
            Club("3001")
        };

        var error = Assert.Throws<InvalidDataException>(() => FM27LiveScanValidator.Validate(records, WindowsFm27LiveGameConnector.LiveProviderKey));

        Assert.Contains("natural-position", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsLowCurrentAbilityCoverage()
    {
        var records = new FMAtlas.Domain.Entities.IEntityRecord[]
        {
            Player("1001", 150),
            Player("1002", null),
            Player("1003", null),
            Player("1004", null),
            Staff("2001", 140),
            Club("3001")
        };

        var error = Assert.Throws<InvalidDataException>(() => FM27LiveScanValidator.Validate(records, WindowsFm27LiveGameConnector.LiveProviderKey));

        Assert.Contains("Current Ability", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AcceptsSmallNumberOfUnreadableEntitiesAndReportsThem()
    {
        var records = new FMAtlas.Domain.Entities.IEntityRecord[]
        {
            Player("1001", 150),
            Player("1002", 110),
            Player("1003", 80),
            Player("1004", 90),
            Staff("2001", 140),
            Staff("2002", 90),
            Club("3001")
        };
        var readSummary = new FM27LiveReadSummary(
            PlayerPointers: 100, PlayersRead: 97, PlayersSkipped: 3,
            StaffPointers: 20, StaffRead: 19, StaffSkipped: 1,
            ClubPointers: 10, ClubsRead: 10, ClubsSkipped: 0,
            FailureSamples: ["Player 0x1234: unreadable page"]);

        var result = FM27LiveScanValidator.Validate(records, WindowsFm27LiveGameConnector.LiveProviderKey, readSummary);

        Assert.Contains(result.Warnings, warning => warning.Contains("3 entries were skipped"));
        Assert.Single(readSummary.FailureSamples);
    }

    [Fact]
    public void RejectsLargeNumberOfUnreadablePlayerEntries()
    {
        var records = new FMAtlas.Domain.Entities.IEntityRecord[]
        {
            Player("1001", 150),
            Player("1002", 110),
            Player("1003", 80),
            Player("1004", 90),
            Staff("2001", 140),
            Staff("2002", 90),
            Club("3001")
        };
        var readSummary = new FM27LiveReadSummary(
            PlayerPointers: 100, PlayersRead: 80, PlayersSkipped: 20,
            StaffPointers: 20, StaffRead: 19, StaffSkipped: 1,
            ClubPointers: 10, ClubsRead: 10, ClubsSkipped: 0,
            FailureSamples: []);

        var error = Assert.Throws<InvalidDataException>(() => FM27LiveScanValidator.Validate(records, WindowsFm27LiveGameConnector.LiveProviderKey, readSummary));

        Assert.Contains("80 of 100 players", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PlayerRecord Player(
        string uid,
        int? currentAbility,
        bool naturalPositionTwenty = true,
        int? potentialAbility = 180,
        string? providerKey = null) =>
        new(new EntityIdentity(EntityKind.Player, uid, providerKey ?? WindowsFm27LiveGameConnector.LiveProviderKey))
        {
            FullName = new SourcedValue<string>($"Player {uid}", Evidence),
            GameUid = new SourcedValue<string>(uid, Evidence),
            CurrentAbility = currentAbility is int ca ? new SourcedValue<int>(ca, Evidence) : null,
            PotentialAbility = potentialAbility is int pa ? new SourcedValue<int>(pa, Evidence) : null,
            PrimaryPosition = new SourcedValue<string>("ST", Evidence),
            PositionRatings = new Dictionary<string, SourcedValue<int>>
            {
                ["st"] = new(naturalPositionTwenty ? 20 : 14, Evidence),
                ["amc"] = new(15, Evidence)
            },
            Attributes = new Dictionary<string, SourcedValue<int>>
            {
                ["finishing"] = new(15, Evidence),
                ["tackling"] = new(10, Evidence)
            }
        };

    private static StaffRecord Staff(string uid, int? currentAbility, string? providerKey = null) =>
        new(new EntityIdentity(EntityKind.Staff, uid, providerKey ?? WindowsFm27LiveGameConnector.LiveProviderKey))
        {
            FullName = new SourcedValue<string>($"Staff {uid}", Evidence),
            GameUid = new SourcedValue<string>(uid, Evidence),
            CurrentAbility = currentAbility is int ca ? new SourcedValue<int>(ca, Evidence) : null
        };

    private static ClubRecord Club(string uid, string? providerKey = null) =>
        new(new EntityIdentity(EntityKind.Club, uid, providerKey ?? WindowsFm27LiveGameConnector.LiveProviderKey))
        {
            Name = new SourcedValue<string>($"Club {uid}", Evidence)
        };
}
