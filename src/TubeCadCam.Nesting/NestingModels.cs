namespace TubeCadCam.Nesting;

public sealed record NestingPart(string PartId, string ProfileKey, string MaterialId, double LengthMm, int Quantity);

/// <summary>A stock bar or remnant from inventory.</summary>
public sealed record StockBar(string BarId, string ProfileKey, string MaterialId, double LengthMm);

/// <param name="KerfMm">Cut width consumed by every cut.</param>
/// <param name="StartTrimMm">Waste removed from the bar start.</param>
/// <param name="EndTrimMm">Reserved waste at the bar end (not part of the usable length).</param>
public sealed record NestingSettings(double KerfMm, double StartTrimMm, double EndTrimMm);

public sealed record PlacedPart(string PartId, double OffsetMm, double LengthMm);

/// <param name="RemnantMm">Usable length left after the last cut (excluding end trim).</param>
public sealed record BarAssignment(StockBar Bar, IReadOnlyList<PlacedPart> Placed, double RemnantMm);

public sealed record NestingResult(IReadOnlyList<BarAssignment> Assignments, IReadOnlyList<NestingPart> Unplaced);
