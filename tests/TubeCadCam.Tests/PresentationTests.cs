using TubeCadCam.Domain.Profiles;
using TubeCadCam.Presentation;

namespace TubeCadCam.Tests;

public class PresentationTests
{
    [Fact]
    public void Editor_starts_with_valid_round_profile()
    {
        var vm = new ProfileEditorViewModel();
        Assert.Equal(ProfileKind.Round, vm.Kind);
        Assert.True(vm.IsValid);
        Assert.IsType<RoundTubeProfile>(vm.Definition);
        Assert.False(double.IsNaN(vm.AreaMm2));
    }

    [Theory]
    [InlineData(ProfileKind.Round, typeof(RoundTubeProfile))]
    [InlineData(ProfileKind.Rectangular, typeof(RectangularTubeProfile))]
    [InlineData(ProfileKind.Square, typeof(RectangularTubeProfile))]
    [InlineData(ProfileKind.U, typeof(UProfile))]
    [InlineData(ProfileKind.L, typeof(LProfile))]
    [InlineData(ProfileKind.I, typeof(IProfile))]
    public void Every_kind_yields_valid_definition_and_preview_inside_box(ProfileKind kind, Type expected)
    {
        var vm = new ProfileEditorViewModel { Kind = kind };
        Assert.IsType(expected, vm.Definition);
        Assert.True(vm.IsValid);
        Assert.NotEmpty(vm.PreviewLoops);
        foreach (var p in vm.PreviewLoops.SelectMany(l => l))
        {
            Assert.InRange(p.X, -1e-9, ProfileEditorViewModel.PreviewSize + 1e-9);
            Assert.InRange(p.Y, -1e-9, ProfileEditorViewModel.PreviewSize + 1e-9);
        }
    }

    [Fact]
    public void Editing_a_parameter_revalidates_and_notifies()
    {
        var vm = new ProfileEditorViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Parameters.First(p => p.Name == "WallThickness").Value = 40; // > D/2
        Assert.False(vm.IsValid);
        Assert.Contains(vm.Issues, i => i.Code == "profile.wall-too-thick");
        Assert.Empty(vm.PreviewLoops);
        Assert.True(double.IsNaN(vm.AreaMm2));
        Assert.Contains(nameof(ProfileEditorViewModel.IsValid), changed);

        vm.Parameters.First(p => p.Name == "WallThickness").Value = 3;
        Assert.True(vm.IsValid);
    }

    [Fact]
    public void Main_view_model_exposes_editor() => Assert.NotNull(new MainViewModel().ProfileEditor);
}
