using TubeCadCam.Domain.Common;
using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

internal sealed record CoreResult(TubeSolid? Solid, List<FeatureFailure> Failures, string? TopologyError);

/// <summary>
/// Builds the boundary of the tube minus the cutters. The tube is a linear extrusion of the section along X; every cutter is a
/// prism along a direction in the section plane. Along that direction the material is a stack of plates, so the boundary can be
/// assembled from exact 2D problems (see docs/Boolean-Entscheidung.md):
/// lateral surfaces, wall pairs lifted from one shared 2D triangulation, opening walls, end faces from refined section loops.
/// </summary>
internal sealed class SolidAssembler
{
    private readonly TubeModel _model;
    private readonly ProfileSection _section;
    private readonly EndPlane _start;
    private readonly EndPlane _end;
    private readonly SolidOptions _options;
    private readonly List<Facet> _facets = [];
    private readonly List<(Vector3 A, Vector3 B, Vector3 C, FaceTag Tag)> _tris = [];
    private readonly List<(Guid Feature, EdgeSide Side, int Loop, Vector3 P, Vector3 Q)> _edgeSegments = [];

    public SolidAssembler(TubeModel model, ProfileSection section, SolidOptions options)
    {
        _model = model;
        _section = section;
        _options = options;
        (_start, _end) = EndPlane.For(model, section.Outer);
        int loop = 0;
        foreach (IReadOnlyList<Vector2> pts in Loops(section))
        {
            for (int i = 0; i < pts.Count; i++)
            {
                _facets.Add(new Facet(loop, i, pts[i], pts[(i + 1) % pts.Count]));
            }

            loop++;
        }
    }

    /// <summary>Loop 0 = outer (counter-clockwise), loops ≥ 1 = voids (clockwise).</summary>
    private static List<IReadOnlyList<Vector2>> Loops(ProfileSection s)
    {
        var loops = new List<IReadOnlyList<Vector2>> { s.Outer.AsCounterClockwise().Vertices };
        foreach (Polygon2 v in s.Voids)
        {
            loops.Add(v.IsCounterClockwise ? v.Vertices.Reverse().ToArray() : v.Vertices);
        }

        return loops;
    }

    /// <summary>
    /// Builds the solid. Cutters that violate a per-feature rule (end faces) are evicted with a named failure and the
    /// remaining ones are built again, so one bad feature never takes the others down.
    /// </summary>
    public CoreResult Build(IReadOnlyList<Cutter> cutters)
    {
        var remaining = cutters.ToList();
        var evictedFailures = new List<FeatureFailure>();
        for (int guard = 0; guard <= cutters.Count; guard++)
        {
            _tris.Clear();
            _edgeSegments.Clear();
            CoreResult r = BuildOnce(remaining, out List<(Cutter Cutter, PlanFailure Failure)> evicted);
            if (evicted.Count == 0)
            {
                List<FeatureFailure> all = evictedFailures.Concat(r.Failures).ToList();
                return new CoreResult(r.Solid is null ? null : r.Solid with { Failures = all }, all, r.TopologyError);
            }

            foreach ((Cutter c, PlanFailure f) in evicted)
            {
                evictedFailures.Add(new FeatureFailure(c.FeatureId, f.Code, f.Message));
                remaining.Remove(c);
            }
        }

        throw new InvalidOperationException("Feature eviction did not converge.");
    }

