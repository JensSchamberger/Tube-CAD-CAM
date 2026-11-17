using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

/// <summary>One boundary segment of the section loops. <see cref="Normal"/> points out of the material.</summary>
internal sealed class Facet
{
    public Facet(int loop, int index, Vector2 a, Vector2 b)
    {
        Loop = loop;
        Index = index;
        A = a;
        B = b;
        Vector2 e = (b - a).Normalized();
        Normal = new Vector2(e.Y, -e.X);
    }

    public int Loop { get; }
    public int Index { get; }
    public Vector2 A { get; }
    public Vector2 B { get; }
    public Vector2 Normal { get; }
    public (int, int) Key => (Loop, Index);

    /// <summary>Section point of this facet at coordinate t (t = point · T).</summary>
    public Vector2 PointAtT(Vector2 t, double tv)
    {
        double ta = A.Dot(t), tb = B.Dot(t);
        return A + ((B - A) * ((tv - ta) / (tb - ta)));
    }
}

internal sealed record PairPlan(Facet Lo, Facet Up);

internal sealed record StripPlan(double TL, double TR, IReadOnlyList<PairPlan> Pairs);

internal sealed record PlanFailure(string Code, string Message);

internal sealed class GroupPlan
{
    public required IReadOnlyList<Cutter> Members { get; init; }
    public required Vector2 N { get; init; }
    public required Vector2 T { get; init; }
    public required double Ta { get; init; }
    public required double Tb { get; init; }
    public required IReadOnlyList<StripPlan> Strips { get; init; }
    public required IReadOnlyDictionary<(int, int), List<double>> Splits { get; init; }

    public bool Participates((int, int) facet) => Splits.ContainsKey(facet);
}

/// <summary>
/// Decides how the cut region of a cutter group maps onto the section: which facets form the wall pairs in every strip
/// of the t axis. Anything that is not unambiguous (profile edges inside the cut, inconsistent pairing) is reported as a failure.
/// </summary>
internal static class SolidPlanner
{
    private const double VerticalEps = 1e-9;

