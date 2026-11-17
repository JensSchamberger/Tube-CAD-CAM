using TubeCadCam.Cad;

namespace TubeCadCam.Cam;

/// <summary>
/// <see cref="Draft"/>: geometry analysed only (contours recognised).
/// <see cref="Validated"/>: complete technical validation passed (requires toolpaths, sequencing, collision check - not yet implemented).
/// </summary>
public enum CamPlanStatus { Draft, Validated }

public sealed record CamPlan(TubeModel Model, IReadOnlyList<Contour> Contours, CamPlanStatus Status);
