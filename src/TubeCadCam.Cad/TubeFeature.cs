using System.Text.Json.Serialization;
using TubeCadCam.Domain.Common;
using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

public enum TubeEnd { Start, End }

/// <summary>
/// Machining feature of a tube. All values are CAD geometry in the documented tube coordinate system
/// (see <see cref="TubeCoordinateSystem"/>); features carry no machine or technology data.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$feature")]
[JsonDerivedType(typeof(HoleFeature), "hole")]
[JsonDerivedType(typeof(RectangularCutoutFeature), "cutout")]
[JsonDerivedType(typeof(SlotFeature), "slot")]
[JsonDerivedType(typeof(ContourCutoutFeature), "contour")]
[JsonDerivedType(typeof(SlitFeature), "slit")]
[JsonDerivedType(typeof(EndNotchFeature), "notch")]
[JsonDerivedType(typeof(EndMiterFeature), "miter")]
public abstract record TubeFeature(Guid Id)
{
    /// <summary>Checks the feature's own numbers (independent of the tube it sits on).</summary>
    public abstract IReadOnlyList<ValidationIssue> ValidateParameters();

    /// <summary>Axial extent on a tube of the given length, or null if the feature is not tied to an axial range (miter).</summary>
    public abstract (double Min, double Max)? AxialRange(double lengthMm);

    protected static bool Pos(double v) => v > 0 && !double.IsInfinity(v);

    protected ValidationIssue Extent(string what) => new("feature.extent-invalid", $"Feature {Id}: {what} must be greater than 0.");
}

/// <summary>A cut through one wall, defined by a planar outline projected along the surface normal.</summary>
public interface IWallCut
{
    Guid Id { get; }
    double AngleDeg { get; }
    double AxialCenter(double lengthMm);

    /// <summary>Outline in the local (u = axial, v = tangential) frame around the feature centre, rotation already applied.</summary>
    Polygon2 Outline(int circleSegments, int cornerSegments);
}

public sealed record HoleFeature(Guid Id, double AxialPositionMm, double AngleDeg, double DiameterMm) : TubeFeature(Id), IWallCut
{
    public double AxialCenter(double lengthMm) => AxialPositionMm;
    public override (double Min, double Max)? AxialRange(double lengthMm) => (AxialPositionMm - (DiameterMm / 2), AxialPositionMm + (DiameterMm / 2));
    public Polygon2 Outline(int circleSegments, int cornerSegments) => Polygon2.Circle(Vector2.Zero, DiameterMm / 2, circleSegments);
    public override IReadOnlyList<ValidationIssue> ValidateParameters() => Pos(DiameterMm) ? [] : [Extent("diameter")];
}

/// <summary>Rectangular opening; <c>WidthMm</c> tangential, <c>LengthMm</c> axial (before rotation), rotated by <c>RotationDeg</c> in the surface plane.</summary>
public sealed record RectangularCutoutFeature(
    Guid Id, double AxialPositionMm, double AngleDeg, double WidthMm, double LengthMm, double CornerRadiusMm = 0, double RotationDeg = 0)
    : TubeFeature(Id), IWallCut
{
    public double AxialCenter(double lengthMm) => AxialPositionMm;

    public Polygon2 Outline(int circleSegments, int cornerSegments) =>
        Polygon2.RoundedRectangle(LengthMm, WidthMm, CornerRadiusMm, Vector2.Zero, cornerSegments).Rotated(RotationDeg);

    public override (double Min, double Max)? AxialRange(double lengthMm) => WallCutRange(this, lengthMm);

    public override IReadOnlyList<ValidationIssue> ValidateParameters()
    {
        var issues = new List<ValidationIssue>();
        if (!Pos(WidthMm)) { issues.Add(Extent("width")); }
        if (!Pos(LengthMm)) { issues.Add(Extent("length")); }
        if (issues.Count == 0 && (CornerRadiusMm < 0 || CornerRadiusMm > Math.Min(WidthMm, LengthMm) / 2 || double.IsNaN(CornerRadiusMm)))
        {
            issues.Add(new ValidationIssue("feature.radius-invalid", $"Cutout {Id}: corner radius must be within [0, min(width,length)/2]."));
        }

        return issues;
    }

    internal static (double, double) WallCutRange(IWallCut c, double lengthMm)
    {
        Bounds2 b = c.Outline(64, 16).Bounds;
        double centre = c.AxialCenter(lengthMm);
        return (centre + b.Min.X, centre + b.Max.X);
    }
}

