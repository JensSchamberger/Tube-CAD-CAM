using TubeCadCam.Domain;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Cad;

/// <summary>Editable CAD model of one tube part: the part definition plus its machining features.</summary>
public sealed record TubeModel(TubePart Part, IReadOnlyList<TubeFeature> Features)
{
    public Guid Id => Part.Id;

    public TubeModel AddFeature(TubeFeature feature) => this with { Features = [.. Features, feature] };

    /// <summary>Replaces the feature with the same id (keeps its position in the list).</summary>
    public TubeModel ReplaceFeature(TubeFeature feature) =>
        this with { Features = Features.Select(f => f.Id == feature.Id ? feature : f).ToArray() };

    public TubeModel RemoveFeature(Guid featureId) => this with { Features = Features.Where(f => f.Id != featureId).ToArray() };

    public IReadOnlyList<ValidationIssue> Validate() => TubeModelValidator.Validate(this);
}