    private CoreResult BuildOnce(IReadOnlyList<Cutter> cutters, out List<(Cutter Cutter, PlanFailure Failure)> evicted)
    {
        evicted = [];
        var failures = new List<FeatureFailure>();
        var groups = SolidCutters.Group(cutters, _options);

        // 1. plan each group; later groups that need facets already used by earlier ones fail
        var plans = new List<GroupPlan>();
        var used = new Dictionary<(int, int), Guid>();
        foreach (List<Cutter> members in groups)
        {
            (GroupPlan? plan, PlanFailure? failure) = SolidPlanner.Plan(_facets, members, _options);
            if (plan is null)
            {
                Fail(failures, members, failure!.Code, failure.Message);
                continue;
            }

            var clash = plan.Splits.Keys.Where(used.ContainsKey).Select(k => used[k]).Cast<Guid?>().FirstOrDefault();
            if (clash is { } other)
            {
                Fail(failures, members, "solid.interaction",
                    $"The cut overlaps the surface already modified by feature {other} with a different cutting direction; such overlaps are not supported.");
                continue;
            }

            foreach ((int, int) k in plan.Splits.Keys)
            {
                used[k] = members[0].FeatureId;
            }

            plans.Add(plan);
        }

        // 2. geometry of the cut regions per strip; end-face interaction
        var geometries = new List<GroupGeometry>();
        foreach (GroupPlan plan in plans)
        {
            (GroupGeometry? geo, PlanFailure? failure, var memberEvictions) = ComputeGeometry(plan);
            if (memberEvictions.Count > 0)
            {
                evicted.AddRange(memberEvictions);
                continue;
            }

            if (geo is null)
            {
                Fail(failures, plan.Members, failure!.Code, failure.Message);
                continue;
            }

            geometries.Add(geo);
        }

        if (evicted.Count > 0)
        {
            return new CoreResult(null, failures, null);
        }

        try
        {
            EmitLateralSurfaces(geometries);
            foreach (GroupGeometry g in geometries)
            {
                EmitPatches(g);
            }

            EmitEndFaces(geometries);
        }
        catch (InvalidOperationException ex)
        {
            return new CoreResult(null, failures, "solid.triangulation: " + ex.Message);
        }

        return Finish(geometries, failures);
    }

    private static void Fail(List<FeatureFailure> failures, IEnumerable<Cutter> members, string code, string message)
    {
        foreach (Cutter c in members)
        {
            failures.Add(new FeatureFailure(c.FeatureId, code, message));
        }
    }

    // ---------------------------------------------------------------- geometry of cut regions

    private sealed record PairGeometry(PairPlan Pair, Polygon2 DomainUp, Polygon2 DomainLo, IReadOnlyList<PolygonRegion> RegionUp, IReadOnlyList<PolygonRegion> RegionLo);

    private sealed class GroupGeometry
    {
        public required GroupPlan Plan { get; init; }
        public required List<List<PairGeometry>> Strips { get; init; }
        public bool CrossesStart { get; set; }
        public bool CrossesEnd { get; set; }
        public List<(double, double)> StartIntervals { get; set; } = [];
        public List<(double, double)> EndIntervals { get; set; } = [];
    }

    private Polygon2 Domain(Facet f, Vector2 t, double tl, double tr)
    {
        Vector2 pl = f.PointAtT(t, tl), pr = f.PointAtT(t, tr);
        return new Polygon2([
            new Vector2(_start.XAt(pl), tl), new Vector2(_end.XAt(pl), tl),
            new Vector2(_end.XAt(pr), tr), new Vector2(_start.XAt(pr), tr),
        ]);
    }

