using TubeCadCam.Cad;
using TubeCadCam.Geometry;

namespace TubeCadCam.Cam;

/// <summary>
/// Derives contours from explicit CAD features (holes, rectangular cutouts).
/// Recognition of contours from imported solids (STEP) is not implemented yet.
/// </summary>
public sealed class FeatureContourRecognizer : IContourRecognizer
{
    public const int CircleSegments = 128;
    public const int CornerSegments = 12;

    public IReadOnlyList<Contour> Recognize(TubeModel model)
    {
        var result = new List<Contour>();
        foreach (TubeFeature f in model.Features)
        {
            switch (f)
            {
                case HoleFeature h:
                    result.Add(new Contour(h.Id, ContourKind.Hole, Polygon2.Circle(Vector2.Zero, h.DiameterMm / 2, CircleSegments)));
                    break;
                case RectangularCutoutFeature c:
                    result.Add(new Contour(c.Id, ContourKind.Cutout,
                        Polygon2.RoundedRectangle(c.LengthMm, c.WidthMm, c.CornerRadiusMm, Vector2.Zero, CornerSegments)));
                    break;
            }
        }

        return result;
    }
}
