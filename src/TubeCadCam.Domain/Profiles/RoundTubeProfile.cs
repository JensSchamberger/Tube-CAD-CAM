using TubeCadCam.Domain.Common;

namespace TubeCadCam.Domain.Profiles;

public sealed record RoundTubeProfile(double OuterDiameterMm, double WallThicknessMm) : ProfileDefinition
{
    public override bool IsClosedSection => true;
    public override double MinOuterDimensionMm => OuterDiameterMm;
    public double InnerDiameterMm => OuterDiameterMm - (2 * WallThicknessMm);

    public override double CrossSectionAreaMm2 =>
        Math.PI / 4.0 * ((OuterDiameterMm * OuterDiameterMm) - (InnerDiameterMm * InnerDiameterMm));

    public override string ProfileKey => $"round-{Fmt(OuterDiameterMm)}x{Fmt(WallThicknessMm)}";

    public override IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();
        Positive(issues, OuterDiameterMm, nameof(OuterDiameterMm));
        Positive(issues, WallThicknessMm, nameof(WallThicknessMm));
        if (issues.Count == 0 && InnerDiameterMm <= 0)
        {
            issues.Add(new ValidationIssue("profile.wall-too-thick", "Wall thickness must be less than half the outer diameter."));
        }

        return issues;
    }
}
