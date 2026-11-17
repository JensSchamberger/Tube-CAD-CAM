using TubeCadCam.Cad;
using TubeCadCam.Cam;
using TubeCadCam.Cam.Machines;
using TubeCadCam.Cam.Nc;
using TubeCadCam.Domain.Profiles;

namespace TubeCadCam.Tests;

public class CamTests
{
    private static readonly CamPlanner Planner = new(new FeatureContourRecognizer());

    private static ConfiguredMachineAdapter Machine(double? maxLen = 6500, params string[] kinds) =>
        new(new MachineDefinition("m1", "Test machine", [], new HashSet<string>(kinds), maxLen));

    [Fact]
    public void Contours_are_recognised_per_feature_with_correct_area()
    {
        TubeModel m = TubeModelTests.NewModel()
            .AddFeature(new HoleFeature(Guid.NewGuid(), 100, 0, 10))
            .AddFeature(new RectangularCutoutFeature(Guid.NewGuid(), 500, 0, 20, 40));
        var plan = Planner.Plan(m).Value;
        Assert.Equal(2, plan.Contours.Count);
        Assert.Equal(ContourKind.Hole, plan.Contours[0].Kind);
        Assert.Equal(Math.PI * 25, plan.Contours[0].Outline.Area, 0);
        Assert.Equal(800, plan.Contours[1].Outline.Area, 9);
        Assert.Equal(CamPlanStatus.Draft, plan.Status);
    }

    [Fact]
    public void Planning_is_deterministic()
    {
        TubeModel m = TubeModelTests.NewModel().AddFeature(new HoleFeature(Guid.NewGuid(), 100, 0, 10));
        var a = Planner.Plan(m).Value.Contours.SelectMany(c => c.Outline.Vertices).ToArray();
        var b = Planner.Plan(m).Value.Contours.SelectMany(c => c.Outline.Vertices).ToArray();
        Assert.Equal(a, b);
    }

    [Fact]
    public void Planning_invalid_model_fails() =>
        Assert.False(Planner.Plan(TubeModelTests.NewModel(length: 0)).IsSuccess);

    [Fact]
    public void Release_gate_blocks_draft_plans_even_for_compatible_machine()
    {
        var plan = Planner.Plan(TubeModelTests.NewModel()).Value;
        var gate = NcReleaseGate.Evaluate(plan, Machine(6500, nameof(RoundTubeProfile)));
        Assert.False(gate.IsSuccess);
        Assert.Contains(gate.Issues, i => i.Code == "nc.plan-not-validated");
    }

    [Fact]
    public void Release_gate_passes_only_for_validated_plan_and_compatible_machine()
    {
        var plan = Planner.Plan(TubeModelTests.NewModel()).Value with { Status = CamPlanStatus.Validated };
        Assert.True(NcReleaseGate.Evaluate(plan, Machine(6500, nameof(RoundTubeProfile))).IsSuccess);
        Assert.Contains(NcReleaseGate.Evaluate(plan, Machine(6500, nameof(UProfile))).Issues, i => i.Code == "machine.profile-unsupported");
    }

    [Fact]
    public void Machine_without_configured_length_limit_is_not_assumed_capable() =>
        Assert.Contains(Machine(null, nameof(RoundTubeProfile)).CheckCompatibility(TubeModelTests.NewModel()), i => i.Code == "machine.length-unknown");

    [Fact]
    public void Machine_length_limit_is_enforced() =>
        Assert.Contains(Machine(500, nameof(RoundTubeProfile)).CheckCompatibility(TubeModelTests.NewModel(1000)), i => i.Code == "machine.length-exceeded");
}
