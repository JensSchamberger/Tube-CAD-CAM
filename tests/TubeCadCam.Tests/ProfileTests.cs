using TubeCadCam.Cad;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;

namespace TubeCadCam.Tests;

public class ProfileTests
{
    public static TheoryData<ProfileDefinition> ValidProfiles => new()
    {
        new RoundTubeProfile(60.3, 2.9),
        new RectangularTubeProfile(80, 40, 3, 6),
        RectangularTubeProfile.Square(50, 3, 4.5),
        RectangularTubeProfile.Square(40, 2),
        new UProfile(100, 50, 6, 8.5),
        new LProfile(50, 30, 5),
        new IProfile(100, 55, 4.1, 5.7),
    };

    [Theory]
    [MemberData(nameof(ValidProfiles))]
    public void Tessellated_section_area_matches_analytic_area(ProfileDefinition profile)
    {
        Assert.Empty(profile.Validate());
        ProfileSection s = ProfileSectionBuilder.Build(profile, circleSegments: 1024, cornerSegments: 64);
        Assert.Equal(profile.CrossSectionAreaMm2, s.Area, profile.CrossSectionAreaMm2 * 1e-3);
        Assert.True(s.Outer.IsCounterClockwise);
    }

    [Fact]
    public void Round_tube_area_formula()
    {
        var p = new RoundTubeProfile(60, 3);
        Assert.Equal(Math.PI / 4 * ((60 * 60) - (54 * 54)), p.CrossSectionAreaMm2, 9);
        Assert.True(p.IsClosedSection);
    }

    [Fact]
    public void Square_is_recognised_and_has_square_key()
    {
        var sq = RectangularTubeProfile.Square(50, 3);
        Assert.True(sq.IsSquare);
        Assert.StartsWith("square-", sq.ProfileKey);
        Assert.StartsWith("rect-", new RectangularTubeProfile(50, 40, 3).ProfileKey);
    }

    [Fact]
    public void Open_profiles_are_not_closed_sections()
    {
        Assert.False(new UProfile(100, 50, 6, 8).IsClosedSection);
        Assert.False(new LProfile(50, 50, 5).IsClosedSection);
        Assert.False(new IProfile(100, 55, 4, 6).IsClosedSection);
    }

    public static TheoryData<ProfileDefinition, string> InvalidProfiles => new()
    {
        { new RoundTubeProfile(0, 1), "profile.not-positive" },
        { new RoundTubeProfile(20, 10), "profile.wall-too-thick" },
        { new RectangularTubeProfile(40, 20, 10), "profile.wall-too-thick" },
        { new RectangularTubeProfile(40, 20, 2, 11), "profile.radius-too-large" },
        { new RectangularTubeProfile(40, 20, 2, -1), "profile.negative-radius" },
        { new UProfile(100, 5, 6, 8), "profile.web-too-thick" },
        { new LProfile(50, 50, 50), "profile.thickness-too-large" },
        { new IProfile(10, 50, 4, 5), "profile.flange-too-thick" },
        { new RoundTubeProfile(double.NaN, 1), "profile.not-positive" },
    };

    [Theory]
    [MemberData(nameof(InvalidProfiles))]
    public void Invalid_profiles_report_issue(ProfileDefinition profile, string code) =>
        Assert.Contains(profile.Validate(), i => i.Code == code);

    [Fact]
    public void Builder_rejects_invalid_profile() =>
        Assert.Throws<ArgumentException>(() => ProfileSectionBuilder.Build(new RoundTubeProfile(10, 6)));

    [Fact]
    public void Custom_profile_area_subtracts_voids()
    {
        var outer = Polygon2.Rectangle(40, 20, Vector2.Zero);
        var hole = Polygon2.Rectangle(30, 10, Vector2.Zero);
        var p = new CustomProfile("box", outer, [hole]);
        Assert.Empty(p.Validate());
        Assert.Equal(500, p.CrossSectionAreaMm2, 9);
        Assert.True(p.IsClosedSection);
        Assert.Equal(500, ProfileSectionBuilder.Build(p).Area, 9);
    }

    [Fact]
    public void Custom_profile_with_oversized_void_is_invalid()
    {
        var p = new CustomProfile("bad", Polygon2.Rectangle(10, 10, Vector2.Zero), [Polygon2.Rectangle(10, 10, Vector2.Zero)]);
        Assert.Contains(p.Validate(), i => i.Code == "profile.voids-too-large");
    }
}
