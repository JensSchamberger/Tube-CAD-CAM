using TubeCadCam.Domain.Common;
using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

internal enum CutMode
{
    /// <summary>Removes the top-most material layer of every cutting line (wall cut).</summary>
    TopPair,

    /// <summary>Removes all material layers of every cutting line (through cut, e.g. slit).</summary>
    AllPairs,
}

/// <summary>
/// A feature expressed as a cutting prism: <see cref="Footprint"/> in (x, t) coordinates where t = section point · T,
/// T = N rotated by +90°, extruded along the direction <see cref="N"/> (heights h = section point · N).
/// </summary>
internal sealed record Cutter(Guid FeatureId, Vector2 N, CutMode Mode, IReadOnlyList<Polygon2> Footprint, bool IsEndFeature)
{
    public Vector2 T => new(-N.Y, N.X);
    public double TMin => Footprint.Min(p => p.Bounds.Min.Y);
    public double TMax => Footprint.Max(p => p.Bounds.Max.Y);
}

internal static class SolidCutters
{
    public static (List<Cutter> Cutters, List<FeatureFailure> Failures) Create(TubeModel model, ProfileSection section, SolidOptions options)
    {
        var cutters = new List<Cutter>();
        var failures = new List<FeatureFailure>();
        var surface = new SectionSurface(section.Outer);
        double length = model.Part.LengthMm;
        double ov = options.EndOvershootMm;

        foreach (TubeFeature f in model.Features)
        {
            if (f.ValidateParameters().Any(i => i.Severity == IssueSeverity.Error))
            {
                failures.Add(new FeatureFailure(f.Id, "solid.invalid-parameters", "The feature parameters are invalid; it is not part of the solid."));
                continue;
            }

            switch (f)
            {
                case IWallCut cut:
                    SurfacePoint? p = surface.Locate(cut.AngleDeg);
                    if (p is null)
                    {
                        failures.Add(new FeatureFailure(f.Id, "solid.no-surface", $"The angle {cut.AngleDeg}° does not hit the profile surface."));
                        break;
                    }

                    double cx = cut.AxialCenter(length);
                    double t0 = p.Position.Dot(p.Tangent);
                    bool isEnd = f is EndNotchFeature;
                    Polygon2 outline = cut.Outline(FeatureTessellation.CircleSegments, FeatureTessellation.CornerSegments);
                    Polygon2 footprint = outline.Transformed(q =>
                    {
                        double x = cx + q.X;
                        if (isEnd)
                        {
                            // the end-face side of a notch reaches past the face so that no coplanar faces are created
                            if (Math.Abs(x) < 1e-9) { x = -ov; }
                            else if (Math.Abs(x - length) < 1e-9) { x = length + ov; }
                        }

                        return new Vector2(x, t0 + q.Y);
                    });
                    cutters.Add(new Cutter(f.Id, p.Normal, CutMode.TopPair, [footprint.AsCounterClockwise()], isEnd));
                    break;

                case SlitFeature slit:
                    Vector2 d = TubeCoordinateSystem.Direction(slit.AngleDeg);
                    double hw = slit.WidthMm / 2;
                    (double x0, double x1) = slit.End == TubeEnd.Start ? (-ov, slit.DepthMm) : (length - slit.DepthMm, length + ov);
                    cutters.Add(new Cutter(f.Id, d, CutMode.AllPairs,
                        [new Polygon2([new Vector2(x0, -hw), new Vector2(x1, -hw), new Vector2(x1, hw), new Vector2(x0, hw)])], true));
                    break;
            }
        }

        return (cutters, failures);
    }

    /// <summary>Groups cutters with the same direction and mode whose (extended) t ranges overlap; they are meshed together.</summary>
    public static List<List<Cutter>> Group(IReadOnlyList<Cutter> cutters, SolidOptions options)
    {
        var parent = Enumerable.Range(0, cutters.Count).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        double m = 2 * options.SeamMarginMm;
        for (int i = 0; i < cutters.Count; i++)
        {
            for (int j = i + 1; j < cutters.Count; j++)
            {
                bool same = cutters[i].Mode == cutters[j].Mode && cutters[i].N.Dot(cutters[j].N) > 1 - Tolerance.SameDirection;
                if (same && cutters[i].TMin - m < cutters[j].TMax && cutters[j].TMin - m < cutters[i].TMax)
                {
                    parent[Find(j)] = Find(i);
                }
            }
        }

        return Enumerable.Range(0, cutters.Count).GroupBy(Find).OrderBy(g => g.Min())
            .Select(g => g.OrderBy(i => i).Select(i => cutters[i]).ToList()).ToList();
    }
}
