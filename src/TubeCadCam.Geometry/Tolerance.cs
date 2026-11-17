namespace TubeCadCam.Geometry;

/// <summary>
/// Central numeric tolerances. Every geometric comparison, weld, snap and validity check in the CAD kernel uses these
/// values so that results stay deterministic and consistent. All lengths in millimetres.
/// </summary>
public static class Tolerance
{
    /// <summary>Linear tolerance (also the coordinate grid of 2D boolean operations: 1e-6 mm).</summary>
    public const double Linear = 1e-6;

    /// <summary>Decimal digits of the 2D boolean coordinate grid (<c>10^-GridDecimals == Linear</c>).</summary>
    public const int GridDecimals = 6;

    /// <summary>Vertices closer than this are one vertex in solid meshes (10 × grid, covers independent roundings).</summary>
    public const double Weld = 1e-5;

    /// <summary>Triangles with a smaller area are degenerate (mm²).</summary>
    public const double MinTriangleArea = 1e-10;

    /// <summary>Two unit directions with a dot product above 1 - this are the same direction.</summary>
    public const double SameDirection = 1e-12;

    public static bool AreEqual(double a, double b, double epsilon = Linear) => Math.Abs(a - b) <= epsilon;
}
