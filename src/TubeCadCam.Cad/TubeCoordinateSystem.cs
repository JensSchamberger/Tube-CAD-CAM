using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

/// <summary>
/// Documented CAD coordinate system (see docs/Koordinatensystem.md). Purely geometric; it contains no machine convention -
/// mapping to a machine's axes is the job of machine adapters in the CAM layer.
/// <list type="bullet">
/// <item>World X = tube axis. The start face lies at x = 0, the (envelope) end at x = LengthMm.</item>
/// <item>The cross-section lives in the world Y/Z plane: section point (u, v) maps to world (y, z) = (u, v).</item>
/// <item>Angles are right-handed about +X, 0° = +Y, 90° = +Z. Angular range [0°, 360°).</item>
/// <item>Standard profiles are centred on the centre of their outer bounding box; custom profiles are used as drawn.</item>
/// <item>Local feature frame: u = along +X, v = tangential (direction of increasing angle), n = outward surface normal; (u, v, n) is right-handed.</item>
/// </list>
/// </summary>
public static class TubeCoordinateSystem
{
    public static Vector3 SectionToWorld(double x, Vector2 section) => new(x, section.X, section.Y);

    /// <summary>Unit direction in the section plane for an angle in degrees (0° = +Y, counter-clockwise towards +Z).</summary>
    public static Vector2 Direction(double angleDeg)
    {
        double a = angleDeg * Math.PI / 180.0;
        return new Vector2(Math.Cos(a), Math.Sin(a));
    }
}
