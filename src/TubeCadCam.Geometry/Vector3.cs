namespace TubeCadCam.Geometry;

public readonly record struct Vector3(double X, double Y, double Z)
{
    public static Vector3 Zero => new(0, 0, 0);
    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public Vector3 Normalized()
    {
        double l = Length;
        return l <= 0 ? this : new Vector3(X / l, Y / l, Z / l);
    }

    public double Dot(Vector3 o) => (X * o.X) + (Y * o.Y) + (Z * o.Z);
    public Vector3 Cross(Vector3 o) => new((Y * o.Z) - (Z * o.Y), (Z * o.X) - (X * o.Z), (X * o.Y) - (Y * o.X));
}
