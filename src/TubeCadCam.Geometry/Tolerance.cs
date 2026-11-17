namespace TubeCadCam.Geometry;

/// <summary>Central numeric tolerances so all comparisons are deterministic and consistent.</summary>
public static class Tolerance
{
    /// <summary>Linear tolerance in millimetres.</summary>
    public const double Linear = 1e-6;

    public static bool AreEqual(double a, double b, double epsilon = Linear) => Math.Abs(a - b) <= epsilon;
}
