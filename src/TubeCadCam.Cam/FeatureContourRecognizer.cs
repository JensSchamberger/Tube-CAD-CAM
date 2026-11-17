using TubeCadCam.Cad;

namespace TubeCadCam.Cam;

/// <summary>
/// Derives closed contours from wall-cut CAD features (hole, cutout, slot, contour cut-out, end notch).
/// Not yet recognised (reported as such): slits (through both walls), miters (end cuts), and contours from imported solids (STEP).
/// </summary>
public sealed class FeatureContourRecognizer : IContourRecognizer
{
    public const int CircleSegments = FeatureTessellation.CircleSegments;
    public const int CornerSegments = FeatureTessellation.CornerSegments;

    public RecognitionResult Recognize(TubeModel model)
    {
        var contours = new List<Contour>();
        var unrecognized = new List<Guid>();
        foreach (TubeFeature f in model.Features)
        {
            ContourKind? kind = f switch
            {
                HoleFeature => ContourKind.Hole,
                RectangularCutoutFeature => ContourKind.Cutout,
                SlotFeature => ContourKind.Slot,
                ContourCutoutFeature => ContourKind.ContourCutout,
                EndNotchFeature => ContourKind.EndNotch,
                _ => null,
            };

            if (kind is { } k && f is IWallCut cut)
            {
                contours.Add(new Contour(f.Id, k, cut.Outline(CircleSegments, CornerSegments)));
            }
            else
            {
                unrecognized.Add(f.Id);
            }
        }

        return new RecognitionResult(contours, unrecognized);
    }
}
