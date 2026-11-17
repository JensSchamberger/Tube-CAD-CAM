using TubeCadCam.Cad;
using TubeCadCam.Domain;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;

namespace TubeCadCam.Tests;

public class CadKernelTests
{
    public static TheoryData<ProfileDefinition> Profiles => new()
    {
        new RoundTubeProfile(60, 3),
        new RectangularTubeProfile(80, 40, 3, 6),
        new RectangularTubeProfile(80, 40, 3, 0),
        new RectangularTubeProfile(50, 50, 4, 3),   // inner radius 0, outer radius > 0 (different vertex counts)
        new UProfile(100, 50, 6, 8),
        new LProfile(50, 30, 5),
        new IProfile(100, 55, 4, 6),
        new CustomProfile("c", Polygon2.Rectangle(40, 20, Vector2.Zero), [Polygon2.Circle(Vector2.Zero, 6, 32)]),
    };

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Body_is_watertight_with_volume_area_times_length(ProfileDefinition profile)
    {
        var model = TubeModelTests.NewModel(500, profile);
        var scene = TubeSceneBuilder.Build(model).Value;
        MeshTestHelpers.AssertWatertight(scene.Body);
        MeshTestHelpers.AssertNormalsMatchWinding(scene.Body);
        Assert.Equal(scene.Section.Area * 500, scene.Body.SignedVolume(), scene.Section.Area * 500 * 1e-9);
    }

    [Fact]
    public void Tube_has_inner_wall_and_wall_thickness_visible_in_bounds()
    {
        var scene = TubeSceneBuilder.Build(TubeModelTests.NewModel(1000, new RoundTubeProfile(60, 3))).Value;
        Assert.Equal(1000, scene.Bounds.Max.X - scene.Bounds.Min.X, 9);
        Assert.Equal(60, scene.Bounds.Max.Y - scene.Bounds.Min.Y, 1);
        // inner radius vertices exist at r = 27
        Assert.Contains(scene.Body.Positions, p => Math.Abs(Math.Sqrt((p.Y * p.Y) + (p.Z * p.Z)) - 27) < 1e-6);
        // enclosed volume equals annulus * L, i.e. the bore is empty
        Assert.Equal(Math.PI / 4 * ((60 * 60) - (54 * 54)) * 1000, scene.Body.SignedVolume(), 1e3);
    }

