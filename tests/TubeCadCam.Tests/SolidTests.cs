using TubeCadCam.Cad;
using TubeCadCam.Cam;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;
using static TubeCadCam.Tests.SolidTestHelpers;

namespace TubeCadCam.Tests;

/// <summary>Real material removal: openings are part of the body mesh, not an overlay.</summary>
public class SolidTests
{
    private static readonly RectangularTubeProfile Rect = new(80, 40, 3, 6);   // faces: +Z (90°) 68 wide, ±Y (0°/180°) 28 high
    private static readonly RoundTubeProfile Round = new(60, 3);

    private static Guid G() => Guid.NewGuid();
    private static TubeModel M(ProfileDefinition p, double length, params TubeFeature[] f) =>
        f.Aggregate(TubeModelTests.NewModel(length, p), (m, x) => m.AddFeature(x));

    private static double Circle128(double d) => Polygon2.Circle(Vector2.Zero, d / 2, FeatureTessellation.CircleSegments).Area;

    // ------------------------------------------------------------------ simple hole

    [Fact]
    public void Hole_in_a_flat_wall_removes_exactly_footprint_area_times_wall_thickness()
    {
        var hole = new HoleFeature(G(), 300, 90, 14);
        TubeModel m = M(Rect, 600, hole);
        TubeSolid s = Solid(m);

        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.Contains(hole.Id, s.AppliedFeatures);
        double expected = Circle128(14) * 3;
        Assert.Equal(expected, BaseVolume(m) - s.Topology.Volume, expected * 1e-6);
    }

    [Fact]
    public void The_opening_is_really_open_and_the_rest_of_the_wall_is_intact()
    {
        TubeSolid s = Solid(M(Rect, 600, new HoleFeature(G(), 300, 90, 14)));
        Assert.False(Inside(s.Mesh, new Vector3(300, 0, 18.5)));       // centre of the opening, mid wall
        Assert.False(Inside(s.Mesh, new Vector3(306, 0, 18.5)));       // inside the opening (r = 7)
        Assert.True(Inside(s.Mesh, new Vector3(308, 0, 18.5)));        // just outside the opening
        Assert.True(Inside(s.Mesh, new Vector3(320, 0, 18.5)));        // wall elsewhere
        Assert.False(Inside(s.Mesh, new Vector3(300, 0, 0)));          // the bore is empty
        Assert.True(Inside(s.Mesh, new Vector3(300, 0, -18.5)));       // opposite wall untouched
    }

    [Fact]
    public void Opening_wall_has_the_wall_thickness_as_height_and_the_outline_as_perimeter()
    {
        var hole = new HoleFeature(G(), 300, 90, 14);
        TubeSolid s = Solid(M(Rect, 600, hole));
        double perimeter = Polygon2.Circle(Vector2.Zero, 7, FeatureTessellation.CircleSegments).Perimeter;
        Assert.Equal(perimeter * 3, s.AreaOf(hole.Id), perimeter * 3 * 1e-6);
        Assert.All(s.TrianglesOf(hole.Id), t => Assert.Equal(SolidFaceKind.FeatureWall, s.TriangleTags[t].Kind));
    }

    [Fact]
    public void Opening_wall_normals_point_into_the_opening()
    {
        var hole = new HoleFeature(G(), 300, 90, 14);
        TubeSolid s = Solid(M(Rect, 600, hole));
        foreach (int t in s.TrianglesOf(hole.Id))
        {
            Vector3 a = s.Mesh.Positions[s.Mesh.Indices[3 * t]], b = s.Mesh.Positions[s.Mesh.Indices[(3 * t) + 1]], c = s.Mesh.Positions[s.Mesh.Indices[(3 * t) + 2]];
            Vector3 n = (b - a).Cross(c - a).Normalized();
            Vector3 centre = (a + b + c) * (1.0 / 3);
            Vector3 towardAxis = new Vector3(300 - centre.X, 0 - centre.Y, 0).Normalized();
            Assert.True(n.Dot(towardAxis) > 0.99, "wall normal must face the opening axis");
        }
    }

    [Fact]
    public void Cut_edges_are_closed_loops_on_both_wall_surfaces_with_the_cam_outline_points()
    {
        var hole = new HoleFeature(G(), 300, 90, 14);
        TubeModel m = M(Rect, 600, hole);
        TubeSolid s = Solid(m);
        Assert.Equal(2, s.Edges.Count);
        FeatureEdge upper = s.Edges.Single(e => e.Side == EdgeSide.Upper), lower = s.Edges.Single(e => e.Side == EdgeSide.Lower);
        Assert.True(upper.IsClosed && lower.IsClosed);
        Assert.Equal(hole.Id, upper.FeatureId);
        Assert.All(upper.Points, p => Assert.Equal(20, p.Z, 1e-6));         // outer surface
        Assert.All(lower.Points, p => Assert.Equal(17, p.Z, 1e-6));         // inner surface (wall 3)
        // CAD edge and CAM contour use the same polygonisation
        Contour contour = new FeatureContourRecognizer().Recognize(m).Contours.Single();
        Assert.Equal(contour.Outline.Vertices.Count, upper.Points.Count);
        Assert.Equal(0, upper.Points.Max(p => Math.Abs(Math.Sqrt(((p.X - 300) * (p.X - 300)) + (p.Y * p.Y)) - 7)), 1e-6);
    }

