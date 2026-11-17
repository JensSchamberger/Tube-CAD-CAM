using TubeCadCam.Geometry;

namespace TubeCadCam.Tests;

public class TriangulatorTests
{
    [Fact]
    public void Square_gives_two_triangles_with_full_area()
    {
        var t = PolygonTriangulator.Triangulate(Polygon2.Rectangle(2, 2, Vector2.Zero), []);
        Assert.Equal(6, t.Indices.Count);
        Assert.Equal(4, t.Area, 9);
    }

    [Fact]
    public void Concave_polygon_area_is_preserved()
    {
        var u = new Polygon2([new(0, 0), new(10, 0), new(10, 10), new(7, 10), new(7, 3), new(3, 3), new(3, 10), new(0, 10)]);
        var t = PolygonTriangulator.Triangulate(u, []);
        Assert.Equal(u.Area, t.Area, 9);
        Assert.Equal(6, t.Indices.Count / 3);
    }

    [Fact]
    public void Clockwise_input_is_handled()
    {
        var cw = new Polygon2(Polygon2.Rectangle(4, 4, Vector2.Zero).Vertices.Reverse().ToArray());
        Assert.Equal(16, PolygonTriangulator.Triangulate(cw, []).Area, 9);
    }

    [Fact]
    public void Annulus_area_equals_outer_minus_hole()
    {
        var outer = Polygon2.Circle(Vector2.Zero, 30, 96);
        var hole = Polygon2.Circle(Vector2.Zero, 27, 96);
        var t = PolygonTriangulator.Triangulate(outer, [hole]);
        Assert.Equal(outer.Area - hole.Area, t.Area, 6);
        Assert.All(Enumerable.Range(0, t.Indices.Count / 3), i =>
        {
            var a = t.Vertices[t.Indices[3 * i]]; var b = t.Vertices[t.Indices[(3 * i) + 1]]; var c = t.Vertices[t.Indices[(3 * i) + 2]];
            Assert.True((b - a).Cross(c - a) >= -1e-9);
        });
    }

    [Fact]
    public void Hole_with_different_vertex_count_and_two_holes_work()
    {
        var outer = Polygon2.RoundedRectangle(100, 40, 8, Vector2.Zero, 8);
        var h1 = Polygon2.Rectangle(20, 10, new Vector2(-25, 0));
        var h2 = Polygon2.Circle(new Vector2(25, 0), 8, 24);
        var t = PolygonTriangulator.Triangulate(outer, [h1, h2]);
        Assert.Equal(outer.Area - h1.Area - h2.Area, t.Area, 6);
    }

    [Fact]
    public void Self_intersecting_outline_is_not_simple()
    {
        var bowtie = new Polygon2([new(0, 0), new(2, 2), new(2, 0), new(0, 2)]);
        Assert.False(bowtie.IsSimple);
        Assert.True(Polygon2.Rectangle(2, 2, Vector2.Zero).IsSimple);
        Assert.True(Polygon2.Circle(Vector2.Zero, 5, 32).IsSimple);
    }

    [Fact]
    public void Obround_area_matches_formula()
    {
        double l = 30, w = 10;
        var p = Polygon2.Obround(l, w, Vector2.Zero, 128);
        Assert.Equal(((l - w) * w) + (Math.PI * w * w / 4), p.Area, 1);
        Assert.True(p.IsCounterClockwise);
        Assert.Equal(l, p.Bounds.Width, 6);
    }

    [Fact]
    public void Contains_and_rotation_work()
    {
        var r = Polygon2.Rectangle(4, 2, Vector2.Zero);
        Assert.True(r.Contains(new Vector2(1, 0.5)));
        Assert.False(r.Contains(new Vector2(3, 0)));
        Assert.Equal(2, r.Rotated(90).Bounds.Width, 9);
    }
}
