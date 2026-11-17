namespace TubeCadCam.Geometry;

public readonly record struct Vector2(double X, double Y)
{
    public static Vector2 Zero => new(0, 0);
    public double Length => Math.Sqrt((X * X) + (Y * Y));
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, double s) => new(a.X * s, a.Y * s);
    public Vector2 Normalized()
    {
        double l = Length;
        return l <= 0 ? this : new Vector2(X / l, Y / l);
    }

    /// <summary>Rotates counter-clockwise by <paramref name="degrees"/> about the origin.</summary>
    public Vector2 Rotated(double degrees)
    {
        double a = degrees * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
        return new Vector2((X * c) - (Y * s), (X * s) + (Y * c));
    }

    public double Dot(Vector2 o) => (X * o.X) + (Y * o.Y);
    public double Cross(Vector2 o) => (X * o.Y) - (Y * o.X);
    public double DistanceTo(Vector2 o) => (this - o).Length;
}
