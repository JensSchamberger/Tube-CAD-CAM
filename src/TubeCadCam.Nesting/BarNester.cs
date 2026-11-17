namespace TubeCadCam.Nesting;

/// <summary>
/// Deterministic 1D nesting (best-fit decreasing) per (profile key, material) group.
/// Every placed part consumes <c>length + kerf</c> (the cut that separates it). Parts are only combined on
/// bars with identical profile key and material. Ties are broken by ordinal id, so results are reproducible.
/// This is a baseline heuristic, not an optimal solver.
/// </summary>
public static class BarNester
{
    public static NestingResult Nest(IEnumerable<NestingPart> parts, IEnumerable<StockBar> stock, NestingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(stock);
        if (settings.KerfMm < 0 || settings.StartTrimMm < 0 || settings.EndTrimMm < 0)
        {
            throw new ArgumentException("Kerf and trims must not be negative.", nameof(settings));
        }

        var assignments = new List<BarAssignment>();
        var unplaced = new List<NestingPart>();

        var groups = parts.GroupBy(p => (p.ProfileKey, p.MaterialId))
            .OrderBy(g => g.Key.ProfileKey, StringComparer.Ordinal)
            .ThenBy(g => g.Key.MaterialId, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            List<StockBar> available = stock
                .Where(b => b.ProfileKey == group.Key.ProfileKey && b.MaterialId == group.Key.MaterialId)
                .OrderBy(b => b.LengthMm).ThenBy(b => b.BarId, StringComparer.Ordinal).ToList();

            var items = group
                .SelectMany(p => Enumerable.Range(0, Math.Max(p.Quantity, 0)).Select(_ => p))
                .OrderByDescending(p => p.LengthMm).ThenBy(p => p.PartId, StringComparer.Ordinal)
                .ToList();

            var open = new List<OpenBar>();
            foreach (NestingPart item in items)
            {
                double need = item.LengthMm + settings.KerfMm;
                OpenBar? target = open.Where(o => o.Remaining >= need)
                    .OrderBy(o => o.Remaining).ThenBy(o => o.Bar.BarId, StringComparer.Ordinal).FirstOrDefault();

                if (target is null)
                {
                    int idx = available.FindIndex(b => b.LengthMm - settings.StartTrimMm - settings.EndTrimMm >= need);
                    if (idx < 0)
                    {
                        unplaced.Add(item with { Quantity = 1 });
                        continue;
                    }

                    StockBar bar = available[idx];
                    available.RemoveAt(idx);
                    target = new OpenBar(bar, bar.LengthMm - settings.StartTrimMm - settings.EndTrimMm, settings.StartTrimMm);
                    open.Add(target);
                }

                target.Placed.Add(new PlacedPart(item.PartId, target.Cursor, item.LengthMm));
                target.Cursor += need;
                target.Remaining -= need;
            }

            assignments.AddRange(open.Select(o => new BarAssignment(o.Bar, o.Placed, o.Remaining)));
        }

        return new NestingResult(assignments, Consolidate(unplaced));
    }

    private static IReadOnlyList<NestingPart> Consolidate(List<NestingPart> unplaced) =>
        unplaced.GroupBy(p => (p.PartId, p.ProfileKey, p.MaterialId, p.LengthMm))
            .OrderBy(g => g.Key.PartId, StringComparer.Ordinal)
            .Select(g => new NestingPart(g.Key.PartId, g.Key.ProfileKey, g.Key.MaterialId, g.Key.LengthMm, g.Count()))
            .ToList();

    private sealed class OpenBar(StockBar bar, double remaining, double cursor)
    {
        public StockBar Bar { get; } = bar;
        public double Remaining { get; set; } = remaining;
        public double Cursor { get; set; } = cursor;
        public List<PlacedPart> Placed { get; } = [];
    }
}
