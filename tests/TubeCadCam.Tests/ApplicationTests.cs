using TubeCadCam.Application.Functions;
using TubeCadCam.Application.Ports;
using TubeCadCam.Application.Services;
using TubeCadCam.Cad;
using TubeCadCam.Cam;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;
using TubeCadCam.Infrastructure;

namespace TubeCadCam.Tests;

public sealed class ApplicationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tubecadcam-tests-" + Guid.NewGuid().ToString("N"));
    private readonly JsonFileTubeModelRepository _repo;
    private readonly TubeModelService _service;

    public ApplicationTests()
    {
        _repo = new JsonFileTubeModelRepository(_dir);
        _service = new TubeModelService(_repo, new CamPlanner(new FeatureContourRecognizer()));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Create_add_feature_and_analyze_roundtrip()
    {
        var created = await _service.CreateAsync("Rohr 1", "S235", new RoundTubeProfile(60, 3), 1000);
        Assert.True(created.IsSuccess);
        var withHole = await _service.AddFeatureAsync(created.Value.Id, new HoleFeature(Guid.NewGuid(), 200, 45, 12));
        Assert.True(withHole.IsSuccess);

        var analysis = await _service.AnalyzeAsync(created.Value.Id);
        Assert.True(analysis.IsSuccess);
        Assert.Equal(1, analysis.Value.FeatureCount);
        Assert.Equal(1, analysis.Value.ContourCount);
        Assert.Equal(new RoundTubeProfile(60, 3).CrossSectionAreaMm2, analysis.Value.CrossSectionAreaMm2, 9);
    }

    [Fact]
    public async Task Invalid_feature_is_not_persisted()
    {
        var m = (await _service.CreateAsync("R", "S235", new RoundTubeProfile(60, 3), 1000)).Value;
        var bad = await _service.AddFeatureAsync(m.Id, new HoleFeature(Guid.NewGuid(), 5, 0, 20));
        Assert.False(bad.IsSuccess);
        Assert.Empty((await _repo.GetAsync(m.Id))!.Features);
    }

    [Fact]
    public async Task Invalid_model_is_not_created()
    {
        Assert.False((await _service.CreateAsync("", "S235", new RoundTubeProfile(60, 3), 1000)).IsSuccess);
        Assert.Empty(await _repo.ListAsync());
    }

    [Fact]
    public async Task Json_repository_roundtrips_all_profile_and_feature_types()
    {
        ProfileDefinition[] profiles =
        [
            new RoundTubeProfile(60, 3), new RectangularTubeProfile(80, 40, 3, 5), new UProfile(100, 50, 6, 8),
            new LProfile(50, 50, 5), new IProfile(100, 55, 4, 6),
            new CustomProfile("c", Polygon2.Rectangle(40, 20, Vector2.Zero), [Polygon2.Rectangle(30, 10, Vector2.Zero)]),
        ];
        foreach (ProfileDefinition p in profiles)
        {
            TubeModel m = TubeModelTests.NewModel(profile: p)
                .AddFeature(new HoleFeature(Guid.NewGuid(), 300, 10, 5))
                .AddFeature(new RectangularCutoutFeature(Guid.NewGuid(), 600, 90, 5, 20, 1));
            await _repo.SaveAsync(m);
            TubeModel loaded = (await _repo.GetAsync(m.Id))!;
            Assert.Equal(p.GetType(), loaded.Part.Profile.GetType());
            Assert.Equal(p.CrossSectionAreaMm2, loaded.Part.Profile.CrossSectionAreaMm2, 9);
            Assert.Equal(m.Features.Select(f => f.GetType()), loaded.Features.Select(f => f.GetType()));
            Assert.Equal(m.Features, loaded.Features);
        }

        Assert.Equal(profiles.Length, (await _repo.ListAsync()).Count);
    }

    [Fact]
    public async Task Repository_returns_null_for_unknown_id() => Assert.Null(await _repo.GetAsync(Guid.NewGuid()));

    [Fact]
    public async Task Step_import_reports_not_implemented()
    {
        IStepImporter importer = new NotSupportedStepImporter();
        var r = await importer.ImportAsync(Stream.Null);
        Assert.False(r.IsSuccess);
        Assert.Equal("step.not-implemented", r.Issues[0].Code);
    }

    [Fact]
    public async Task Function_catalog_validates_and_invokes()
    {
        var m = (await _service.CreateAsync("R", "S235", new RoundTubeProfile(60, 3), 1000)).Value;
        var catalog = new AppFunctionCatalog([new AnalyzeTubeModelFunction(_service)]);

        Assert.Single(catalog.List());
        Assert.True(catalog.List()[0].IsReadOnly);
        Assert.True((await catalog.InvokeAsync("analyze_tube_model", new Dictionary<string, string> { ["modelId"] = m.Id.ToString() })).IsSuccess);
        Assert.Equal("function.unknown", (await catalog.InvokeAsync("nope", new Dictionary<string, string>())).Issues[0].Code);
        Assert.Equal("function.arguments-missing", (await catalog.InvokeAsync("analyze_tube_model", new Dictionary<string, string>())).Issues[0].Code);
        Assert.Equal("function.argument-invalid", (await catalog.InvokeAsync("analyze_tube_model", new Dictionary<string, string> { ["modelId"] = "x" })).Issues[0].Code);
        Assert.Equal("model.not-found", (await catalog.InvokeAsync("analyze_tube_model", new Dictionary<string, string> { ["modelId"] = Guid.NewGuid().ToString() })).Issues[0].Code);
    }

    [Fact]
    public void Duplicate_function_names_are_rejected() =>
        Assert.Throws<ArgumentException>(() => new AppFunctionCatalog([new AnalyzeTubeModelFunction(_service), new AnalyzeTubeModelFunction(_service)]));
}
