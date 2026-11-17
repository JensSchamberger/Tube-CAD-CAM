using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

/// <summary>A point on the outer surface of the section.</summary>
/// <param name="Position">Section coordinates.</param>
/// <param name="Normal">Outward unit normal of the facet that was hit.</param>
/// <param name="Tangent">Unit tangent in direction of increasing angle (counter-clockwise).</param>
/// <param name="ArcPosition">Arc length from the first outline vertex.</param>
public sealed record SurfacePoint(Vector2 Position, Vector2 Normal, Vector2 Tangent, double ArcPosition, int SegmentIndex);

/// <summary>Queries on the outer outline of a profile section: ray location and arc-length walks.</summary>
public sealed class SectionSurface
{
    private readonly IReadOnlyList<Vector2> _v;
    private readonly double[] _cum;

    public SectionSurface(Polygon2 outer)
    {
        _v = outer.AsCounterClockwise().Vertices;
        _cum = new double[_v.Count + 1];
        for (int i = 0; i < _v.Count; i++)
        {
            _cum[i + 1] = _cum[i] + _v[i].DistanceTo(_v[(i + 1) % _v.Count]);
        }
    }

    public double Perimeter => _cum[^1];
    public int SegmentCount => _v.Count;

    /// <summary>
    /// Casts a ray from the section origin in direction <paramref name="angleDeg"/> and returns the farthest outline hit
    /// (the outer surface), or null if the ray misses the outline (e.g. towards the opening of a U profile).
    /// </summary>
    public SurfacePoint? Locate(double angleDeg)
    {
        Vector2 d = TubeCoordinateSystem.Direction(angleDeg);
        double bestT = double.NegativeInfinity;
        SurfacePoint? best = null;
        for (int i = 0; i < _v.Count; i++)
        {
            Vector2 a = _v[i], b = _v[(i + 1) % _v.Count];
            Vector2 e = b - a;
            double denom = d.Cross(e);
            if (Math.Abs(denom) < 1e-12)
            {
                continue;
            }

            double t = a.Cross(e) / denom;       // along ray
            double s = a.Cross(d) / denom;       // along segment
            if (t > 1e-9 && s >= -1e-12 && s <= 1 + 1e-12 && t > bestT)
            {
                Vector2 tangent = e.Normalized();
                bestT = t;
                best = new SurfacePoint(d * t, new Vector2(tangent.Y, -tangent.X), tangent,
                    _cum[i] + (s * e.Length), i);
            }
        }

        return best;
    }

    /// <summary>
    /// Walks <paramref name="distance"/> of arc length from a surface point in one direction (+1 / -1) and returns
    /// the largest single-vertex turning angle (deg) and the accumulated turning (deg) on the way.
    /// </summary>
    public (double MaxTurnDeg, double TotalTurnDeg) Walk(SurfacePoint from, double distance, int direction)
    {
        int n = _v.Count;
        double remaining = distance, maxTurn = 0, total = 0;
        int seg = from.SegmentIndex;
        double toEnd = direction > 0 ? _cum[seg + 1] - from.ArcPosition : from.ArcPosition - _cum[seg];
        remaining -= toEnd;
        int guard = 0;
        while (remaining > 0 && guard++ < n)
        {
            int next = direction > 0 ? (seg + 1) % n : (seg + n - 1) % n;
            Vector2 e1 = Edge(seg), e2 = Edge(next);
            double turn = Math.Abs(Math.Atan2(e1.Cross(e2), e1.Dot(e2))) * 180.0 / Math.PI;
            maxTurn = Math.Max(maxTurn, turn);
            total += turn;
            remaining -= _cum[next + 1] - _cum[next];
            seg = next;
        }

        return (maxTurn, total);
    }

    private Vector2 Edge(int i) => _v[(i + 1) % _v.Count] - _v[i];

    /// <summary>Arc-length distance on the closed outline between two arc positions (shortest way round).</summary>
    public double ArcDistance(double s1, double s2)
    {
        double d = Math.Abs(s1 - s2) % Perimeter;
        return Math.Min(d, Perimeter - d);
    }
}
