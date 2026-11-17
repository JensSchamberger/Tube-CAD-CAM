namespace TubeCadCam.Presentation;

/// <summary>German UI captions (units included). Kept out of the views so they are testable.</summary>
public static class ParameterLabels
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        ["OuterDiameter"] = "Außendurchmesser [mm]",
        ["WallThickness"] = "Wandstärke [mm]",
        ["Width"] = "Breite [mm]",
        ["Height"] = "Höhe [mm]",
        ["Side"] = "Seitenlänge [mm]",
        ["CornerRadius"] = "Eckenradius [mm]",
        ["FlangeWidth"] = "Flanschbreite [mm]",
        ["WebThickness"] = "Stegdicke [mm]",
        ["FlangeThickness"] = "Flanschdicke [mm]",
        ["LegA"] = "Schenkel A [mm]",
        ["LegB"] = "Schenkel B [mm]",
        ["Thickness"] = "Dicke [mm]",
        ["AxialPosition"] = "Position axial [mm]",
        ["Angle"] = "Winkel [°]",
        ["Diameter"] = "Durchmesser [mm]",
        ["Length"] = "Länge [mm]",
        ["Rotation"] = "Drehung [°]",
        ["Depth"] = "Tiefe [mm]",
        ["InnerCornerRadius"] = "Innenradius [mm]",
        ["TiltAngle"] = "Neigung [°]",
        ["TiltAxisAngle"] = "Neigungsachse [°]",
    };

    public static string Label(string name) => Map.TryGetValue(name, out string? l) ? l : name;
}

public static class ProfileKindLabels
{
    public static string Label(ProfileKind k) => k switch
    {
        ProfileKind.Round => "Rundrohr",
        ProfileKind.Rectangular => "Rechteckrohr",
        ProfileKind.Square => "Quadratrohr",
        ProfileKind.U => "U-Profil",
        ProfileKind.L => "L-Profil",
        ProfileKind.I => "I-Profil",
        ProfileKind.Custom => "Benutzerdefiniert",
        _ => k.ToString(),
    };
}
