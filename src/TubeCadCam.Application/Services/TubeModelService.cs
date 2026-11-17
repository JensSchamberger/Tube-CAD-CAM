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

    public async Task<Result<TubeModel>> UpdateFeatureAsync(Guid modelId, TubeFeature feature, CancellationToken ct = default) =>
        await MutateAsync(modelId, m => m.Features.Any(f => f.Id == feature.Id)
            ? m.ReplaceFeature(feature)
            : null, "feature.not-found", $"Feature {feature.Id} does not exist.", ct);

    public async Task<Result<TubeModel>> RemoveFeatureAsync(Guid modelId, Guid featureId, CancellationToken ct = default) =>
        await MutateAsync(modelId, m => m.Features.Any(f => f.Id == featureId) ? m.RemoveFeature(featureId) : null,
            "feature.not-found", $"Feature {featureId} does not exist.", ct);

    /// <summary>Changes name, material, profile and/or length; features are kept and re-validated against the new part.</summary>
    public async Task<Result<TubeModel>> UpdatePartAsync(Guid modelId, string? name = null, string? materialId = null,
        ProfileDefinition? profile = null, double? lengthMm = null, CancellationToken ct = default) =>
        await MutateAsync(modelId, m => m with
        {
            Part = m.Part with
            {
                Name = name ?? m.Part.Name,
                MaterialId = materialId ?? m.Part.MaterialId,
                Profile = profile ?? m.Part.Profile,
                LengthMm = lengthMm ?? m.Part.LengthMm,
            },
        }, "model.not-found", $"Tube model {modelId} does not exist.", ct);

    private async Task<Result<TubeModel>> MutateAsync(Guid modelId, Func<TubeModel, TubeModel?> change, string missingCode, string missingMessage, CancellationToken ct)
    {
        TubeModel? model = await repository.GetAsync(modelId, ct);
        if (model is null)
        {
            return Result<TubeModel>.Failure("model.not-found", $"Tube model {modelId} does not exist.");
        }

        TubeModel? updated = change(model);
        if (updated is null)
        {
            return Result<TubeModel>.Failure(missingCode, missingMessage);
        }

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
            model.Features.Count, plan.Value.Contours.Count, plan.Value.UnrecognizedFeatureIds?.Count ?? 0, plan.Issues), plan.Issues);
    }
}
