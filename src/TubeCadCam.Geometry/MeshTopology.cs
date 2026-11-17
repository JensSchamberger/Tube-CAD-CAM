namespace TubeCadCam.Geometry;

/// <summary>Result of a topology/quality analysis of an indexed triangle mesh.</summary>
public sealed record MeshTopologyReport(
    int Vertices,
    int Triangles,
    int OpenEdges,
    int NonManifoldEdges,
    int OrientationConflicts,
    int DegenerateTriangles,
    int DuplicateTriangles,
    double Volume)
{
    /// <summary>Closed, consistently oriented 2-manifold without degenerate or duplicate triangles and with positive volume.</summary>
    public bool IsValidSolid =>
        OpenEdges == 0 && NonManifoldEdges == 0 && OrientationConflicts == 0 && DegenerateTriangles == 0 && DuplicateTriangles == 0 && Volume > 0;

    public override string ToString() =>
        $"V={Vertices} T={Triangles} open={OpenEdges} nonManifold={NonManifoldEdges} orientation={OrientationConflicts} degenerate={DegenerateTriangles} duplicate={DuplicateTriangles} volume={Volume:0.###}";
}

/// <summary>Vertex welding and topology analysis (the single place that defines "valid mesh topology").</summary>
public static class MeshTopology
{
    /// <summary>Merges vertices closer than <paramref name="tolerance"/> (first occurrence wins; deterministic).</summary>
    public static (Vector3[] Positions, int[] Map) Weld(IReadOnlyList<Vector3> positions, double tolerance = Tolerance.Weld)
    {
        var unique = new List<Vector3>();
        var map = new int[positions.Count];
        var cells = new Dictionary<(long, long, long), List<int>>();
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 p = positions[i];
            (long cx, long cy, long cz) = ((long)Math.Floor(p.X / tolerance), (long)Math.Floor(p.Y / tolerance), (long)Math.Floor(p.Z / tolerance));
            int found = -1;
            for (long dx = -1; dx <= 1 && found < 0; dx++)
            {
                for (long dy = -1; dy <= 1 && found < 0; dy++)
                {
                    for (long dz = -1; dz <= 1 && found < 0; dz++)
                    {
                        if (!cells.TryGetValue((cx + dx, cy + dy, cz + dz), out List<int>? list))
                        {
                            continue;
                        }

                        foreach (int u in list)
                        {
                            if ((unique[u] - p).Length <= tolerance)
                            {
                                found = u;
                                break;
                            }
                        }
                    }
                }
            }

            if (found < 0)
            {
                found = unique.Count;
                unique.Add(p);
                if (!cells.TryGetValue((cx, cy, cz), out List<int>? own))
                {
                    cells[(cx, cy, cz)] = own = [];
                }

                own.Add(found);
            }

            map[i] = found;
        }

        return (unique.ToArray(), map);
    }

    public static MeshTopologyReport Analyze(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices)
    {
        var directed = new Dictionary<(int, int), int>();
        var seen = new HashSet<(int, int, int)>();
        int degenerate = 0, duplicate = 0;
        double volume = 0;
        for (int i = 0; i < indices.Count; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            Vector3 pa = positions[a], pb = positions[b], pc = positions[c];
            double area2 = (pb - pa).Cross(pc - pa).Length;
            if (a == b || b == c || a == c || area2 / 2 < Tolerance.MinTriangleArea)
            {
                degenerate++;
                continue;
            }

            int[] sorted = [a, b, c];
            Array.Sort(sorted);
            if (!seen.Add((sorted[0], sorted[1], sorted[2])))
            {
                duplicate++;
            }

            volume += pa.Dot(pb.Cross(pc)) / 6.0;
            foreach ((int u, int v) in new[] { (a, b), (b, c), (c, a) })
            {
                directed[(u, v)] = directed.GetValueOrDefault((u, v)) + 1;
            }
        }

        int open = 0, nonManifold = 0, orientation = 0;
        foreach (((int u, int v), int n) in directed)
        {
            if (u > v && directed.ContainsKey((v, u)))
            {
                continue; // counted with its (v,u) partner
            }

            int reverse = directed.GetValueOrDefault((v, u));
            int total = n + reverse;
            if (total == 1)
            {
                open++;
            }
            else if (total > 2)
            {
                nonManifold++;
            }
            else if (n == 2 || reverse == 2)
            {
                orientation++;
            }
        }

        return new MeshTopologyReport(positions.Count, indices.Count / 3, open, nonManifold, orientation, degenerate, duplicate, volume);
    }

    /// <summary>Welds the mesh positions first (tolerance <see cref="Tolerance.Weld"/>), then analyses.</summary>
    public static MeshTopologyReport Analyze(TriangleMesh mesh)
    {
        (Vector3[] positions, int[] map) = Weld(mesh.Positions);
        return Analyze(positions, mesh.Indices.Select(i => map[i]).ToArray());
    }
}
