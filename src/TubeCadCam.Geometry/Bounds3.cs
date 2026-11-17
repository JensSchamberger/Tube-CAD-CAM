namespace TubeCadCam.Geometry;

public readonly record struct Bounds3(Vector3 Min, Vector3 Max)
{
    public Vector3 Center => new((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2, (Min.Z + Max.Z) / 2);
    public double Radius => (Max - Min).Length / 2;

    public static Bounds3 Of(IEnumerable<Vector3> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        bool any = false;
        foreach (Vector3 p in points)
        {
            any = true;
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); minZ = Math.Min(minZ, p.Z);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); maxZ = Math.Max(maxZ, p.Z);
        }

        return any ? new Bounds3(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ)) : default;
    }
}
