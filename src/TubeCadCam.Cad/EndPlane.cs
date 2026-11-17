using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

/// <summary>End face plane x(p) = X0 + Gradient·p over section points p. Perpendicular end: Gradient = 0.</summary>
public readonly record struct EndPlane(double X0, Vector2 Gradient)
{
    public double XAt(Vector2 p) => X0 + Gradient.Dot(p);

    public static EndPlane Perpendicular(double x) => new(x, Vector2.Zero);

    /// <summary>Plane for a miter at the given end of a section (see <see cref="EndMiterFeature"/>).</summary>
    public static EndPlane ForMiter(EndMiterFeature miter, Polygon2 outer, double lengthMm)
    {
        double t = Math.Abs(Math.Tan(miter.TiltAngleDeg * Math.PI / 180.0));
        Vector2 axis = TubeCoordinateSystem.Direction(miter.TiltAxisAngleDeg);
        Vector2 b = new Vector2(-axis.Y, axis.X) * Math.Sign(miter.TiltAngleDeg);
        double min = outer.Vertices.Min(p => p.Dot(b));
        return miter.End == TubeEnd.Start
            ? new EndPlane(-t * min, b * t)
            : new EndPlane(lengthMm + (t * min), b * (-t));
    }

    public static (EndPlane Start, EndPlane End) For(TubeModel model, Polygon2 outer)
    {
        double len = model.Part.LengthMm;
        EndPlane start = Perpendicular(0), end = Perpendicular(len);
        foreach (EndMiterFeature m in model.Features.OfType<EndMiterFeature>())
        {
            if (m.End == TubeEnd.Start) { start = ForMiter(m, outer, len); }
            else { end = ForMiter(m, outer, len); }
        }

        return (start, end);
    }
}
