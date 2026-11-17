using TubeCadCam.Domain.Common;

namespace TubeCadCam.Application.Services;

public sealed record TubeModelAnalysis(
    Guid ModelId,
    string ProfileKey,
    double CrossSectionAreaMm2,
    int FeatureCount,
    int ContourCount,
    IReadOnlyList<ValidationIssue> Issues);
