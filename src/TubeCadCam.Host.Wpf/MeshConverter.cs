using System.Windows.Media.Media3D;
using TubeCadCam.Geometry;

namespace TubeCadCam.Host.Wpf;

public static class MeshConverter
{
    public static MeshGeometry3D ToGeometry(TriangleMesh mesh)
    {
        var g = new MeshGeometry3D
        {
            Positions = new Point3DCollection(mesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
            Normals = new Vector3DCollection(mesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z))),
            TriangleIndices = new System.Windows.Media.Int32Collection(mesh.Indices),
        };
        g.Freeze();
        return g;
    }
}
