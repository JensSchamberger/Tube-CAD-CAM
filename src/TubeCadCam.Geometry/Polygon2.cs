namespace TubeCadCam.Geometry;

/// <summary>Closed planar polygon (the closing edge from last to first vertex is implicit).</summary>
public sealed record Polygon2
{
    public Polygon2(IReadOnlyList<Vector2> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        if (vertices.Count < 3)
        {
            throw new ArgumentException("A polygon needs at least 3 vertices.", nameof(vertices));
        }

        Vertices = vertices.ToArray();
    }

    public IReadOnlyList<Vector2> Vertices { get; }

    /// <summary>Signed area; positive for counter-clockwise orientation (shoelace formula).</summary>
    public double SignedArea
    {
        get
        {
            double sum = 0;
            for (int i = 0; i < Vertices.Count; i++)
            {
                sum += Vertices[i].Cross(Vertices[(i + 1) % Vertices.Count]);
            }

            return sum / 2.0;
        }
    }

    public double Area => Math.Abs(SignedArea);
    public bool IsCounterClockwise => SignedArea > 0;

    public double Perimeter
    {
        get
        {
            double sum = 0;
            for (int i = 0; i < Vertices.Count; i++)
            {
                sum += Vertices[i].DistanceTo(Vertices[(i + 1) % Vertices.Count]);
            }

            return sum;
        }
    }

    public Bounds2 Bounds
    {
        get
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (Vector2 v in Vertices)
            {
                minX = Math.Min(minX, v.X);
                minY = Math.Min(minY, v.Y);
                maxX = Math.Max(maxX, v.X);
                maxY = Math.Max(maxY, v.Y);
            }

            return new Bounds2(new Vector2(minX, minY), new Vector2(maxX, maxY));
        }
    }

    /// <summary>Returns the same outline with counter-clockwise orientation.</summary>
    public Polygon2 AsCounterClockwise() => IsCounterClockwise ? this : new Polygon2(Vertices.Reverse().ToArray());

    public Polygon2 Transformed(Func<Vector2, Vector2> map) => new(Vertices.Select(map).ToArray());

    public Polygon2 Translated(Vector2 offset) => Transformed(v => v + offset);

    public Polygon2 Rotated(double degrees) => Transformed(v => v.Rotated(degrees));

    /// <summary>Point-in-polygon (even-odd rule). Points exactly on the boundary may be classified either way.</summary>
    public bool Contains(Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = Vertices.Count - 1; i < Vertices.Count; j = i++)
        {
            Vector2 a = Vertices[i], b = Vertices[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < ((b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>True if no two non-adjacent edges intersect (and no vertex is repeated).</summary>
    public bool IsSimple
    {
        get
        {
            int n = Vertices.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = Vertices[i], b = Vertices[(i + 1) % n];
                if (a.DistanceTo(b) <= Tolerance.Linear)
                {
                    return false;
                }

                for (int j = i + 1; j < n; j++)
                {
                    bool adjacent = j == i + 1 || (i == 0 && j == n - 1);
                    Vector2 c = Vertices[j], d = Vertices[(j + 1) % n];
                    if (adjacent)
                    {
                        // adjacent edges may only share their common vertex (reject overlapping collinear back-tracking)
                        Vector2 shared = j == i + 1 ? b : a;
                        Vector2 e1 = j == i + 1 ? a : b;
                        Vector2 e2 = j == i + 1 ? d : c;
                        double cross = (shared - e1).Cross(e2 - shared);
                        if (Math.Abs(cross) <= 1e-12 && (shared - e1).Dot(e2 - shared) < 0)
                        {
                            return false;
                        }

                        continue;
                    }

                    if (SegmentsIntersect(a, b, c, d))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        double d1 = Orient(p3, p4, p1), d2 = Orient(p3, p4, p2), d3 = Orient(p1, p2, p3), d4 = Orient(p1, p2, p4);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
        {
            return true;
        }

        return (d1 == 0 && OnSegment(p3, p4, p1)) || (d2 == 0 && OnSegment(p3, p4, p2))
            || (d3 == 0 && OnSegment(p1, p2, p3)) || (d4 == 0 && OnSegment(p1, p2, p4));
    }

    private static double Orient(Vector2 a, Vector2 b, Vector2 c) => (b - a).Cross(c - a);

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p) =>
        Math.Min(a.X, b.X) <= p.X && p.X <= Math.Max(a.X, b.X) && Math.Min(a.Y, b.Y) <= p.Y && p.Y <= Math.Max(a.Y, b.Y);

    /// <summary>Counter-clockwise obround (stadium) with its length along local u; end arcs use <paramref name="arcSegments"/> segments each (half circle).</summary>
    public static Polygon2 Obround(double length, double width, Vector2 center, int arcSegments)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(arcSegments, 2);
        double r = width / 2, cx = (length / 2) - r;
        var pts = new List<Vector2>();
        for (int i = 0; i <= arcSegments; i++)
        {
            double a = (-90.0 + (180.0 * i / arcSegments)) * Math.PI / 180.0;
            pts.Add(new Vector2(center.X + cx + (r * Math.Cos(a)), center.Y + (r * Math.Sin(a))));
        }

        for (int i = 0; i <= arcSegments; i++)
        {
            double a = (90.0 + (180.0 * i / arcSegments)) * Math.PI / 180.0;
            pts.Add(new Vector2(center.X - cx + (r * Math.Cos(a)), center.Y + (r * Math.Sin(a))));
        }

        return new Polygon2(pts);
    }

    public static Polygon2 Rectangle(double width, double height, Vector2 center) =>
        new([
            new Vector2(center.X - (width / 2), center.Y - (height / 2)),
            new Vector2(center.X + (width / 2), center.Y - (height / 2)),
            new Vector2(center.X + (width / 2), center.Y + (height / 2)),
            new Vector2(center.X - (width / 2), center.Y + (height / 2)),
        ]);

    /// <summary>Counter-clockwise polygon inscribed in the circle.</summary>
    public static Polygon2 Circle(Vector2 center, double radius, int segments)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(segments, 3);
        var pts = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            double a = 2 * Math.PI * i / segments;
            pts[i] = new Vector2(center.X + (radius * Math.Cos(a)), center.Y + (radius * Math.Sin(a)));
        }

        return new Polygon2(pts);
    }

    /// <summary>Counter-clockwise rectangle with rounded corners; <paramref name="arcSegments"/> per 90° corner.</summary>
    public static Polygon2 RoundedRectangle(double width, double height, double cornerRadius, Vector2 center, int arcSegments)
    {
        if (cornerRadius <= 0)
        {
            return Rectangle(width, height, center);
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(arcSegments, 1);
        double cx = (width / 2) - cornerRadius;
        double cy = (height / 2) - cornerRadius;
        (double ox, double oy, double startDeg)[] corners =
        [
            (cx, -cy, -90), (cx, cy, 0), (-cx, cy, 90), (-cx, -cy, 180),
        ];
        var pts = new List<Vector2>();
        foreach ((double ox, double oy, double startDeg) in corners)
        {
            for (int i = 0; i <= arcSegments; i++)
            {
                double a = (startDeg + (90.0 * i / arcSegments)) * Math.PI / 180.0;
                pts.Add(new Vector2(
                    center.X + ox + (cornerRadius * Math.Cos(a)),
                    center.Y + oy + (cornerRadius * Math.Sin(a))));
            }
        }

        return new Polygon2(pts);
    }
}
