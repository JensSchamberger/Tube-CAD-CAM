using System.Text.Json.Serialization;

namespace TubeCadCam.Cad;

/// <summary>
/// Machining feature positioned in the tube's own coordinate system:
/// axial position along the tube axis (mm from the start face) and angular position around it (degrees).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$feature")]
[JsonDerivedType(typeof(HoleFeature), "hole")]
[JsonDerivedType(typeof(RectangularCutoutFeature), "cutout")]
public abstract record TubeFeature(Guid Id, double AxialPositionMm, double AngleDeg)
{
    /// <summary>Extent of the feature along the tube axis in mm.</summary>
    [JsonIgnore] public abstract double AxialExtentMm { get; }
}

public sealed record HoleFeature(Guid Id, double AxialPositionMm, double AngleDeg, double DiameterMm)
    : TubeFeature(Id, AxialPositionMm, AngleDeg)
{
    public override double AxialExtentMm => DiameterMm;
}

/// <summary>Rectangular cutout/through-opening; <c>WidthMm</c> is measured circumferentially, <c>LengthMm</c> axially.</summary>
public sealed record RectangularCutoutFeature(
    Guid Id, double AxialPositionMm, double AngleDeg, double WidthMm, double LengthMm, double CornerRadiusMm = 0)
    : TubeFeature(Id, AxialPositionMm, AngleDeg)
{
    public override double AxialExtentMm => LengthMm;
}
