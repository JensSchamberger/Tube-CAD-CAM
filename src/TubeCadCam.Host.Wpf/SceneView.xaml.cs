using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using TubeCadCam.Cad;
using TubeCadCam.Geometry;
using TubeCadCam.Presentation;

namespace TubeCadCam.Host.Wpf;

/// <summary>Interactive 3D view. All camera maths lives in <see cref="OrbitCamera"/>; this class only forwards input and renders.</summary>
public partial class SceneView : UserControl
{
    private static readonly Material BodyMaterial = Frozen(new MaterialGroup
    {
        Children = { new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0xB4, 0xBC, 0xC6))), new SpecularMaterial(Brushes.White, 40) },
    });

    private static readonly Material BodyBackMaterial = Frozen(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0x6C, 0x74, 0x80))));
    private static readonly Material PreviewMaterial = Frozen(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(70, 0x4C, 0x9B, 0xE8))));
    private static readonly Material SelectedMaterial = Frozen(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(120, 0xFF, 0xA5, 0x00))));
    private static readonly Material WallHighlightMaterial = Frozen(new MaterialGroup
    {
        Children = { new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00))), new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(120, 0xFF, 0x8C, 0x00))) },
    });

    private readonly PerspectiveCamera _camera = new() { FieldOfView = OrbitCamera.FieldOfViewDeg };
    private readonly Dictionary<Visual3D, Guid> _featureVisuals = [];
    private GeometryModel3D? _bodyModel;
    private TubeScene? _bodyScene;
    private MeshGeometry3D? _bodyGeometry;
    private MainViewModel? _vm;
    private Point _lastPoint, _downPoint;
    private bool _moved;
    private MouseButton? _button;

    public SceneView()
    {
        InitializeComponent();
        Viewport.Camera = _camera;
        DataContextChanged += OnDataContextChanged;
        Surface.MouseDown += OnMouseDown;
        Surface.MouseMove += OnMouseMove;
        Surface.MouseUp += OnMouseUp;
        Surface.MouseWheel += OnMouseWheel;
        SizeChanged += (_, _) => ApplyCamera();
    }

    /// <summary>Number of feature cutter visuals currently displayed (used by UI tests).</summary>
    public int FeatureVisualCount => _featureVisuals.Count;

    /// <summary>Triangle count of the displayed body (used by UI tests).</summary>
    public int BodyTriangleCount => _bodyGeometry?.TriangleIndices.Count / 3 ?? 0;

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.Camera.Changed -= OnCameraChanged;
        }

        _vm = e.NewValue as MainViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.Camera.Changed += OnCameraChanged;
        }

        RebuildScene();
        ApplyCamera();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Scene):
                RebuildScene();
                ApplyCamera();
                break;
            case nameof(MainViewModel.SelectedFeature):
            case nameof(MainViewModel.ShowFeaturePreview):
                RebuildScene();
                break;
        }
    }

    private void OnCameraChanged(object? sender, EventArgs e) => ApplyCamera();

    private void RebuildScene()
    {
        Viewport.Children.Clear();
        _featureVisuals.Clear();
        _bodyModel = null;
        TubeScene? scene = _vm?.Scene;
        if (scene is null)
        {
            _bodyGeometry = null;
            _bodyScene = null;
            return;
        }

        Viewport.Children.Add(new ModelVisual3D
        {
            Content = new Model3DGroup
            {
                Children =
                {
                    new AmbientLight(Color.FromRgb(0x58, 0x58, 0x58)),
                    new DirectionalLight(Color.FromRgb(0xC8, 0xC8, 0xC8), new Vector3D(-1, -0.6, -1.2)),
                    new DirectionalLight(Color.FromRgb(0x60, 0x60, 0x60), new Vector3D(1, 1, 0.4)),
                },
            },
        });

        // The real, machined body (openings are part of the mesh). The geometry is converted once per scene.
        if (!ReferenceEquals(_bodyScene, scene))
        {
            _bodyGeometry = MeshConverter.ToGeometry(scene.Body);
            _bodyScene = scene;
        }

        _bodyModel = new GeometryModel3D(_bodyGeometry, BodyMaterial) { BackMaterial = BodyBackMaterial };
        Viewport.Children.Add(new ModelVisual3D { Content = _bodyModel });

        AddAxes(scene.Bounds);

        Guid? selected = _vm?.SelectedFeature?.Id;
        bool showAll = _vm?.ShowFeaturePreview == true;

        // opening wall of the selected feature, highlighted on top of the body
        if (selected is { } sel)
        {
            int[] wall = scene.Solid.TrianglesOf(sel).ToArray();
            if (wall.Length > 0)
            {
                Viewport.Children.Add(new ModelVisual3D
                {
                    Content = new GeometryModel3D(MeshConverter.ToGeometry(scene.Solid.ExtractMesh(wall, 0.03)), WallHighlightMaterial)
                    {
                        BackMaterial = WallHighlightMaterial,
                    },
                });
            }
        }

        // optional transparent preview of the cutting volumes (a visual aid only)
        foreach (FeatureShape shape in scene.PreviewShapes)
        {
            bool isSelected = shape.FeatureId == selected;
            if (!showAll && !isSelected)
            {
                continue;
            }

            Material m = isSelected ? SelectedMaterial : PreviewMaterial;
            var visual = new ModelVisual3D { Content = new GeometryModel3D(MeshConverter.ToGeometry(shape.Mesh), m) { BackMaterial = m } };
            Viewport.Children.Add(visual);
            _featureVisuals[visual] = shape.FeatureId;
        }
    }

    private void AddAxes(Bounds3 bounds)
    {
        double len = Math.Max(bounds.Radius * 0.25, 20), r = len * 0.02;
        var square = Polygon2.Rectangle(2 * r, 2 * r, Vector2.Zero);
        void Axis(Func<Vector2, double, Vector3> map, Color color)
        {
            var mesh = PrismBuilder.Build(square, map, 0, len);
            Viewport.Children.Add(new ModelVisual3D { Content = new GeometryModel3D(MeshConverter.ToGeometry(mesh), Frozen(new DiffuseMaterial(new SolidColorBrush(color)))) });
        }

        Axis((q, l) => new Vector3(l, q.X, q.Y), Colors.OrangeRed);       // X = tube axis
        Axis((q, l) => new Vector3(q.X, l, q.Y), Colors.LimeGreen);       // Y (0°)
        Axis((q, l) => new Vector3(q.X, q.Y, l), Colors.DodgerBlue);      // Z (90°)
    }

    private void ApplyCamera()
    {
        if (_vm is null)
        {
            return;
        }

        OrbitCamera c = _vm.Camera;
        double radius = _vm.Scene?.Bounds.Radius ?? 100;
        _camera.Position = new Point3D(c.Position.X, c.Position.Y, c.Position.Z);
        _camera.LookDirection = new Vector3D(c.LookDirection.X, c.LookDirection.Y, c.LookDirection.Z);
        _camera.UpDirection = new Vector3D(c.UpDirection.X, c.UpDirection.Y, c.UpDirection.Z);
        _camera.NearPlaneDistance = Math.Max(c.Distance * 0.01, 0.1);
        _camera.FarPlaneDistance = (c.Distance + (radius * 4)) * 4;
    }

    // ---- mouse ----

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        _button = e.ChangedButton;
        _lastPoint = _downPoint = e.GetPosition(Surface);
        _moved = false;
        Surface.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_vm is null || _button is null || !Surface.IsMouseCaptured)
        {
            return;
        }

        Point p = e.GetPosition(Surface);
        double dx = p.X - _lastPoint.X, dy = p.Y - _lastPoint.Y;
        _lastPoint = p;
        if (Math.Abs(p.X - _downPoint.X) > 3 || Math.Abs(p.Y - _downPoint.Y) > 3)
        {
            _moved = true;
        }

        if (_button == MouseButton.Left)
        {
            _vm.Camera.Orbit(dx, dy);
        }
        else
        {
            _vm.Camera.Pan(dx, dy, Math.Max(Surface.ActualHeight, 1));
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        Surface.ReleaseMouseCapture();
        if (_vm is not null && e.ChangedButton == MouseButton.Left && !_moved)
        {
            _vm.SelectFeature(PickFeature(e.GetPosition(Viewport)));
        }

        _button = null;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e) => _vm?.Camera.Zoom(e.Delta);

    /// <summary>
    /// Front-most hit decides: a preview volume or the wall of an opening selects that feature; the body surface elsewhere
    /// or empty space returns null. The wall is identified through the face provenance of the hit triangle.
    /// </summary>
    public Guid? PickFeature(Point position)
    {
        Guid? found = null;
        VisualTreeHelper.HitTest(Viewport, null, result =>
        {
            if (result is not RayMeshGeometry3DHitTestResult hit)
            {
                return HitTestResultBehavior.Continue;
            }

            if (_featureVisuals.TryGetValue(hit.VisualHit, out Guid id))
            {
                found = id;
            }
            else if (ReferenceEquals(hit.ModelHit, _bodyModel) && _bodyScene is { } scene)
            {
                int triangle = hit.VertexIndex1 / 3; // the render mesh has three own vertices per triangle, in order
                if (triangle >= 0 && triangle < scene.Solid.TriangleTags.Count)
                {
                    found = scene.Solid.TriangleTags[triangle].FeatureId;
                }
            }

            return HitTestResultBehavior.Stop;
        }, new PointHitTestParameters(position));
        return found;
    }
}
