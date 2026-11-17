using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Cam.Machines;

/// <summary>Machine-specific behaviour is added through adapters; the CAM core stays machine-neutral.</summary>
public interface IMachineAdapter
{
    MachineDefinition Definition { get; }

    IReadOnlyList<ValidationIssue> CheckCompatibility(TubeModel model);
}
