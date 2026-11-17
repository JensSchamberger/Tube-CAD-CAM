using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;
using TubeCadCam.Infrastructure;
using TubeCadCam.Presentation;
using static TubeCadCam.Tests.SolidTestHelpers;

namespace TubeCadCam.Tests;

public sealed class SolidDocumentAndViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tubecadcam-solid-" + Guid.NewGuid().ToString("N"));

    public SolidDocumentAndViewModelTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class Dialogs : IFileDialogService
    {
        public string? OpenPath { get; set; }
        public string? SavePath { get; set; }
        public string? PickFileToOpen() => OpenPath;
        public string? PickFileToSave(string suggestedName) => SavePath;
    }

    private static TubeModel Model(ProfileDefinition p, double len, params TubeFeature[] f) =>
        f.Aggregate(TubeModelTests.NewModel(len, p), (m, x) => m.AddFeature(x));

    [Fact]
    public async Task Saved_and_loaded_model_yields_the_identical_solid()
    {
        var rect = new RectangularTubeProfile(80, 40, 3, 6);
        TubeModel m = Model(rect, 800,
            new HoleFeature(Guid.NewGuid(), 200, 90, 12), new SlotFeature(Guid.NewGuid(), 350, 90, 40, 8, 20),
            new RectangularCutoutFeature(Guid.NewGuid(), 500, 270, 12, 25, 2, 15),
            new ContourCutoutFeature(Guid.NewGuid(), 650, 90, new Polygon2([new(-8, -5), new(8, -5), new(0, 7)]), 10),
            new EndNotchFeature(Guid.NewGuid(), TubeEnd.Start, 270, 12, 25, 3), new SlitFeature(Guid.NewGuid(), TubeEnd.Start, 0, 4, 40),
            new EndMiterFeature(Guid.NewGuid(), TubeEnd.End, 20, 90));

        var store = new JsonModelDocumentStore();
        string path = Path.Combine(_dir, "m.json");
        Assert.True((await store.SaveAsync(path, m)).IsSuccess);
        TubeModel loaded = (await store.LoadAsync(path)).Value;

        TubeSolid a = Solid(m), b = Solid(loaded);
        Assert.Empty(a.Failures);
        AssertValid(b);
        Assert.Equal(a.Mesh.Positions, b.Mesh.Positions);
        Assert.Equal(a.Mesh.Indices, b.Mesh.Indices);
        Assert.Equal(a.TriangleTags, b.TriangleTags);
        Assert.Equal(a.Topology.Volume, b.Topology.Volume);
        Assert.Equal(a.AppliedFeatures, b.AppliedFeatures);
        Assert.Equal(a.Edges.Count, b.Edges.Count);
    }

    [Fact]
    public async Task Save_and_open_through_the_editor_restores_the_machined_body_and_keeps_it_editable()
    {
        string path = Path.Combine(_dir, "editor.json");
        var dialogs = new Dialogs { SavePath = path, OpenPath = path };
        var vm = new MainViewModel(new JsonModelDocumentStore(), new JsonProfileCatalogProvider(null, Path.Combine(_dir, "u.json")), dialogs);
        vm.ProfileEditor.Kind = ProfileKind.Rectangular;
        FeatureViewModel hole = vm.AddFeature(FeatureKind.Hole);
        hole.Parameters.First(p => p.Name == "Angle").Value = 90;
        vm.AddFeature(FeatureKind.Slot).Parameters.First(p => p.Name == "AxialPosition").Value = 700;
        Assert.False(vm.HasErrors, string.Join("; ", vm.Issues.Select(i => i.Message)));
        await vm.SaveAsync(saveAs: true);

        var other = new MainViewModel(new JsonModelDocumentStore(), new JsonProfileCatalogProvider(null, Path.Combine(_dir, "u2.json")), dialogs);
        await other.OpenAsync();
        Assert.Equal(vm.Scene!.Body.Positions, other.Scene!.Body.Positions);
        Assert.Equal(vm.Scene.Solid.Topology.Volume, other.Scene.Solid.Topology.Volume);

        double before = other.Scene.Solid.Topology.Volume;
        other.Features[0].Parameters.First(p => p.Name == "Diameter").Value = 20;
        Assert.True(other.Scene!.Solid.Topology.Volume < before);                    // the body follows the parametric model
    }

    // ---------------------------------------------------------------- editor behaviour

    [Fact]
    public void Adding_a_feature_shows_a_real_opening_in_the_displayed_body()
    {
        var vm = new MainViewModel();
        vm.ProfileEditor.Kind = ProfileKind.Rectangular;
        double before = vm.Scene!.Solid.Topology.Volume;
        int trianglesBefore = vm.Scene.Body.TriangleCount;
        FeatureViewModel hole = vm.AddFeature(FeatureKind.Hole);

        Assert.False(vm.HasErrors);
        Assert.True(vm.Scene!.Solid.Topology.Volume < before);
        Assert.NotEqual(trianglesBefore, vm.Scene.Body.TriangleCount);
        double x = hole.Parameters.First(p => p.Name == "AxialPosition").Value;
        Assert.False(Inside(vm.Scene.Body, new Vector3(x, 0, 18.5)));              // default: top face, 90°
        Assert.Contains(vm.Scene.Solid.TriangleTags, t => t.Kind == SolidFaceKind.FeatureWall && t.FeatureId == hole.Id);
    }

    [Fact]
    public void Editing_any_parameter_recomputes_the_body_and_deleting_restores_it()
    {
        var vm = new MainViewModel();
        vm.ProfileEditor.Kind = ProfileKind.Rectangular;
        double baseVolume = vm.Scene!.Solid.Topology.Volume;
        int baseTriangles = vm.Scene.Body.TriangleCount;
        FeatureViewModel hole = vm.AddFeature(FeatureKind.Hole);
        double v1 = vm.Scene!.Solid.Topology.Volume;

        hole.Parameters.First(p => p.Name == "Diameter").Value = 20;
        double v2 = vm.Scene!.Solid.Topology.Volume;
        Assert.True(v2 < v1);

        vm.ProfileEditor.Parameters.First(p => p.Name == "WallThickness").Value = 5;
        Assert.True(vm.Scene!.Solid.Topology.Volume != v2);
        vm.ProfileEditor.Parameters.First(p => p.Name == "WallThickness").Value = 3;

        vm.LengthMm = 1400;
        Assert.Equal(1400, vm.Scene!.Body.Bounds.Max.X - vm.Scene.Body.Bounds.Min.X, 1e-9);
        vm.LengthMm = 1000;

        vm.SelectFeature(hole.Id);
        vm.RemoveSelectedFeature();
        Assert.Equal(baseVolume, vm.Scene!.Solid.Topology.Volume, 1e-6);
        Assert.Equal(baseTriangles, vm.Scene.Body.TriangleCount);
        Assert.Null(vm.Model!.Features.FirstOrDefault());
    }

    [Fact]
    public void A_feature_that_cannot_be_computed_is_marked_and_listed_as_an_error_and_not_drawn()
    {
        var vm = new MainViewModel();
        vm.ProfileEditor.Kind = ProfileKind.I;
        double baseVolume = vm.Scene!.Solid.Topology.Volume;
        FeatureViewModel cut = vm.AddFeature(FeatureKind.RectangularCutout);        // default: 90° = over the web junction of the I profile

        Assert.True(cut.HasFailed);
        Assert.StartsWith("✗ ", cut.ListLabel);
        Assert.Contains("Nicht im 3D-Körper enthalten", cut.FailureText);
        Assert.True(vm.HasErrors);
        Assert.Contains(vm.Issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("nicht im 3D-Körper enthalten"));
        Assert.Equal(baseVolume, vm.Scene!.Solid.Topology.Volume, 1e-9);            // no substitute geometry
        Assert.Contains(vm.Scene.Failures, f => f.FeatureId == cut.Id);

        // fixing the parameters clears the failure
        vm.ProfileEditor.Kind = ProfileKind.Rectangular;
        Assert.False(cut.HasFailed);
        Assert.DoesNotContain("✗", cut.ListLabel);
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public void The_cutter_preview_is_optional_and_never_part_of_the_body()
    {
        var vm = new MainViewModel();
        vm.ProfileEditor.Kind = ProfileKind.Rectangular;
        vm.AddFeature(FeatureKind.Hole);
        Assert.False(vm.ShowFeaturePreview);
        Assert.Single(vm.Scene!.PreviewShapes);
        // body mesh identical whether or not the preview is shown
        var positions = vm.Scene.Body.Positions;
        vm.ShowFeaturePreview = true;
        Assert.Equal(positions, vm.Scene.Body.Positions);
        Assert.Equal(vm.Scene.Solid.Mesh.TriangleCount, vm.Scene.Body.TriangleCount);
    }

    [Fact]
    public void A_click_on_the_wall_of_an_opening_identifies_the_feature_through_the_face_provenance()
    {
        var vm = new MainViewModel();
        vm.ProfileEditor.Kind = ProfileKind.Rectangular;
        FeatureViewModel a = vm.AddFeature(FeatureKind.Hole);
        FeatureViewModel b = vm.AddFeature(FeatureKind.Slot);
        b.Parameters.First(p => p.Name == "AxialPosition").Value = 800;
        TubeScene scene = vm.Scene!;

        // the host picks a triangle of the body mesh (render mesh: three own vertices per triangle) and reads its tag
        Guid? Pick(int triangle) => scene.Solid.TriangleTags[triangle].FeatureId;
        int wallOfA = scene.Solid.TrianglesOf(a.Id).First(), wallOfB = scene.Solid.TrianglesOf(b.Id).First();
        Assert.Equal(a.Id, Pick(wallOfA));
        Assert.Equal(b.Id, Pick(wallOfB));
        vm.SelectFeature(Pick(wallOfA));
        Assert.Same(a, vm.SelectedFeature);
        int surface = Enumerable.Range(0, scene.Solid.TriangleTags.Count).First(t => scene.Solid.TriangleTags[t].Kind == SolidFaceKind.OuterSurface);
        vm.SelectFeature(Pick(surface));
        Assert.Null(vm.SelectedFeature);
    }

    [Fact]
    public void Extracted_wall_mesh_of_a_feature_has_exactly_its_triangles()
    {
        TubeModel m = Model(new RectangularTubeProfile(80, 40, 3, 6), 600, new HoleFeature(Guid.NewGuid(), 300, 90, 14));
        TubeSolid s = Solid(m);
        Guid id = s.AppliedFeatures.Single();
        TriangleMesh wall = s.ExtractMesh(s.TrianglesOf(id), 0.03);
        Assert.Equal(s.TrianglesOf(id).Count(), wall.TriangleCount);
        Assert.Equal(wall.TriangleCount * 3, wall.Positions.Count);
    }
}
