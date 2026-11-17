using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

/// <summary>Polygonal (tessellated) cross-section: outer loop (CCW) plus void loops, for preview and geometric checks.</summary>
public sealed record ProfileSection(Polygon2 Outer, IReadOnlyList<Polygon2> Voids)
{
    public double Area => Outer.Area - Voids.Sum(v => v.Area);
}
