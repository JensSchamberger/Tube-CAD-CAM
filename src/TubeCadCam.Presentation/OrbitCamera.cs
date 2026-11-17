using TubeCadCam.Geometry;

namespace TubeCadCam.Presentation;

public enum ViewPreset { Isometric, Side, Top, End }

/// <summary>
/// Orbit camera around a target with world +Z up (tube axis = X). Pure math so the interaction logic is testable;
/// the WPF host only forwards mouse input and applies Position/LookDirection/UpDirection.
/// </summary>
public sealed class OrbitCamera
{
    public const double FieldOfViewDeg = 45;
    public const double MinElevationDeg = -89;
    public const double MaxElevationDeg = 89;

    public double AzimuthDeg { get; private set; } = -60;
    public double ElevationDeg { get; private set; } = 25;
    public double Distance { get; private set; } = 500;
    public Vector3 Target { get; private set; } = Vector3.Zero;
    public double MinDistance { get; set; } = 1;
    public double MaxDistance { get; set; } = 1e7;

    public event EventHandler? Changed;

    public Vector3 Position
    {
        get
        {
            double az = AzimuthDeg * Math.PI / 180, el = ElevationDeg * Math.PI / 180;
            return Target + (new Vector3(Math.Cos(el) * Math.Cos(az), Math.Cos(el) * Math.Sin(az), Math.Sin(el)) * Distance);
        }
    }

    public Vector3 LookDirection => (Target - Position).Normalized();
    public Vector3 Right => LookDirection.Cross(new Vector3(0, 0, 1)).Normalized();
    public Vector3 UpDirection => Right.Cross(LookDirection).Normalized();

    /// <summary>Rotates by a mouse movement in pixels (dragging right turns the model to the right).</summary>
    public void Orbit(double dxPixels, double dyPixels, double degreesPerPixel = 0.4)
    {
        AzimuthDeg -= dxPixels * degreesPerPixel;
        ElevationDeg = Math.Clamp(ElevationDeg + (dyPixels * degreesPerPixel), MinElevationDeg, MaxElevationDeg);
        Raise();
    }

    /// <summary>Moves the target parallel to the view plane; the model follows the mouse.</summary>
    public void Pan(double dxPixels, double dyPixels, double viewportHeightPixels)
    {
        double perPixel = 2 * Distance * Math.Tan(FieldOfViewDeg * Math.PI / 360) / Math.Max(viewportHeightPixels, 1);
        Target += (Right * (-dxPixels * perPixel)) + (UpDirection * (dyPixels * perPixel));
        Raise();
    }

    /// <summary>Zooms by a mouse-wheel delta (120 = one notch); positive delta moves closer.</summary>
    public void Zoom(double wheelDelta)
    {
        Distance = Math.Clamp(Distance * Math.Pow(0.9, wheelDelta / 120.0), MinDistance, MaxDistance);
        Raise();
    }

    public void Fit(Bounds3 bounds)
    {
        Target = bounds.Center;
        double r = Math.Max(bounds.Radius, 1);
        Distance = Math.Clamp(r / Math.Sin(FieldOfViewDeg * Math.PI / 360) * 1.15, MinDistance, MaxDistance);
        Raise();
    }

    public void SetPreset(ViewPreset preset)
    {
        (AzimuthDeg, ElevationDeg) = preset switch
        {
            ViewPreset.Isometric => (-60.0, 25.0),
            ViewPreset.Side => (-90.0, 0.0),
            ViewPreset.Top => (-90.0, MaxElevationDeg),
            ViewPreset.End => (0.0, 0.0),
            _ => throw new ArgumentOutOfRangeException(nameof(preset)),
        };
        Raise();
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
