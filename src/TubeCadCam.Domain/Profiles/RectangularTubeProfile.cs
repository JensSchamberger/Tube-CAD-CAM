using TubeCadCam.Domain.Common;

namespace TubeCadCam.Domain.Profiles;

/// <summary>Rectangular tube; a square tube is the case <c>WidthMm == HeightMm</c> (see <see cref="Square"/>).</summary>
public sealed record RectangularTubeProfile(double WidthMm, double HeightMm, double WallThicknessMm, double OuterCornerRadiusMm = 0)
    : ProfileDefinition
{
    public static RectangularTubeProfile Square(double sideMm, double wallThicknessMm, double outerCornerRadiusMm = 0) =>
        new(sideMm, sideMm, wallThicknessMm, outerCornerRadiusMm);

    public bool IsSquare => TubeCadCam.Geometry.Tolerance.AreEqual(WidthMm, HeightMm);
    public double InnerCornerRadiusMm => Math.Max(OuterCornerRadiusMm - WallThicknessMm, 0);

    public override bool IsClosedSection => true;
    public override double MinOuterDimensionMm => Math.Min(WidthMm, HeightMm);

    public override double CrossSectionAreaMm2
    {
        get
        {
            double cornerLoss = 4.0 - Math.PI;
            double outer = (WidthMm * HeightMm) - (cornerLoss * OuterCornerRadiusMm * OuterCornerRadiusMm);
            double ri = InnerCornerRadiusMm;
            double inner = ((WidthMm - (2 * WallThicknessMm)) * (HeightMm - (2 * WallThicknessMm))) - (cornerLoss * ri * ri);
            return outer - inner;
        }
    }

    public override string ProfileKey => IsSquare
        ? $"square-{Fmt(WidthMm)}x{Fmt(WallThicknessMm)}-r{Fmt(OuterCornerRadiusMm)}"
        : $"rect-{Fmt(WidthMm)}x{Fmt(HeightMm)}x{Fmt(WallThicknessMm)}-r{Fmt(OuterCornerRadiusMm)}";

    public override IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();
        Positive(issues, WidthMm, nameof(WidthMm));
        Positive(issues, HeightMm, nameof(HeightMm));
        Positive(issues, WallThicknessMm, nameof(WallThicknessMm));
        if (OuterCornerRadiusMm < 0 || double.IsNaN(OuterCornerRadiusMm))
        {
            issues.Add(new ValidationIssue("profile.negative-radius", "OuterCornerRadiusMm must not be negative."));
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        if ((2 * WallThicknessMm) >= MinOuterDimensionMm)
        {
            issues.Add(new ValidationIssue("profile.wall-too-thick", "Wall thickness must be less than half of the smaller outer dimension."));
        }

        if (OuterCornerRadiusMm > MinOuterDimensionMm / 2)
        {
            issues.Add(new ValidationIssue("profile.radius-too-large", "Outer corner radius must not exceed half of the smaller outer dimension."));
        }

        return issues;
    }
}
