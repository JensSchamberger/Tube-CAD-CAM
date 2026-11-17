using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;

namespace TubeCadCam.Tests;

public class FeatureTests
{
    private static TubeModel Round(params TubeFeature[] f) =>
        f.Aggregate(TubeModelTests.NewModel(1000, new RoundTubeProfile(60, 3)), (m, x) => m.AddFeature(x));

    private static TubeModel Rect(params TubeFeature[] f) =>
        f.Aggregate(TubeModelTests.NewModel(1000, new RectangularTubeProfile(80, 40, 3, 0)), (m, x) => m.AddFeature(x));

    private static Guid G() => Guid.NewGuid();
    private static bool HasError(TubeModel m, string code) => m.Validate().Any(i => i.Code == code && i.Severity == IssueSeverity.Error);

    [Fact]
    public void Valid_features_of_every_kind_pass_validation()
    {
        TubeModel m = Round(
            new HoleFeature(G(), 100, 0, 10),
            new RectangularCutoutFeature(G(), 300, 90, 12, 30, 2, 15),
            new SlotFeature(G(), 500, 180, 30, 8, 30),
            new ContourCutoutFeature(G(), 700, 270, new Polygon2([new(-8, -5), new(8, -5), new(0, 7)]), 10),
            new EndNotchFeature(G(), TubeEnd.Start, 45, 15, 25, 3),
            new SlitFeature(G(), TubeEnd.End, 0, 4, 60),
            new EndMiterFeature(G(), TubeEnd.End, 20, 90));
        Assert.DoesNotContain(m.Validate(), i => i.Severity == IssueSeverity.Error);
        var scene = TubeSceneBuilder.Build(m).Value;
        Assert.Equal(6, scene.PreviewShapes.Count);       // all but the miter (part of the body)
        Assert.Empty(scene.SkippedFeatures);
    }

    [Theory]
    [InlineData(0, "feature.extent-invalid")]
    [InlineData(-3, "feature.extent-invalid")]
    [InlineData(double.NaN, "feature.extent-invalid")]
    public void Hole_diameter_must_be_positive(double d, string code) => Assert.True(HasError(Round(new HoleFeature(G(), 500, 0, d)), code));

    [Fact]
    public void Slot_must_be_longer_than_wide() => Assert.True(HasError(Round(new SlotFeature(G(), 500, 0, 10, 10)), "feature.slot-length"));

    [Fact]
    public void Slot_extent_is_checked_against_tube_length() => Assert.True(HasError(Round(new SlotFeature(G(), 5, 0, 30, 8)), "feature.outside-length"));

    [Fact]
    public void Rotation_changes_the_axial_extent_used_for_range_checks()
    {
        // 40 long slot at 10 mm from the start: fits unrotated (needs >= 20), but not when rotated to 90° (axial extent is 8 -> still fits) -> use opposite case
        Assert.True(HasError(Round(new SlotFeature(G(), 15, 0, 40, 8, 0)), "feature.outside-length"));
        Assert.DoesNotContain(Round(new SlotFeature(G(), 15, 0, 40, 8, 90)).Validate(), i => i.Code == "feature.outside-length");
    }

    [Fact]
    public void Self_intersecting_contour_is_rejected() =>
        Assert.True(HasError(Round(new ContourCutoutFeature(G(), 500, 0, new Polygon2([new(0, 0), new(5, 5), new(5, 0), new(0, 5)]))), "feature.contour-invalid"));

    [Fact]
    public void Degenerate_contour_is_rejected() =>
        Assert.True(HasError(Round(new ContourCutoutFeature(G(), 500, 0, new Polygon2([new(0, 0), new(5, 0), new(10, 0)]))), "feature.contour-invalid"));

    [Fact]
    public void Slit_that_would_split_the_tube_or_is_too_wide_is_rejected()
    {
        Assert.True(HasError(Round(new SlitFeature(G(), TubeEnd.Start, 0, 4, 1000)), "feature.slit-splits-tube"));
        Assert.True(HasError(Round(new SlitFeature(G(), TubeEnd.Start, 0, 60, 50)), "feature.slit-too-wide"));
        Assert.True(HasError(Round(new SlitFeature(G(), TubeEnd.Start, 0, 4, 1200)), "feature.outside-length"));
    }

    [Fact]
    public void Notch_radius_and_depth_are_checked()
    {
        Assert.True(HasError(Round(new EndNotchFeature(G(), TubeEnd.End, 0, 10, 20, 8)), "feature.radius-invalid"));
        Assert.True(HasError(Round(new EndNotchFeature(G(), TubeEnd.End, 0, 10, 2000, 0)), "feature.outside-length"));
    }

    [Fact]
    public void Notch_outline_is_ccw_and_open_towards_its_end()
    {
        foreach (TubeEnd end in Enum.GetValues<TubeEnd>())
        {
            var n = new EndNotchFeature(G(), end, 0, 10, 20, 3);
            Polygon2 o = n.Outline(32, 8);
            Assert.True(o.IsCounterClockwise);
            Assert.Equal(20, o.Bounds.Width, 9);
            double centre = n.AxialCenter(1000);
            Assert.Equal(end == TubeEnd.Start ? 10.0 : 990.0, centre, 9);
            Assert.Equal(end == TubeEnd.Start ? 0.0 : 1000.0, end == TubeEnd.Start ? centre + o.Bounds.Min.X : centre + o.Bounds.Max.X, 9);
        }
    }

