using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

/// <summary>Cutter volume of one feature for display. This is a visualisation aid, not a boolean subtraction from the body.</summary>
public sealed record FeatureShape(Guid FeatureId, TriangleMesh Mesh);

public sealed record TubeScene(
    ProfileSection Section,
    TriangleMesh Body,
    IReadOnlyList<FeatureShape> Features,
    IReadOnlyList<Guid> SkippedFeatures,
    Bounds3 Bounds);

/// <summary>Creates the display geometry of a tube model. Pure CAD: no CAM or machine data involved.</summary>
public static class TubeSceneBuilder
{
    public static Result<TubeScene> Build(TubeModel model)
    {
        IReadOnlyList<ValidationIssue> partIssues = model.Part.Validate();
        if (partIssues.Any(i => i.Severity == IssueSeverity.Error))
        {
            return Result<TubeScene>.Failure(partIssues);
        }

        ProfileDefinition profile = model.Part.Profile;
        ProfileSection section = ProfileSectionBuilder.Build(profile);
        var surface = new SectionSurface(section.Outer);
        double length = model.Part.LengthMm;
        (EndPlane start, EndPlane end) = EndPlane.For(model, section.Outer);
        TriangleMesh body = TubeBodyBuilder.Build(section, start, end);

        var shapes = new List<FeatureShape>();
        var skipped = new List<Guid>();
        double wall = Math.Max(profile.NominalWallThicknessMm, 0.1);
        Bounds2 sb = section.Outer.Bounds;
        double reach = Math.Sqrt((sb.Width * sb.Width) + (sb.Height * sb.Height));

        foreach (TubeFeature f in model.Features)
        {
            if (f.ValidateParameters().Any(i => i.Severity == IssueSeverity.Error))
            {
                skipped.Add(f.Id);
                continue;
            }

            switch (f)
            {
                case IWallCut cut:
                    SurfacePoint? p = surface.Locate(cut.AngleDeg);
                    if (p is null)
                    {
                        skipped.Add(f.Id);
                        break;
                    }

                    double cx = cut.AxialCenter(length);
                    Vector2 pos = p.Position, t = p.Tangent, nrm = p.Normal;
                    Vector3 Map(Vector2 uv, double h) => new(cx + uv.X, pos.X + (t.X * uv.Y) + (nrm.X * h), pos.Y + (t.Y * uv.Y) + (nrm.Y * h));
                    shapes.Add(new FeatureShape(f.Id, PrismBuilder.Build(cut.Outline(48, 8), Map, -1.5 * wall, 0.5 * wall)));
                    break;

                case SlitFeature slit:
                    Vector2 d = TubeCoordinateSystem.Direction(slit.AngleDeg);
                    Vector2 perp = new(-d.Y, d.X);
                    double half = reach * 0.75;
                    var rect = new Polygon2([
                        (d * -half) + (perp * (-slit.WidthMm / 2)), (d * half) + (perp * (-slit.WidthMm / 2)),
                        (d * half) + (perp * (slit.WidthMm / 2)), (d * -half) + (perp * (slit.WidthMm / 2)),
                    ]);
                    (double x0, double x1) = slit.End == TubeEnd.Start ? (-1.0, slit.DepthMm) : (length - slit.DepthMm, length + 1.0);
                    shapes.Add(new FeatureShape(f.Id, PrismBuilder.Build(rect, (q, x) => new Vector3(x, q.X, q.Y), x0, x1)));
                    break;

                case EndMiterFeature:
                    break; // part of the body geometry

                default:
                    skipped.Add(f.Id);
                    break;
            }
        }

        IEnumerable<Vector3> all = body.Positions.Concat(shapes.SelectMany(s => s.Mesh.Positions));
        return Result<TubeScene>.Success(new TubeScene(section, body, shapes, skipped, Bounds3.Of(all)));
    }
}
