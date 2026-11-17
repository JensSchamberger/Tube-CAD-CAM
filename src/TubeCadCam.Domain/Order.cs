namespace TubeCadCam.Domain;

/// <summary>Production status as mirrored from / reported to the control centre (Leitzentrale).</summary>
public enum ProductionStatus { Planned, Prepared, Released, InProduction, Completed, Cancelled }

public sealed record OrderLine(Guid PartId, int Quantity);

public sealed record Order(string Id, string Customer, IReadOnlyList<OrderLine> Lines, ProductionStatus Status);