    [Fact]
    public void Feature_over_the_edge_of_a_rectangular_tube_is_rejected()
    {
        // top face is 80 wide; hole of 70 centred on it fits, centred off-axis it crosses the edge
        Assert.DoesNotContain(Rect(new HoleFeature(G(), 500, 90, 38)).Validate(), i => i.Severity == IssueSeverity.Error);
        Assert.True(HasError(Rect(new RectangularCutoutFeature(G(), 500, 90, 30, 20, 0, 0)), "feature.crosses-edge") == false);
        Assert.True(HasError(Rect(new RectangularCutoutFeature(G(), 500, 90, 30, 20, 0, 0).With(angle: 20)), "feature.crosses-edge"));
    }

    [Fact]
    public void Feature_wrapping_far_around_a_round_tube_is_rejected() =>
        Assert.True(HasError(Round(new RectangularCutoutFeature(G(), 500, 0, 64, 10)), "feature.wraps-surface"));

    [Fact]
    public void Angle_that_misses_an_open_profile_is_reported()
    {
        var m = TubeModelTests.NewModel(500, new UProfile(100, 50, 6, 8)).AddFeature(new HoleFeature(G(), 250, 0, 10));
        Assert.True(HasError(m, "feature.no-surface"));
        Assert.False(HasError(TubeModelTests.NewModel(500, new UProfile(100, 50, 6, 8)).AddFeature(new HoleFeature(G(), 250, 180, 10)), "feature.no-surface"));
    }

    [Fact]
    public void Overlapping_features_produce_a_warning_not_an_error()
    {
        TubeModel m = Round(new HoleFeature(G(), 500, 0, 10), new HoleFeature(G(), 504, 0, 10));
        Assert.Contains(m.Validate(), i => i.Code == "feature.overlap" && i.Severity == IssueSeverity.Warning);
        Assert.DoesNotContain(m.Validate(), i => i.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(Round(new HoleFeature(G(), 300, 0, 10), new HoleFeature(G(), 500, 0, 10)).Validate(), i => i.Code == "feature.overlap");
    }

    [Fact]
    public void Feature_reaching_beyond_a_mitred_end_is_rejected()
    {
        var m = TubeModelTests.NewModel(500, new RoundTubeProfile(60, 3))
            .AddFeature(new EndMiterFeature(G(), TubeEnd.Start, 45, 90))
            .AddFeature(new HoleFeature(G(), 15, 270, 10)); // on the side where the miter removes the most material
        Assert.True(HasError(m, "feature.beyond-end"));
    }

    [Fact]
    public void Replace_and_remove_feature_keep_model_consistent()
    {
        var id = G();
        TubeModel m = Round(new HoleFeature(id, 300, 0, 10));
        m = m.ReplaceFeature(new HoleFeature(id, 400, 0, 12));
        Assert.Equal(12, Assert.IsType<HoleFeature>(Assert.Single(m.Features)).DiameterMm);
        Assert.Empty(m.RemoveFeature(id).Features);
    }

    [Fact]
    public void Cutter_meshes_are_closed_and_positioned_on_the_requested_surface()
    {
        var hole = new HoleFeature(G(), 400, 90, 10);          // +Z side
        var scene = TubeSceneBuilder.Build(Round(hole)).Value;
        var cutter = scene.PreviewShapes.Single().Mesh;
        MeshTestHelpers.AssertWatertight(cutter);
        Bounds3 b = cutter.Bounds;
        Assert.Equal(400, b.Center.X, 0.5);
        Assert.True(b.Max.Z > 29 && b.Min.Z > 20);              // sits on the top wall around z = 30
        Assert.Equal(0, b.Center.Y, 0.5);
    }

    [Fact]
    public void Slit_cutter_runs_through_the_tube_from_the_end()
    {
        var scene = TubeSceneBuilder.Build(Round(new SlitFeature(G(), TubeEnd.End, 0, 4, 60))).Value;
        Bounds3 b = scene.PreviewShapes.Single().Mesh.Bounds;
        Assert.True(b.Min.X <= 940 + 1e-9 && b.Max.X >= 1000);
        Assert.True(b.Max.Y > 30 && b.Min.Y < -30);             // along section direction 0° (Y), through both walls
        Assert.Equal(4, b.Max.Z - b.Min.Z, 9);
    }

    [Fact]
    public void Invalid_feature_parameters_are_skipped_in_scene_not_thrown()
    {
        var scene = TubeSceneBuilder.Build(Round(new HoleFeature(G(), 400, 0, -5))).Value;
        Assert.Single(scene.SkippedFeatures);
        Assert.Empty(scene.PreviewShapes);
    }

    [Fact]
    public void Scene_of_invalid_part_fails_cleanly() =>
        Assert.False(TubeSceneBuilder.Build(TubeModelTests.NewModel(1000, new RoundTubeProfile(10, 9))).IsSuccess);
}

internal static class FeatureTestExtensions
{
    public static RectangularCutoutFeature With(this RectangularCutoutFeature f, double angle) => f with { AngleDeg = angle };
}