/// <summary>Slot (Langloch): obround with overall <c>LengthMm</c> along u and <c>WidthMm</c> along v, rotated by <c>RotationDeg</c>.</summary>
public sealed record SlotFeature(Guid Id, double AxialPositionMm, double AngleDeg, double LengthMm, double WidthMm, double RotationDeg = 0)
    : TubeFeature(Id), IWallCut
{
    public double AxialCenter(double lengthMm) => AxialPositionMm;
    public Polygon2 Outline(int circleSegments, int cornerSegments) => Polygon2.Obround(LengthMm, WidthMm, Vector2.Zero, Math.Max(circleSegments / 2, 2)).Rotated(RotationDeg);
    public override (double Min, double Max)? AxialRange(double lengthMm) => RectangularCutoutFeature.WallCutRange(this, lengthMm);

    public override IReadOnlyList<ValidationIssue> ValidateParameters()
    {
        var issues = new List<ValidationIssue>();
        if (!Pos(WidthMm)) { issues.Add(Extent("width")); }
        if (!Pos(LengthMm)) { issues.Add(Extent("length")); }
        if (issues.Count == 0 && LengthMm <= WidthMm)
        {
            issues.Add(new ValidationIssue("feature.slot-length", $"Slot {Id}: length must be greater than width (otherwise use a hole)."));
        }

        return issues;
    }
}

/// <summary>Free contour cut-out: closed, simple polygon in the local frame (u axial, v tangential) around the feature centre, rotated by <c>RotationDeg</c>.</summary>
public sealed record ContourCutoutFeature(Guid Id, double AxialPositionMm, double AngleDeg, Polygon2 ContourOutline, double RotationDeg = 0)
    : TubeFeature(Id), IWallCut
{
    public double AxialCenter(double lengthMm) => AxialPositionMm;
    public Polygon2 Outline(int circleSegments, int cornerSegments) => ContourOutline.AsCounterClockwise().Rotated(RotationDeg);
    public override (double Min, double Max)? AxialRange(double lengthMm) => RectangularCutoutFeature.WallCutRange(this, lengthMm);

    public override IReadOnlyList<ValidationIssue> ValidateParameters()
    {
        var issues = new List<ValidationIssue>();
        if (ContourOutline.Area <= Tolerance.Linear)
        {
            issues.Add(new ValidationIssue("feature.contour-invalid", $"Contour {Id}: outline has no area."));
        }
        else if (!ContourOutline.IsSimple)
        {
            issues.Add(new ValidationIssue("feature.contour-invalid", $"Contour {Id}: outline must not intersect itself."));
        }

        return issues;
    }
}

