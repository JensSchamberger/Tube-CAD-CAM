using TubeCadCam.Infrastructure.StepCorpus;

namespace TubeCadCam.Tests;

/// <summary>
/// STEP-Korpus-Infrastruktur. Reale Daten kommen ausschliesslich aus der Umgebungsvariable
/// TUBECAD_STEP_DIR (Standard: kein Pfad). Sind sie nicht erreichbar, werden die Korpustests von xUnit als
/// SKIPPED gemeldet (nie als bestanden). Es werden keine Rohrdaten erfunden.
/// </summary>
/// <summary>Fact, der ohne erreichbaren STEP-Korpus als ÜBERSPRUNGEN (nicht bestanden) gemeldet wird.</summary>
public sealed class RequiresStepCorpusFactAttribute : FactAttribute
{
    public RequiresStepCorpusFactAttribute()
    {
        var p = Environment.GetEnvironmentVariable(StepCorpusTests.EnvVar);
        if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p))
            Skip = $"STEP-Korpus nicht erreichbar ({StepCorpusTests.EnvVar} nicht gesetzt oder Ordner fehlt) - kein Geometrienachweis.";
    }
}

public class StepCorpusTests
{
    public const string EnvVar = "TUBECAD_STEP_DIR";

    private static string? RealRoot()
    {
        var p = Environment.GetEnvironmentVariable(EnvVar);
        return !string.IsNullOrWhiteSpace(p) && Directory.Exists(p) ? p : null;
    }

    // ---- Parser-Tests: kleine Text-Fixtures nur fuer den Scanner (keine Geometrie, keine Rohrdaten) ----

    private const string Fixture =
        "ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION(('x'),'2;1');\n" +
        "FILE_NAME('a.step','2020-01-01',('au'),('org'),'prep','orig','');\n" +
        "FILE_SCHEMA(('AUTOMOTIVE_DESIGN'));\nENDSEC;\nDATA;\n" +
        "#1=PLANE('',#9);\n#2=ADVANCED_FACE('',(#3),#1,.T.);\n#3 = CYLINDRICAL_SURFACE('',#9,5.);\n" +
        "ENDSEC;\nEND-ISO-10303-21;\n";

    [Fact]
    public void Analyze_text_counts_entities_and_reads_header()
    {
        var i = StepCorpusScanner.AnalyzeText("a.step", 1, "h", Fixture);
        Assert.Equal(3, i.EntityCount);
        Assert.Equal(1, i.EntityCounts["ADVANCED_FACE"]);
        Assert.Equal(1, i.EntityCounts["CYLINDRICAL_SURFACE"]);
        Assert.Equal("AUTOMOTIVE_DESIGN", i.Schema);
        Assert.Equal("a.step", i.FileName);
        Assert.Empty(i.Notes);
    }

    [Fact]
    public void Truncated_or_foreign_files_are_reported_not_hidden()
    {
        var i = StepCorpusScanner.AnalyzeText("b.stp", 1, "h", "garbage");
        Assert.Contains(i.Notes, n => n.StartsWith("step.no-part21-header"));
        Assert.Contains(i.Notes, n => n.StartsWith("step.truncated"));
        Assert.Contains(i.Notes, n => n.StartsWith("step.no-entities"));
    }

    [Fact]
    public void Scan_is_recursive_sorted_deterministic_and_never_modifies_sources()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tubecad-corpus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "z.STEP"), Fixture);
            File.WriteAllText(Path.Combine(dir, "sub", "a.stp"), Fixture);
            File.WriteAllText(Path.Combine(dir, "ignore.txt"), "x");
            var before = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                .ToDictionary(f => f, f => (File.GetLastWriteTimeUtc(f), File.ReadAllBytes(f).Length));

            var m1 = StepCorpusScanner.Scan(dir);
            var m2 = StepCorpusScanner.Scan(dir);

            Assert.Equal(2, m1.FileCount);
            Assert.Equal(["sub/a.stp", "z.STEP"], m1.Files.Select(f => f.RelativePath));
            Assert.Equal(m1.Files.Select(f => f.Sha256), m2.Files.Select(f => f.Sha256));
            Assert.All(m1.Files, f => Assert.DoesNotContain(dir, f.RelativePath));
            foreach (var kv in before)
                Assert.Equal(kv.Value, (File.GetLastWriteTimeUtc(kv.Key), File.ReadAllBytes(kv.Key).Length));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Missing_root_is_an_explicit_error()
    {
        var m = StepCorpusScanner.Scan(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N")));
        Assert.Equal(0, m.FileCount);
        Assert.Contains(m.Errors, e => e.StartsWith("corpus.root-missing"));
    }

    // ---- Korpus-Tests: laufen nur mit realen Daten ----

    [RequiresStepCorpusFact]
    public void Real_corpus_scan_has_no_read_errors()
    {
        var root = RealRoot()!;
        var m = StepCorpusScanner.Scan(root);
        Assert.Empty(m.Errors);
        Assert.True(m.FileCount > 0, "Ordner erreichbar, aber ohne STEP/STP-Dateien.");
    }
}
