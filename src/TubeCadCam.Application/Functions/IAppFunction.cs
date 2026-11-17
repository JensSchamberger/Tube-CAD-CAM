using TubeCadCam.Domain.Common;

namespace TubeCadCam.Application.Functions;

/// <summary>
/// A controlled, validated application function. This is the only surface a future AI/MCP layer may call:
/// every function has a fixed name, declared parameters and validates its own input.
/// </summary>
public interface IAppFunction
{
    string Name { get; }
    string Description { get; }

    /// <summary>True if the function never changes state (safe to call without confirmation).</summary>
    bool IsReadOnly { get; }

    IReadOnlyList<string> RequiredArguments { get; }

    Task<Result<object>> ExecuteAsync(IReadOnlyDictionary<string, string> arguments, CancellationToken ct = default);
}
