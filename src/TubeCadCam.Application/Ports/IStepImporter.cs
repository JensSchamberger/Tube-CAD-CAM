using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Application.Ports;

public interface IStepImporter
{
    Task<Result<TubeModel>> ImportAsync(Stream step, CancellationToken ct = default);
}
