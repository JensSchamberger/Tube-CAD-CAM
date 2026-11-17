using TubeCadCam.Geometry;

namespace TubeCadCam.Tests;

internal static class MeshTestHelpers
{
    private static (long, long, long) Key(Vector3 p) =>
        ((long)Math.Round(p.X * 1e5), (long)Math.Round(p.Y * 1e5), (long)Math.Round(p.Z * 1e5));

    /// <summary>Closed and consistently oriented: every directed edge occurs once and its reverse occurs once.</summary>
    public static void AssertWatertight(TriangleMesh m)
    {
        var directed = new Dictionary<((long, long, long), (long, long, long)), int>();
        for (int i = 0; i < m.Indices.Count; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                var a = Key(m.Positions[m.Indices[i + k]]);
                var b = Key(m.Positions[m.Indices[i + ((k + 1) % 3)]]);
                directed[(a, b)] = directed.GetValueOrDefault((a, b)) + 1;
            }
        }

        foreach (var ((a, b), count) in directed)
        {
            Assert.True(count == 1, $"directed edge used {count}x");
            Assert.True(directed.GetValueOrDefault((b, a)) == 1, "missing opposite edge (open or inconsistently oriented mesh)");
        }
    }

    /// <summary>Stored normals agree with the triangle winding (front face = outside).</summary>
    public static void AssertNormalsMatchWinding(TriangleMesh m)
    {
        for (int i = 0; i < m.Indices.Count; i += 3)
        {
            Vector3 a = m.Positions[m.Indices[i]], b = m.Positions[m.Indices[i + 1]], c = m.Positions[m.Indices[i + 2]];
            Vector3 geo = (b - a).Cross(c - a);
            if (geo.Length < 1e-12)
            {
                continue; // degenerate
            }

            Vector3 n = m.Normals[m.Indices[i]] + m.Normals[m.Indices[i + 1]] + m.Normals[m.Indices[i + 2]];
            Assert.True(geo.Dot(n) > 0, "normal contradicts winding");
        }
    }
}

internal static class SolidTestHelpers
{
    /// <summary>Parity ray casting along an irrational direction (robust against hitting edges exactly).</summary>
    public static bool Inside(TriangleMesh mesh, Vector3 p)
    {
        Vector3 d = new Vector3(0.5773502691896258, 0.3123456789, 0.7541234567).Normalized();
        int hits = 0;
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            Vector3 a = mesh.Positions[mesh.Indices[i]], b = mesh.Positions[mesh.Indices[i + 1]], c = mesh.Positions[mesh.Indices[i + 2]];
            Vector3 e1 = b - a, e2 = c - a, h = d.Cross(e2);
            double det = e1.Dot(h);
            if (Math.Abs(det) < 1e-14)
            {
                continue;
            }

            Vector3 s = p - a;
            double u = s.Dot(h) / det;
            if (u < 0 || u > 1)
            {
                continue;
            }

            Vector3 q = s.Cross(e1);
            double v = d.Dot(q) / det;
            if (v < 0 || u + v > 1)
            {
                continue;
            }

            if (e2.Dot(q) / det > 1e-12)
            {
                hits++;
            }
        }

        return hits % 2 == 1;
    }

    public static TubeCadCam.Cad.TubeSolid Solid(TubeCadCam.Cad.TubeModel m) => TubeCadCam.Cad.TubeSceneBuilder.Build(m).Value.Solid;

    public static double BaseVolume(TubeCadCam.Cad.TubeModel m) => Solid(m with { Features = [] }).Topology.Volume;

    public static void AssertValid(TubeCadCam.Cad.TubeSolid s)
    {
        Assert.True(s.Topology.IsValidSolid, s.Topology.ToString());
        MeshTopologyReport again = MeshTopology.Analyze(s.Mesh);   // independent re-check on the final render mesh
        Assert.True(again.IsValidSolid, again.ToString());
        Assert.Equal(s.Topology.Volume, again.Volume, Math.Abs(s.Topology.Volume) * 1e-9);
        Assert.Equal(s.Mesh.TriangleCount, s.TriangleTags.Count);
    }

    public static double EndFaceArea(TubeCadCam.Cad.TubeSolid s, TubeCadCam.Cad.TubeEnd end) =>
        Enumerable.Range(0, s.TriangleTags.Count).Where(t => s.TriangleTags[t].Kind == TubeCadCam.Cad.SolidFaceKind.EndFace && s.TriangleTags[t].End == end).Sum(s.TriangleArea);
}
