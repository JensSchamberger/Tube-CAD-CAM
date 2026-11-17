using TubeCadCam.Application.Ports;
using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Infrastructure;

/// <summary>Honest placeholder binding: STEP import is not implemented; every call fails with a clear error.</summary>
public sealed class NotSupportedStepImporter : IStepImporter
{
    public Task<Result<TubeModel>> ImportAsync(Stream step, CancellationToken ct = default) =>
        Task.FromResult(Result<TubeModel>.Failure("step.not-implemented", "STEP import is not implemented yet."));
}
