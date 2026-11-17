using TubeCadCam.Cad;

namespace TubeCadCam.Cam;

public interface IContourRecognizer
{
    IReadOnlyList<Contour> Recognize(TubeModel model);
}
