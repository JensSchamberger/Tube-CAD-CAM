using TubeCadCam.Application.Ports;
using TubeCadCam.Cad;
using TubeCadCam.Cam;
using TubeCadCam.Domain;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;

namespace TubeCadCam.Application.Services;

/// <summary>Application use cases around tube models. UI, tests and a future MCP server all go through here.</summary>
public sealed class TubeModelService(ITubeModelRepository repository, CamPlanner planner)
{
    public async Task<Result<TubeModel>> CreateAsync(string name, string materialId, ProfileDefinition profile, double lengthMm, CancellationToken ct = default)
    {
        var model = new TubeModel(new TubePart(Guid.NewGuid(), name, materialId, profile, lengthMm), []);
        IReadOnlyList<ValidationIssue> issues = model.Validate();
        if (issues.Any(i => i.Severity == IssueSeverity.Error))
        {
            return Result<TubeModel>.Failure(issues);
        }

        await repository.SaveAsync(model, ct);
        return Result<TubeModel>.Success(model, issues);
    }

    public async Task<Result<TubeModel>> AddFeatureAsync(Guid modelId, TubeFeature feature, CancellationToken ct = default)
    {
        TubeModel? model = await repository.GetAsync(modelId, ct);
        if (model is null)
        {
            return Result<TubeModel>.Failure("model.not-found", $"Tube model {modelId} does not exist.");
        }

        TubeModel updated = model.AddFeature(feature);
        IReadOnlyList<ValidationIssue> issues = updated.Validate();
        if (issues.Any(i => i.Severity == IssueSeverity.Error))
        {
            return Result<TubeModel>.Failure(issues);
        }

        await repository.SaveAsync(updated, ct);
        return Result<TubeModel>.Success(updated, issues);
    }

    public async Task<Result<TubeModelAnalysis>> AnalyzeAsync(Guid modelId, CancellationToken ct = default)
    {
        TubeModel? model = await repository.GetAsync(modelId, ct);
        if (model is null)
        {
            return Result<TubeModelAnalysis>.Failure("model.not-found", $"Tube model {modelId} does not exist.");
        }

        Result<CamPlan> plan = planner.Plan(model);
        if (!plan.IsSuccess)
        {
            return Result<TubeModelAnalysis>.Failure(plan.Issues);
        }

        return Result<TubeModelAnalysis>.Success(new TubeModelAnalysis(
            model.Id, model.Part.Profile.ProfileKey, model.Part.Profile.CrossSectionAreaMm2,
            model.Features.Count, plan.Value.Contours.Count, plan.Issues), plan.Issues);
    }
}
