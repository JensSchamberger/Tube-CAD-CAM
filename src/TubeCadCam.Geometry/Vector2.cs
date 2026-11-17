namespace TubeCadCam.Geometry;

public readonly record struct Vector2(double X, double Y)
{
    public static Vector2 Zero => new(0, 0);
    public double Length => Math.Sqrt((X * X) + (Y * Y));
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, double s) => new(a.X * s, a.Y * s);
    public double Dot(Vector2 o) => (X * o.X) + (Y * o.Y);
    public double Cross(Vector2 o) => (X * o.Y) - (Y * o.X);
    public double DistanceTo(Vector2 o) => (this - o).Length;
}
