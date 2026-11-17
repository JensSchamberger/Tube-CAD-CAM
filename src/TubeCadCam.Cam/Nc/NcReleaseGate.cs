using TubeCadCam.Cam.Machines;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Cam.Nc;

/// <summary>
/// Single choke point that decides whether productive NC output may be produced.
/// NC output requires: validated plan, compatible machine. In phase 1 no plan can reach
/// <see cref="CamPlanStatus.Validated"/>, so release is always blocked (by design).
/// </summary>
public static class NcReleaseGate
{
    public static Result Evaluate(CamPlan plan, IMachineAdapter machine)
    {
        var issues = new List<ValidationIssue>();
        if (plan.Status != CamPlanStatus.Validated)
        {
            issues.Add(new ValidationIssue("nc.plan-not-validated", "CAM plan has not passed full technical validation (toolpaths, sequence, collision check)."));
        }

        issues.AddRange(machine.CheckCompatibility(plan.Model));
        return issues.Count == 0 ? Result.Success() : Result.Failure([.. issues]);
    }
}
