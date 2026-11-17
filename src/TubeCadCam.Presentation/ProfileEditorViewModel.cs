using System.Collections.ObjectModel;
using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;

namespace TubeCadCam.Presentation;

/// <summary>Edits the parameters of a parametric profile and exposes validation, area and a normalised 2D preview.</summary>
public sealed class ProfileEditorViewModel : ViewModelBase
{
    public const double PreviewSize = 200;

    private ProfileKind _kind;
    private ProfileDefinition? _definition;
    private IReadOnlyList<ValidationIssue> _issues = [];
    private IReadOnlyList<IReadOnlyList<Vector2>> _previewLoops = [];

    public ProfileEditorViewModel()
    {
        Parameters = [];
        Kinds = Enum.GetValues<ProfileKind>();
        _kind = ProfileKind.Round;
        LoadDefaults(_kind);
        Recalculate();
    }

    public IReadOnlyList<ProfileKind> Kinds { get; }
    public ObservableCollection<ParameterViewModel> Parameters { get; }

    public ProfileKind Kind
    {
        get => _kind;
        set
        {
            if (Set(ref _kind, value))
            {
                LoadDefaults(value);
                Recalculate();
            }
        }
    }

    public ProfileDefinition? Definition { get => _definition; private set => Set(ref _definition, value); }
    public IReadOnlyList<ValidationIssue> Issues { get => _issues; private set => Set(ref _issues, value); }
    public bool IsValid => Definition is not null && Issues.Count == 0;
    public double AreaMm2 => IsValid ? Definition!.CrossSectionAreaMm2 : double.NaN;

    /// <summary>Preview polygons scaled into a <see cref="PreviewSize"/> box, y pointing down (screen coordinates).</summary>
    public IReadOnlyList<IReadOnlyList<Vector2>> PreviewLoops { get => _previewLoops; private set => Set(ref _previewLoops, value); }

    private void LoadDefaults(ProfileKind kind)
    {
        // Illustrative starting values for the editor only; not technology data.
        (string, double)[] defaults = kind switch
        {
            ProfileKind.Round => [("OuterDiameter", 60), ("WallThickness", 3)],
            ProfileKind.Rectangular => [("Width", 80), ("Height", 40), ("WallThickness", 3), ("CornerRadius", 5)],
            ProfileKind.Square => [("Side", 50), ("WallThickness", 3), ("CornerRadius", 5)],
            ProfileKind.U => [("Height", 100), ("FlangeWidth", 50), ("WebThickness", 6), ("FlangeThickness", 8)],
            ProfileKind.L => [("LegA", 50), ("LegB", 50), ("Thickness", 5)],
            ProfileKind.I => [("Height", 100), ("FlangeWidth", 55), ("WebThickness", 4), ("FlangeThickness", 6)],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        Parameters.Clear();
        foreach ((string name, double value) in defaults)
        {
            Parameters.Add(new ParameterViewModel(name, value, Recalculate));
        }
    }

    private double P(string name) => Parameters.First(p => p.Name == name).Value;

    private ProfileDefinition BuildDefinition() => _kind switch
    {
        ProfileKind.Round => new RoundTubeProfile(P("OuterDiameter"), P("WallThickness")),
        ProfileKind.Rectangular => new RectangularTubeProfile(P("Width"), P("Height"), P("WallThickness"), P("CornerRadius")),
        ProfileKind.Square => RectangularTubeProfile.Square(P("Side"), P("WallThickness"), P("CornerRadius")),
        ProfileKind.U => new UProfile(P("Height"), P("FlangeWidth"), P("WebThickness"), P("FlangeThickness")),
        ProfileKind.L => new LProfile(P("LegA"), P("LegB"), P("Thickness")),
        ProfileKind.I => new IProfile(P("Height"), P("FlangeWidth"), P("WebThickness"), P("FlangeThickness")),
        _ => throw new ArgumentOutOfRangeException(),
    };

    private void Recalculate()
    {
        ProfileDefinition def = BuildDefinition();
        Definition = def;
        Issues = def.Validate();
        PreviewLoops = Issues.Count == 0 ? BuildPreview(def) : [];
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(AreaMm2));
    }

    private static IReadOnlyList<IReadOnlyList<Vector2>> BuildPreview(ProfileDefinition def)
    {
        ProfileSection section = ProfileSectionBuilder.Build(def, circleSegments: 96, cornerSegments: 8);
        Bounds2 b = section.Outer.Bounds;
        double scale = PreviewSize / Math.Max(b.Width, b.Height);
        Vector2 Map(Vector2 v) => new((v.X - b.Min.X) * scale, PreviewSize - ((v.Y - b.Min.Y) * scale));
        return new[] { section.Outer }.Concat(section.Voids).Select(p => (IReadOnlyList<Vector2>)p.Vertices.Select(Map).ToArray()).ToArray();
    }
}
