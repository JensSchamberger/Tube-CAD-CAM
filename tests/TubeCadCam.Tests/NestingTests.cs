using TubeCadCam.Nesting;

namespace TubeCadCam.Tests;

public class NestingTests
{
    private static readonly NestingSettings NoTrim = new(0, 0, 0);

    private static StockBar Bar(string id, double len, string profile = "p", string mat = "m") => new(id, profile, mat, len);
    private static NestingPart Part(string id, double len, int qty, string profile = "p", string mat = "m") => new(id, profile, mat, len, qty);

    [Fact]
    public void Parts_are_packed_with_kerf_and_offsets()
    {
        var r = BarNester.Nest([Part("A", 400, 2), Part("B", 150, 1)], [Bar("S1", 1000)], new NestingSettings(3, 5, 10));
        BarAssignment a = Assert.Single(r.Assignments);
        Assert.Empty(r.Unplaced);
        Assert.Equal(["A", "A", "B"], a.Placed.Select(p => p.PartId));
        Assert.Equal([5.0, 408.0, 811.0], a.Placed.Select(p => p.OffsetMm));
        Assert.Equal(1000 - 5 - 10 - ((400 + 3) * 2) - (150 + 3), a.RemnantMm, 9);
    }

    [Fact]
    public void Parts_never_overlap_or_exceed_bar()
    {
        var parts = Enumerable.Range(1, 12).Select(i => Part($"P{i}", 100 + (i * 37 % 300), 3)).ToList();
        var stock = Enumerable.Range(1, 40).Select(i => Bar($"S{i}", 3000)).ToList();
        var s = new NestingSettings(2, 4, 6);
        var r = BarNester.Nest(parts, stock, s);
        Assert.Empty(r.Unplaced);
        foreach (BarAssignment a in r.Assignments)
        {
            double end = s.StartTrimMm;
            foreach (PlacedPart p in a.Placed)
            {
                Assert.True(p.OffsetMm >= end - 1e-9);
                end = p.OffsetMm + p.LengthMm + s.KerfMm;
            }

            Assert.True(end <= a.Bar.LengthMm - s.EndTrimMm + 1e-9);
            Assert.True(a.RemnantMm >= 0);
        }

        Assert.Equal(parts.Sum(p => p.Quantity), r.Assignments.Sum(a => a.Placed.Count));
    }

    [Fact]
    public void Profile_and_material_must_match()
    {
        var r = BarNester.Nest([Part("A", 100, 1, "p1", "m1")], [Bar("S1", 1000, "p2", "m1"), Bar("S2", 1000, "p1", "m2")], NoTrim);
        Assert.Empty(r.Assignments);
        Assert.Equal(1, Assert.Single(r.Unplaced).Quantity);
    }

    [Fact]
    public void Smallest_sufficient_bar_is_used_first_so_remnants_are_consumed()
    {
        var r = BarNester.Nest([Part("A", 450, 1)], [Bar("LONG", 6000), Bar("REM", 500)], NoTrim);
        Assert.Equal("REM", Assert.Single(r.Assignments).Bar.BarId);
    }

    [Fact]
    public void Too_long_part_is_reported_unplaced()
    {
        var r = BarNester.Nest([Part("A", 1000, 2)], [Bar("S", 999)], NoTrim);
        Assert.Equal(2, Assert.Single(r.Unplaced).Quantity);
    }

    [Fact]
    public void Kerf_makes_exact_fit_impossible()
    {
        var r = BarNester.Nest([Part("A", 500, 2)], [Bar("S", 1000)], new NestingSettings(1, 0, 0));
        Assert.Single(r.Unplaced);
        Assert.Single(Assert.Single(r.Assignments).Placed);
    }

    [Fact]
    public void Result_is_independent_of_input_order()
    {
        var parts = new[] { Part("A", 300, 3), Part("B", 700, 2), Part("C", 120, 5) };
        var stock = new[] { Bar("S1", 2000), Bar("S2", 1500), Bar("S3", 2000) };
        string Sig(NestingResult r) => string.Join("|", r.Assignments.Select(a => a.Bar.BarId + ":" + string.Join(",", a.Placed.Select(p => p.PartId + p.OffsetMm))));
        var s = new NestingSettings(2, 0, 0);
        Assert.Equal(Sig(BarNester.Nest(parts, stock, s)), Sig(BarNester.Nest(parts.Reverse(), stock.Reverse(), s)));
    }

    [Fact]
    public void Negative_settings_throw() =>
        Assert.Throws<ArgumentException>(() => BarNester.Nest([], [], new NestingSettings(-1, 0, 0)));
}
