namespace TubeCadCam.Geometry;

public readonly record struct Bounds2(Vector2 Min, Vector2 Max)
{
    public double Width => Max.X - Min.X;
    public double Height => Max.Y - Min.Y;
}