    private (GroupGeometry?, PlanFailure?, List<(Cutter, PlanFailure)>) ComputeGeometry(GroupPlan plan)
    {
        var none = new List<(Cutter, PlanFailure)>();
        var strips = new List<List<PairGeometry>>();
        foreach (StripPlan s in plan.Strips)
        {
            var fps = plan.Members.SelectMany(m => m.Footprint)
                .Where(p => p.Bounds.Min.Y < s.TR + 1e-6 && p.Bounds.Max.Y > s.TL - 1e-6).ToList();
            var pairs = new List<PairGeometry>();
            foreach (PairPlan pair in s.Pairs)
            {
                Polygon2 dUp = Domain(pair.Up, plan.T, s.TL, s.TR), dLo = Domain(pair.Lo, plan.T, s.TL, s.TR);
                var rUp = Polygon2Boolean.Difference([dUp], fps);
                var rLo = SamePolygon(dUp, dLo) ? rUp : Polygon2Boolean.Difference([dLo], fps);
                pairs.Add(new PairGeometry(pair, dUp, dLo, rUp, rLo));
            }

            strips.Add(pairs);
        }

        var geo = new GroupGeometry { Plan = plan, Strips = strips };

        // Which end faces does each feature reach? Only notches and slits may; everything else keeps a clearance.
        double clearance = _options.EndClearanceMm;
        bool startMiter = _start.Gradient.Length > 0, endMiter = _end.Gradient.Length > 0;
        var evictions = new List<(Cutter, PlanFailure)>();
        foreach (Cutter m in plan.Members)
        {
            double minToStart = double.MaxValue, minToEnd = double.MaxValue;
            foreach (Vector2 v in m.Footprint.SelectMany(p => p.Vertices))
            {
                int k = plan.Strips.ToList().FindIndex(s => v.Y >= s.TL - 1e-9 && v.Y <= s.TR + 1e-9);
                if (k < 0)
                {
                    return (null, new PlanFailure("solid.internal", "Footprint outside the re-meshed region."), none);
                }

                Vector2 q = plan.Strips[k].Pairs[0].Up.PointAtT(plan.T, v.Y);
                minToStart = Math.Min(minToStart, v.X - _start.XAt(q));
                minToEnd = Math.Min(minToEnd, _end.XAt(q) - v.X);
            }

            foreach ((double dist, bool miter, string name) in new[] { (minToStart, startMiter, "start"), (minToEnd, endMiter, "end") })
            {
                bool crosses = dist < 0;
                if (crosses && miter)
                {
                    evictions.Add((m, new PlanFailure("solid.end-unsupported", $"A cut that reaches the mitred {name} face is not supported.")));
                }
                else if (crosses && !m.IsEndFeature)
                {
                    evictions.Add((m, new PlanFailure("solid.end-unsupported", $"The feature extends beyond the {name} face of the tube.")));
                }
                else if (!crosses && dist < clearance && !m.IsEndFeature)
                {
                    evictions.Add((m, new PlanFailure("solid.too-close-to-end", $"The feature is closer than {clearance} mm to the {name} face.")));
                }
                else if (crosses)
                {
                    if (name == "start") { geo.CrossesStart = true; } else { geo.CrossesEnd = true; }
                }
            }
        }

        if (evictions.Count > 0)
        {
            return (null, null, evictions.GroupBy(e => e.Item1.FeatureId).Select(g => g.First()).ToList());
        }

        if (geo.CrossesStart)
        {
            geo.StartIntervals = Intervals(plan, _start.X0);
        }

        if (geo.CrossesEnd)
        {
            geo.EndIntervals = Intervals(plan, _end.X0);
        }

        return (geo, null, none);
    }

    private static bool SamePolygon(Polygon2 a, Polygon2 b) =>
        a.Vertices.Count == b.Vertices.Count && a.Vertices.Zip(b.Vertices).All(z => (z.First - z.Second).Length < 1e-12);

    /// <summary>t intervals of the footprint on the plane x = <paramref name="x"/> (even-odd scan over all outlines).</summary>
    private static List<(double, double)> Intervals(GroupPlan plan, double x)
    {
        var ts = new List<double>();
        foreach (Polygon2 p in plan.Members.SelectMany(m => m.Footprint))
        {
            for (int i = 0; i < p.Vertices.Count; i++)
            {
                Vector2 a = p.Vertices[i], b = p.Vertices[(i + 1) % p.Vertices.Count];
                if ((a.X <= x) != (b.X <= x))
                {
                    ts.Add(a.Y + ((x - a.X) / (b.X - a.X) * (b.Y - a.Y)));
                }
            }
        }

        ts.Sort();
        var result = new List<(double, double)>();
        for (int i = 0; i + 1 < ts.Count; i += 2)
        {
            result.Add((ts[i], ts[i + 1]));
        }

        return MergeIntervals(result);
    }

