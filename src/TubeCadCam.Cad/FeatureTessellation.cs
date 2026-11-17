namespace TubeCadCam.Cad;

/// <summary>
/// Single definition of how curved feature outlines (holes, rounded corners, slot ends) are polygonised.
/// The solid builder (CAD) and the contour recogniser (CAM) both use these values, so CAM sees exactly the CAD edges.
/// </summary>
public static class FeatureTessellation
{
    public const int CircleSegments = 128;
    public const int CornerSegments = 12;
}
