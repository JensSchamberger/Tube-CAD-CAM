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