    public static (GroupPlan? Plan, PlanFailure? Failure) Plan(IReadOnlyList<Facet> facets, IReadOnlyList<Cutter> members, SolidOptions options)
    {
        Vector2 n = members[0].N;
        Vector2 t = new(-n.Y, n.X);
        CutMode mode = members[0].Mode;
        double tmin = members.Min(c => c.TMin), tmax = members.Max(c => c.TMax);

        // Margin: the re-meshed region must stay clear of profile events (vertical faces, extrema in t).
        List<double> events = Events(facets, t);
        double left = events.Where(e => e < tmin - 1e-9).DefaultIfEmpty(double.NegativeInfinity).Max();
        double right = events.Where(e => e > tmax + 1e-9).DefaultIfEmpty(double.PositiveInfinity).Min();
        double ml = Math.Min(options.SeamMarginMm, 0.5 * (tmin - left)), mr = Math.Min(options.SeamMarginMm, 0.5 * (right - tmax));
        if (ml < options.MinSeamMarginMm || mr < options.MinSeamMarginMm)
        {
            return (null, new PlanFailure("solid.profile-edge", "The cut region lies directly at an edge of the profile; the wall layer is not defined there."));
        }

        double ta = tmin - ml, tb = tmax + mr;
        List<double>? seams = SeamValues(facets, t, ta, tb);
        if (seams == null)
        {
            return (null, new PlanFailure("solid.seam-too-close", "Profile vertices lie too close together next to the cut region."));
        }

        var strips = new List<StripPlan>();
        for (int k = 0; k < seams.Count - 1; k++)
        {
            double tl = seams[k], tr = seams[k + 1], tm = (tl + tr) / 2;
            var spanning = facets
                .Where(f => Math.Abs(f.B.Dot(t) - f.A.Dot(t)) > VerticalEps && Math.Min(f.A.Dot(t), f.B.Dot(t)) <= tl + 1e-9 && Math.Max(f.A.Dot(t), f.B.Dot(t)) >= tr - 1e-9)
                .Select(f => (Facet: f, H: f.PointAtT(t, tm).Dot(n)))
                .OrderBy(x => x.H).ToList();
            var pairs = new List<PairPlan>();
            if (mode == CutMode.TopPair)
            {
                if (spanning.Count < 2)
                {
                    return (null, new PlanFailure("solid.pairing", "No wall layer found at the cut position."));
                }

                pairs.Add(new PairPlan(spanning[^2].Facet, spanning[^1].Facet));
            }
            else
            {
                if (spanning.Count == 0 || spanning.Count % 2 != 0)
                {
                    return (null, new PlanFailure("solid.pairing", "The material layers along the cutting direction are not well defined."));
                }

                for (int i = 0; i < spanning.Count; i += 2)
                {
                    pairs.Add(new PairPlan(spanning[i].Facet, spanning[i + 1].Facet));
                }
            }

            foreach (PairPlan p in pairs)
            {
                if (p.Lo.Normal.Dot(n) >= -1e-9 || p.Up.Normal.Dot(n) <= 1e-9 || p.Up.PointAtT(t, tm).Dot(n) - p.Lo.PointAtT(t, tm).Dot(n) <= 1e-6)
                {
                    return (null, new PlanFailure("solid.pairing", "The wall layer along the cutting direction is not a simple plate (it passes along a face or a junction)."));
                }
            }

            strips.Add(new StripPlan(tl, tr, pairs));
        }

        // The wall pairs must continue without jumps from strip to strip (a jump means the cut crosses a profile edge).
        for (int k = 0; k < strips.Count - 1; k++)
        {
            double seam = strips[k].TR;
            if (strips[k].Pairs.Count != strips[k + 1].Pairs.Count)
            {
                return (null, new PlanFailure("solid.profile-edge", "The cut crosses an edge of the profile; the number of wall layers changes inside the cut."));
            }

            for (int j = 0; j < strips[k].Pairs.Count; j++)
            {
                PairPlan a = strips[k].Pairs[j], b = strips[k + 1].Pairs[j];
                if ((a.Lo.PointAtT(t, seam) - b.Lo.PointAtT(t, seam)).Length > Tolerance.Weld
                    || (a.Up.PointAtT(t, seam) - b.Up.PointAtT(t, seam)).Length > Tolerance.Weld)
                {
                    return (null, new PlanFailure("solid.profile-edge", "The cut crosses an edge of the profile; the wall surfaces are not continuous inside the cut."));
                }
            }
        }

        // Every participating facet must be a pair member in all strips it spans.
        var participating = strips.SelectMany(s => s.Pairs).SelectMany(p => new[] { p.Lo, p.Up }).GroupBy(f => f.Key).Select(g => g.First()).ToList();
        var splits = new Dictionary<(int, int), List<double>>();
        foreach (Facet f in participating)
        {
            double fa = Math.Min(f.A.Dot(t), f.B.Dot(t)), fb = Math.Max(f.A.Dot(t), f.B.Dot(t));
            foreach (StripPlan s in strips)
            {
                bool spans = fa <= s.TL + 1e-9 && fb >= s.TR - 1e-9;
                bool member = s.Pairs.Any(p => p.Lo.Key == f.Key || p.Up.Key == f.Key);
                if (spans && !member)
                {
                    return (null, new PlanFailure("solid.pairing", "The wall layer changes inside the cut region."));
                }
            }

            splits[f.Key] = seams.Where(sv => sv > fa + 1e-9 && sv < fb - 1e-9).ToList();
        }

        return (new GroupPlan { Members = members, N = n, T = t, Ta = ta, Tb = tb, Strips = strips, Splits = splits }, null);
    }

    private static List<double> Events(IReadOnlyList<Facet> facets, Vector2 t)
    {
        var events = new List<double>();
        foreach (IGrouping<int, Facet> loop in facets.GroupBy(f => f.Loop))
        {
            Facet[] fs = loop.OrderBy(f => f.Index).ToArray();
            for (int i = 0; i < fs.Length; i++)
            {
                double d1 = fs[i].B.Dot(t) - fs[i].A.Dot(t);
                Facet next = fs[(i + 1) % fs.Length];
                double d2 = next.B.Dot(t) - next.A.Dot(t);
                if (Math.Abs(d1) <= VerticalEps)
                {
                    events.Add(fs[i].A.Dot(t));
                }
                else if (Math.Abs(d2) > VerticalEps && d1 * d2 < 0)
                {
                    events.Add(fs[i].B.Dot(t));
                }
            }
        }

        return events;
    }

    /// <summary>All profile vertex t values inside (ta, tb) plus the two ends; null if two seams are closer than the weld tolerance.</summary>
    private static List<double>? SeamValues(IReadOnlyList<Facet> facets, Vector2 t, double ta, double tb)
    {
        var vs = facets.SelectMany(f => new[] { f.A.Dot(t), f.B.Dot(t) }).Where(v => v > ta && v < tb).OrderBy(v => v).ToList();
        var seams = new List<double> { ta };
        foreach (double v in vs)
        {
            if (v - seams[^1] > 1e-9)
            {
                seams.Add(v);
            }
        }

        if (tb - seams[^1] > 1e-9)
        {
            seams.Add(tb);
        }
        else
        {
            seams[^1] = tb;
        }

        for (int i = 1; i < seams.Count; i++)
        {
            if (seams[i] - seams[i - 1] < 4 * Tolerance.Weld)
            {
                return null;
            }
        }

        return seams;
    }
}
