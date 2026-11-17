using TubeCadCam.Geometry;

namespace TubeCadCam.Cam;

public enum ContourKind { Hole, Cutout }

/// <summary>Closed contour of one feature, in the feature's local 2D frame (origin = feature centre, u = axial, v = circumferential).</summary>
public sealed record Contour(Guid FeatureId, ContourKind Kind, Polygon2 Outline);