    [Fact]
    public void Hole_in_a_round_tube_removes_only_the_near_wall()
    {
        TubeModel m = M(Round, 600, new HoleFeature(G(), 300, 90, 14));
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        double removed = BaseVolume(m) - s.Topology.Volume;
        Assert.InRange(removed, Circle128(14) * 3 * 0.99, Circle128(14) * 3 * 1.03);   // curved wall: slightly more than the plate
        Assert.False(Inside(s.Mesh, new Vector3(300, 0, 28.5)));
        Assert.True(Inside(s.Mesh, new Vector3(300, 0, -28.5)));
        Assert.True(Inside(s.Mesh, new Vector3(340, 0, 28.5 - 0.5)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Holes_on_every_side_of_a_round_tube_are_identical_in_volume(double angle)
    {
        TubeModel m = M(Round, 600, new HoleFeature(G(), 300, angle, 10));
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.Equal(236.452, BaseVolume(m) - s.Topology.Volume, 0.5);
    }

    // ------------------------------------------------------------------ all feature types: analytic volumes on flat faces

    [Fact]
    public void Every_wall_cut_type_removes_outline_area_times_wall_thickness()
    {
        var feats = new TubeFeature[]
        {
            new HoleFeature(G(), 100, 90, 12),
            new RectangularCutoutFeature(G(), 200, 90, 12, 25, 2, 15),
            new SlotFeature(G(), 300, 90, 30, 8, 30),
            new ContourCutoutFeature(G(), 400, 90, new Polygon2([new(-8, -5), new(8, -5), new(0, 7)]), 10),
        };
        foreach (TubeFeature f in feats)
        {
            TubeModel m = M(Rect, 600, f);
            TubeSolid s = Solid(m);
            AssertValid(s);
            Assert.Empty(s.Failures);
            double area = ((IWallCut)f).Outline(FeatureTessellation.CircleSegments, FeatureTessellation.CornerSegments).Area;
            Assert.Equal(area * 3, BaseVolume(m) - s.Topology.Volume, area * 3 * 1e-6);
        }
    }

    [Fact]
    public void Notch_removes_its_area_and_notches_the_end_face()
    {
        var notch = new EndNotchFeature(G(), TubeEnd.Start, 90, 12, 25, 3);
        TubeModel m = M(Rect, 600, notch);
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        double area = notch.Outline(FeatureTessellation.CircleSegments, FeatureTessellation.CornerSegments).Area;
        Assert.Equal(area * 3, BaseVolume(m) - s.Topology.Volume, area * 3 * 1e-6);
        // the end face lost the cross-section of the notch: width 12 × wall 3
        double before = EndFaceArea(Solid(m with { Features = [] }), TubeEnd.Start);
        Assert.Equal(12 * 3, before - EndFaceArea(s, TubeEnd.Start), 1e-4);
        Assert.Equal(EndFaceArea(Solid(m with { Features = [] }), TubeEnd.End), EndFaceArea(s, TubeEnd.End), 1e-6);
        // the notch is open towards the end: material between the notch and the face is gone, behind it it is intact
        Assert.False(Inside(s.Mesh, new Vector3(10, 0, 18.5)));
        Assert.True(Inside(s.Mesh, new Vector3(40, 0, 18.5)));
        Assert.True(Inside(s.Mesh, new Vector3(10, 20, 18.5)));
    }

    [Fact]
    public void Slit_cuts_through_both_walls_from_the_end_face()
    {
        var slit = new SlitFeature(G(), TubeEnd.End, 0, 4, 40);   // along ±Y, through the two side walls
        TubeModel m = M(Rect, 600, slit);
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.Equal(4 * 40 * 3 * 2, BaseVolume(m) - s.Topology.Volume, 1e-4);
        Assert.Equal(2 * 4 * 3, EndFaceArea(Solid(m with { Features = [] }), TubeEnd.End) - EndFaceArea(s, TubeEnd.End), 1e-4);
        Assert.False(Inside(s.Mesh, new Vector3(590, 38.5, 0)));
        Assert.False(Inside(s.Mesh, new Vector3(590, -38.5, 0)));
        Assert.True(Inside(s.Mesh, new Vector3(590, 38.5, 5)));       // beside the slit
        Assert.True(Inside(s.Mesh, new Vector3(500, 38.5, 0)));       // behind the slit
        Assert.True(Inside(s.Mesh, new Vector3(590, 0, 18.5)));       // top/bottom walls untouched
        Assert.Equal(4, s.Edges.Count);                                // two walls × upper/lower
    }

    [Fact]
    public void Slit_end_notch_and_wall_cuts_can_share_one_tube()
    {
        TubeModel m = M(Rect, 600,
            new HoleFeature(G(), 200, 90, 12),
            new SlotFeature(G(), 350, 90, 40, 8),
            new EndNotchFeature(G(), TubeEnd.Start, 270, 12, 25, 0),
            new SlitFeature(G(), TubeEnd.End, 0, 4, 40));
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.Equal(4, s.AppliedFeatures.Count);
        Assert.True(BaseVolume(m) - s.Topology.Volume > 960 + 400);
    }

    // ------------------------------------------------------------------ slots on different faces and profile types

    [Theory]
    [InlineData(0, 38.5, 0)]
    [InlineData(90, 0, 18.5)]
    [InlineData(180, -38.5, 0)]
    [InlineData(270, 0, -18.5)]
    public void Slots_on_all_four_faces_of_a_rectangular_tube(double angle, double y, double z)
    {
        var slot = new SlotFeature(G(), 300, angle, 30, 8, 0);
        TubeModel m = M(Rect, 600, slot);
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        double area = slot.Outline(FeatureTessellation.CircleSegments, FeatureTessellation.CornerSegments).Area;
        Assert.Equal(area * 3, BaseVolume(m) - s.Topology.Volume, area * 3 * 1e-6);
        Assert.False(Inside(s.Mesh, new Vector3(300, y, z)));
        Assert.True(Inside(s.Mesh, new Vector3(340, y, z)));
    }

    [Fact]
    public void Slot_on_a_flange_and_on_the_web_of_a_u_profile()
    {
        var u = new UProfile(100, 50, 6, 8);   // flange thickness 8, web thickness 6
        foreach ((double angle, double wall) in new[] { (90.0, 8.0), (180.0, 6.0), (270.0, 8.0) })
        {
            var slot = new SlotFeature(G(), 300, angle, 30, 8, 0);
            TubeModel m = M(u, 600, slot);
            TubeSolid s = Solid(m);
            AssertValid(s);
            Assert.Empty(s.Failures);
            double area = slot.Outline(FeatureTessellation.CircleSegments, FeatureTessellation.CornerSegments).Area;
            Assert.Equal(area * wall, BaseVolume(m) - s.Topology.Volume, area * wall * 1e-6);
        }
    }

    [Fact]
    public void Holes_in_the_legs_of_an_angle_profile()
    {
        var l = new LProfile(50, 50, 5);
        foreach (double angle in new[] { 180.0, 270.0 })
        {
            TubeModel m = M(l, 400, new HoleFeature(G(), 200, angle, 10));
            TubeSolid s = Solid(m);
            AssertValid(s);
            Assert.Empty(s.Failures);
            Assert.Equal(Circle128(10) * 5, BaseVolume(m) - s.Topology.Volume, Circle128(10) * 5 * 1e-6);
        }
    }

    [Fact]
    public void Hole_in_the_web_and_in_a_flange_overhang_of_an_i_profile()
    {
        var i = new IProfile(100, 55, 4, 6);
        TubeModel web = M(i, 600, new HoleFeature(G(), 300, 180, 10));
        TubeSolid sw = Solid(web);
        AssertValid(sw);
        Assert.Empty(sw.Failures);
        Assert.Equal(Circle128(10) * 4, BaseVolume(web) - sw.Topology.Volume, Circle128(10) * 4 * 1e-6);

        // flange overhang: the ray hits the top flange at y = 20 (beside the web), flange thickness 6
        TubeModel flange = M(i, 600, new HoleFeature(G(), 300, Math.Atan2(50, 20) * 180 / Math.PI, 10));
        TubeSolid sf = Solid(flange);
        AssertValid(sf);
        Assert.Empty(sf.Failures);
        Assert.Equal(Circle128(10) * 6, BaseVolume(flange) - sf.Topology.Volume, Circle128(10) * 6 * 1e-6);
    }

    [Fact]
    public void Hole_in_a_custom_profile_with_an_inner_void()
    {
        var p = new CustomProfile("c", Polygon2.Rectangle(40, 20, Vector2.Zero), [Polygon2.Circle(Vector2.Zero, 6, 32)]);
        TubeModel m = M(p, 400, new HoleFeature(G(), 200, 90, 6));
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.True(BaseVolume(m) - s.Topology.Volume > 0);
    }

    // ------------------------------------------------------------------ profile edges

    [Fact]
    public void Cutout_across_the_transition_from_flat_face_to_rounded_edge_is_computed()
    {
        // hole of d = 8 whose centre lies at y ≈ 32: the footprint crosses the start of the corner radius (y = 34)
        var hole = new HoleFeature(G(), 300, 32, 8);
        TubeModel m = M(Rect, 600, hole);
        Assert.DoesNotContain(m.Validate(), i => i.Severity == IssueSeverity.Error);
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.InRange(BaseVolume(m) - s.Topology.Volume, Circle128(8) * 3 * 0.98, Circle128(8) * 3 * 1.03);
    }

    [Fact]
    public void Cut_over_a_sharp_edge_fails_explicitly_and_leaves_the_body_untouched()
    {
        var sharp = new RectangularTubeProfile(80, 40, 3, 0);
        var cutout = new RectangularCutoutFeature(G(), 300, 20, 30, 20);
        TubeModel m = M(sharp, 600, cutout);
        TubeScene scene = TubeSceneBuilder.Build(m).Value;
        FeatureFailure f = Assert.Single(scene.Failures);
        Assert.Equal(cutout.Id, f.FeatureId);
        Assert.StartsWith("solid.", f.Code);
        Assert.DoesNotContain(cutout.Id, scene.Solid.AppliedFeatures);
        AssertValid(scene.Solid);
        Assert.Equal(BaseVolume(m), scene.Solid.Topology.Volume, 1e-9);        // nothing was removed, no substitute geometry
        Assert.Empty(scene.Solid.TrianglesOf(cutout.Id));
        Assert.Contains(scene.PreviewShapes, p => p.FeatureId == cutout.Id);    // the preview is still available for editing
    }

    [Fact]
    public void Cut_reaching_into_a_region_with_a_different_wall_layer_fails_explicitly()
    {
        // cutout on the web of an I profile that is taller than the web (88 mm): it reaches the flange junctions
        var i = new IProfile(100, 55, 4, 6);
        var cut = new RectangularCutoutFeature(G(), 300, 180, 95, 20);
        TubeScene scene = TubeSceneBuilder.Build(M(i, 600, cut)).Value;
        Assert.Single(scene.Failures);
        Assert.Equal(BaseVolume(M(i, 600)), scene.Solid.Topology.Volume, 1e-9);
    }

    // ------------------------------------------------------------------ overlapping features

    [Fact]
    public void Overlapping_holes_with_the_same_cutting_direction_are_united()
    {
        var a = new HoleFeature(G(), 300, 90, 12);
        var b = new HoleFeature(G(), 306, 90, 12);
        TubeModel m = M(Rect, 600, a, b);
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Polygon2 pa = a.Outline(128, 12).Translated(new Vector2(300, 0)), pb = b.Outline(128, 12).Translated(new Vector2(306, 0));
        double union = Polygon2Boolean.Union([pa, pb]).Sum(r => r.Area);
        Assert.True(union < pa.Area + pb.Area);
        Assert.Equal(union * 3, BaseVolume(m) - s.Topology.Volume, union * 3 * 1e-5);
        Assert.True(s.AreaOf(a.Id) > 0 && s.AreaOf(b.Id) > 0);                 // both features own part of the boundary
        Assert.False(Inside(s.Mesh, new Vector3(303, 0, 18.5)));               // between the centres
    }

    [Fact]
    public void Holes_on_the_same_face_at_different_angles_share_the_cutting_direction()
    {
        TubeModel m = M(Rect, 600, new HoleFeature(G(), 300, 80, 10), new HoleFeature(G(), 303, 100, 10));
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.Equal(2, s.AppliedFeatures.Count);
    }

    [Fact]
    public void A_hole_inside_a_slot_changes_nothing_but_stays_applied()
    {
        var slot = new SlotFeature(G(), 300, 90, 60, 20);
        var hole = new HoleFeature(G(), 300, 90, 8);
        TubeModel only = M(Rect, 600, slot), both = M(Rect, 600, slot, hole);
        TubeSolid s = Solid(both);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.Equal(Solid(only).Topology.Volume, s.Topology.Volume, 1e-6);
    }

    [Fact]
    public void Overlap_with_a_different_cutting_direction_fails_the_later_feature_explicitly()
    {
        var first = new HoleFeature(G(), 300, 85, 8);
        var second = new HoleFeature(G(), 302, 95, 8);
        TubeModel m = M(Round, 600, first, second);
        TubeScene scene = TubeSceneBuilder.Build(m).Value;
        FeatureFailure f = Assert.Single(scene.Failures);
        Assert.Equal(second.Id, f.FeatureId);
        Assert.Equal("solid.interaction", f.Code);
        AssertValid(scene.Solid);
        Assert.Equal(Solid(M(Round, 600, first)).Topology.Volume, scene.Solid.Topology.Volume, 1e-6);
    }

    [Fact]
    public void Holes_on_the_same_wall_far_enough_apart_do_not_interact_on_a_round_tube()
    {
        TubeModel m = M(Round, 600, new HoleFeature(G(), 300, 60, 8), new HoleFeature(G(), 302, 120, 8));
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.Equal(2, s.AppliedFeatures.Count);
    }

    [Fact]
    public void Many_features_on_one_face_are_computed_together()
    {
        var feats = Enumerable.Range(1, 8).Select(i => (TubeFeature)new HoleFeature(G(), (100 * i) - 40, 90, 12)).ToArray();
        TubeModel m = M(Rect, 1000, feats);
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.Equal(8 * Circle128(12) * 3, BaseVolume(m) - s.Topology.Volume, 8 * Circle128(12) * 3 * 1e-6);
    }

    // ------------------------------------------------------------------ close to the end faces

    [Fact]
    public void Hole_close_to_the_end_face_keeps_a_thin_web()
    {
        var hole = new HoleFeature(G(), 5.5, 90, 10);            // 0.5 mm between hole and end face
        TubeModel m = M(Rect, 600, hole);
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.Equal(Circle128(10) * 3, BaseVolume(m) - s.Topology.Volume, Circle128(10) * 3 * 1e-6);
        Assert.True(Inside(s.Mesh, new Vector3(0.25, 0, 18.5)));     // the web is there
        Assert.False(Inside(s.Mesh, new Vector3(5.5, 0, 18.5)));
    }

    [Theory]
    [InlineData(5.0)]       // touching the face
    [InlineData(5.0005)]    // 0.5 µm: thinner than the tolerance allows
    public void Hole_closer_to_the_end_face_than_the_tolerance_is_rejected_explicitly(double position)
    {
        var hole = new HoleFeature(G(), position, 90, 10);
        TubeScene scene = TubeSceneBuilder.Build(M(Rect, 600, hole)).Value;
        FeatureFailure f = Assert.Single(scene.Failures);
        Assert.Equal("solid.too-close-to-end", f.Code);
        AssertValid(scene.Solid);
    }

    [Fact]
    public void Hole_close_to_the_far_end_of_a_round_tube()
    {
        TubeSolid s = Solid(M(Round, 600, new HoleFeature(G(), 594.2, 90, 10)));
        AssertValid(s);
        Assert.Empty(s.Failures);
    }

    [Fact]
    public void Hole_right_behind_a_notch_is_computed_next_to_it()
    {
        TubeModel m = M(Rect, 600, new EndNotchFeature(G(), TubeEnd.Start, 90, 12, 25, 0), new HoleFeature(G(), 31, 90, 10));
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
    }

    // ------------------------------------------------------------------ miters

    [Fact]
    public void Miter_with_hole_and_slot_removes_exactly_the_cutouts_from_the_mitred_body()
    {
        var hole = new HoleFeature(G(), 200, 90, 12);
        var slot = new SlotFeature(G(), 350, 90, 40, 8);
        TubeModel miterOnly = M(Rect, 600, new EndMiterFeature(G(), TubeEnd.Start, 30, 90), new EndMiterFeature(G(), TubeEnd.End, -30, 90));
        TubeModel full = miterOnly.AddFeature(hole).AddFeature(slot);
        Assert.DoesNotContain(full.Validate(), i => i.Severity == IssueSeverity.Error);
        TubeSolid s = Solid(full);
        AssertValid(s);
        Assert.Empty(s.Failures);
        double area = Circle128(12) + slot.Outline(128, 12).Area;
        Assert.Equal(area * 3, Solid(miterOnly).Topology.Volume - s.Topology.Volume, area * 3 * 1e-6);
    }

    [Fact]
    public void Miter_on_a_round_tube_with_cutouts_on_both_sides()
    {
        TubeModel m = M(Round, 600,
            new EndMiterFeature(G(), TubeEnd.Start, 30, 90), new EndMiterFeature(G(), TubeEnd.End, -30, 90),
            new SlotFeature(G(), 300, 90, 40, 8), new RectangularCutoutFeature(G(), 300, 270, 10, 20));
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
        Assert.Equal(2, s.AppliedFeatures.Count);
    }

    [Fact]
    public void Slit_or_notch_reaching_a_mitred_end_face_is_reported_as_unsupported()
    {
        var miter = new EndMiterFeature(G(), TubeEnd.Start, 30, 90);
        var notch = new EndNotchFeature(G(), TubeEnd.Start, 90, 10, 20, 0);
        var slit = new SlitFeature(G(), TubeEnd.Start, 0, 4, 40);
        TubeScene scene = TubeSceneBuilder.Build(M(Rect, 600, miter, notch, slit)).Value;
        Assert.Equal(2, scene.Failures.Count);
        Assert.All(scene.Failures, f => Assert.Equal("solid.end-unsupported", f.Code));
        AssertValid(scene.Solid);
        Assert.Equal(Solid(M(Rect, 600, miter)).Topology.Volume, scene.Solid.Topology.Volume, 1e-6);
    }

    [Fact]
    public void Slit_at_the_other_end_of_a_mitred_tube_is_fine()
    {
        TubeModel m = M(Rect, 600, new EndMiterFeature(G(), TubeEnd.Start, 30, 90), new SlitFeature(G(), TubeEnd.End, 0, 4, 40));
        TubeSolid s = Solid(m);
        AssertValid(s);
        Assert.Empty(s.Failures);
    }

    // ------------------------------------------------------------------ topology across the whole matrix

    public static TheoryData<string> ProfileNames => new() { "round", "rect", "sharp", "square", "u", "l", "i", "custom" };

    private static ProfileDefinition ProfileOf(string n) => n switch
    {
        "round" => new RoundTubeProfile(60, 3),
        "rect" => new RectangularTubeProfile(80, 40, 3, 6),
        "sharp" => new RectangularTubeProfile(80, 40, 3, 0),
        "square" => RectangularTubeProfile.Square(50, 3, 4),
        "u" => new UProfile(100, 50, 6, 8),
        "l" => new LProfile(50, 50, 5),
        "i" => new IProfile(100, 55, 4, 6),
        _ => new CustomProfile("c", Polygon2.Rectangle(40, 20, Vector2.Zero), [Polygon2.Circle(Vector2.Zero, 6, 32)]),
    };

    private static readonly string[] KnownFailureCodes =
    [
        "solid.profile-edge", "solid.pairing", "solid.interaction", "solid.end-unsupported", "solid.too-close-to-end",
        "solid.no-surface", "solid.seam-too-close", "solid.topology", "solid.invalid-parameters",
    ];

    private static IEnumerable<TubeFeature> AllKinds() =>
    [
        new HoleFeature(G(), 300, 90, 10), new HoleFeature(G(), 300, 180, 10), new HoleFeature(G(), 300, 270, 10),
        new RectangularCutoutFeature(G(), 300, 90, 12, 25, 2, 15), new SlotFeature(G(), 300, 90, 30, 8, 30),
        new ContourCutoutFeature(G(), 300, 90, new Polygon2([new(-8, -5), new(8, -5), new(0, 7)]), 10),
        new EndNotchFeature(G(), TubeEnd.Start, 90, 12, 25, 3), new EndNotchFeature(G(), TubeEnd.End, 270, 12, 25, 0),
        new SlitFeature(G(), TubeEnd.End, 0, 4, 40), new SlitFeature(G(), TubeEnd.Start, 90, 4, 40),
    ];

    [Theory]
    [MemberData(nameof(ProfileNames))]
    public void Every_feature_on_every_profile_either_yields_a_valid_solid_or_fails_with_a_named_reason(string profile)
    {
        int applied = 0, failed = 0;
        foreach (TubeFeature f in AllKinds())
        {
            TubeModel m = M(ProfileOf(profile), 600, f);
            TubeScene scene = TubeSceneBuilder.Build(m).Value;
            AssertValid(scene.Solid);                                    // the delivered body is ALWAYS a valid closed solid
            double removed = BaseVolume(m) - scene.Solid.Topology.Volume;
            if (scene.Failures.Count == 0 && scene.SkippedFeatures.Count == 0)
            {
                applied++;
                Assert.Contains(f.Id, scene.Solid.AppliedFeatures);
                Assert.True(removed > 0, $"{profile}/{f.GetType().Name} removed no material");
            }
            else
            {
                failed++;
                Assert.Equal(0, removed, 1e-9);                          // a failed feature changes nothing (no substitute)
                Assert.All(scene.Failures, x => Assert.Contains(x.Code, KnownFailureCodes));
            }
        }

        Assert.True(applied > 0, "at least some features must be computable on every profile");
        Assert.Equal(AllKinds().Count(), applied + failed);
    }

    // ------------------------------------------------------------------ provenance / CAM usability

    [Fact]
    public void Every_triangle_has_provenance_and_walls_belong_only_to_applied_features()
    {
        var hole = new HoleFeature(G(), 200, 90, 12);
        var slot = new SlotFeature(G(), 350, 90, 40, 8);
        var notch = new EndNotchFeature(G(), TubeEnd.Start, 270, 12, 25, 3);
        TubeSolid s = Solid(M(Rect, 600, hole, slot, notch));
        Assert.Equal(s.Mesh.TriangleCount, s.TriangleTags.Count);
        Assert.Contains(s.TriangleTags, t => t.Kind == SolidFaceKind.OuterSurface);
        Assert.Contains(s.TriangleTags, t => t.Kind == SolidFaceKind.InnerSurface);
        Assert.Contains(s.TriangleTags, t => t.Kind == SolidFaceKind.EndFace && t.End == TubeEnd.Start);
        Assert.Contains(s.TriangleTags, t => t.Kind == SolidFaceKind.EndFace && t.End == TubeEnd.End);
        foreach (FaceTag t in s.TriangleTags.Where(t => t.Kind == SolidFaceKind.FeatureWall))
        {
            Assert.Contains(t.FeatureId!.Value, new[] { hole.Id, slot.Id, notch.Id });
        }

        Assert.All(new[] { hole.Id, slot.Id, notch.Id }, id => Assert.True(s.AreaOf(id) > 0));
        Assert.All(s.Edges, e => Assert.Contains(e.FeatureId, new[] { hole.Id, slot.Id, notch.Id }));
        Assert.Equal(6, s.Edges.Count);                                  // upper + lower edge per feature
    }

    [Fact]
    public void Walls_meet_the_surfaces_exactly_on_the_cut_edges()
    {
        var hole = new HoleFeature(G(), 300, 90, 14);
        TubeSolid s = Solid(M(Rect, 600, hole));
        // every wall vertex lies on one of the two cut edges
        var edgePoints = s.Edges.SelectMany(e => e.Points).ToList();
        foreach (int t in s.TrianglesOf(hole.Id))
        {
            for (int k = 0; k < 3; k++)
            {
                Vector3 v = s.Mesh.Positions[s.Mesh.Indices[(3 * t) + k]];
                Assert.Contains(edgePoints, p => (p - v).Length < 1e-4);
            }
        }
    }

    // ------------------------------------------------------------------ determinism and parametric derivation

    [Fact]
    public void Solid_generation_is_deterministic()
    {
        var ids = Enumerable.Range(1, 5).Select(i => Guid.Parse($"00000000-0000-0000-0000-00000000000{i}")).ToArray();
        TubeModel m = M(Round, 800,
            new HoleFeature(ids[0], 300, 90, 12), new SlotFeature(ids[1], 450, 200, 30, 8, 20),
            new EndNotchFeature(ids[2], TubeEnd.Start, 270, 12, 25, 3), new SlitFeature(ids[3], TubeEnd.End, 0, 4, 40),
            new EndMiterFeature(ids[4], TubeEnd.End, 20, 90));
        TubeSolid a = Solid(m), b = Solid(m);
        Assert.Equal(a.Mesh.Positions, b.Mesh.Positions);
        Assert.Equal(a.Mesh.Indices, b.Mesh.Indices);
        Assert.Equal(a.Mesh.Normals, b.Mesh.Normals);
        Assert.Equal(a.TriangleTags, b.TriangleTags);
        Assert.Equal(a.Edges.Select(e => e.Points), b.Edges.Select(e => e.Points));
    }

    [Fact]
    public void Removing_a_feature_returns_exactly_to_the_body_without_it()
    {
        var hole = new HoleFeature(G(), 300, 90, 12);
        TubeModel with = M(Rect, 600, hole);
        TubeSolid a = Solid(with.RemoveFeature(hole.Id)), b = Solid(M(Rect, 600));
        Assert.Equal(b.Mesh.Positions, a.Mesh.Positions);
        Assert.Equal(b.Topology.Volume, a.Topology.Volume);
    }

    [Theory]
    [InlineData(8.0)]
    [InlineData(12.0)]
    [InlineData(20.0)]
    public void Changing_the_diameter_recomputes_the_opening(double diameter)
    {
        var hole = new HoleFeature(G(), 300, 90, 10);
        TubeModel m = M(Rect, 600, hole);
        TubeModel changed = m.ReplaceFeature(hole with { DiameterMm = diameter });
        Assert.Equal(Circle128(diameter) * 3, BaseVolume(changed) - Solid(changed).Topology.Volume, Circle128(diameter) * 3 * 1e-6);
    }

    [Fact]
    public void Changing_position_wall_thickness_and_length_moves_and_resizes_the_solid()
    {
        var hole = new HoleFeature(G(), 300, 90, 12);
        TubeModel m = M(Rect, 600, hole);
        TubeSolid s = Solid(m);

        TubeModel moved = m.ReplaceFeature(hole with { AxialPositionMm = 450 });
        TubeSolid sm = Solid(moved);
        AssertValid(sm);
        Assert.False(Inside(sm.Mesh, new Vector3(450, 0, 18.5)));
        Assert.True(Inside(sm.Mesh, new Vector3(300, 0, 18.5)));
        Assert.Equal(s.Topology.Volume, sm.Topology.Volume, 1e-6);

        TubeModel thicker = m with { Part = m.Part with { Profile = new RectangularTubeProfile(80, 40, 5, 6) } };
        TubeSolid st = Solid(thicker);
        AssertValid(st);
        Assert.Equal(Circle128(12) * 5, BaseVolume(thicker) - st.Topology.Volume, Circle128(12) * 5 * 1e-6);

        TubeModel longer = m with { Part = m.Part with { LengthMm = 900 } };
        TubeSolid sl = Solid(longer);
        AssertValid(sl);
        Assert.Equal(900, sl.Mesh.Bounds.Max.X - sl.Mesh.Bounds.Min.X, 1e-9);
        Assert.Equal(s.Topology.Volume + (ProfileSectionBuilder.Build(Rect).Area * 300), sl.Topology.Volume, 1e-3);
    }

    // ------------------------------------------------------------------ explicit failure reporting in the scene

    [Fact]
    public void A_failed_feature_is_not_in_the_body_but_reported_and_still_previewable()
    {
        var good = new HoleFeature(G(), 200, 90, 12);
        var bad = new RectangularCutoutFeature(G(), 300, 20, 30, 20);
        TubeModel m = M(new RectangularTubeProfile(80, 40, 3, 0), 600, good, bad);
        TubeScene scene = TubeSceneBuilder.Build(m).Value;
        Assert.Equal([bad.Id], scene.Failures.Select(f => f.FeatureId));
        Assert.Equal([good.Id], scene.Solid.AppliedFeatures);
        Assert.Equal(Circle128(12) * 3, BaseVolume(m) - scene.Solid.Topology.Volume, Circle128(12) * 3 * 1e-6);
        Assert.Equal(2, scene.PreviewShapes.Count);
    }

    [Fact]
    public void Features_without_a_surface_are_failed_not_skipped_silently()
    {
        var u = new UProfile(100, 50, 6, 8);
        var hole = new HoleFeature(G(), 250, 0, 10);                    // towards the opening of the U: misses the profile
        TubeScene scene = TubeSceneBuilder.Build(M(u, 500, hole)).Value;
        Assert.Contains(scene.Failures, f => f.FeatureId == hole.Id && f.Code == "solid.no-surface");
        Assert.Contains(hole.Id, scene.SkippedFeatures);
    }

    [Fact]
    public void Invalid_feature_parameters_are_reported_as_failures()
    {
        var hole = new HoleFeature(G(), 300, 90, -5);
        TubeScene scene = TubeSceneBuilder.Build(M(Rect, 600, hole)).Value;
        Assert.Contains(scene.Failures, f => f.FeatureId == hole.Id && f.Code == "solid.invalid-parameters");
    }
}

/// <summary>Independent geometric check: random points are classified by ray casting against the mesh and by a direct membership test.</summary>
public class SolidOracleTests
{
    private static Guid G() => Guid.NewGuid();

    /// <summary>
    /// Direct definition of the machined tube (no use of the solver): inside the section, between the (mitred) end planes, and
    /// not in a cutting prism. Prisms are defined from the public feature outlines for the two supported cutting directions.
    /// </summary>
    private static bool OracleInside(TubeModel m, ProfileSection section, Vector3 p)
    {
        Vector2 sp = new(p.Y, p.Z);
        bool inSection = section.Outer.Contains(sp) && !section.Voids.Any(v => v.Contains(sp));
        if (!inSection)
        {
            return false;
        }

        (EndPlane s, EndPlane e) = EndPlane.For(m, section.Outer);
        if (p.X < s.XAt(sp) || p.X > e.XAt(sp))
        {
            return false;
        }

        foreach (TubeFeature f in m.Features)
        {
            switch (f)
            {
                case IWallCut c when c.AngleDeg == 90:       // cutting direction +Z, t = -y, top wall (z > 0)
                    if (p.Z > 0 && Footprint(c, m).Contains(new Vector2(p.X, -p.Y))) { return false; }
                    break;
                case IWallCut c when c.AngleDeg == 0:        // cutting direction +Y, t = z, right wall (y > 0)
                    if (p.Y > 0 && Footprint(c, m).Contains(new Vector2(p.X, p.Z))) { return false; }
                    break;
                case SlitFeature slit when slit.AngleDeg == 270:   // along ±Z through both walls: t = y (T = rot90(N), N = -Z)
                    double x0 = slit.End == TubeEnd.Start ? 0 : m.Part.LengthMm - slit.DepthMm;
                    double x1 = slit.End == TubeEnd.Start ? slit.DepthMm : m.Part.LengthMm;
                    if (p.X >= x0 && p.X <= x1 && Math.Abs(p.Y) <= slit.WidthMm / 2) { return false; }
                    break;
            }
        }

        return true;
    }

    private static Polygon2 Footprint(IWallCut c, TubeModel m) =>
        c.Outline(FeatureTessellation.CircleSegments, FeatureTessellation.CornerSegments).Translated(new Vector2(c.AxialCenter(m.Part.LengthMm), 0));

    private static void CheckAgainstOracle(TubeModel m, int samples, int seed)
    {
        TubeScene scene = TubeSceneBuilder.Build(m).Value;
        Assert.Empty(scene.Failures);
        TriangleMesh mesh = scene.Body;
        Bounds3 b = mesh.Bounds;
        var rnd = new Random(seed);
        double[] d = [0.03, -0.03];
        int compared = 0, mismatches = 0;
        for (int i = 0; i < samples; i++)
        {
            var p = new Vector3(
                b.Min.X + (rnd.NextDouble() * (b.Max.X - b.Min.X)),
                b.Min.Y + (rnd.NextDouble() * (b.Max.Y - b.Min.Y)),
                b.Min.Z + (rnd.NextDouble() * (b.Max.Z - b.Min.Z)));
            // skip points within 0.03 mm of any boundary of the oracle (the two models may differ by tessellation tolerance there)
            bool oracle = OracleInside(m, scene.Section, p);
            bool stable = d.All(dx => OracleInside(m, scene.Section, p + new Vector3(dx, 0, 0)) == oracle)
                && d.All(dy => OracleInside(m, scene.Section, p + new Vector3(0, dy, 0)) == oracle)
                && d.All(dz => OracleInside(m, scene.Section, p + new Vector3(0, 0, dz)) == oracle);
            if (!stable)
            {
                continue;
            }

            compared++;
            if (SolidTestHelpers.Inside(mesh, p) != oracle)
            {
                mismatches++;
            }
        }

        Assert.True(compared > samples / 3, $"too few stable samples ({compared})");
        Assert.Equal(0, mismatches);
    }

    [Fact]
    public void Machined_rectangular_tube_matches_the_independent_membership_test()
    {
        var m = TubeModelTests.NewModel(400, new RectangularTubeProfile(80, 40, 3, 6))
            .AddFeature(new HoleFeature(G(), 80, 90, 14))
            .AddFeature(new SlotFeature(G(), 160, 90, 50, 12, 0))
            .AddFeature(new ContourCutoutFeature(G(), 250, 90, new Polygon2([new(-12, -8), new(12, -8), new(0, 14)]), 0))
            .AddFeature(new RectangularCutoutFeature(G(), 160, 0, 14, 40, 3, 0))
            .AddFeature(new EndNotchFeature(G(), TubeEnd.Start, 90, 30, 30, 4))
            .AddFeature(new EndMiterFeature(G(), TubeEnd.End, 25, 90));
        CheckAgainstOracle(m, 6000, 1);
    }

    [Fact]
    public void A_slit_that_shares_a_wall_with_earlier_cuts_is_failed_explicitly_not_merged()
    {
        var hole = new HoleFeature(G(), 100, 90, 16);
        var slot = new SlotFeature(G(), 200, 90, 40, 10, 0);
        var slit = new SlitFeature(G(), TubeEnd.Start, 270, 5, 40);     // cuts along Z through the top wall that hole and slot use
        TubeScene scene = TubeSceneBuilder.Build(TubeModelTests.NewModel(300, new RoundTubeProfile(60, 3))
            .AddFeature(hole).AddFeature(slot).AddFeature(slit)).Value;
        FeatureFailure f = Assert.Single(scene.Failures);
        Assert.Equal(slit.Id, f.FeatureId);
        Assert.Equal("solid.interaction", f.Code);
        Assert.Equal([hole.Id, slot.Id], scene.Solid.AppliedFeatures.OrderBy(i => i == hole.Id ? 0 : 1));
        SolidTestHelpers.AssertValid(scene.Solid);
    }

    [Fact]
    public void Machined_tube_with_slit_and_side_cutouts_matches_the_independent_membership_test()
    {
        // slit through top and bottom wall (angle 270) at the start, side cutout at the right wall (angle 0): disjoint walls
        var m = TubeModelTests.NewModel(300, new RectangularTubeProfile(80, 40, 3, 6))
            .AddFeature(new SlitFeature(G(), TubeEnd.Start, 270, 5, 40))
            .AddFeature(new RectangularCutoutFeature(G(), 200, 0, 14, 40, 3, 0))
            .AddFeature(new EndMiterFeature(G(), TubeEnd.End, 20, 90));
        CheckAgainstOracle(m, 5000, 7);
    }
}
