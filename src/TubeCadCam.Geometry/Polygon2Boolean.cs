using Clipper2Lib;

namespace TubeCadCam.Geometry;

/// <summary>A polygon with holes (outer counter-clockwise, holes clockwise).</summary>
public sealed record PolygonRegion(Polygon2 Outer, IReadOnlyList<Polygon2> Holes)
{
    public double Area => Outer.Area - Holes.Sum(h => h.Area);
}

/// <summary>
/// Robust 2D boolean operations on the <see cref="Tolerance.GridDecimals"/> grid, implemented with Clipper2
/// (integer arithmetic, Boost licence). Results are clean rings without collinear vertices or spikes; vertices that must
/// survive on an edge are re-inserted with <see cref="InsertOnEdgePoints"/>.
/// Subjects keep their given orientation (outer ccw / holes cw); clip polygons are normalised to ccw (union semantics).
/// </summary>
public static class Polygon2Boolean
{
    public static IReadOnlyList<PolygonRegion> Difference(IEnumerable<Polygon2> subject, IEnumerable<Polygon2> clip) =>
        Execute(ClipType.Difference, subject, clip);

    public static IReadOnlyList<PolygonRegion> Intersection(IEnumerable<Polygon2> subject, IEnumerable<Polygon2> clip) =>
        Execute(ClipType.Intersection, subject, clip);

    /// <summary>Union of ccw-normalised polygons.</summary>
    public static IReadOnlyList<PolygonRegion> Union(IEnumerable<Polygon2> polygons) =>
        Execute(ClipType.Union, polygons.Select(p => p.AsCounterClockwise()), []);

    private static IReadOnlyList<PolygonRegion> Execute(ClipType type, IEnumerable<Polygon2> subject, IEnumerable<Polygon2> clip)
    {
        var c = new ClipperD(Tolerance.GridDecimals);
        foreach (Polygon2 s in subject)
        {
            c.AddPath(ToPath(s), PathType.Subject);
        }

        foreach (Polygon2 p in clip)
        {
            c.AddPath(ToPath(p.AsCounterClockwise()), PathType.Clip);
        }

        var tree = new PolyTreeD();
        c.Execute(type, FillRule.NonZero, tree);
        var result = new List<PolygonRegion>();
        Collect(tree, result);
        return result;
    }

    /// <summary>
    /// Returns the ring with every point of <paramref name="points"/> that lies on one of its edges (within
    /// <paramref name="tolerance"/>, not at its end points) inserted in order along that edge.
    /// </summary>
    public static Polygon2 InsertOnEdgePoints(Polygon2 ring, IReadOnlyList<Vector2> points, double tolerance)
    {
        var result = new List<Vector2>();
        IReadOnlyList<Vector2> v = ring.Vertices;
        for (int i = 0; i < v.Count; i++)
        {
            Vector2 a = v[i], b = v[(i + 1) % v.Count];
            result.Add(a);
            Vector2 ab = b - a;
            double l2 = ab.Dot(ab);
            if (l2 <= 0)
            {
                continue;
            }

            var onEdge = new List<(double S, Vector2 P)>();
            foreach (Vector2 p in points)
            {
                double s = (p - a).Dot(ab) / l2;
                if (s <= 0 || s >= 1 || (p - a).Length <= tolerance || (p - b).Length <= tolerance)
                {
                    continue;
                }

                if ((p - (a + (ab * s))).Length <= tolerance)
                {
                    onEdge.Add((s, p));
                }
            }

            result.AddRange(onEdge.OrderBy(e => e.S).Select(e => e.P));
        }

        return new Polygon2(result);
    }

    private static PathD ToPath(Polygon2 p) => new(p.Vertices.Select(v => new PointD(v.X, v.Y)));

    private static void Collect(PolyPathD node, List<PolygonRegion> result)
    {
        for (int i = 0; i < node.Count; i++)
        {
            PolyPathD child = node[i];
            if (child.IsHole)
            {
                Collect(child, result); // islands inside holes
                continue;
            }

            Polygon2? outer = ToPolygon(child.Polygon);
            var holes = new List<Polygon2>();
            for (int j = 0; j < child.Count; j++)
            {
                PolyPathD hole = child[j];
                if (ToPolygon(hole.Polygon) is { } h)
                {
                    holes.Add(h);
                }

                Collect(hole, result);
            }

            if (outer is not null)
            {
                result.Add(new PolygonRegion(outer, holes));
            }
        }
    }

    private static Polygon2? ToPolygon(PathD? path) =>
        path is { Count: >= 3 } ? new Polygon2(path.Select(p => new Vector2(p.x, p.y)).ToArray()) : null;
}
