namespace TubeCadCam.Geometry;

/// <summary>Extrudes a planar polygon between two levels through a caller-supplied mapping. Flat shaded; orientation is made outward-facing automatically.</summary>
public static class PrismBuilder
{
    public static TriangleMesh Build(Polygon2 polygon, Func<Vector2, double, Vector3> map, double level0, double level1)
    {
        IReadOnlyList<Vector2> v = polygon.AsCounterClockwise().Vertices;
        int n = v.Count;
        var tris = new List<(Vector3 A, Vector3 B, Vector3 C)>();
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector3 b0 = map(v[i], level0), b1 = map(v[j], level0), t0 = map(v[i], level1), t1 = map(v[j], level1);
            tris.Add((b0, b1, t1));
            tris.Add((b0, t1, t0));
        }

        Triangulation tri = PolygonTriangulator.Triangulate(new Polygon2(v), []);
        for (int i = 0; i < tri.Indices.Count; i += 3)
        {
            Vector2 a = tri.Vertices[tri.Indices[i]], b = tri.Vertices[tri.Indices[i + 1]], c = tri.Vertices[tri.Indices[i + 2]];
            tris.Add((map(a, level0), map(c, level0), map(b, level0)));   // bottom: reversed
            tris.Add((map(a, level1), map(b, level1), map(c, level1)));   // top
        }

        double volume = tris.Sum(t => t.A.Dot(t.B.Cross(t.C))) / 6.0;
        bool flip = volume < 0;
        var mb = new MeshBuilder();
        foreach ((Vector3 A, Vector3 B, Vector3 C) in tris)
        {
            (Vector3 p0, Vector3 p1, Vector3 p2) = flip ? (A, C, B) : (A, B, C);
            Vector3 nrm = (p1 - p0).Cross(p2 - p0).Normalized();
            mb.AddTriangle(mb.AddVertex(p0, nrm), mb.AddVertex(p1, nrm), mb.AddVertex(p2, nrm));
        }

        return mb.ToMesh();
    }
}
