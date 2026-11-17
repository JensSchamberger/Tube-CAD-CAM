using TubeCadCam.Domain.Common;
using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

/// <summary>Geometric tolerances of the CAD validity checks (CAD-level heuristics, not technology data).</summary>
public sealed record FeatureValidationOptions(double MaxEdgeTurnDeg = 20, double MaxWrapDeg = 60)
{
    public static FeatureValidationOptions Default { get; } = new();
}

public static class TubeModelValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(TubeModel model, FeatureValidationOptions? options = null)
    {
        options ??= FeatureValidationOptions.Default;
        var issues = new List<ValidationIssue>(model.Part.Validate());
        if (issues.Any(i => i.Severity == IssueSeverity.Error))
        {
            return issues; // feature checks need a valid part
        }

        double length = model.Part.LengthMm;
        double minDim = model.Part.Profile.MinOuterDimensionMm;
        ProfileSection section = ProfileSectionBuilder.Build(model.Part.Profile, 64, 8);
        var surface = new SectionSurface(section.Outer);
        var ids = new HashSet<Guid>();

        foreach (TubeFeature f in model.Features)
        {
            if (!ids.Add(f.Id))
            {
                issues.Add(new ValidationIssue("feature.duplicate-id", $"Feature id {f.Id} is used more than once."));
            }
        }

        // End miters
        foreach (var g in model.Features.OfType<EndMiterFeature>().GroupBy(m => m.End).Where(g => g.Count() > 1))
        {
            issues.Add(new ValidationIssue("feature.miter-duplicate", $"Only one miter per end is allowed ({g.Key})."));
        }

        (EndPlane start, EndPlane end) = EndPlane.For(model, section.Outer);
        if (model.Features.OfType<EndMiterFeature>().Any()
            && section.Outer.Vertices.Any(p => start.XAt(p) >= end.XAt(p) - 1e-6))
        {
            issues.Add(new ValidationIssue("feature.miter-too-large", "The end cuts leave no material between start and end face (tube too short for the miter angle)."));
        }

        bool hasMiter = model.Features.OfType<EndMiterFeature>().Any();
        var wallCuts = new List<(TubeFeature Feature, IWallCut Cut, (double Min, double Max) Range, double Arc, double HalfV)>();

        foreach (TubeFeature f in model.Features)
        {
            IReadOnlyList<ValidationIssue> own = f.ValidateParameters();
            issues.AddRange(own);

            double? angle = f switch { IWallCut c => c.AngleDeg, SlitFeature s => s.AngleDeg, _ => null };
            bool angleOk = true;
            if (angle is { } a && (a < 0 || a >= 360 || double.IsNaN(a)))
            {
                issues.Add(new ValidationIssue("feature.angle-range", $"Feature {f.Id}: angle must be in [0, 360)."));
                angleOk = false;
            }

            if (own.Any(i => i.Severity == IssueSeverity.Error) || !angleOk)
            {
                continue;
            }

            (double Min, double Max)? range = f.AxialRange(length);
            if (range is { } r && (r.Min < -1e-9 || r.Max > length + 1e-9))
            {
                issues.Add(new ValidationIssue("feature.outside-length", $"Feature {f.Id}: lies outside the tube length."));
                continue;
            }

            switch (f)
            {
                case SlitFeature slit:
                    if (slit.WidthMm >= minDim)
                    {
                        issues.Add(new ValidationIssue("feature.slit-too-wide", $"Slit {slit.Id}: width must be smaller than the smallest outer profile dimension."));
                    }

                    if (slit.DepthMm >= length - 1e-9)
                    {
                        issues.Add(new ValidationIssue("feature.slit-splits-tube", $"Slit {slit.Id}: depth must be smaller than the tube length (the tube would be split)."));
                    }

                    break;

                case IWallCut cut:
                    CheckWallCut(f, cut, surface, section, options, length, minDim, hasMiter, start, end, issues, wallCuts, range!.Value);
                    break;
            }
        }

        // Overlap warnings (bounding approximation in unrolled axial/arc coordinates)
        for (int i = 0; i < wallCuts.Count; i++)
        {
            for (int j = i + 1; j < wallCuts.Count; j++)
            {
                var a = wallCuts[i];
                var b = wallCuts[j];
                bool axial = a.Range.Min < b.Range.Max && b.Range.Min < a.Range.Max;
                bool arc = surface.ArcDistance(a.Arc, b.Arc) < a.HalfV + b.HalfV;
                if (axial && arc)
                {
                    issues.Add(new ValidationIssue("feature.overlap", $"Features {a.Feature.Id} and {b.Feature.Id} may overlap.", IssueSeverity.Warning));
                }
            }
        }

        return issues;
    }

    private static void CheckWallCut(
        TubeFeature f, IWallCut cut, SectionSurface surface, ProfileSection section, FeatureValidationOptions options,
        double length, double minDim, bool hasMiter, EndPlane start, EndPlane end, List<ValidationIssue> issues,
        List<(TubeFeature, IWallCut, (double, double), double, double)> wallCuts, (double Min, double Max) range)
    {
        if (f is HoleFeature h && h.DiameterMm >= minDim)
        {
            issues.Add(new ValidationIssue("feature.hole-too-large", $"Hole {h.Id}: diameter must be smaller than the smallest outer profile dimension."));
            return;
        }

        SurfacePoint? p = surface.Locate(cut.AngleDeg);
        if (p is null)
        {
            issues.Add(new ValidationIssue("feature.no-surface", $"Feature {f.Id}: the angle {cut.AngleDeg}° does not hit the profile surface."));
            return;
        }

        Polygon2 outline = cut.Outline(64, 16);
        Bounds2 b = outline.Bounds;
        bool failed = false;
        foreach ((double dist, int dir) in new[] { (Math.Max(b.Max.Y, 0), 1), (Math.Max(-b.Min.Y, 0), -1) })
        {
            (double maxTurn, double total) = surface.Walk(p, dist, dir);
            if (maxTurn >= options.MaxEdgeTurnDeg)
            {
                issues.Add(new ValidationIssue("feature.crosses-edge", $"Feature {f.Id}: extends over an edge of the profile; place it on a single face."));
                failed = true;
                break;
            }

            if (total >= options.MaxWrapDeg)
            {
                issues.Add(new ValidationIssue("feature.wraps-surface", $"Feature {f.Id}: wraps too far around a curved surface."));
                failed = true;
                break;
            }
        }

        if (failed)
        {
            return;
        }

        if (hasMiter)
        {
            double cx = cut.AxialCenter(length);
            foreach (Vector2 q in outline.Vertices)
            {
                Vector2 pos = p.Position + (p.Tangent * q.Y);
                double x = cx + q.X;
                if (x < start.XAt(pos) - 1e-9 || x > end.XAt(pos) + 1e-9)
                {
                    issues.Add(new ValidationIssue("feature.beyond-end", $"Feature {f.Id}: reaches beyond the (mitred) tube end."));
                    return;
                }
            }
        }

        wallCuts.Add((f, cut, range, p.ArcPosition, Math.Max(Math.Abs(b.Min.Y), Math.Abs(b.Max.Y))));
    }
}
