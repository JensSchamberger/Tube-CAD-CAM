using System.Text.Json.Serialization;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Domain.Profiles;

/// <summary>
/// Parametric description of a tube/profile cross-section. All dimensions in millimetres.
/// Pure data + deterministic formulas; no machine or cutting knowledge.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$profile")]
[JsonDerivedType(typeof(RoundTubeProfile), "round")]
[JsonDerivedType(typeof(RectangularTubeProfile), "rectangular")]
[JsonDerivedType(typeof(UProfile), "u")]
[JsonDerivedType(typeof(LProfile), "l")]
[JsonDerivedType(typeof(IProfile), "i")]
[JsonDerivedType(typeof(CustomProfile), "custom")]
public abstract record ProfileDefinition
{
    /// <summary>True for closed tube sections, false for open profiles (U, L, I, ...).</summary>
    [JsonIgnore] public abstract bool IsClosedSection { get; }

    /// <summary>Analytical cross-section area in mm².</summary>
    [JsonIgnore] public abstract double CrossSectionAreaMm2 { get; }

    /// <summary>Smallest outer extent of the section in mm.</summary>
    [JsonIgnore] public abstract double MinOuterDimensionMm { get; }

    /// <summary>Stable key used for material/profile compatibility checks (e.g. in nesting).</summary>
    [JsonIgnore] public abstract string ProfileKey { get; }

    public abstract IReadOnlyList<ValidationIssue> Validate();

    protected static void Positive(List<ValidationIssue> issues, double value, string name)
    {
        if (!(value > 0) || double.IsInfinity(value))
        {
            issues.Add(new ValidationIssue("profile.not-positive", $"{name} must be a finite number greater than 0."));
        }
    }

    protected static string Fmt(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
