namespace TubeCadCam.Geometry;

/// <summary>Triangulation result. <see cref="Vertices"/> = outer vertices followed by each hole's vertices (original order); indices refer to it.</summary>
public sealed record Triangulation(IReadOnlyList<Vector2> Vertices, IReadOnlyList<int> Indices)
{
    public double Area
    {
        get
        {
            double a = 0;
            for (int i = 0; i < Indices.Count; i += 3)
            {
                a += (Vertices[Indices[i + 1]] - Vertices[Indices[i]]).Cross(Vertices[Indices[i + 2]] - Vertices[Indices[i]]) / 2;
            }

            return a;
        }
    }
}

/// <summary>
/// Deterministic ear-clipping triangulation of a simple polygon with optional holes (holes are joined by bridges).
/// Triangles are emitted counter-clockwise. Degenerate (zero-area) ears are emitted so that boundary vertices
/// stay connected, i.e. the triangulation never creates T-junctions with an extruded wall.
/// </summary>
public static class PolygonTriangulator
{
    private const double Eps = 1e-12;

    public static Triangulation Triangulate(Polygon2 outer, IReadOnlyList<Polygon2> holes)
    {
        var verts = new List<Vector2>(outer.Vertices);
        var outerIdx = Enumerable.Range(0, verts.Count).ToList();
        if (!outer.IsCounterClockwise)
        {
            outerIdx.Reverse();
        }

        var holeLoops = new List<List<int>>();
        foreach (Polygon2 h in holes)
        {
            int start = verts.Count;
            verts.AddRange(h.Vertices);
            var idx = Enumerable.Range(start, h.Vertices.Count).ToList();
            if (h.IsCounterClockwise)
            {
                idx.Reverse(); // holes must be clockwise
            }

            holeLoops.Add(idx);
        }

        List<int> poly = outerIdx;
        // Bridge holes in order of decreasing maximal x so that earlier bridges do not block later ones.
        foreach (List<int> hole in holeLoops.OrderByDescending(l => l.Max(i => verts[i].X)).ThenBy(l => l[0]))
        {
            poly = Bridge(verts, poly, hole, holeLoops);
        }

        return new Triangulation(verts, EarClip(verts, poly));
    }

    private static List<int> Bridge(List<Vector2> v, List<int> poly, List<int> hole, List<List<int>> allHoles)
    {
        // candidate hole vertices: rightmost first
        foreach (int m in hole.OrderByDescending(i => v[i].X).ThenBy(i => i))
        {
            foreach (int pi in Enumerable.Range(0, poly.Count)
                         .OrderBy(k => v[poly[k]].DistanceTo(v[m])).ThenBy(k => k))
            {
                int p = poly[pi];
                if (!IsVisible(v, poly, allHoles, hole, v[m], m, v[p], p))
                {
                    continue;
                }

                int hm = hole.IndexOf(m);
                var merged = new List<int>(poly.Count + hole.Count + 2);
                for (int k = 0; k <= pi; k++)
                {
                    merged.Add(poly[k]);
                }

                for (int k = 0; k <= hole.Count; k++)
                {
                    merged.Add(hole[(hm + k) % hole.Count]);
                }

                merged.Add(p);
                for (int k = pi + 1; k < poly.Count; k++)
                {
                    merged.Add(poly[k]);
                }

                return merged;
            }
        }

        throw new InvalidOperationException("Could not connect a hole to the outline (holes must lie inside the outer polygon and must not intersect it).");
    }

    private static bool IsVisible(List<Vector2> v, List<int> poly, List<List<int>> allHoles, List<int> thisHole, Vector2 a, int ai, Vector2 b, int bi)
    {
        if (a.DistanceTo(b) <= Eps)
        {
            return false;
        }

        // The bridge must not cross any current polygon edge or any (not yet merged) hole edge.
        foreach (IReadOnlyList<int> loop in new[] { poly }.Concat(allHoles.Where(h => !ReferenceEquals(h, thisHole))).Prepend(thisHole))
        {
            for (int k = 0; k < loop.Count; k++)
            {
                int c = loop[k], d = loop[(k + 1) % loop.Count];
                if (c == ai || d == ai || c == bi || d == bi)
                {
                    continue;
                }

                if (Polygon2.SegmentsIntersect(a, b, v[c], v[d]))
                {
                    return false;
                }
            }
        }

        // midpoint must lie in the material: inside outer boundary and outside every hole
        Vector2 mid = new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        return Inside(v, poly, mid);
    }

    private static bool Inside(List<Vector2> v, List<int> loop, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
        {
            Vector2 a = v[loop[i]], b = v[loop[j]];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < ((b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static List<int> EarClip(List<Vector2> v, List<int> poly)
    {
        var result = new List<int>();
        var rem = new List<int>(poly);
        int guard = 0;
        while (rem.Count > 3)
        {
            int n = rem.Count;
            int found = -1;
            for (int i = 0; i < n && found < 0; i++)
            {
                if (IsEar(v, rem, i))
                {
                    found = i;
                }
            }

            if (found < 0)
            {
                // Fallback for numerically degenerate input: clip the least reflex vertex.
                double best = double.MinValue;
                for (int i = 0; i < n; i++)
                {
                    double c = Cross(v, rem, i);
                    if (c > best)
                    {
                        best = c;
                        found = i;
                    }
                }

                if (++guard > poly.Count * 2 || best < -1e-9)
                {
                    throw new InvalidOperationException("Triangulation failed (self-intersecting or degenerate polygon).");
                }
            }

            int prev = rem[(found + n - 1) % n], cur = rem[found], next = rem[(found + 1) % n];
            result.Add(prev);
            result.Add(cur);
            result.Add(next);
            rem.RemoveAt(found);
        }

        result.AddRange(rem);
        return result;
    }

    private static double Cross(List<Vector2> v, List<int> rem, int i)
    {
        int n = rem.Count;
        Vector2 a = v[rem[(i + n - 1) % n]], b = v[rem[i]], c = v[rem[(i + 1) % n]];
        return (b - a).Cross(c - b);
    }

    private static bool IsEar(List<Vector2> v, List<int> rem, int i)
    {
        int n = rem.Count;
        Vector2 a = v[rem[(i + n - 1) % n]], b = v[rem[i]], c = v[rem[(i + 1) % n]];
        if ((b - a).Cross(c - b) < -Eps)
        {
            return false; // reflex
        }

        for (int k = 0; k < n; k++)
        {
            Vector2 p = v[rem[k]];
            if (p == a || p == b || p == c)
            {
                continue; // coincident bridge copies
            }

            if (PointInTriangle(p, a, b, c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        // Inclusive test: a vertex lying on the ear's diagonal would make the remaining polygon touch itself.
        double area = (b - a).Cross(c - a);
        if (Math.Abs(area) <= Eps)
        {
            // collinear ear: blocked only by a vertex on the segment spanned by the three points
            double minX = Math.Min(a.X, Math.Min(b.X, c.X)), maxX = Math.Max(a.X, Math.Max(b.X, c.X));
            double minY = Math.Min(a.Y, Math.Min(b.Y, c.Y)), maxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
            return Math.Abs((b - a).Cross(p - a)) <= Eps && p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY;
        }

        double d1 = (b - a).Cross(p - a), d2 = (c - b).Cross(p - b), d3 = (a - c).Cross(p - c);
        return d1 >= -Eps && d2 >= -Eps && d3 >= -Eps;
    }
}
