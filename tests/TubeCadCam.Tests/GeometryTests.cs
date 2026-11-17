using TubeCadCam.Geometry;

namespace TubeCadCam.Tests;

public class GeometryTests
{
    [Fact]
    public void Rectangle_has_expected_area_perimeter_and_is_ccw()
    {
        var r = Polygon2.Rectangle(10, 4, Vector2.Zero);
        Assert.Equal(40, r.Area, 9);
        Assert.Equal(28, r.Perimeter, 9);
        Assert.True(r.IsCounterClockwise);
        Assert.Equal(10, r.Bounds.Width, 9);
    }

    [Fact]
    public void Circle_area_converges_to_pi_r_squared()
    {
        var c = Polygon2.Circle(Vector2.Zero, 10, 720);
        Assert.Equal(Math.PI * 100, c.Area, 1);
        Assert.True(c.Area < Math.PI * 100); // inscribed
    }

    [Fact]
    public void RoundedRectangle_area_matches_formula()
    {
        double w = 80, h = 40, r = 6;
        var p = Polygon2.RoundedRectangle(w, h, r, Vector2.Zero, 64);
        double expected = (w * h) - ((4 - Math.PI) * r * r);
        Assert.Equal(expected, p.Area, 1);
        Assert.True(p.IsCounterClockwise);
    }

    [Fact]
    public void Polygon_rejects_fewer_than_three_vertices() =>
        Assert.Throws<ArgumentException>(() => new Polygon2([new Vector2(0, 0), new Vector2(1, 1)]));

    [Fact]
    public void Vector_operations_are_correct()
    {
        Assert.Equal(1, new Vector2(1, 0).Cross(new Vector2(0, 1)));
        Assert.Equal(new Vector3(0, 0, 1), new Vector3(1, 0, 0).Cross(new Vector3(0, 1, 0)));
        Assert.Equal(5, new Vector2(3, 4).Length);
    }
}
