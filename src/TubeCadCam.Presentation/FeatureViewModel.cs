using System.Collections.ObjectModel;
using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;
using TubeCadCam.Geometry;

namespace TubeCadCam.Presentation;

/// <summary>Editable view of one feature. <see cref="Build"/> turns the current inputs into a CAD feature (or reports input errors).</summary>
public sealed class FeatureViewModel : ViewModelBase
{
    private readonly Action _changed;
    private TubeEnd _end;
    private string _contourText;
    private bool _isSelected;
    private string _displayName;
    private string? _failureText;

    public FeatureViewModel(FeatureKind kind, Guid id, IEnumerable<(string Name, double Value)> values, TubeEnd end, string contourText, Action changed)
    {
        Kind = kind;
        Id = id;
        _end = end;
        _contourText = contourText;
        _changed = changed;
        _displayName = FeatureDescriptors.Label(kind);
        Parameters = [];
        foreach ((string name, double value) in values)
        {
            Parameters.Add(new ParameterViewModel(name, value, changed));
        }
    }

    public FeatureKind Kind { get; }
    public Guid Id { get; }
    public ObservableCollection<ParameterViewModel> Parameters { get; }
    public IReadOnlyList<TubeEnd> Ends { get; } = Enum.GetValues<TubeEnd>();
    public bool HasEnd => FeatureDescriptors.HasEnd(Kind);
    public bool HasContourText => FeatureDescriptors.HasContourText(Kind);

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (Set(ref _displayName, value))
            {
                OnPropertyChanged(nameof(ListLabel));
            }
        }
    }

    /// <summary>Set when the feature could not be subtracted from the body (it is then NOT part of the 3D model).</summary>
    public string? FailureText
    {
        get => _failureText;
        private set
        {
            if (Set(ref _failureText, value))
            {
                OnPropertyChanged(nameof(HasFailed));
                OnPropertyChanged(nameof(ListLabel));
            }
        }
    }

    public bool HasFailed => _failureText is not null;

    public string ListLabel => (HasFailed ? "✗ " : string.Empty) + DisplayName;

    public void SetFailure(FeatureFailure? failure) =>
        FailureText = failure is null ? null : $"Nicht im 3D-Körper enthalten ({failure.Code}): {failure.Message}";
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    public TubeEnd End
    {
        get => _end;
        set { if (Set(ref _end, value)) { _changed(); } }
    }

    public string ContourText
    {
        get => _contourText;
        set { if (Set(ref _contourText, value)) { _changed(); } }
    }

    public static FeatureViewModel FromFeature(TubeFeature f, Action changed)
    {
        FeatureKind kind = FeatureDescriptors.KindOf(f);
        TubeEnd end = f switch { SlitFeature s => s.End, EndNotchFeature n => n.End, EndMiterFeature m => m.End, _ => TubeEnd.Start };
        string text = f is ContourCutoutFeature c ? PointListParser.Format(c.ContourOutline) : FeatureDescriptors.DefaultContourText;
        return new FeatureViewModel(kind, f.Id, FeatureDescriptors.Describe(f), end, text, changed);
    }

    public (TubeFeature? Feature, IReadOnlyList<ValidationIssue> Issues) Build()
    {
        Polygon2? contour = null;
        if (Kind == FeatureKind.ContourCutout)
        {
            var (loops, issues) = PointListParser.Parse(_contourText, DisplayName);
            if (loops.Count != 1 || loops[0].Count < 3)
            {
                issues = [.. issues, new ValidationIssue("input.contour-points", $"{DisplayName}: exactly one contour with at least 3 points is required.")];
            }

            if (issues.Count > 0)
            {
                return (null, issues);
            }

            contour = new Polygon2(loops[0]);
        }

        double P(string name) => Parameters.First(p => p.Name == name).Value;
        return (FeatureDescriptors.Create(Kind, Id, _end, P, contour), []);
    }
}
