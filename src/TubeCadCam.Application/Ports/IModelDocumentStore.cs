using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Application.Ports;

/// <summary>Saves/loads the complete parametric model as one self-contained, versioned document file.</summary>
public interface IModelDocumentStore
{
    Task<Result> SaveAsync(string path, TubeModel model, CancellationToken ct = default);
    Task<Result<TubeModel>> LoadAsync(string path, CancellationToken ct = default);
}
