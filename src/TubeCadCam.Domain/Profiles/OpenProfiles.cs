using TubeCadCam.Domain.Common;

namespace TubeCadCam.Domain.Profiles;

/// <summary>Parallel-flange U profile; web on the left (x=0), flanges opening towards +x.</summary>
public sealed record UProfile(double HeightMm, double FlangeWidthMm, double WebThicknessMm, double FlangeThicknessMm)
    : ProfileDefinition
{
    public override bool IsClosedSection => false;
    public override double MinOuterDimensionMm => Math.Min(HeightMm, FlangeWidthMm);

    public override double CrossSectionAreaMm2 =>
        (WebThicknessMm * HeightMm) + (2 * (FlangeWidthMm - WebThicknessMm) * FlangeThicknessMm);

    public override string ProfileKey =>
        $"u-{Fmt(HeightMm)}x{Fmt(FlangeWidthMm)}x{Fmt(WebThicknessMm)}x{Fmt(FlangeThicknessMm)}";

    public override IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();
        Positive(issues, HeightMm, nameof(HeightMm));
        Positive(issues, FlangeWidthMm, nameof(FlangeWidthMm));
        Positive(issues, WebThicknessMm, nameof(WebThicknessMm));
        Positive(issues, FlangeThicknessMm, nameof(FlangeThicknessMm));
        if (issues.Count == 0)
        {
            if (WebThicknessMm >= FlangeWidthMm)
            {
                issues.Add(new ValidationIssue("profile.web-too-thick", "Web thickness must be less than the flange width."));
            }

            if (2 * FlangeThicknessMm >= HeightMm)
            {
                issues.Add(new ValidationIssue("profile.flange-too-thick", "Twice the flange thickness must be less than the height."));
            }
        }

        return issues;
    }
}

/// <summary>Equal or unequal angle; leg A along +x, leg B along +y, corner at the origin.</summary>
public sealed record LProfile(double LegAMm, double LegBMm, double ThicknessMm) : ProfileDefinition
{
    public override bool IsClosedSection => false;
    public override double MinOuterDimensionMm => Math.Min(LegAMm, LegBMm);
    public override double CrossSectionAreaMm2 => ThicknessMm * (LegAMm + LegBMm - ThicknessMm);
    public override string ProfileKey => $"l-{Fmt(LegAMm)}x{Fmt(LegBMm)}x{Fmt(ThicknessMm)}";

    public override IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();
        Positive(issues, LegAMm, nameof(LegAMm));
        Positive(issues, LegBMm, nameof(LegBMm));
        Positive(issues, ThicknessMm, nameof(ThicknessMm));
        if (issues.Count == 0 && ThicknessMm >= Math.Min(LegAMm, LegBMm))
        {
            issues.Add(new ValidationIssue("profile.thickness-too-large", "Thickness must be less than both legs."));
        }

        return issues;
    }
}

/// <summary>Parallel-flange I profile with centred web.</summary>
public sealed record IProfile(double HeightMm, double FlangeWidthMm, double WebThicknessMm, double FlangeThicknessMm)
    : ProfileDefinition
{
    public override bool IsClosedSection => false;
    public override double MinOuterDimensionMm => Math.Min(HeightMm, FlangeWidthMm);

    public override double CrossSectionAreaMm2 =>
        (2 * FlangeWidthMm * FlangeThicknessMm) + ((HeightMm - (2 * FlangeThicknessMm)) * WebThicknessMm);

    public override string ProfileKey =>
        $"i-{Fmt(HeightMm)}x{Fmt(FlangeWidthMm)}x{Fmt(WebThicknessMm)}x{Fmt(FlangeThicknessMm)}";

    public override IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();
        Positive(issues, HeightMm, nameof(HeightMm));
        Positive(issues, FlangeWidthMm, nameof(FlangeWidthMm));
        Positive(issues, WebThicknessMm, nameof(WebThicknessMm));
        Positive(issues, FlangeThicknessMm, nameof(FlangeThicknessMm));
        if (issues.Count == 0)
        {
            if (WebThicknessMm >= FlangeWidthMm)
            {
                issues.Add(new ValidationIssue("profile.web-too-thick", "Web thickness must be less than the flange width."));
            }

            if (2 * FlangeThicknessMm >= HeightMm)
            {
                issues.Add(new ValidationIssue("profile.flange-too-thick", "Twice the flange thickness must be less than the height."));
            }
        }

        return issues;
    }
}
