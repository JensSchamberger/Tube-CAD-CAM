using TubeCadCam.Domain.Common;

namespace TubeCadCam.Application.Contracts.ControlCenter.V1;

/// <summary>Port to the control centre. Implementations live in Infrastructure; the contract is versioned (see <see cref="ControlCenterApi.Version"/>).</summary>
public interface IControlCenterGateway
{
    Task<Result<IReadOnlyList<OrderDto>>> GetOpenOrdersAsync(CancellationToken ct = default);
    Task<Result<PartDto>> GetPartAsync(Guid partId, CancellationToken ct = default);
    Task<Result> ReportStatusAsync(ProductionStatusUpdateDto update, CancellationToken ct = default);
}
