namespace TubeCadCam.Application.Contracts.ControlCenter.V1;

public static class ControlCenterApi
{
    /// <summary>Version of the contract between Tube CAD/CAM and the control centre (Leitzentrale).</summary>
    public const string Version = "1.0";
}

public sealed record PartDto(Guid PartId, string Name, string MaterialId, string ProfileKey, double LengthMm);
public sealed record OrderLineDto(Guid PartId, int Quantity);
public sealed record OrderDto(string OrderId, string Customer, IReadOnlyList<OrderLineDto> Lines, string Status);
public sealed record ProductionStatusUpdateDto(string OrderId, string Status, DateTimeOffset Timestamp);
