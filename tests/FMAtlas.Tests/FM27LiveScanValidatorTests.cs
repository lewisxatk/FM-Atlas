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
        SourceVersion: "0.9.2435.0",
        TargetBuildFingerprint: "PE:6AE8CF77:17969000");

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

        var result = FM27LiveScanValidator.Validate(records);

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

        var result = FM27LiveScanValidator.Validate(records);

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

        var error = Assert.Throws<InvalidDataException>(() => FM27LiveScanValidator.Validate(records));

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

        var error = Assert.Throws<InvalidDataException>(() => FM27LiveScanValidator.Validate(records));

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

        var error = Assert.Throws<InvalidDataException>(() => FM27LiveScanValidator.Validate(records));

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

        var error = Assert.Throws<InvalidDataException>(() => FM27LiveScanValidator.Validate(records));

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

        var result = FM27LiveScanValidator.Validate(records, readSummary);

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

        var error = Assert.Throws<InvalidDataException>(() => FM27LiveScanValidator.Validate(records, readSummary));

        Assert.Contains("80 of 100 players", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PlayerRecord Player(
        string uid,
        int? currentAbility,
        bool naturalPositionTwenty = true,
        int? potentialAbility = 180) =>
        new(new EntityIdentity(EntityKind.Player, uid, WindowsFm27LiveGameConnector.LiveProviderKey))
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

    private static StaffRecord Staff(string uid, int? currentAbility) =>
        new(new EntityIdentity(EntityKind.Staff, uid, WindowsFm27LiveGameConnector.LiveProviderKey))
        {
            FullName = new SourcedValue<string>($"Staff {uid}", Evidence),
            GameUid = new SourcedValue<string>(uid, Evidence),
            CurrentAbility = currentAbility is int ca ? new SourcedValue<int>(ca, Evidence) : null
        };

    private static ClubRecord Club(string uid) =>
        new(new EntityIdentity(EntityKind.Club, uid, WindowsFm27LiveGameConnector.LiveProviderKey))
        {
            Name = new SourcedValue<string>($"Club {uid}", Evidence)
        };
}
