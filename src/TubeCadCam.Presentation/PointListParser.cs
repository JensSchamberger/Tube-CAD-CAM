using System.Globalization;
using TubeCadCam.Domain.Common;
using TubeCadCam.Geometry;

namespace TubeCadCam.Presentation;

/// <summary>Parses point lists: one "x;y" (or "x y") per line, '.' as decimal separator; blank lines separate loops.</summary>
public static class PointListParser
{
    public static (IReadOnlyList<IReadOnlyList<Vector2>> Loops, IReadOnlyList<ValidationIssue> Issues) Parse(string? text, string what)
    {
        var loops = new List<IReadOnlyList<Vector2>>();
        var issues = new List<ValidationIssue>();
        var current = new List<Vector2>();
        int lineNo = 0;
        foreach (string raw in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
        {
            lineNo++;
            string line = raw.Trim();
            if (line.Length == 0)
            {
                if (current.Count > 0)
                {
                    loops.Add(current);
                    current = [];
                }

                continue;
            }

            string[] parts = line.Split([';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y)
                && double.IsFinite(x) && double.IsFinite(y))
            {
                current.Add(new Vector2(x, y));
            }
            else
            {
                issues.Add(new ValidationIssue("input.point-invalid", $"{what}, line {lineNo}: expected \"x;y\" with '.' as decimal separator."));
            }
        }

        if (current.Count > 0)
        {
            loops.Add(current);
        }

        return (loops, issues);
    }

    public static string Format(Polygon2 polygon) =>
        string.Join("\n", polygon.Vertices.Select(v => string.Create(CultureInfo.InvariantCulture, $"{v.X:0.###};{v.Y:0.###}")));
}
