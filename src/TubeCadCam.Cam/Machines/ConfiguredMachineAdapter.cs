using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Cam.Machines;

/// <summary>Generic adapter that checks a tube against the configured <see cref="MachineDefinition"/> only.</summary>
public sealed class ConfiguredMachineAdapter(MachineDefinition definition) : IMachineAdapter
{
    public MachineDefinition Definition { get; } = definition;

    public IReadOnlyList<ValidationIssue> CheckCompatibility(TubeModel model)
    {
        var issues = new List<ValidationIssue>();
        string kind = model.Part.Profile.GetType().Name;
        if (!Definition.SupportedProfileKinds.Contains(kind))
        {
            issues.Add(new ValidationIssue("machine.profile-unsupported", $"Machine '{Definition.Name}' is not configured for profile type {kind}."));
        }

        if (Definition.MaxTubeLengthMm is not { } max)
        {
            issues.Add(new ValidationIssue("machine.length-unknown", "Machine maximum tube length is not configured."));
        }
        else if (model.Part.LengthMm > max)
        {
            issues.Add(new ValidationIssue("machine.length-exceeded", $"Tube length {model.Part.LengthMm} mm exceeds machine limit {max} mm."));
        }

        return issues;
    }
}