    private static List<(double, double)> MergeIntervals(List<(double Lo, double Hi)> intervals)
    {
        var merged = new List<(double, double)>();
        foreach ((double lo, double hi) in intervals.OrderBy(i => i.Lo))
        {
            if (merged.Count > 0 && lo <= merged[^1].Item2 + 1e-9)
            {
                merged[^1] = (merged[^1].Item1, Math.Max(merged[^1].Item2, hi));
            }
            else
            {
                merged.Add((lo, hi));
            }
        }

        return merged;
    }

    // ---------------------------------------------------------------- emission

    private static SolidFaceKind SurfaceKind(int loop) => loop == 0 ? SolidFaceKind.OuterSurface : SolidFaceKind.InnerSurface;

    private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, FaceTag tag, Vector3? desiredNormal)
    {
        if (desiredNormal is { } n && (b - a).Cross(c - a).Dot(n) < 0)
        {
            (b, c) = (c, b);
        }

        _tris.Add((a, b, c, tag));
    }

    private Vector3 Pos(Vector2 p, double x) => new(x, p.X, p.Y);

    /// <summary>Default meshing of all facets (two triangles per facet piece); pieces replaced by a patch are skipped.</summary>
    private void EmitLateralSurfaces(IReadOnlyList<GroupGeometry> geometries)
    {
        foreach (Facet f in _facets)
        {
            GroupGeometry? owner = geometries.FirstOrDefault(g => g.Plan.Participates(f.Key));
            List<Vector2> pts = FacetPoints(f, owner);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                if (owner is not null)
                {
                    double tm = (((a + b) * 0.5)).Dot(owner.Plan.T);
                    if (tm > owner.Plan.Ta && tm < owner.Plan.Tb)
                    {
                        continue; // replaced by the patch
                    }
                }

                Vector3 a0 = Pos(a, _start.XAt(a)), a1 = Pos(a, _end.XAt(a)), b0 = Pos(b, _start.XAt(b)), b1 = Pos(b, _end.XAt(b));
                var tag = new FaceTag(SurfaceKind(f.Loop), f.Loop, null, null);
                Vector3 nrm = new(0, f.Normal.X, f.Normal.Y);
                AddTriangle(a0, b0, b1, tag, nrm);
                AddTriangle(a0, b1, a1, tag, nrm);
            }
        }
    }

    /// <summary>Facet vertices from A to B including the seam points inserted for a participating facet.</summary>
    private static List<Vector2> FacetPoints(Facet f, GroupGeometry? owner)
    {
        var pts = new List<Vector2> { f.A };
        if (owner is not null && owner.Plan.Splits.TryGetValue(f.Key, out List<double>? ts))
        {
            double ta = f.A.Dot(owner.Plan.T), tb = f.B.Dot(owner.Plan.T);
            IEnumerable<double> ordered = tb > ta ? ts.OrderBy(v => v) : ts.OrderByDescending(v => v);
            pts.AddRange(ordered.Select(v => f.PointAtT(owner.Plan.T, v)));
        }

        pts.Add(f.B);
        return pts;
    }

    private void EmitPatches(GroupGeometry g)
    {
        GroupPlan plan = g.Plan;
        for (int k = 0; k < plan.Strips.Count; k++)
        {
            StripPlan strip = plan.Strips[k];
            foreach (PairGeometry pg in g.Strips[k])
            {
                Facet up = pg.Pair.Up, lo = pg.Pair.Lo;
                Vector3 Lift(Facet f, Vector2 xt) => Pos(f.PointAtT(plan.T, xt.Y), xt.X);

                EmitRegions(pg.RegionUp, up, Lift, new Vector3(0, up.Normal.X, up.Normal.Y), g);
                EmitRegions(pg.RegionLo, lo, Lift, new Vector3(0, lo.Normal.X, lo.Normal.Y), g);

                // opening walls: boundary of the region that belongs to the footprint (not to a seam or an end face)
                foreach (PolygonRegion region in pg.RegionUp)
                {
                    foreach (Polygon2 ring in new[] { region.Outer }.Concat(region.Holes))
                    {
                        for (int i = 0; i < ring.Vertices.Count; i++)
                        {
                            Vector2 p1 = ring.Vertices[i], p2 = ring.Vertices[(i + 1) % ring.Vertices.Count];
                            if (IsDomainEdge(p1, p2, strip, up, plan.T))
                            {
                                continue;
                            }

                            EmitWall(p1, p2, pg.Pair, plan, g);
                        }
                    }
                }
            }
        }
    }

    private void EmitRegions(IReadOnlyList<PolygonRegion> regions, Facet facet, Func<Facet, Vector2, Vector3> lift, Vector3 normal, GroupGeometry g)
    {
        var tag = new FaceTag(SurfaceKind(facet.Loop), facet.Loop, null, null);
        foreach (PolygonRegion r in regions)
        {
            Triangulation tri = PolygonTriangulator.Triangulate(r.Outer, r.Holes, 4 * Tolerance.Weld, improveQuality: true);
            for (int i = 0; i < tri.Indices.Count; i += 3)
            {
                AddTriangle(lift(facet, tri.Vertices[tri.Indices[i]]), lift(facet, tri.Vertices[tri.Indices[i + 1]]), lift(facet, tri.Vertices[tri.Indices[i + 2]]), tag, normal);
            }
        }
    }

    private bool IsDomainEdge(Vector2 p1, Vector2 p2, StripPlan strip, Facet up, Vector2 t)
    {
        const double e = 2e-6;
        if ((Math.Abs(p1.Y - strip.TL) <= e && Math.Abs(p2.Y - strip.TL) <= e) || (Math.Abs(p1.Y - strip.TR) <= e && Math.Abs(p2.Y - strip.TR) <= e))
        {
            return true; // seam towards the neighbouring strip / surface
        }

        double Xs(Vector2 q) => _start.XAt(up.PointAtT(t, q.Y));
        double Xe(Vector2 q) => _end.XAt(up.PointAtT(t, q.Y));
        return (Math.Abs(p1.X - Xs(p1)) <= e && Math.Abs(p2.X - Xs(p2)) <= e) || (Math.Abs(p1.X - Xe(p1)) <= e && Math.Abs(p2.X - Xe(p2)) <= e);
    }

    private void EmitWall(Vector2 p1, Vector2 p2, PairPlan pair, GroupPlan plan, GroupGeometry g)
    {
        Vector2 d = p2 - p1;
        double len = d.Length;
        if (len < Tolerance.Weld)
        {
            return;
        }

        Vector2 n2 = new Vector2(d.Y, -d.X) * (1 / len); // right of the region boundary = into the opening
        Vector3 normal = new((n2.X), plan.T.X * n2.Y, plan.T.Y * n2.Y);
        Vector3 U(Vector2 q) => Pos(pair.Up.PointAtT(plan.T, q.Y), q.X);
        Vector3 L(Vector2 q) => Pos(pair.Lo.PointAtT(plan.T, q.Y), q.X);
        Guid owner = OwnerOf(plan, (p1 + p2) * 0.5);
        var tag = new FaceTag(SolidFaceKind.FeatureWall, pair.Up.Loop, owner, null);
        AddTriangle(L(p1), L(p2), U(p2), tag, normal);
        AddTriangle(L(p1), U(p2), U(p1), tag, normal);
        _edgeSegments.Add((owner, EdgeSide.Upper, pair.Up.Loop, U(p1), U(p2)));
        _edgeSegments.Add((owner, EdgeSide.Lower, pair.Lo.Loop, L(p1), L(p2)));
    }

    private static Guid OwnerOf(GroupPlan plan, Vector2 mid)
    {
        Guid best = plan.Members[0].FeatureId;
        double bestDist = double.MaxValue;
        foreach (Cutter c in plan.Members)
        {
            foreach (Polygon2 poly in c.Footprint)
            {
                for (int i = 0; i < poly.Vertices.Count; i++)
                {
                    double dist = DistanceToSegment(mid, poly.Vertices[i], poly.Vertices[(i + 1) % poly.Vertices.Count]);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = c.FeatureId;
                    }
                }
            }
        }

        return best;
    }

    private static double DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        double l2 = ab.Dot(ab);
        double s = l2 <= 0 ? 0 : Math.Clamp((p - a).Dot(ab) / l2, 0, 1);
        return (p - (a + (ab * s))).Length;
    }

    /// <summary>End faces from the refined section loops (including seam points); notched where cutters reach the face.</summary>
    private void EmitEndFaces(IReadOnlyList<GroupGeometry> geometries)
    {
        var outerAndVoids = new List<List<Vector2>>();
        int loop = 0;
        foreach (IReadOnlyList<Vector2> pts in Loops(_section))
        {
            var refined = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                var f = _facets.First(x => x.Loop == loop && x.Index == i);
                GroupGeometry? owner = geometries.FirstOrDefault(g => g.Plan.Participates(f.Key));
                List<Vector2> fp = FacetPoints(f, owner);
                refined.AddRange(fp.Take(fp.Count - 1));
            }

            outerAndVoids.Add(refined);
            loop++;
        }

        foreach ((TubeEnd end, EndPlane plane) in new[] { (TubeEnd.Start, _start), (TubeEnd.End, _end) })
        {
            var cuts = new List<Polygon2>();
            foreach (GroupGeometry g in geometries)
            {
                bool crosses = end == TubeEnd.Start ? g.CrossesStart : g.CrossesEnd;
                if (!crosses)
                {
                    continue;
                }

                foreach ((double t1, double t2) in end == TubeEnd.Start ? g.StartIntervals : g.EndIntervals)
                {
                    foreach (StripPlan s in g.Plan.Strips)
                    {
                        double ta = Math.Max(t1, s.TL), tb = Math.Min(t2, s.TR);
                        if (tb - ta < 1e-7)
                        {
                            continue;
                        }

                        foreach (PairPlan pair in s.Pairs)
                        {
                            // Extended along the cutting direction so that no edge of the cut coincides with a section edge
                            // (coincident edges make 2D booleans produce zero-width spikes). t of the sides is unchanged.
                            Vector2 over = g.Plan.N * _options.CapCutOvershootMm;
                            cuts.Add(new Polygon2([
                                pair.Lo.PointAtT(g.Plan.T, ta) - over, pair.Lo.PointAtT(g.Plan.T, tb) - over,
                                pair.Up.PointAtT(g.Plan.T, tb) + over, pair.Up.PointAtT(g.Plan.T, ta) + over]));
                        }
                    }
                }
            }

            var outer = new Polygon2(outerAndVoids[0]);
            var voids = outerAndVoids.Skip(1).Select(v => new Polygon2(v)).ToList();
            IReadOnlyList<PolygonRegion> regions = cuts.Count == 0
                ? [new PolygonRegion(outer, voids)]
                : Polygon2Boolean.Difference(new[] { outer.AsCounterClockwise() }.Concat(voids.Select(v => v.IsCounterClockwise ? new Polygon2(v.Vertices.Reverse().ToArray()) : v)), cuts);

            var tag = new FaceTag(SolidFaceKind.EndFace, -1, null, end);
            Vector3 normal = new Vector3(1, -plane.Gradient.X, -plane.Gradient.Y).Normalized() * (end == TubeEnd.Start ? -1 : 1);
            List<Vector2> pool = outerAndVoids.SelectMany(l => l).ToList();
            foreach (PolygonRegion region in regions)
            {
                // boundary points of the section loops must stay on the notched face (Clipper removes collinear points)
                PolygonRegion r = cuts.Count == 0 ? region : new PolygonRegion(
                    Polygon2Boolean.InsertOnEdgePoints(region.Outer, pool, 4 * Tolerance.Linear),
                    region.Holes.Select(h => Polygon2Boolean.InsertOnEdgePoints(h, pool, 4 * Tolerance.Linear)).ToList());
                Triangulation tri = PolygonTriangulator.Triangulate(r.Outer, r.Holes, 4 * Tolerance.Weld, improveQuality: true);
                for (int i = 0; i < tri.Indices.Count; i += 3)
                {
                    Vector2 a = tri.Vertices[tri.Indices[i]], b = tri.Vertices[tri.Indices[i + 1]], c = tri.Vertices[tri.Indices[i + 2]];
                    AddTriangle(Pos(a, plane.XAt(a)), Pos(b, plane.XAt(b)), Pos(c, plane.XAt(c)), tag, normal);
                }
            }
        }
    }

    // ---------------------------------------------------------------- finishing

    private CoreResult Finish(IReadOnlyList<GroupGeometry> geometries, List<FeatureFailure> failures)
    {
        var raw = new List<Vector3>(_tris.Count * 3);
        foreach (var t in _tris)
        {
            raw.Add(t.A);
            raw.Add(t.B);
            raw.Add(t.C);
        }

        (Vector3[] welded, int[] map) = MeshTopology.Weld(raw);
        var indices = new List<int>();
        var tags = new List<FaceTag>();
        for (int i = 0; i < _tris.Count; i++)
        {
            int a = map[3 * i], b = map[(3 * i) + 1], c = map[(3 * i) + 2];
            if (a == b || b == c || a == c)
            {
                continue; // collapsed by welding: zero area, its edges cancel
            }

            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
            tags.Add(_tris[i].Tag);
        }

        MeshTopologyReport report = MeshTopology.Analyze(welded, indices);
        if (!report.IsValidSolid)
        {
            return new CoreResult(null, failures, $"solid.topology: {report}");
        }

        TriangleMesh mesh = MeshSmoothing.WithCreaseNormals(welded, indices, _options.CreaseDeg);
        IReadOnlyList<Guid> applied = geometries.SelectMany(g => g.Plan.Members).Select(m => m.FeatureId).ToList();
        var solid = new TubeSolid(mesh, tags, ChainEdges(), applied, failures, report);
        return new CoreResult(solid, failures, null);
    }

    private List<FeatureEdge> ChainEdges()
    {
        var result = new List<FeatureEdge>();
        static (long, long, long) Key(Vector3 p) => ((long)Math.Round(p.X / Tolerance.Weld), (long)Math.Round(p.Y / Tolerance.Weld), (long)Math.Round(p.Z / Tolerance.Weld));
        foreach (var grp in _edgeSegments.GroupBy(s => (s.Feature, s.Side, s.Loop)))
        {
            var segs = grp.ToList();
            var byStart = new Dictionary<(long, long, long), List<int>>();
            for (int i = 0; i < segs.Count; i++)
            {
                (byStart.TryGetValue(Key(segs[i].P), out List<int>? l) ? l : byStart[Key(segs[i].P)] = []).Add(i);
            }

            var used = new bool[segs.Count];
            for (int i = 0; i < segs.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                var pts = new List<Vector3> { segs[i].P };
                int cur = i;
                bool closed = false;
                while (true)
                {
                    used[cur] = true;
                    pts.Add(segs[cur].Q);
                    if (Key(segs[cur].Q) == Key(pts[0]))
                    {
                        closed = true;
                        pts.RemoveAt(pts.Count - 1);
                        break;
                    }

                    int next = byStart.TryGetValue(Key(segs[cur].Q), out List<int>? cand) ? cand.FirstOrDefault(c => !used[c], -1) : -1;
                    if (next < 0)
                    {
                        break;
                    }

                    cur = next;
                }

                result.Add(new FeatureEdge(grp.Key.Feature, grp.Key.Side, grp.Key.Loop, pts, closed));
            }
        }

        return result;
    }
}
