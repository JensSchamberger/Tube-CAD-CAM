using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

public enum SolidFaceKind
{
    /// <summary>Outer lateral surface (loop 0).</summary>
    OuterSurface,

    /// <summary>Inner lateral surface of a hollow section (loops ≥ 1).</summary>
    InnerSurface,

    /// <summary>Planar end face (possibly mitred or notched).</summary>
    EndFace,

    /// <summary>Wall of an opening created by a feature (<see cref="FaceTag.FeatureId"/> identifies it).</summary>
    FeatureWall,
}

/// <summary>Provenance of one triangle of the solid.</summary>
/// <param name="LoopIndex">Section loop of the surface (0 = outer, ≥ 1 = voids); for walls the loop of the upper facet; -1 for end faces.</param>
public readonly record struct FaceTag(SolidFaceKind Kind, int LoopIndex, Guid? FeatureId, TubeEnd? End);

public enum EdgeSide { Upper, Lower }

/// <summary>
/// Cut edge of a feature: the boundary between the feature's wall and a lateral surface. <see cref="Side"/> is relative to the
/// cutting direction (Upper = side facing the cutting direction). Points are ordered; closed loops repeat no point.
/// </summary>
public sealed record FeatureEdge(Guid FeatureId, EdgeSide Side, int LoopIndex, IReadOnlyList<Vector3> Points, bool IsClosed);

/// <summary>A feature that could not be subtracted from the body. The body does NOT contain this feature.</summary>
public sealed record FeatureFailure(Guid FeatureId, string Code, string Message);

/// <summary>
/// Actual solid body of a tube after all successful features were subtracted, with provenance for downstream (CAM) use.
/// <see cref="TriangleTags"/> has one entry per triangle of <see cref="Mesh"/>.
/// </summary>
public sealed record TubeSolid(
    TriangleMesh Mesh,
    IReadOnlyList<FaceTag> TriangleTags,
    IReadOnlyList<FeatureEdge> Edges,
    IReadOnlyList<Guid> AppliedFeatures,
    IReadOnlyList<FeatureFailure> Failures,
    MeshTopologyReport Topology)
{
    public IEnumerable<int> TrianglesOf(Guid featureId) =>
        Enumerable.Range(0, TriangleTags.Count).Where(i => TriangleTags[i].FeatureId == featureId);

    /// <summary>
    /// Copies the given triangles into a separate mesh, optionally pushed along their normals (used to highlight the wall
    /// of a feature on top of the body without depth-fighting).
    /// </summary>
    public TriangleMesh ExtractMesh(IEnumerable<int> triangles, double normalOffsetMm = 0)
    {
        var pos = new List<Vector3>();
        var nrm = new List<Vector3>();
        var idx = new List<int>();
        foreach (int t in triangles)
        {
            for (int k = 0; k < 3; k++)
            {
                int i = Mesh.Indices[(3 * t) + k];
                pos.Add(Mesh.Positions[i] + (Mesh.Normals[i] * normalOffsetMm));
                nrm.Add(Mesh.Normals[i]);
                idx.Add(pos.Count - 1);
            }
        }

        return new TriangleMesh(pos, nrm, idx);
    }

    public double AreaOf(Guid featureId) => TrianglesOf(featureId).Sum(TriangleArea);

    public double TriangleArea(int t)
    {
        Vector3 a = Mesh.Positions[Mesh.Indices[3 * t]], b = Mesh.Positions[Mesh.Indices[(3 * t) + 1]], c = Mesh.Positions[Mesh.Indices[(3 * t) + 2]];
        return (b - a).Cross(c - a).Length / 2;
    }
}

/// <summary>Modelling options of the solid builder (geometric tolerances live in <see cref="Tolerance"/>).</summary>
public sealed record SolidOptions
{
    /// <summary>Width of the extra strip around a cut region that is re-meshed together with it.</summary>
    public double SeamMarginMm { get; init; } = 0.5;

    /// <summary>Smallest allowed seam margin; narrower situations (profile edge too close to the cut) fail explicitly.</summary>
    public double MinSeamMarginMm { get; init; } = 1e-4;

    /// <summary>A (non end-) feature closer to an end face than this is rejected (the web would be thinner than the tolerance allows).</summary>
    public double EndClearanceMm { get; init; } = 1e-3;

    /// <summary>How far the end-face cut regions reach beyond the wall surfaces along the cutting direction.</summary>
    public double CapCutOvershootMm { get; init; } = 1e-3;

    /// <summary>How far notch/slit cutters reach beyond the end face.</summary>
    public double EndOvershootMm { get; init; } = 1.0;

    /// <summary>Faces deviating less than this angle are shaded smoothly in the render mesh.</summary>
    public double CreaseDeg { get; init; } = 30;

    public static SolidOptions Default { get; } = new();
}