    [Fact]
    public void Geometry_generation_is_deterministic()
    {
        var m = TubeModelTests.NewModel(700, new RectangularTubeProfile(80, 40, 3, 6))
            .AddFeature(new HoleFeature(Guid.Parse("00000000-0000-0000-0000-000000000001"), 300, 90, 12));
        var a = TubeSceneBuilder.Build(m).Value;
        var b = TubeSceneBuilder.Build(m).Value;
        Assert.Equal(a.Body.Positions, b.Body.Positions);
        Assert.Equal(a.Body.Indices, b.Body.Indices);
        Assert.Equal(a.PreviewShapes[0].Mesh.Positions, b.PreviewShapes[0].Mesh.Positions);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 250)]
    [InlineData(3, 1000)]
    public void Every_parameter_change_is_reflected_in_the_body(int which, double newLength)
    {
        var baseModel = TubeModelTests.NewModel(500, new RectangularTubeProfile(80, 40, 3, 6));
        double v0 = TubeSceneBuilder.Build(baseModel).Value.Body.SignedVolume();
        TubeModel changed = which switch
        {
            1 => baseModel with { Part = baseModel.Part with { LengthMm = newLength } },
            2 => baseModel with { Part = baseModel.Part with { Profile = new RectangularTubeProfile(80, 40, 5, 6) } },
            _ => baseModel with { Part = baseModel.Part with { Profile = new RectangularTubeProfile(90, 40, 3, 0) } },
        };
        Assert.NotEqual(v0, TubeSceneBuilder.Build(changed).Value.Body.SignedVolume());
    }

    [Fact]
    public void Standard_profiles_are_centred_on_their_bounding_box()
    {
        foreach (ProfileDefinition p in new ProfileDefinition[] { new UProfile(100, 50, 6, 8), new LProfile(50, 30, 5), new IProfile(100, 55, 4, 6) })
        {
            Bounds2 b = ProfileSectionBuilder.Build(p).Outer.Bounds;
            Assert.Equal(0, b.Min.X + b.Max.X, 9);
            Assert.Equal(0, b.Min.Y + b.Max.Y, 9);
        }
    }

    [Fact]
    public void Miter_volume_matches_analytic_formula_and_mesh_stays_closed()
    {
        // rectangular tube 80 (y) x 40 (z); tilt axis = +Z (90°) -> b = (-1, 0): tilt across the 80 mm width
        var model = TubeModelTests.NewModel(500, new RectangularTubeProfile(80, 40, 3, 0))
            .AddFeature(new EndMiterFeature(Guid.NewGuid(), TubeEnd.Start, 30, 90));
        Assert.DoesNotContain(model.Validate(), i => i.Severity == TubeCadCam.Domain.Common.IssueSeverity.Error);
        var scene = TubeSceneBuilder.Build(model).Value;
        MeshTestHelpers.AssertWatertight(scene.Body);
        MeshTestHelpers.AssertNormalsMatchWinding(scene.Body);

        double area = scene.Section.Area;
        double t = Math.Tan(30 * Math.PI / 180);
        double expected = area * (500 - (t * 80 / 2)); // centroid is at mid-width
        Assert.Equal(expected, scene.Body.SignedVolume(), expected * 1e-9);
        Assert.Equal(500, scene.Bounds.Max.X - scene.Bounds.Min.X, 9); // envelope length unchanged
    }

    [Fact]
    public void Miters_on_both_ends_stay_closed()
    {
        var model = TubeModelTests.NewModel(500, new RoundTubeProfile(60, 3))
            .AddFeature(new EndMiterFeature(Guid.NewGuid(), TubeEnd.Start, 45, 0))
            .AddFeature(new EndMiterFeature(Guid.NewGuid(), TubeEnd.End, -45, 0));
        Assert.Empty(model.Validate());
        var body = TubeSceneBuilder.Build(model).Value.Body;
        MeshTestHelpers.AssertWatertight(body);
        Assert.True(body.SignedVolume() > 0);
    }

    [Fact]
    public void Miter_too_large_for_tube_length_is_rejected()
    {
        var model = TubeModelTests.NewModel(40, new RoundTubeProfile(60, 3))
            .AddFeature(new EndMiterFeature(Guid.NewGuid(), TubeEnd.Start, 60, 0));
        Assert.Contains(model.Validate(), i => i.Code == "feature.miter-too-large");
    }

    [Fact]
    public void Duplicate_miter_on_same_end_is_rejected()
    {
        var model = TubeModelTests.NewModel(500)
            .AddFeature(new EndMiterFeature(Guid.NewGuid(), TubeEnd.Start, 10, 0))
            .AddFeature(new EndMiterFeature(Guid.NewGuid(), TubeEnd.Start, 20, 0));
        Assert.Contains(model.Validate(), i => i.Code == "feature.miter-duplicate");
    }

    [Fact]
    public void Miter_angle_out_of_range_is_rejected() =>
        Assert.Contains(TubeModelTests.NewModel().AddFeature(new EndMiterFeature(Guid.NewGuid(), TubeEnd.End, 89.5, 0)).Validate(), i => i.Code == "feature.tilt-range");

    // ---- coordinate system ----

    [Fact]
    public void Coordinate_system_angles_are_right_handed_about_x_starting_at_plus_y()
    {
        Assert.Equal(new Vector2(1, 0), Round(TubeCoordinateSystem.Direction(0)));
        Assert.Equal(new Vector2(0, 1), Round(TubeCoordinateSystem.Direction(90)));
        Assert.Equal(new Vector2(-1, 0), Round(TubeCoordinateSystem.Direction(180)));
        Assert.Equal(new Vector3(7, 1, 2), TubeCoordinateSystem.SectionToWorld(7, new Vector2(1, 2)));
        // right-handed: +X (axis) x +Y(0°) = +Z(90°)
        Assert.Equal(new Vector3(0, 0, 1), new Vector3(1, 0, 0).Cross(new Vector3(0, 1, 0)));
    }

    private static Vector2 Round(Vector2 v) => new(Math.Round(v.X, 9), Math.Round(v.Y, 9));

    [Fact]
    public void Surface_location_on_round_tube_is_radial()
    {
        var s = new SectionSurface(ProfileSectionBuilder.Build(new RoundTubeProfile(60, 3)).Outer);
        foreach (double a in new[] { 0.0, 33, 90, 200, 359 })
        {
            SurfacePoint p = s.Locate(a)!;
            Assert.Equal(30, p.Position.Length, 0.05); // facet chord
            Vector2 dir = TubeCoordinateSystem.Direction(a);
            Assert.True(p.Normal.Dot(dir) > 0.999);
            Assert.True(p.Tangent.Cross(p.Normal) < 0 || Math.Abs(p.Tangent.Dot(p.Normal)) < 1e-9);
        }
    }

    [Fact]
    public void Surface_location_on_rectangular_tube_hits_the_face()
    {
        var s = new SectionSurface(ProfileSectionBuilder.Build(new RectangularTubeProfile(80, 40, 3, 0)).Outer);
        Assert.Equal(40, s.Locate(0)!.Position.X, 9);   // +Y face at y = 40
        Assert.Equal(20, s.Locate(90)!.Position.Y, 9);  // +Z face at z = 20
        Assert.Equal(new Vector2(0, 1), Round(s.Locate(90)!.Normal));
    }

    [Fact]
    public void Ray_towards_opening_of_a_u_profile_misses()
    {
        var s = new SectionSurface(ProfileSectionBuilder.Build(new UProfile(100, 50, 6, 8)).Outer);
        Assert.NotNull(s.Locate(180)); // web side
        Assert.Null(s.Locate(0));      // open side
    }
}
