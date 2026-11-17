namespace TubeCadCam.Geometry;

public static class MeshSmoothing
{
    /// <summary>
    /// Builds a render mesh (one vertex per triangle corner) whose normals are the area-weighted average of all faces at the
    /// vertex that deviate less than <paramref name="creaseDeg"/> from the corner's own face. Triangle order is preserved.
    /// </summary>
    public static TriangleMesh WithCreaseNormals(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices, double creaseDeg)
    {
        int tris = indices.Count / 3;
        var faceNormal = new Vector3[tris];
        var faceArea = new double[tris];
        var incident = new List<int>[positions.Count];
        for (int t = 0; t < tris; t++)
        {
            Vector3 a = positions[indices[3 * t]], b = positions[indices[(3 * t) + 1]], c = positions[indices[(3 * t) + 2]];
            Vector3 n = (b - a).Cross(c - a);
            faceArea[t] = n.Length / 2;
            faceNormal[t] = n.Normalized();
            for (int k = 0; k < 3; k++)
            {
                (incident[indices[(3 * t) + k]] ??= []).Add(t);
            }
        }

        double cos = Math.Cos(creaseDeg * Math.PI / 180.0);
        var outPos = new Vector3[tris * 3];
        var outNrm = new Vector3[tris * 3];
        var outIdx = new int[tris * 3];
        for (int t = 0; t < tris; t++)
        {
            for (int k = 0; k < 3; k++)
            {
                int v = indices[(3 * t) + k];
                Vector3 sum = Vector3.Zero;
                foreach (int u in incident[v])
                {
                    if (faceNormal[t].Dot(faceNormal[u]) >= cos)
                    {
                        sum += faceNormal[u] * faceArea[u];
                    }
                }

                int o = (3 * t) + k;
                outPos[o] = positions[v];
                outNrm[o] = sum.Length > 0 ? sum.Normalized() : faceNormal[t];
                outIdx[o] = o;
            }
        }

        return new TriangleMesh(outPos, outNrm, outIdx);
    }
}
