using System.Collections.ObjectModel;
using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;

namespace TubeCadCam.Presentation;

/// <summary>Edits the parameters of a parametric profile and exposes validation, area and a normalised 2D section preview.</summary>
public sealed class ProfileEditorViewModel : ViewModelBase
{
    public const double PreviewSize = 200;

    private ProfileKind _kind;
    private ProfileDefinition? _definition;
    private IReadOnlyList<ValidationIssue> _issues = [];
    private IReadOnlyList<IReadOnlyList<Vector2>> _previewLoops = [];
    private string _customName = "Custom";
    private string _customOuterText = "-20;-10\n20;-10\n20;10\n-20;10";
    private string _customVoidsText = string.Empty;
    private bool _suppress;

    public ProfileEditorViewModel()
    {
        Parameters = [];
        Kinds = Enum.GetValues<ProfileKind>();
        _kind = ProfileKind.Round;
        LoadDefaults(_kind);
        Recalculate();
    }

    /// <summary>Raised whenever the profile (kind, parameters or custom outline) changed.</summary>
    public event EventHandler? ProfileChanged;

    public IReadOnlyList<ProfileKind> Kinds { get; }
    public ObservableCollection<ParameterViewModel> Parameters { get; }
    public bool IsCustom => _kind == ProfileKind.Custom;

    public ProfileKind Kind
    {
        get => _kind;
        set
        {
            if (Set(ref _kind, value))
            {
                LoadDefaults(value);
                OnPropertyChanged(nameof(IsCustom));
                Recalculate();
            }
        }
    }

    public string CustomName { get => _customName; set { if (Set(ref _customName, value)) { Recalculate(); } } }
    public string CustomOuterText { get => _customOuterText; set { if (Set(ref _customOuterText, value)) { Recalculate(); } } }
    public string CustomVoidsText { get => _customVoidsText; set { if (Set(ref _customVoidsText, value)) { Recalculate(); } } }

    public ProfileDefinition? Definition { get => _definition; private set => Set(ref _definition, value); }
    public IReadOnlyList<ValidationIssue> Issues { get => _issues; private set => Set(ref _issues, value); }
    public bool IsValid => Definition is not null && Issues.Count == 0;
    public double AreaMm2 => IsValid ? Definition!.CrossSectionAreaMm2 : double.NaN;

    /// <summary>Preview polygons scaled into a <see cref="PreviewSize"/> box, y pointing down (screen coordinates).</summary>
    public IReadOnlyList<IReadOnlyList<Vector2>> PreviewLoops { get => _previewLoops; private set => Set(ref _previewLoops, value); }

    /// <summary>Replaces the editor content with an existing profile (library entry or loaded document).</summary>
    public void Load(ProfileDefinition profile)
    {
        _suppress = true;
        try
        {
            ProfileKind kind = profile switch
            {
                RoundTubeProfile => ProfileKind.Round,
                RectangularTubeProfile r => r.IsSquare ? ProfileKind.Square : ProfileKind.Rectangular,
                UProfile => ProfileKind.U,
                LProfile => ProfileKind.L,
                IProfile => ProfileKind.I,
                CustomProfile => ProfileKind.Custom,
                _ => throw new NotSupportedException($"Unknown profile type {profile.GetType().Name}."),
            };

            _kind = kind;
            OnPropertyChanged(nameof(Kind));
            OnPropertyChanged(nameof(IsCustom));
            LoadDefaults(kind);
            (string Name, double Value)[] values = profile switch
            {
                RoundTubeProfile p => [("OuterDiameter", p.OuterDiameterMm), ("WallThickness", p.WallThicknessMm)],
                RectangularTubeProfile { IsSquare: true } p => [("Side", p.WidthMm), ("WallThickness", p.WallThicknessMm), ("CornerRadius", p.OuterCornerRadiusMm)],
                RectangularTubeProfile p => [("Width", p.WidthMm), ("Height", p.HeightMm), ("WallThickness", p.WallThicknessMm), ("CornerRadius", p.OuterCornerRadiusMm)],
                UProfile p => [("Height", p.HeightMm), ("FlangeWidth", p.FlangeWidthMm), ("WebThickness", p.WebThicknessMm), ("FlangeThickness", p.FlangeThicknessMm)],
                LProfile p => [("LegA", p.LegAMm), ("LegB", p.LegBMm), ("Thickness", p.ThicknessMm)],
                IProfile p => [("Height", p.HeightMm), ("FlangeWidth", p.FlangeWidthMm), ("WebThickness", p.WebThicknessMm), ("FlangeThickness", p.FlangeThicknessMm)],
                _ => [],
            };

            foreach ((string name, double value) in values)
            {
                Parameters.First(x => x.Name == name).SetSilently(value);
            }

            if (profile is CustomProfile c)
            {
                CustomName = c.Name;
                CustomOuterText = PointListParser.Format(c.Outer);
                CustomVoidsText = string.Join("\n\n", c.Voids.Select(PointListParser.Format));
            }
        }
        finally
        {
            _suppress = false;
        }

        Recalculate();
    }