/// <summary>Notch (Ausklinkung) at a tube end through one wall: <c>WidthMm</c> tangential, <c>DepthMm</c> axial from the end face; inner corners may be rounded.</summary>
public sealed record EndNotchFeature(Guid Id, TubeEnd End, double AngleDeg, double WidthMm, double DepthMm, double InnerCornerRadiusMm = 0)
    : TubeFeature(Id), IWallCut
{
    public double AxialCenter(double lengthMm) => End == TubeEnd.Start ? DepthMm / 2 : lengthMm - (DepthMm / 2);

    public Polygon2 Outline(int circleSegments, int cornerSegments)
    {
        double hu = DepthMm / 2, hv = WidthMm / 2, r = InnerCornerRadiusMm;
        var pts = new List<Vector2> { new(-hu, -hv) };
        if (r > 0)
        {
            for (int i = 0; i <= cornerSegments; i++)
            {
                double a = (-90.0 + (90.0 * i / cornerSegments)) * Math.PI / 180.0;
                pts.Add(new Vector2(hu - r + (r * Math.Cos(a)), -hv + r + (r * Math.Sin(a))));
            }

            for (int i = 0; i <= cornerSegments; i++)
            {
                double a = (90.0 * i / cornerSegments) * Math.PI / 180.0;
                pts.Add(new Vector2(hu - r + (r * Math.Cos(a)), hv - r + (r * Math.Sin(a))));
            }
        }
        else
        {
            pts.Add(new Vector2(hu, -hv));
            pts.Add(new Vector2(hu, hv));
        }

        pts.Add(new Vector2(-hu, hv));
        var poly = new Polygon2(pts);
        return End == TubeEnd.Start ? poly : poly.Transformed(p => new Vector2(-p.X, p.Y)).AsCounterClockwise();
    }

    public override (double Min, double Max)? AxialRange(double lengthMm) =>
        End == TubeEnd.Start ? (0, DepthMm) : (lengthMm - DepthMm, lengthMm);

    public override IReadOnlyList<ValidationIssue> ValidateParameters()
    {
        var issues = new List<ValidationIssue>();
        if (!Pos(WidthMm)) { issues.Add(Extent("width")); }
        if (!Pos(DepthMm)) { issues.Add(Extent("depth")); }
        if (issues.Count == 0 && (InnerCornerRadiusMm < 0 || InnerCornerRadiusMm > Math.Min(WidthMm / 2, DepthMm) || double.IsNaN(InnerCornerRadiusMm)))
        {
            issues.Add(new ValidationIssue("feature.radius-invalid", $"Notch {Id}: inner corner radius must be within [0, min(width/2, depth)]."));
        }

        return issues;
    }
}

/// <summary>
/// Slit (Schlitz): narrow cut of width <c>WidthMm</c> running <c>DepthMm</c> into the tube from an end face,
/// through <b>both</b> walls along the section direction <c>AngleDeg</c> (passing through the section origin).
/// </summary>
public sealed record SlitFeature(Guid Id, TubeEnd End, double AngleDeg, double WidthMm, double DepthMm) : TubeFeature(Id)
{
    public override (double Min, double Max)? AxialRange(double lengthMm) =>
        End == TubeEnd.Start ? (0, DepthMm) : (lengthMm - DepthMm, lengthMm);

    public override IReadOnlyList<ValidationIssue> ValidateParameters()
    {
        var issues = new List<ValidationIssue>();
        if (!Pos(WidthMm)) { issues.Add(Extent("width")); }
        if (!Pos(DepthMm)) { issues.Add(Extent("depth")); }
        return issues;
    }
}

/// <summary>
/// Planar miter cut at a tube end. The cut plane contains the section direction <c>TiltAxisAngleDeg</c> and is tilted by
/// <c>TiltAngleDeg</c> against the plane perpendicular to the tube axis. Positive tilt removes more material on the
/// +b side, b = the tilt axis direction rotated by +90°. The tube length is the envelope length: the shortest point of
/// each end face stays at x = 0 (start) or x = L (end).
/// </summary>
public sealed record EndMiterFeature(Guid Id, TubeEnd End, double TiltAngleDeg, double TiltAxisAngleDeg) : TubeFeature(Id)
{
    public override (double Min, double Max)? AxialRange(double lengthMm) => null;

    public override IReadOnlyList<ValidationIssue> ValidateParameters()
    {
        var issues = new List<ValidationIssue>();
        if (!(Math.Abs(TiltAngleDeg) < 89))
        {
            issues.Add(new ValidationIssue("feature.tilt-range", $"Miter {Id}: tilt angle must be within (-89°, 89°)."));
        }

        if (TiltAxisAngleDeg is < 0 or >= 360 || double.IsNaN(TiltAxisAngleDeg))
        {
            issues.Add(new ValidationIssue("feature.angle-range", $"Miter {Id}: tilt axis angle must be in [0, 360)."));
        }

        return issues;
    }
}
