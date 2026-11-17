using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;
using TubeCadCam.Geometry;

namespace TubeCadCam.Presentation;

public enum FeatureKind { Hole, RectangularCutout, Slot, ContourCutout, Slit, EndNotch, EndMiter }

/// <summary>Maps between CAD features and editable parameter lists (names double as UI labels).</summary>
public static class FeatureDescriptors
{
    public static string Label(FeatureKind k) => k switch
    {
        FeatureKind.Hole => "Bohrung",
        FeatureKind.RectangularCutout => "Rechteckausschnitt",
        FeatureKind.Slot => "Langloch",
        FeatureKind.ContourCutout => "Konturausschnitt",
        FeatureKind.Slit => "Schlitz (stirnseitig)",
        FeatureKind.EndNotch => "Ausklinkung (stirnseitig)",
        FeatureKind.EndMiter => "Gehrung (stirnseitig)",
        _ => k.ToString(),
    };

    public static bool HasEnd(FeatureKind k) => k is FeatureKind.Slit or FeatureKind.EndNotch or FeatureKind.EndMiter;
    public static bool HasContourText(FeatureKind k) => k == FeatureKind.ContourCutout;

    public static FeatureKind KindOf(TubeFeature f) => f switch
    {
        HoleFeature => FeatureKind.Hole,
        RectangularCutoutFeature => FeatureKind.RectangularCutout,
        SlotFeature => FeatureKind.Slot,
        ContourCutoutFeature => FeatureKind.ContourCutout,
        SlitFeature => FeatureKind.Slit,
        EndNotchFeature => FeatureKind.EndNotch,
        EndMiterFeature => FeatureKind.EndMiter,
        _ => throw new NotSupportedException($"Unknown feature type {f.GetType().Name}."),
    };

    /// <summary>Starting values for a new feature (positions derived from the tube length); illustrative, not technology data.</summary>
    public static (string Name, double Value)[] Defaults(FeatureKind k, double lengthMm)
    {
        double mid = lengthMm > 0 ? Math.Round(lengthMm / 2, 3) : 100;
        return k switch
        {
            FeatureKind.Hole => [("AxialPosition", mid), ("Angle", 90), ("Diameter", 10)],
            FeatureKind.RectangularCutout => [("AxialPosition", mid), ("Angle", 90), ("Width", 15), ("Length", 25), ("CornerRadius", 2), ("Rotation", 0)],
            FeatureKind.Slot => [("AxialPosition", mid), ("Angle", 90), ("Length", 30), ("Width", 8), ("Rotation", 0)],
            FeatureKind.ContourCutout => [("AxialPosition", mid), ("Angle", 90), ("Rotation", 0)],
            FeatureKind.Slit => [("Angle", 0), ("Width", 4), ("Depth", 40)],
            FeatureKind.EndNotch => [("Angle", 90), ("Width", 15), ("Depth", 25), ("InnerCornerRadius", 3)],
            FeatureKind.EndMiter => [("TiltAngle", 30), ("TiltAxisAngle", 90)],
            _ => throw new ArgumentOutOfRangeException(nameof(k)),
        };
    }

    public const string DefaultContourText = "-10;-6\n10;-6\n10;6\n0;12\n-10;6";

    public static TubeFeature Create(FeatureKind k, Guid id, TubeEnd end, Func<string, double> p, Polygon2? contour) => k switch
    {
        FeatureKind.Hole => new HoleFeature(id, p("AxialPosition"), p("Angle"), p("Diameter")),
        FeatureKind.RectangularCutout => new RectangularCutoutFeature(id, p("AxialPosition"), p("Angle"), p("Width"), p("Length"), p("CornerRadius"), p("Rotation")),
        FeatureKind.Slot => new SlotFeature(id, p("AxialPosition"), p("Angle"), p("Length"), p("Width"), p("Rotation")),
        FeatureKind.ContourCutout => new ContourCutoutFeature(id, p("AxialPosition"), p("Angle"), contour!, p("Rotation")),
        FeatureKind.Slit => new SlitFeature(id, end, p("Angle"), p("Width"), p("Depth")),
        FeatureKind.EndNotch => new EndNotchFeature(id, end, p("Angle"), p("Width"), p("Depth"), p("InnerCornerRadius")),
        FeatureKind.EndMiter => new EndMiterFeature(id, end, p("TiltAngle"), p("TiltAxisAngle")),
        _ => throw new ArgumentOutOfRangeException(nameof(k)),
    };

    public static (string Name, double Value)[] Describe(TubeFeature f) => f switch
    {
        HoleFeature h => [("AxialPosition", h.AxialPositionMm), ("Angle", h.AngleDeg), ("Diameter", h.DiameterMm)],
        RectangularCutoutFeature r => [("AxialPosition", r.AxialPositionMm), ("Angle", r.AngleDeg), ("Width", r.WidthMm), ("Length", r.LengthMm), ("CornerRadius", r.CornerRadiusMm), ("Rotation", r.RotationDeg)],
        SlotFeature s => [("AxialPosition", s.AxialPositionMm), ("Angle", s.AngleDeg), ("Length", s.LengthMm), ("Width", s.WidthMm), ("Rotation", s.RotationDeg)],
        ContourCutoutFeature c => [("AxialPosition", c.AxialPositionMm), ("Angle", c.AngleDeg), ("Rotation", c.RotationDeg)],
        SlitFeature s => [("Angle", s.AngleDeg), ("Width", s.WidthMm), ("Depth", s.DepthMm)],
        EndNotchFeature n => [("Angle", n.AngleDeg), ("Width", n.WidthMm), ("Depth", n.DepthMm), ("InnerCornerRadius", n.InnerCornerRadiusMm)],
        EndMiterFeature m => [("TiltAngle", m.TiltAngleDeg), ("TiltAxisAngle", m.TiltAxisAngleDeg)],
        _ => throw new NotSupportedException(),
    };
}
