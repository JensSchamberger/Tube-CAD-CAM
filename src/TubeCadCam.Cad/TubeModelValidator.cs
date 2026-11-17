using TubeCadCam.Domain.Common;

namespace TubeCadCam.Cad;

public static class TubeModelValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(TubeModel model)
    {
        var issues = new List<ValidationIssue>(model.Part.Validate());
        if (issues.Any(i => i.Severity == IssueSeverity.Error))
        {
            return issues; // feature checks need a valid part
        }

        double length = model.Part.LengthMm;
        double minDim = model.Part.Profile.MinOuterDimensionMm;
        var ids = new HashSet<Guid>();

        foreach (TubeFeature f in model.Features)
        {
            if (!ids.Add(f.Id))
            {
                issues.Add(new ValidationIssue("feature.duplicate-id", $"Feature id {f.Id} is used more than once."));
            }

            if (f.AngleDeg is < 0 or >= 360 || double.IsNaN(f.AngleDeg))
            {
                issues.Add(new ValidationIssue("feature.angle-range", $"Feature {f.Id}: angle must be in [0, 360)."));
            }

            double half = f.AxialExtentMm / 2;
            if (!(f.AxialExtentMm > 0))
            {
                issues.Add(new ValidationIssue("feature.extent-invalid", $"Feature {f.Id}: size must be greater than 0."));
            }
            else if (f.AxialPositionMm - half < 0 || f.AxialPositionMm + half > length)
            {
                issues.Add(new ValidationIssue("feature.outside-length", $"Feature {f.Id}: lies outside the tube length."));
            }

            switch (f)
            {
                case HoleFeature h when h.DiameterMm >= minDim:
                    issues.Add(new ValidationIssue("feature.hole-too-large", $"Hole {h.Id}: diameter must be smaller than the smallest outer profile dimension."));
                    break;
                case RectangularCutoutFeature c:
                    if (!(c.WidthMm > 0))
                    {
                        issues.Add(new ValidationIssue("feature.extent-invalid", $"Cutout {c.Id}: width must be greater than 0."));
                    }

                    if (c.CornerRadiusMm < 0 || c.CornerRadiusMm > Math.Min(c.WidthMm, c.LengthMm) / 2)
                    {
                        issues.Add(new ValidationIssue("feature.radius-invalid", $"Cutout {c.Id}: corner radius must be within [0, min(width,length)/2]."));
                    }

                    break;
            }
        }

        return issues;
    }
}
