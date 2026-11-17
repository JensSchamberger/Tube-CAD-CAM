using TubeCadCam.Geometry;

namespace TubeCadCam.Tests;

public class GeometryBooleanTests
{
    [Fact]
    public void Difference_with_an_inner_hole_returns_outer_and_hole()
    {
        var sq = Polygon2.Rectangle(10, 10, Vector2.Zero);
        var hole = Polygon2.Rectangle(4, 4, Vector2.Zero);
        PolygonRegion r = Assert.Single(Polygon2Boolean.Difference([sq], [hole]));
        Assert.Equal(100 - 16, r.Area, 1e-9);
        Assert.Single(r.Holes);
        Assert.True(r.Outer.IsCounterClockwise);
        Assert.False(r.Holes[0].IsCounterClockwise);
    }

    [Fact]
    public void Difference_that_cuts_a_notch_into_the_boundary_has_no_hole()
    {
        var sq = Polygon2.Rectangle(10, 10, Vector2.Zero);
        var cut = Polygon2.Rectangle(4, 4, new Vector2(5, 0));
        PolygonRegion r = Assert.Single(Polygon2Boolean.Difference([sq], [cut]));
        Assert.Empty(r.Holes);
        Assert.Equal(100 - 8, r.Area, 1e-9);
    }

    [Fact]
    public void Difference_that_splits_a_polygon_returns_two_regions()
    {
        var sq = Polygon2.Rectangle(10, 10, Vector2.Zero);
        var band = Polygon2.Rectangle(2, 20, Vector2.Zero);
        var regions = Polygon2Boolean.Difference([sq], [band]);
        Assert.Equal(2, regions.Count);
        Assert.Equal(80, regions.Sum(x => x.Area), 1e-9);
    }

    [Fact]
    public void Union_merges_overlaps_and_normalises_orientation()
    {
        var a = Polygon2.Rectangle(4, 4, Vector2.Zero);
        var b = new Polygon2(Polygon2.Rectangle(4, 4, new Vector2(2, 0)).Vertices.Reverse().ToArray());   // clockwise input
        PolygonRegion r = Assert.Single(Polygon2Boolean.Union([a, b]));
        Assert.Equal(24, r.Area, 1e-9);
    }

    [Fact]
    public void Intersection_returns_the_common_area()
    {
        var a = Polygon2.Rectangle(4, 4, Vector2.Zero);
        var b = Polygon2.Rectangle(4, 4, new Vector2(2, 0));
        Assert.Equal(8, Polygon2Boolean.Intersection([a], [b]).Sum(x => x.Area), 1e-9);
    }

    [Fact]
    public void Results_are_snapped_to_the_central_grid()
    {
        var a = new Polygon2([new(0, 0), new(10, 0), new(10, 10), new(0, 10)]);
        var b = Polygon2.Rectangle(100, 3.3333333, new Vector2(0, 5.1234567));
        foreach (Vector2 v in Polygon2Boolean.Intersection([a], [b]).SelectMany(r => r.Outer.Vertices))
        {
            Assert.Equal(Math.Round(v.Y, Tolerance.GridDecimals), v.Y, 1e-12);
        }
    }

    [Fact]
    public void InsertOnEdgePoints_restores_points_on_edges_in_order_and_ignores_others()
    {
        var ring = new Polygon2([new(0, 0), new(10, 0), new(10, 10), new(0, 10)]);
        Polygon2 r = Polygon2Boolean.InsertOnEdgePoints(ring, [new Vector2(7, 0), new Vector2(3, 0), new Vector2(10, 5), new Vector2(5, 5), new Vector2(10, 0)], 1e-6);
        Assert.Equal([new Vector2(0, 0), new Vector2(3, 0), new Vector2(7, 0), new Vector2(10, 0), new Vector2(10, 5), new Vector2(10, 10), new Vector2(0, 10)], r.Vertices);
    }

    // ---------------------------------------------------------------- tolerances

