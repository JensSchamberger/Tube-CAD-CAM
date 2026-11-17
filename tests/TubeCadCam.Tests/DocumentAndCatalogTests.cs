using TubeCadCam.Application.Functions;
using TubeCadCam.Application.Services;
using TubeCadCam.Cad;
using TubeCadCam.Cam;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;
using TubeCadCam.Infrastructure;

namespace TubeCadCam.Tests;

public sealed class DocumentAndCatalogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tubecadcam-p2-" + Guid.NewGuid().ToString("N"));

    public DocumentAndCatalogTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static TubeModel FullModel() =>
        TubeModelTests.NewModel(1200, new RectangularTubeProfile(80, 40, 3, 6))
            .AddFeature(new HoleFeature(Guid.NewGuid(), 100, 90, 12))
            .AddFeature(new RectangularCutoutFeature(Guid.NewGuid(), 300, 90, 20, 30, 2, 15))
            .AddFeature(new SlotFeature(Guid.NewGuid(), 500, 90, 30, 8, 20))
            .AddFeature(new ContourCutoutFeature(Guid.NewGuid(), 700, 90, new Polygon2([new(-8, -5), new(8, -5), new(0, 7)]), 10))
            .AddFeature(new EndNotchFeature(Guid.NewGuid(), TubeEnd.Start, 90, 15, 25, 3))
            .AddFeature(new SlitFeature(Guid.NewGuid(), TubeEnd.End, 90, 4, 60))
            .AddFeature(new EndMiterFeature(Guid.NewGuid(), TubeEnd.End, 20, 90));

    [Fact]
    public async Task Complete_parametric_model_survives_save_and_load()
    {
        var store = new JsonModelDocumentStore();
        string path = Path.Combine(_dir, "part.tubecam.json");
        TubeModel model = FullModel();
        Assert.DoesNotContain(model.Validate(), i => i.Severity == IssueSeverity.Error);

        Assert.True((await store.SaveAsync(path, model)).IsSuccess);
        Result<TubeModel> loaded = await store.LoadAsync(path);
        Assert.True(loaded.IsSuccess);

        Assert.Equal(model.Part.Profile, loaded.Value.Part.Profile);
        Assert.Equal(model.Part with { Profile = null! }, loaded.Value.Part with { Profile = null! });
        Assert.Equal(model.Features.Count, loaded.Value.Features.Count);
        for (int i = 0; i < model.Features.Count; i++)
        {
            Assert.Equal(model.Features[i].GetType(), loaded.Value.Features[i].GetType());
            if (model.Features[i] is not ContourCutoutFeature)
            {
                Assert.Equal(model.Features[i], loaded.Value.Features[i]);
            }
        }

        var c1 = (ContourCutoutFeature)model.Features[3];
        var c2 = (ContourCutoutFeature)loaded.Value.Features[3];
        Assert.Equal(c1.ContourOutline.Vertices, c2.ContourOutline.Vertices);

        // identical geometry after reload
        var a = TubeSceneBuilder.Build(model).Value;
        var b = TubeSceneBuilder.Build(loaded.Value).Value;
        Assert.Equal(a.Body.Positions, b.Body.Positions);
        Assert.Equal(a.Features.Count, b.Features.Count);
    }

    [Fact]
    public async Task Load_reports_problems_instead_of_throwing()
    {
        var store = new JsonModelDocumentStore();
        string bad = Path.Combine(_dir, "bad.json");
        await File.WriteAllTextAsync(bad, "{ not json");
        Assert.Equal("document.load-failed", (await store.LoadAsync(bad)).Issues[0].Code);
        Assert.Equal("document.load-failed", (await store.LoadAsync(Path.Combine(_dir, "missing.json"))).Issues[0].Code);

        string future = Path.Combine(_dir, "future.json");
        await store.SaveAsync(future, FullModel());
        await File.WriteAllTextAsync(future, (await File.ReadAllTextAsync(future)).Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 99"));
        Assert.Equal("document.version-too-new", (await store.LoadAsync(future)).Issues[0].Code);
    }

    // ---- catalog (test data is clearly marked synthetic; the product ships no standard data) ----

    private static ProfileCatalogEntry Entry(string name, ProfileDefinition p, bool verified = false) =>
        new(name, "TEST-ONLY", "synthetic test fixture, not a real standard", verified, p);

    [Fact]
    public void Catalog_requires_source_standard_designation_and_valid_profile()
    {
        var bad = new ProfileCatalogEntry("", "", "", false, new RoundTubeProfile(10, 9));
        var codes = ProfileCatalog.Create([bad]).Issues.Select(i => i.Code).ToArray();
        Assert.Contains("catalog.designation-missing", codes);
        Assert.Contains("catalog.standard-missing", codes);
        Assert.Contains("catalog.source-missing", codes);
        Assert.Contains("profile.wall-too-thick", codes);
    }

    [Fact]
    public void Catalog_rejects_duplicates_and_merge_never_overrides()
    {
        var a = ProfileCatalog.Create([Entry("X-1", new RoundTubeProfile(20, 2))]).Value;
        var dup = ProfileCatalog.Create([Entry("x-1", new RoundTubeProfile(30, 2))]).Value;
        Assert.Contains(ProfileCatalog.Merge(a, dup).Issues, i => i.Code == "catalog.duplicate");
        Assert.NotNull(a.Find("X-1"));
        Assert.Null(a.Find("nope"));
        Assert.Empty(ProfileCatalog.Empty.Entries);
    }

    [Fact]
    public async Task Provider_loads_shipped_directory_plus_user_catalog_and_persists_user_entries()
    {
        string shipped = Path.Combine(_dir, "catalogs");
        Directory.CreateDirectory(shipped);
        var doc = new ProfileCatalogDocument(1, "fixture", [Entry("T-ROUND", new RoundTubeProfile(20, 2)), Entry("T-U", new UProfile(50, 25, 4, 5)), Entry("T-CUSTOM", new CustomProfile("c", Polygon2.Rectangle(10, 10, Vector2.Zero), []))]);
        await File.WriteAllTextAsync(Path.Combine(shipped, "fixture.json"), System.Text.Json.JsonSerializer.Serialize(doc));

        var provider = new JsonProfileCatalogProvider(shipped, Path.Combine(_dir, "user", "user.json"));
        var cat = await provider.LoadAsync();
        Assert.Equal(["T-CUSTOM", "T-ROUND", "T-U"], cat.Value.Entries.Select(e => e.Designation));

        var mine = new ProfileCatalogEntry("MY-1", "user-defined", "user-defined", false, new LProfile(40, 40, 4));
        Assert.True((await provider.AddUserEntryAsync(mine)).IsSuccess);
        Assert.Equal(4, (await provider.LoadAsync()).Value.Entries.Count);
        Assert.Contains(((await provider.AddUserEntryAsync(mine)).Issues), i => i.Code == "catalog.duplicate");
        Assert.Equal(4, (await provider.LoadAsync()).Value.Entries.Count);
    }

    [Fact]
    public async Task Provider_without_files_yields_an_empty_catalog_and_broken_file_is_reported()
    {
        var empty = new JsonProfileCatalogProvider(Path.Combine(_dir, "none"), Path.Combine(_dir, "u.json"));
        Assert.Empty((await empty.LoadAsync()).Value.Entries);

        string shipped = Path.Combine(_dir, "c2");
        Directory.CreateDirectory(shipped);
        await File.WriteAllTextAsync(Path.Combine(shipped, "broken.json"), "[]");
        var r = await new JsonProfileCatalogProvider(shipped, Path.Combine(_dir, "u2.json")).LoadAsync();
        Assert.False(r.IsSuccess);
        Assert.Equal("catalog.file-invalid", r.Issues[0].Code);
    }

    // ---- application use cases ----

    private (TubeModelService Service, JsonFileTubeModelRepository Repo) NewService()
    {
        var repo = new JsonFileTubeModelRepository(Path.Combine(_dir, "repo"));
        return (new TubeModelService(repo, new CamPlanner(new FeatureContourRecognizer())), repo);
    }

    [Fact]
    public async Task Update_and_remove_feature_and_part_use_cases()
    {
        var (svc, repo) = NewService();
        var m = (await svc.CreateAsync("A", "S235", new RoundTubeProfile(60, 3), 1000)).Value;
        var h = new HoleFeature(Guid.NewGuid(), 300, 0, 10);
        Assert.True((await svc.AddFeatureAsync(m.Id, h)).IsSuccess);

        Assert.True((await svc.UpdateFeatureAsync(m.Id, h with { DiameterMm = 14 })).IsSuccess);
        Assert.Equal(14, ((HoleFeature)(await repo.GetAsync(m.Id))!.Features[0]).DiameterMm);
        Assert.False((await svc.UpdateFeatureAsync(m.Id, h with { DiameterMm = 80 })).IsSuccess);   // invalid is rejected and not stored
        Assert.Equal(14, ((HoleFeature)(await repo.GetAsync(m.Id))!.Features[0]).DiameterMm);
        Assert.Equal("feature.not-found", (await svc.UpdateFeatureAsync(m.Id, new HoleFeature(Guid.NewGuid(), 300, 0, 10))).Issues[0].Code);

        // shrinking the tube so that the hole falls outside is rejected
        Assert.False((await svc.UpdatePartAsync(m.Id, lengthMm: 200)).IsSuccess);
        Assert.True((await svc.UpdatePartAsync(m.Id, name: "B", lengthMm: 800, profile: new RoundTubeProfile(70, 4))).IsSuccess);
        Assert.Equal(800, (await repo.GetAsync(m.Id))!.Part.LengthMm);

        Assert.True((await svc.RemoveFeatureAsync(m.Id, h.Id)).IsSuccess);
        Assert.Equal("feature.not-found", (await svc.RemoveFeatureAsync(m.Id, h.Id)).Issues[0].Code);
        Assert.Equal("model.not-found", (await svc.UpdatePartAsync(Guid.NewGuid(), name: "x")).Issues[0].Code);
    }

    [Fact]
    public async Task Analysis_reports_unrecognised_features_explicitly()
    {
        var (svc, _) = NewService();
        var m = (await svc.CreateAsync("A", "S235", new RoundTubeProfile(60, 3), 1000)).Value;
        await svc.AddFeatureAsync(m.Id, new HoleFeature(Guid.NewGuid(), 300, 0, 10));
        await svc.AddFeatureAsync(m.Id, new SlitFeature(Guid.NewGuid(), TubeEnd.End, 0, 4, 60));
        await svc.AddFeatureAsync(m.Id, new EndMiterFeature(Guid.NewGuid(), TubeEnd.Start, 10, 0));
        var a = (await svc.AnalyzeAsync(m.Id)).Value;
        Assert.Equal(3, a.FeatureCount);
        Assert.Equal(1, a.ContourCount);
        Assert.Equal(2, a.UnrecognizedFeatureCount);
    }

    [Fact]
    public async Task Contours_are_recognised_for_all_wall_cut_types()
    {
        var plan = new CamPlanner(new FeatureContourRecognizer()).Plan(FullModel()).Value;
        Assert.Equal([ContourKind.Hole, ContourKind.Cutout, ContourKind.Slot, ContourKind.ContourCutout, ContourKind.EndNotch], plan.Contours.Select(c => c.Kind));
        Assert.Equal(2, plan.UnrecognizedFeatureIds!.Count);            // slit + miter
        Assert.Equal(CamPlanStatus.Draft, plan.Status);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Mcp_ready_functions_validate_and_mutate_only_when_valid()
    {
        var (svc, repo) = NewService();
        var m = (await svc.CreateAsync("A", "S235", new RoundTubeProfile(60, 3), 1000)).Value;
        var provider = new JsonProfileCatalogProvider(null, Path.Combine(_dir, "u.json"));
        var catalog = new AppFunctionCatalog([new AnalyzeTubeModelFunction(svc), new AddHoleFeatureFunction(svc), new ListProfileLibraryFunction(provider)]);

        Assert.Equal(["add_hole_feature", "analyze_tube_model", "list_profile_library"], catalog.List().Select(f => f.Name));
        Assert.False(catalog.List().Single(f => f.Name == "add_hole_feature").IsReadOnly);

        var ok = await catalog.InvokeAsync("add_hole_feature", new Dictionary<string, string> { ["modelId"] = m.Id.ToString(), ["axialPositionMm"] = "300.5", ["angleDeg"] = "90", ["diameterMm"] = "10" });
        Assert.True(ok.IsSuccess);
        Assert.Single((await repo.GetAsync(m.Id))!.Features);

        var bad = await catalog.InvokeAsync("add_hole_feature", new Dictionary<string, string> { ["modelId"] = m.Id.ToString(), ["axialPositionMm"] = "1", ["angleDeg"] = "90", ["diameterMm"] = "10" });
        Assert.False(bad.IsSuccess);
        Assert.Single((await repo.GetAsync(m.Id))!.Features);

        Assert.Equal("function.argument-invalid", (await catalog.InvokeAsync("add_hole_feature", new Dictionary<string, string> { ["modelId"] = m.Id.ToString(), ["axialPositionMm"] = "x", ["angleDeg"] = "1", ["diameterMm"] = "1" })).Issues[0].Code);
        Assert.True((await catalog.InvokeAsync("list_profile_library", new Dictionary<string, string>())).IsSuccess);
    }
}
