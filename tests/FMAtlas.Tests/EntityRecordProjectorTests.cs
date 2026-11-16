using FMAtlas.Application.Projection;
using FMAtlas.Domain.Clubs;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Staff;
using Xunit;

namespace FMAtlas.Tests;

public sealed class EntityRecordProjectorTests
{
    private static readonly FieldProvenance Evidence =
        new(DataSourceKind.CareerSave, FieldEvidenceStatus.ReadVerified, SourceVersion: "FM27 fixture");

    [Fact]
    public void PlayerProjectionUsesStableKeysAndRetainsProvenance()
    {
        var identity = new EntityIdentity(EntityKind.Player, "100", "gamepass");
        var player = new PlayerRecord(identity)
        {
            FullName = new SourcedValue<string>("Example", Evidence),
            CurrentAbility = new SourcedValue<int>(140, Evidence),
            Attributes = new Dictionary<string, SourcedValue<int>>
            {
                ["pace"] = new(16, Evidence),
                ["finishing"] = new(14, Evidence)
            },
            HiddenAttributes = new Dictionary<string, SourcedValue<int>>
            {
                ["consistency"] = new(17, Evidence)
            },
            Personality = new Dictionary<string, SourcedValue<int>>
            {
                ["professionalism"] = new(18, Evidence)
            },
            FootRatings = new Dictionary<string, SourcedValue<int>>
            {
                ["left_foot"] = new(20, Evidence),
                ["right_foot"] = new(8, Evidence)
            },
            SeasonStatistics = new Dictionary<string, SourcedValue<decimal>>
            {
                ["goals_per_90"] = new(0.75m, Evidence)
            }
        };

        var result = new EntityRecordProjector().Project(player);

        Assert.Equal(identity, result.Identity);
        Assert.Equal("Example", result.DisplayName);
        Assert.Contains(result.Fields, x => x.Key == "current_ability" && x.IntegerValue == 140);
        Assert.Contains(result.Fields, x => x.Key == "attribute_pace" && x.IntegerValue == 16);
        Assert.Contains(result.Fields, x => x.Key == "attribute_consistency" && x.IntegerValue == 17);
        Assert.Contains(result.Fields, x => x.Key == "personality_professionalism" && x.IntegerValue == 18);
        Assert.Contains(result.Fields, x => x.Key == "foot_left_foot" && x.IntegerValue == 20);
        Assert.Contains(result.Fields, x => x.Key == "foot_right_foot" && x.IntegerValue == 8);
        Assert.Contains(result.Fields, x => x.Key == "stat_goals_per_90" && x.DecimalValue == 0.75m);
        Assert.All(result.Fields, x => Assert.True(x.HasValidValueShape()));
        Assert.All(result.Fields, x => Assert.Equal(FieldEvidenceStatus.ReadVerified, x.Provenance.EvidenceStatus));
    }

    [Fact]
    public void MissingFieldsAreNotProjectedAsDefaults()
    {
        var player = new PlayerRecord(new EntityIdentity(EntityKind.Player, "101", "fixture"));
        var result = new EntityRecordProjector().Project(player);

        Assert.Empty(result.Fields);
        Assert.Null(result.DisplayName);
    }

    [Fact]
    public void StaffProjectionIncludesKnowledgeAndClubProjectionIncludesFacilities()
    {
        var staff = new StaffRecord(new EntityIdentity(EntityKind.Staff, "s1", "fixture"))
        {
            Knowledge = new Dictionary<string, SourcedValue<int>>
            {
                ["judging_potential"] = new(17, Evidence)
            }
        };
        var club = new ClubRecord(new EntityIdentity(EntityKind.Club, "c1", "fixture"))
        {
            Facilities = new Dictionary<string, SourcedValue<int>>
            {
                ["training"] = new(19, Evidence)
            }
        };

        var projector = new EntityRecordProjector();
        Assert.Contains(projector.Project(staff).Fields, x => x.Key == "knowledge_judging_potential");
        Assert.Contains(projector.Project(club).Fields, x => x.Key == "facility_training");
    }

    [Fact]
    public void ProjectorRejectsRecordIdentityOfWrongKind()
    {
        var invalid = new PlayerRecord(new EntityIdentity(EntityKind.Staff, "s1", "fixture"));
        Assert.Throws<ArgumentException>(() => new EntityRecordProjector().Project(invalid));
    }
}