    [Fact]
    public void Central_tolerances_are_consistent()
    {
        Assert.Equal(Tolerance.Linear, Math.Pow(10, -Tolerance.GridDecimals), 1e-15);
        Assert.True(Tolerance.Weld >= 5 * Tolerance.Linear);                       // covers independent roundings of both operands
        Assert.True(Tolerance.MinTriangleArea < Tolerance.Weld * Tolerance.Weld * 10);
    }

    // ---------------------------------------------------------------- topology analysis

    private static (Vector3[] P, int[] I) Tetra() =>
        ([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)], [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3]);

    [Fact]
    public void A_tetrahedron_is_a_valid_solid_with_the_right_volume()
    {
        var (p, i) = Tetra();
        MeshTopologyReport r = MeshTopology.Analyze(p, i);
        Assert.True(r.IsValidSolid, r.ToString());
        Assert.Equal(1.0 / 6, r.Volume, 1e-12);
    }

    [Fact]
    public void Open_flipped_degenerate_and_duplicate_triangles_are_detected()
    {
        var (p, i) = Tetra();
        Assert.Equal(3, MeshTopology.Analyze(p, i[..9]).OpenEdges);                         // one face missing
        int[] flipped = [.. i]; (flipped[0], flipped[1]) = (flipped[1], flipped[0]);
        Assert.True(MeshTopology.Analyze(p, flipped).OrientationConflicts > 0);
        Assert.Equal(1, MeshTopology.Analyze([.. p, new Vector3(2, 0, 0)], [.. i, 1, 4, 1]).DegenerateTriangles);
        Assert.Equal(1, MeshTopology.Analyze(p, [.. i, 0, 2, 1]).DuplicateTriangles);
        Assert.True(MeshTopology.Analyze(p, [.. i, 0, 2, 1]).NonManifoldEdges > 0);
        var collinear = new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(2, 0, 0) };
        Assert.Equal(1, MeshTopology.Analyze(collinear, [0, 1, 2]).DegenerateTriangles);   // zero area but distinct indices
    }

    [Fact]
    public void Weld_merges_close_vertices_deterministically_including_across_cell_borders()
    {
        double w = Tolerance.Weld;
        Vector3[] pts = [new(0, 0, 0), new((w * 0.9), 0, 0), new(w * 1.1 + 5, 0, 0), new((w * 1.1) + 5 + (w * 0.5), 0, 0), new(1, 1, 1)];
        (Vector3[] u, int[] map) = MeshTopology.Weld(pts);
        Assert.Equal([0, 0, 1, 1, 2], map);
        Assert.Equal(3, u.Length);
        Assert.Equal(pts[0], u[0]);                                                          // first occurrence wins
    }

    [Fact]
    public void Crease_smoothing_keeps_hard_edges_hard_and_rounds_soft_ones()
    {
        // two triangles sharing an edge, angle 10° (smooth) vs 90° (hard)
        Vector3[] flat = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0.176)];
        TriangleMesh soft = MeshSmoothing.WithCreaseNormals(flat, [0, 1, 2, 1, 3, 2], 30);
        Assert.True(soft.Normals[1].Dot(soft.Normals[3]) > 0.999);     // vertex 1 shared: averaged
        Vector3[] hard = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 1)];
        TriangleMesh h = MeshSmoothing.WithCreaseNormals(hard, [0, 1, 2, 1, 3, 2], 30);
        Assert.True(h.Normals[1].Dot(h.Normals[3]) < 0.9);
        Assert.Equal(6, h.Indices.Count);
    }

    // ---------------------------------------------------------------- triangulator robustness

    [Fact]
    public void Near_collinear_boundary_points_do_not_produce_flat_triangles_when_a_flat_tolerance_is_given()
    {
        // a C-shaped ring whose inner side carries points that are collinear up to 4e-7 (rounding of a 2D boolean)
        var ring = new Polygon2([
            new(0, 0), new(2, 4e-7), new(4, 0), new(6, 3e-7), new(8, 0), new(8, 3), new(4, 3 + 2e-7), new(0, 3)]);
        Triangulation t = PolygonTriangulator.Triangulate(ring, [], flatTolerance: 4 * Tolerance.Weld);
        Assert.Equal(ring.Area, t.Area, 1e-9);
        for (int i = 0; i < t.Indices.Count; i += 3)
        {
            Vector2 a = t.Vertices[t.Indices[i]], b = t.Vertices[t.Indices[i + 1]], c = t.Vertices[t.Indices[i + 2]];
            double height = Math.Abs((b - a).Cross(c - a)) / Math.Max((c - a).Length, Math.Max((b - a).Length, (c - b).Length));
            Assert.True(height > 1e-4, "flat sliver triangle");
        }
    }

    [Fact]
    public void Quality_improvement_keeps_area_and_boundary_and_yields_a_delaunay_triangulation()
    {
        var outer = Polygon2.Rectangle(100, 20, Vector2.Zero);
        var hole = Polygon2.Circle(new Vector2(10, 0), 5, 48);
        Triangulation plain = PolygonTriangulator.Triangulate(outer, [hole]);
        Triangulation good = PolygonTriangulator.Triangulate(outer, [hole], improveQuality: true);
        Assert.Equal(plain.Area, good.Area, 1e-9);
        Assert.Equal(plain.Indices.Count, good.Indices.Count);

        // boundary edges (used by exactly one triangle) are identical before and after
        static HashSet<(int, int)> Boundary(Triangulation t)
        {
            var count = new Dictionary<(int, int), int>();
            for (int i = 0; i < t.Indices.Count; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = t.Indices[i + k], b = t.Indices[i + ((k + 1) % 3)];
                    var key = a < b ? (a, b) : (b, a);
                    count[key] = count.GetValueOrDefault(key) + 1;
                }
            }

            return count.Where(c => c.Value == 1).Select(c => c.Key).ToHashSet();
        }

        Assert.True(Boundary(plain).SetEquals(Boundary(good)));

        // no interior edge can be improved by a flip (local Delaunay property)
        for (int i = 0; i < good.Indices.Count; i += 3)
        {
            for (int j = i + 3; j < good.Indices.Count; j += 3)
            {
                int[] a = [good.Indices[i], good.Indices[i + 1], good.Indices[i + 2]], b = [good.Indices[j], good.Indices[j + 1], good.Indices[j + 2]];
                int[] shared = a.Intersect(b).ToArray();
                if (shared.Length != 2)
                {
                    continue;
                }

                int oa = a.Single(x => !shared.Contains(x)), ob = b.Single(x => !shared.Contains(x));
                Assert.True(InCircle(good.Vertices, a, good.Vertices[ob]) <= 1e-9, "interior edge violates the Delaunay criterion");
                Assert.True(InCircle(good.Vertices, b, good.Vertices[oa]) <= 1e-9, "interior edge violates the Delaunay criterion");
            }
        }
    }

    private static double InCircle(IReadOnlyList<Vector2> v, int[] tri, Vector2 d)
    {
        Vector2 a = v[tri[0]], b = v[tri[1]], c = v[tri[2]];
        if ((b - a).Cross(c - a) < 0)
        {
            (b, c) = (c, b);
        }

        double adx = a.X - d.X, ady = a.Y - d.Y, bdx = b.X - d.X, bdy = b.Y - d.Y, cdx = c.X - d.X, cdy = c.Y - d.Y;
        double ad = (adx * adx) + (ady * ady), bd = (bdx * bdx) + (bdy * bdy), cd = (cdx * cdx) + (cdy * cdy);
        return (adx * ((bdy * cd) - (bd * cdy))) - (ady * ((bdx * cd) - (bd * cdx))) + (ad * ((bdx * cdy) - (bdy * cdx)));
    }
}
