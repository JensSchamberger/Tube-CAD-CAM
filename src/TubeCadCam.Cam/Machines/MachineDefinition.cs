namespace TubeCadCam.Cam.Machines;

public enum AxisKind { Linear, Rotary }

public sealed record MachineAxis(string Name, AxisKind Kind, double MinPosition, double MaxPosition);

/// <summary>
/// Machine capabilities exactly as configured by the user/vendor. There are no built-in defaults:
/// a capability that is not configured is treated as unavailable (never invented).
/// </summary>
public sealed record MachineDefinition(
    string Id,
    string Name,
    IReadOnlyList<MachineAxis> Axes,
    IReadOnlySet<string> SupportedProfileKinds,
    double? MaxTubeLengthMm);
