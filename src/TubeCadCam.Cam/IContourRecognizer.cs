using TubeCadCam.Cad;

namespace TubeCadCam.Cam;

public sealed record RecognitionResult(IReadOnlyList<Contour> Contours, IReadOnlyList<Guid> UnrecognizedFeatureIds);

public interface IContourRecognizer
{
    /// <summary>Contours of the features it understands; the ids of all other features are reported, never dropped silently.</summary>
    RecognitionResult Recognize(TubeModel model);
}
