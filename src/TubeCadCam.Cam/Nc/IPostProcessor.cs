using TubeCadCam.Domain.Common;

namespace TubeCadCam.Cam.Nc;

public sealed record NcProgram(string MachineId, string Text);

/// <summary>Machine-specific NC generation. Implementations must call <see cref="NcReleaseGate"/> before emitting output.</summary>
public interface IPostProcessor
{
    string MachineId { get; }

    Result<NcProgram> Generate(CamPlan plan);
}
