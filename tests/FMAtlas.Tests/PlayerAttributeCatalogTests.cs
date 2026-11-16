using FMAtlas.Domain.Players;
using Xunit;

namespace FMAtlas.Tests;

public sealed class PlayerAttributeCatalogTests
{
    [Fact]
    public void CatalogueContains52UniqueNonFootAttributes()
    {
        Assert.Equal(52, PlayerAttributeCatalog.AllUniqueNonFootAttributes.Count);
        Assert.Equal(
            PlayerAttributeCatalog.AllUniqueNonFootAttributes.Count,
            PlayerAttributeCatalog.AllUniqueNonFootAttributes.Select(x => x.Key).Distinct().Count());
    }

    [Fact]
    public void TechnicalAndGoalkeepingShareFirstTouchAndPassing()
    {
        var technical = PlayerAttributeCatalog.GetProfileGroups(false)
            .Single(x => x.Kind == PlayerAttributeGroupKind.Technical);
        var goalkeeping = PlayerAttributeCatalog.GetProfileGroups(true)
            .Single(x => x.Kind == PlayerAttributeGroupKind.Goalkeeping);

        Assert.Contains(technical.Attributes, x => x.Key == "first_touch");
        Assert.Contains(technical.Attributes, x => x.Key == "passing");
        Assert.Contains(goalkeeping.Attributes, x => x.Key == "first_touch");
        Assert.Contains(goalkeeping.Attributes, x => x.Key == "passing");
    }

    [Fact]
    public void OutfieldAndGoalkeeperProfilesUseDifferentPrimaryGroups()
    {
        Assert.Contains(PlayerAttributeCatalog.GetProfileGroups(false),
            x => x.Kind == PlayerAttributeGroupKind.Technical);
        Assert.DoesNotContain(PlayerAttributeCatalog.GetProfileGroups(false),
            x => x.Kind == PlayerAttributeGroupKind.Goalkeeping);

        Assert.Contains(PlayerAttributeCatalog.GetProfileGroups(true),
            x => x.Kind == PlayerAttributeGroupKind.Goalkeeping);
        Assert.DoesNotContain(PlayerAttributeCatalog.GetProfileGroups(true),
            x => x.Kind == PlayerAttributeGroupKind.Technical);
    }

    [Fact]
    public void HiddenAttributesRemainSeparateFromPersonalityAndStandardAttributes()
    {
        var hidden = PlayerAttributeCatalog.GetProfileGroups(false)
            .Single(x => x.Kind == PlayerAttributeGroupKind.Hidden);

        Assert.Equal(5, hidden.Attributes.Count);
        Assert.All(hidden.Attributes, x => Assert.True(x.IsHidden));
        Assert.DoesNotContain(PlayerAttributeCatalog.AllUniqueNonFootAttributes,
            x => x.Group == PlayerAttributeGroupKind.Feet);
    }

    [Fact]
    public void PersonalityHasEightSeparateFieldsInFMStorageOrder()
    {
        Assert.Equal(new[]
        {
            "adaptability", "ambition", "loyalty", "pressure",
            "professionalism", "sportsmanship", "temperament", "controversy"
        }, PlayerAttributeCatalog.PersonalityAttributes.Select(x => x.Key));

        Assert.Contains(PlayerAttributeCatalog.GetProfileGroups(false),
            x => x.Kind == PlayerAttributeGroupKind.Personality);
        Assert.Contains(PlayerAttributeCatalog.GetProfileGroups(true),
            x => x.Kind == PlayerAttributeGroupKind.Personality);
        Assert.Equal(PlayerAttributeGroupKind.Personality,
            PlayerAttributeCatalog.PersonalityAttributes[4].Group);
    }

    [Fact]
    public void FootRatingsAreSeparateFromThe52NonFootAttributes()
    {
        Assert.Equal(2, PlayerAttributeCatalog.FootAttributes.Count);
        Assert.Equal("left_foot", PlayerAttributeCatalog.FootAttributes[0].Key);
        Assert.Equal("right_foot", PlayerAttributeCatalog.FootAttributes[1].Key);
        Assert.Equal(52, PlayerAttributeCatalog.AllUniqueNonFootAttributes.Count);
    }

    [Fact]
    public void TechnicalOrderMatchesTheDocumentedFMProfileCatalogue()
    {
        var actual = PlayerAttributeCatalog.GetProfileGroups(false)
            .Single(x => x.Kind == PlayerAttributeGroupKind.Technical)
            .Attributes.Select(x => x.Key);

        Assert.Equal(new[]
        {
            "corners", "crossing", "dribbling", "finishing", "first_touch",
            "free_kicks", "heading", "long_shots", "long_throws", "marking",
            "passing", "penalty_taking", "tackling", "technique"
        }, actual);
    }

    [Fact]
    public void LookupFindsAttributesWithoutCaseSensitivity()
    {
        Assert.True(PlayerAttributeCatalog.TryGetByKey("PACE", out var pace));
        Assert.Equal("Pace", pace!.DisplayName);
        Assert.False(PlayerAttributeCatalog.TryGetByKey("not_a_real_attribute", out _));
    }
}
