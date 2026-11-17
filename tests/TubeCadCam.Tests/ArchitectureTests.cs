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

    [Fact]
    public void Cad_kernel_contains_no_machine_or_nc_concepts()
    {
        string[] forbidden = ["Machine", "PostProcessor", "NcProgram", "Kinematic", "Chuck", "Nesting", "Cutting", "Laser"];
        foreach (string layer in new[] { "TubeCadCam.Geometry", "TubeCadCam.Domain", "TubeCadCam.Cad" })
        {
            foreach (Type t in Assembly.Load(layer).GetTypes())
            {
                Assert.DoesNotContain(forbidden, f => t.Name.Contains(f, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void Cam_reads_cad_but_cad_never_references_cam_or_nesting()
    {
        string[] cadRefs = Assembly.Load("TubeCadCam.Cad").GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.DoesNotContain("TubeCadCam.Cam", cadRefs);
        Assert.DoesNotContain("TubeCadCam.Nesting", cadRefs);
        string[] camRefs = Assembly.Load("TubeCadCam.Cam").GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.DoesNotContain("TubeCadCam.Nesting", camRefs);
        string[] nestRefs = Assembly.Load("TubeCadCam.Nesting").GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.DoesNotContain("TubeCadCam.Cam", nestRefs);
        Assert.DoesNotContain("TubeCadCam.Cad", nestRefs);
    }

    [Fact]
    public void Control_center_contract_v1_is_preserved()
    {
        Assembly app = Assembly.Load("TubeCadCam.Application");
        Type api = app.GetType("TubeCadCam.Application.Contracts.ControlCenter.V1.ControlCenterApi")!;
        Assert.Equal("1.0", api.GetField("Version")!.GetValue(null));
        Type gw = app.GetType("TubeCadCam.Application.Contracts.ControlCenter.V1.IControlCenterGateway")!;
        Assert.Equal(["GetOpenOrdersAsync", "GetPartAsync", "ReportStatusAsync"], gw.GetMethods().Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Presentation_does_not_depend_on_wpf_or_infrastructure()
    {
        string[] refs = Assembly.Load("TubeCadCam.Presentation").GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.DoesNotContain(refs, r => r.StartsWith("PresentationFramework", StringComparison.Ordinal) || r.StartsWith("WindowsBase", StringComparison.Ordinal));
        Assert.DoesNotContain("TubeCadCam.Infrastructure", refs);
    }

    [Fact]
    public void No_licensing_implementation_exists_yet()
    {
        foreach (string layer in Allowed.Keys)
        {
            Assert.DoesNotContain(Assembly.Load(layer).GetTypes(), t => t.IsClass && !t.IsAbstract && typeof(TubeCadCam.Application.Ports.ILicenseGate).IsAssignableFrom(t));
        }
    }
}
