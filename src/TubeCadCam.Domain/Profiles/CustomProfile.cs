using TubeCadCam.Domain.Common;
using TubeCadCam.Geometry;

namespace TubeCadCam.Domain.Profiles;

/// <summary>User-defined section: one outer polygon plus optional void polygons (hollow chambers).</summary>
public sealed record CustomProfile(string Name, Polygon2 Outer, IReadOnlyList<Polygon2> Voids) : ProfileDefinition
{
    public override bool IsClosedSection => Voids.Count > 0;
    public override double MinOuterDimensionMm => Math.Min(Outer.Bounds.Width, Outer.Bounds.Height);
    /// <summary>Estimated as 2·A/P (exact for constant-thickness thin-walled sections).</summary>
    public override double NominalWallThicknessMm
    {
        get
        {
            double perimeter = Outer.Perimeter + Voids.Sum(v => v.Perimeter);
            return perimeter > 0 ? 2 * CrossSectionAreaMm2 / perimeter : 0;
        }
    }

    public override double CrossSectionAreaMm2 => Outer.Area - Voids.Sum(v => v.Area);
    public override string ProfileKey => $"custom-{Name}-{Fmt(CrossSectionAreaMm2)}";

    public override IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            issues.Add(new ValidationIssue("profile.name-missing", "Custom profile requires a name."));
        }

        if (Outer.Area <= 0)
        {
            issues.Add(new ValidationIssue("profile.degenerate-outline", "Outer outline has no area."));
        }

        foreach (Polygon2 v in Voids)
        {
            if (v.Area <= 0)
            {
                issues.Add(new ValidationIssue("profile.degenerate-void", "A void polygon has no area."));
            }
        }

        if (issues.Count == 0 && CrossSectionAreaMm2 <= 0)
        {
            issues.Add(new ValidationIssue("profile.voids-too-large", "Voids remove more area than the outline provides."));
        }

        return issues;
    }
}
