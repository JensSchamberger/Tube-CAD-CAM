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
