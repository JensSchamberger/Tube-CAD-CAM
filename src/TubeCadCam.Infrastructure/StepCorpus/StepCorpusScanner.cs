using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TubeCadCam.Infrastructure.StepCorpus;

/// <summary>Ergebnis der reinen Text-Analyse einer STEP-Datei (ISO 10303-21). Keine Geometrie, keine Interpretation.</summary>
public sealed record StepFileInfo(
    string RelativePath, long SizeBytes, string Sha256,
    string? Schema, string? FileName, string? Originator, string? Preprocessor,
    int EntityCount, IReadOnlyDictionary<string, int> EntityCounts,
    IReadOnlyList<string> Notes);

/// <summary>Reproduzierbarer Korpus-Katalog: sortiert, ohne absolute Pfade, ohne Zeitstempel.</summary>
public sealed record StepCorpusManifest(int Version, int FileCount, IReadOnlyList<StepFileInfo> Files, IReadOnlyList<string> Errors);

/// <summary>
/// Erfasst *.step/*.stp rekursiv (read-only) und katalogisiert sie. Die Quelldateien werden ausschliesslich gelesen.
/// Das Manifest enthaelt Hashes und Entitaetsstatistik, aber keine Dateiinhalte und keine absoluten Pfade.
/// </summary>
public static partial class StepCorpusScanner
{
    public const int ManifestVersion = 1;

    /// <summary>Geometrierelevante Entitaeten, die im Manifest immer ausgewiesen werden (Hinweise, keine Erkennung).</summary>
    public static readonly string[] KeyEntities =
    [
        "ADVANCED_FACE", "CYLINDRICAL_SURFACE", "PLANE", "CIRCLE", "ELLIPSE", "B_SPLINE_SURFACE_WITH_KNOTS",
        "TOROIDAL_SURFACE", "CONICAL_SURFACE", "MANIFOLD_SOLID_BREP", "SHELL_BASED_SURFACE_MODEL", "PRODUCT",
    ];

    public static StepCorpusManifest Scan(string root)
    {
        var errors = new List<string>();
        var files = new List<StepFileInfo>();
        if (!Directory.Exists(root))
        {
            errors.Add("corpus.root-missing: Ordner nicht erreichbar.");
            return new StepCorpusManifest(ManifestVersion, 0, files, errors);
        }

        var opt = new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive, IgnoreInaccessible = true };
        var paths = Directory.EnumerateFiles(root, "*", opt)
            .Where(p => p.EndsWith(".step", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".stp", StringComparison.OrdinalIgnoreCase))
            .Select(p => (Full: p, Rel: Path.GetRelativePath(root, p).Replace('\\', '/')))
            .OrderBy(x => x.Rel, StringComparer.Ordinal);

        foreach (var (full, rel) in paths)
        {
            try { files.Add(Analyze(full, rel)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"corpus.read-failed: {rel}: {ex.GetType().Name}");
            }
        }
        return new StepCorpusManifest(ManifestVersion, files.Count, files, errors);
    }

    public static StepFileInfo Analyze(string fullPath, string relativePath)
    {
        // FileShare.Read + nur Lesezugriff: Originaldatei bleibt unveraendert.
        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bytes = new byte[fs.Length];
        fs.ReadExactly(bytes);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return AnalyzeText(relativePath, bytes.LongLength, sha, Encoding.Latin1.GetString(bytes));
    }

    public static StepFileInfo AnalyzeText(string relativePath, long size, string sha256, string text)
    {
        var notes = new List<string>();
        if (!text.TrimStart().StartsWith("ISO-10303-21", StringComparison.Ordinal))
            notes.Add("step.no-part21-header: Datei beginnt nicht mit ISO-10303-21.");
        if (!text.Contains("END-ISO-10303-21", StringComparison.Ordinal))
            notes.Add("step.truncated: END-ISO-10303-21 fehlt.");

        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (Match m in EntityRegex().Matches(text))
        {
            var t = m.Groups[1].Value.ToUpperInvariant();
            counts[t] = counts.GetValueOrDefault(t) + 1;
        }
        var total = counts.Values.Sum();
        if (total == 0) notes.Add("step.no-entities: keine Entitaeten erkannt.");
        else if (!counts.ContainsKey("ADVANCED_FACE") && !counts.ContainsKey("FACE_SURFACE"))
            notes.Add("step.no-faces: keine Flaechen (ADVANCED_FACE/FACE_SURFACE) – evtl. nur Kurven/Assembly.");

        var key = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var k in KeyEntities) if (counts.TryGetValue(k, out var c)) key[k] = c;

        return new StepFileInfo(relativePath, size, sha256,
            Schema: Header("FILE_SCHEMA", text, 0), FileName: Header("FILE_NAME", text, 0),
            Originator: Header("FILE_NAME", text, 5), Preprocessor: Header("FILE_NAME", text, 4),
            total, key, notes);
    }

    /// <summary>Liest das n-te Argument eines Header-Eintrags als Rohtext (stark vereinfacht; nur Anzeige).</summary>
    private static string? Header(string entry, string text, int argIndex)
    {
        var start = text.IndexOf(entry + "(", StringComparison.Ordinal);
        if (start < 0) return null;
        var end = text.IndexOf(");", start, StringComparison.Ordinal);
        if (end < 0) return null;
        var body = text[(start + entry.Length + 1)..end];
        var args = new List<string>(); var depth = 0; var inStr = false; var sb = new StringBuilder();
        foreach (var ch in body)
        {
            if (ch == '\'') inStr = !inStr;
            if (!inStr && ch == '(') depth++;
            if (!inStr && ch == ')') depth--;
            if (!inStr && depth == 0 && ch == ',') { args.Add(sb.ToString().Trim()); sb.Clear(); continue; }
            sb.Append(ch);
        }
        args.Add(sb.ToString().Trim());
        if (argIndex >= args.Count) return null;
        var v = args[argIndex].Trim('(', ')', '\'', ' ');
        return v.Length == 0 ? null : v.ToString(CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"#\d+\s*=\s*(?:\(\s*)?([A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex EntityRegex();
}
