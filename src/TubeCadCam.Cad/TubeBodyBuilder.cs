using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

/// <summary>
/// Builds the closed 3D body of a tube: outer wall, inner wall(s) and the two end faces (planar, optionally mitred).
/// Deterministic; features other than miters are not subtracted (see <see cref="TubeSceneBuilder"/>).
/// </summary>
public static class TubeBodyBuilder
{
    /// <summary>Loop vertices whose neighbouring facets turn less than this are shaded smoothly.</summary>
    private const double SmoothTurnDeg = 30;

    public static TriangleMesh Build(ProfileSection section, EndPlane start, EndPlane end)
    {
        var mb = new MeshBuilder();
        AddWall(mb, section.Outer.AsCounterClockwise().Vertices, start, end);
        foreach (Polygon2 v in section.Voids)
        {
            // Void loops run clockwise so that the same formula yields normals pointing into the void.
            AddWall(mb, (v.IsCounterClockwise ? v.Vertices.Reverse().ToArray() : v.Vertices), start, end);
        }

        Triangulation tri = PolygonTriangulator.Triangulate(section.Outer, section.Voids);
        AddCap(mb, tri, start, flip: true, outwardSign: -1);
        AddCap(mb, tri, end, flip: false, outwardSign: 1);
        return mb.ToMesh();
    }

    private static void AddWall(MeshBuilder mb, IReadOnlyList<Vector2> loop, EndPlane start, EndPlane end)
    {
        int n = loop.Count;
        Vector3 SegNormal(int i)
        {
            Vector2 e = loop[(i + 1) % n] - loop[i];
            Vector2 t = e.Normalized();
            return new Vector3(0, t.Y, -t.X);
        }

        Vector3 VertexNormal(int vertex, int segBefore, int segAfter, int useSeg)
        {
            Vector3 a = SegNormal(segBefore), b = SegNormal(segAfter);
            double turn = Math.Acos(Math.Clamp(a.Dot(b), -1, 1)) * 180.0 / Math.PI;
            return turn < SmoothTurnDeg ? (a + b).Normalized() : SegNormal(useSeg);
        }

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector2 a = loop[i], b = loop[j];
            Vector3 nA = VertexNormal(i, (i + n - 1) % n, i, i);
            Vector3 nB = VertexNormal(j, i, j, i);
            int a0 = mb.AddVertex(new Vector3(start.XAt(a), a.X, a.Y), nA);
            int a1 = mb.AddVertex(new Vector3(end.XAt(a), a.X, a.Y), nA);
            int b0 = mb.AddVertex(new Vector3(start.XAt(b), b.X, b.Y), nB);
            int b1 = mb.AddVertex(new Vector3(end.XAt(b), b.X, b.Y), nB);
            mb.AddQuad(a0, b0, b1, a1);
        }
    }

    private static void AddCap(MeshBuilder mb, Triangulation tri, EndPlane plane, bool flip, int outwardSign)
    {
        // plane: F = x - g·p - X0 = 0, gradient (1, -g); outward = outwardSign * gradient
        Vector3 normal = new Vector3(1, -plane.Gradient.X, -plane.Gradient.Y).Normalized() * outwardSign;
        var map = new int[tri.Vertices.Count];
        for (int k = 0; k < map.Length; k++)
        {
            Vector2 p = tri.Vertices[k];
            map[k] = mb.AddVertex(new Vector3(plane.XAt(p), p.X, p.Y), normal);
        }

        for (int i = 0; i < tri.Indices.Count; i += 3)
        {
            int a = map[tri.Indices[i]], b = map[tri.Indices[i + 1]], c = map[tri.Indices[i + 2]];
            if (flip)
            {
                mb.AddTriangle(a, c, b);
            }
            else
            {
                mb.AddTriangle(a, b, c);
            }
        }
    }
}
