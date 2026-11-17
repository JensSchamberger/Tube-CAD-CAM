namespace TubeCadCam.Domain;

/// <summary>Material master data. Intentionally contains no cutting parameters (those come from configured technology data).</summary>
public sealed record Material(string Id, string Name, string Grade);
