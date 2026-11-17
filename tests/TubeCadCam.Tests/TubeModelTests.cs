using TubeCadCam.Cad;
using TubeCadCam.Domain;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;

namespace TubeCadCam.Tests;

public class TubeModelTests
{
    internal static TubeModel NewModel(double length = 1000, ProfileDefinition? profile = null) =>
        new(new TubePart(Guid.NewGuid(), "P1", "S235", profile ?? new RoundTubeProfile(60, 3), length), []);

    [Fact]
    public void Valid_model_with_hole_and_cutout_has_no_errors()
    {
        TubeModel m = NewModel()
            .AddFeature(new HoleFeature(Guid.NewGuid(), 100, 90, 10))
            .AddFeature(new RectangularCutoutFeature(Guid.NewGuid(), 500, 180, 20, 40, 3));
        Assert.Empty(m.Validate());
    }

    [Theory]
    [InlineData(2, "feature.outside-length")]      // hole d=10 at 2 => starts at -3
    [InlineData(998, "feature.outside-length")]
    public void Feature_outside_tube_is_rejected(double pos, string code)
    {
        TubeModel m = NewModel().AddFeature(new HoleFeature(Guid.NewGuid(), pos, 0, 10));
        Assert.Contains(m.Validate(), i => i.Code == code);
    }

    [Fact]
    public void Hole_larger_than_profile_is_rejected() =>
        Assert.Contains(NewModel().AddFeature(new HoleFeature(Guid.NewGuid(), 500, 0, 60)).Validate(), i => i.Code == "feature.hole-too-large");

    [Theory]
    [InlineData(-1)]
    [InlineData(360)]
    public void Angle_out_of_range_is_rejected(double angle) =>
        Assert.Contains(NewModel().AddFeature(new HoleFeature(Guid.NewGuid(), 500, angle, 10)).Validate(), i => i.Code == "feature.angle-range");

    [Fact]
    public void Duplicate_feature_ids_are_rejected()
    {
        var id = Guid.NewGuid();
        TubeModel m = NewModel().AddFeature(new HoleFeature(id, 300, 0, 10)).AddFeature(new HoleFeature(id, 600, 0, 10));
        Assert.Contains(m.Validate(), i => i.Code == "feature.duplicate-id");
    }

    [Fact]
    public void Invalid_cutout_corner_radius_is_rejected() =>
        Assert.Contains(NewModel().AddFeature(new RectangularCutoutFeature(Guid.NewGuid(), 500, 0, 10, 40, 6)).Validate(), i => i.Code == "feature.radius-invalid");

    [Fact]
    public void Invalid_part_short_circuits_feature_checks()
    {
        TubeModel m = NewModel(length: -5);
        Assert.Contains(m.Validate(), i => i.Code == "part.length-invalid");
    }

    [Fact]
    public void RemoveFeature_removes_by_id()
    {
        var id = Guid.NewGuid();
        TubeModel m = NewModel().AddFeature(new HoleFeature(id, 300, 0, 10)).RemoveFeature(id);
        Assert.Empty(m.Features);
    }

    [Fact]
    public void Result_value_access_on_failure_throws()
    {
        Result<int> r = Result<int>.Failure("x", "y");
        Assert.False(r.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => r.Value);
    }
}
