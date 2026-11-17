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
    private static readonly Material CutterMaterial = Frozen(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(150, 0xE0, 0x3C, 0x3C))));
    private static readonly Material SelectedMaterial = Frozen(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(210, 0xFF, 0xA5, 0x00))));

    private readonly PerspectiveCamera _camera = new() { FieldOfView = OrbitCamera.FieldOfViewDeg };
    private readonly Dictionary<Visual3D, Guid> _featureVisuals = [];
    private readonly Dictionary<Guid, GeometryModel3D> _featureModels = [];
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
                UpdateSelection();
                break;
        }
    }

    private void OnCameraChanged(object? sender, EventArgs e) => ApplyCamera();

    private void RebuildScene()
    {
        Viewport.Children.Clear();
        _featureVisuals.Clear();
        _featureModels.Clear();
        TubeScene? scene = _vm?.Scene;
        if (scene is null)
        {
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

        Viewport.Children.Add(new ModelVisual3D
        {
            Content = new GeometryModel3D(MeshConverter.ToGeometry(scene.Body), BodyMaterial) { BackMaterial = BodyBackMaterial },
        });

        AddAxes(scene.Bounds);

        Guid? selected = _vm?.SelectedFeature?.Id;
        foreach (FeatureShape shape in scene.Features)
        {
            var model = new GeometryModel3D(MeshConverter.ToGeometry(shape.Mesh), shape.FeatureId == selected ? SelectedMaterial : CutterMaterial)
            {
                BackMaterial = shape.FeatureId == selected ? SelectedMaterial : CutterMaterial,
            };
            var visual = new ModelVisual3D { Content = model };
            Viewport.Children.Add(visual);
            _featureVisuals[visual] = shape.FeatureId;
            _featureModels[shape.FeatureId] = model;
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

    private void UpdateSelection()
    {
        Guid? selected = _vm?.SelectedFeature?.Id;
        foreach ((Guid id, GeometryModel3D model) in _featureModels)
        {
            Material m = id == selected ? SelectedMaterial : CutterMaterial;
            model.Material = m;
            model.BackMaterial = m;
        }
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

    /// <summary>Front-most hit decides: a feature id if a cutter is hit first, otherwise null (body / empty space).</summary>
    public Guid? PickFeature(Point position)
    {
        Guid? found = null;
        VisualTreeHelper.HitTest(Viewport, null, result =>
        {
            if (result is RayMeshGeometry3DHitTestResult hit)
            {
                found = _featureVisuals.TryGetValue(hit.VisualHit, out Guid id) ? id : null;
                return HitTestResultBehavior.Stop;
            }

            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(position));
        return found;
    }
}