    private void LoadDefaults(ProfileKind kind)
    {
        // Illustrative starting values for the editor only; not standard or technology data.
        (string, double)[] defaults = kind switch
        {
            ProfileKind.Round => [("OuterDiameter", 60), ("WallThickness", 3)],
            ProfileKind.Rectangular => [("Width", 80), ("Height", 40), ("WallThickness", 3), ("CornerRadius", 5)],
            ProfileKind.Square => [("Side", 50), ("WallThickness", 3), ("CornerRadius", 5)],
            ProfileKind.U => [("Height", 100), ("FlangeWidth", 50), ("WebThickness", 6), ("FlangeThickness", 8)],
            ProfileKind.L => [("LegA", 50), ("LegB", 50), ("Thickness", 5)],
            ProfileKind.I => [("Height", 100), ("FlangeWidth", 55), ("WebThickness", 4), ("FlangeThickness", 6)],
            ProfileKind.Custom => [],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        Parameters.Clear();
        foreach ((string name, double value) in defaults)
        {
            Parameters.Add(new ParameterViewModel(name, value, Recalculate));
        }
    }

    private double P(string name) => Parameters.First(p => p.Name == name).Value;

    private (ProfileDefinition? Definition, IReadOnlyList<ValidationIssue> ParseIssues) BuildDefinition()
    {
        switch (_kind)
        {
            case ProfileKind.Round: return (new RoundTubeProfile(P("OuterDiameter"), P("WallThickness")), []);
            case ProfileKind.Rectangular: return (new RectangularTubeProfile(P("Width"), P("Height"), P("WallThickness"), P("CornerRadius")), []);
            case ProfileKind.Square: return (RectangularTubeProfile.Square(P("Side"), P("WallThickness"), P("CornerRadius")), []);
            case ProfileKind.U: return (new UProfile(P("Height"), P("FlangeWidth"), P("WebThickness"), P("FlangeThickness")), []);
            case ProfileKind.L: return (new LProfile(P("LegA"), P("LegB"), P("Thickness")), []);
            case ProfileKind.I: return (new IProfile(P("Height"), P("FlangeWidth"), P("WebThickness"), P("FlangeThickness")), []);
            case ProfileKind.Custom:
                var (outerLoops, i1) = PointListParser.Parse(_customOuterText, "Outer contour");
                var (voidLoops, i2) = PointListParser.Parse(_customVoidsText, "Void");
                var parse = i1.Concat(i2).ToList();
                if (outerLoops.Count != 1)
                {
                    parse.Add(new ValidationIssue("input.outer-count", "Exactly one outer contour is required."));
                }

                if (outerLoops.Concat(voidLoops).Any(l => l.Count < 3))
                {
                    parse.Add(new ValidationIssue("input.too-few-points", "Every contour needs at least 3 points."));
                }

                return parse.Count > 0
                    ? (null, parse)
                    : (new CustomProfile(_customName, new Polygon2(outerLoops[0]), voidLoops.Select(l => new Polygon2(l)).ToArray()), []);
            default: throw new ArgumentOutOfRangeException();
        }
    }

    private void Recalculate()
    {
        if (_suppress)
        {
            return;
        }

        (ProfileDefinition? def, IReadOnlyList<ValidationIssue> parseIssues) = BuildDefinition();
        Definition = def;
        Issues = def is null ? parseIssues : def.Validate();
        PreviewLoops = IsValid ? BuildPreview(def!) : [];
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(AreaMm2));
        ProfileChanged?.Invoke(this, EventArgs.Empty);
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
