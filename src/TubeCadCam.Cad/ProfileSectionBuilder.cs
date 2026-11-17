using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

/// <summary>Builds tessellated cross-sections from parametric profile definitions. Deterministic.</summary>
public static class ProfileSectionBuilder
{
    public const int DefaultCircleSegments = 256;
    public const int DefaultCornerSegments = 16;

    public static ProfileSection Build(ProfileDefinition profile, int circleSegments = DefaultCircleSegments, int cornerSegments = DefaultCornerSegments)
    {
        ArgumentNullException.ThrowIfNull(profile);
        IReadOnlyList<Domain.Common.ValidationIssue> issues = profile.Validate();
        if (issues.Count > 0)
        {
            throw new ArgumentException($"Profile is invalid: {issues[0].Message}", nameof(profile));
        }

        Vector2 c = Vector2.Zero;
        switch (profile)
        {
            case RoundTubeProfile r:
                return new ProfileSection(
                    Polygon2.Circle(c, r.OuterDiameterMm / 2, circleSegments),
                    [Polygon2.Circle(c, r.InnerDiameterMm / 2, circleSegments)]);

            case RectangularTubeProfile t:
                double wi = t.WidthMm - (2 * t.WallThicknessMm);
                double hi = t.HeightMm - (2 * t.WallThicknessMm);
                return new ProfileSection(
                    Polygon2.RoundedRectangle(t.WidthMm, t.HeightMm, t.OuterCornerRadiusMm, c, cornerSegments),
                    [Polygon2.RoundedRectangle(wi, hi, t.InnerCornerRadiusMm, c, cornerSegments)]);

            case UProfile u:
                double h = u.HeightMm, w = u.FlangeWidthMm, tw = u.WebThicknessMm, tf = u.FlangeThicknessMm;
                return new ProfileSection(new Polygon2([
                    new(0, 0), new(w, 0), new(w, tf), new(tw, tf),
                    new(tw, h - tf), new(w, h - tf), new(w, h), new(0, h),
                ]), []);

            case LProfile l:
                return new ProfileSection(new Polygon2([
                    new(0, 0), new(l.LegAMm, 0), new(l.LegAMm, l.ThicknessMm),
                    new(l.ThicknessMm, l.ThicknessMm), new(l.ThicknessMm, l.LegBMm), new(0, l.LegBMm),
                ]), []);

            case IProfile i:
                double ih = i.HeightMm, b = i.FlangeWidthMm, iw = i.WebThicknessMm, itf = i.FlangeThicknessMm;
                double x0 = (b - iw) / 2, x1 = (b + iw) / 2;
                return new ProfileSection(new Polygon2([
                    new(0, 0), new(b, 0), new(b, itf), new(x1, itf), new(x1, ih - itf), new(b, ih - itf),
                    new(b, ih), new(0, ih), new(0, ih - itf), new(x0, ih - itf), new(x0, itf), new(0, itf),
                ]), []);

            case CustomProfile cp:
                return new ProfileSection(cp.Outer, cp.Voids);

            default:
                throw new NotSupportedException($"Unknown profile type {profile.GetType().Name}.");
        }
    }
}
