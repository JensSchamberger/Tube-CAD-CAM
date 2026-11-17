using System.Reflection;

namespace TubeCadCam.Tests;

/// <summary>Guards the layering rules from the architecture plan (docs/Architekturplan.md).</summary>
public class ArchitectureTests
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["TubeCadCam.Geometry"] = [],
        ["TubeCadCam.Domain"] = ["TubeCadCam.Geometry"],
        ["TubeCadCam.Cad"] = ["TubeCadCam.Geometry", "TubeCadCam.Domain"],
        ["TubeCadCam.Cam"] = ["TubeCadCam.Geometry", "TubeCadCam.Domain", "TubeCadCam.Cad"],
        ["TubeCadCam.Nesting"] = ["TubeCadCam.Domain"],
        ["TubeCadCam.Application"] = ["TubeCadCam.Geometry", "TubeCadCam.Domain", "TubeCadCam.Cad", "TubeCadCam.Cam", "TubeCadCam.Nesting"],
        ["TubeCadCam.Infrastructure"] = ["TubeCadCam.Geometry", "TubeCadCam.Domain", "TubeCadCam.Cad", "TubeCadCam.Application"],
        ["TubeCadCam.Presentation"] = ["TubeCadCam.Geometry", "TubeCadCam.Domain", "TubeCadCam.Cad", "TubeCadCam.Application"],
    };

    public static TheoryData<string> Layers => new(Allowed.Keys);

    [Theory]
    [MemberData(nameof(Layers))]
    public void Layer_references_only_allowed_layers(string layer)
    {
        Assembly asm = Assembly.Load(layer);
        string[] referenced = asm.GetReferencedAssemblies()
            .Select(a => a.Name!).Where(n => n.StartsWith("TubeCadCam.", StringComparison.Ordinal)).ToArray();
        Assert.All(referenced, r => Assert.Contains(r, Allowed[layer]));
    }

    [Fact]
    public void Cam_and_nesting_are_independent_of_each_other_and_of_ui()
    {
        Assert.DoesNotContain("TubeCadCam.Nesting", Allowed["TubeCadCam.Cam"]);
        Assert.DoesNotContain("TubeCadCam.Cam", Allowed["TubeCadCam.Nesting"]);
    }
}
