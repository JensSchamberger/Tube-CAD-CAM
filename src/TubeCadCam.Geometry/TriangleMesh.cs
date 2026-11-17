namespace TubeCadCam.Geometry;

/// <summary>Indexed triangle mesh with per-vertex normals. Triangles are counter-clockwise when seen from outside.</summary>
public sealed class TriangleMesh(IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> normals, IReadOnlyList<int> indices)
{
    public static TriangleMesh Empty { get; } = new([], [], []);

    public IReadOnlyList<Vector3> Positions { get; } = positions;
    public IReadOnlyList<Vector3> Normals { get; } = normals;
    public IReadOnlyList<int> Indices { get; } = indices;
    public int TriangleCount => Indices.Count / 3;

    public Bounds3 Bounds => Bounds3.Of(Positions);

    /// <summary>Enclosed volume via the divergence theorem (meaningful for closed, consistently oriented meshes).</summary>
    public double SignedVolume()
    {
        double v = 0;
        for (int i = 0; i < Indices.Count; i += 3)
        {
            Vector3 a = Positions[Indices[i]], b = Positions[Indices[i + 1]], c = Positions[Indices[i + 2]];
            v += a.Dot(b.Cross(c));
        }

        return v / 6.0;
    }
}

public sealed class MeshBuilder
{
    private readonly List<Vector3> _positions = [];
    private readonly List<Vector3> _normals = [];
    private readonly List<int> _indices = [];

    public int AddVertex(Vector3 position, Vector3 normal)
    {
        _positions.Add(position);
        _normals.Add(normal);
        return _positions.Count - 1;
    }

    public void AddTriangle(int a, int b, int c)
    {
        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
    }

    /// <summary>Adds a quad (a,b,c,d counter-clockwise from outside) as two triangles.</summary>
    public void AddQuad(int a, int b, int c, int d)
    {
        AddTriangle(a, b, c);
        AddTriangle(a, c, d);
    }

    public TriangleMesh ToMesh() => new(_positions.ToArray(), _normals.ToArray(), _indices.ToArray());
}
