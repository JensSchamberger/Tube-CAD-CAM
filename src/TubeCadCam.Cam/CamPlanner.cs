using TubeCadCam.Domain.Common;

namespace TubeCadCam.Cam;

/// <summary>Phase-1 planner: validates the model and recognises contours. Always yields a <see cref="CamPlanStatus.Draft"/> plan.</summary>
public sealed class CamPlanner(IContourRecognizer recognizer)
{
    public Result<CamPlan> Plan(Cad.TubeModel model)
    {
        IReadOnlyList<ValidationIssue> issues = model.Validate();
        if (issues.Any(i => i.Severity == IssueSeverity.Error))
        {
            return Result<CamPlan>.Failure(issues);
        }

        return Result<CamPlan>.Success(new CamPlan(model, recognizer.Recognize(model), CamPlanStatus.Draft), issues);
    }
}
