using TubeCadCam.Cad;

namespace TubeCadCam.Application.Ports;

public interface ITubeModelRepository
{
    Task SaveAsync(TubeModel model, CancellationToken ct = default);
    Task<TubeModel?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TubeModel>> ListAsync(CancellationToken ct = default);
}
