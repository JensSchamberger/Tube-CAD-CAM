using TubeCadCam.Application.Services;
using TubeCadCam.Cad;
using TubeCadCam.Cam;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;
using TubeCadCam.Geometry;
using TubeCadCam.Infrastructure;
using TubeCadCam.Presentation;

namespace TubeCadCam.Tests;

public class ProfileEditorTests
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
    [InlineData(ProfileKind.Custom, typeof(CustomProfile))]
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
        int profileChanged = 0;
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.ProfileChanged += (_, _) => profileChanged++;

        vm.Parameters.First(p => p.Name == "WallThickness").Value = 40;
        Assert.False(vm.IsValid);
        Assert.Contains(vm.Issues, i => i.Code == "profile.wall-too-thick");
        Assert.Empty(vm.PreviewLoops);
        Assert.True(double.IsNaN(vm.AreaMm2));
        Assert.Contains(nameof(ProfileEditorViewModel.IsValid), changed);
        Assert.Equal(1, profileChanged);

        vm.Parameters.First(p => p.Name == "WallThickness").Value = 3;
        Assert.True(vm.IsValid);
    }

    public static TheoryData<ProfileDefinition> Loadable => new()
    {
        new RoundTubeProfile(88.9, 4),
        new RectangularTubeProfile(100, 60, 4, 8),
        RectangularTubeProfile.Square(40, 3, 4),
        new UProfile(120, 55, 7, 9),
        new LProfile(60, 40, 6),
        new IProfile(140, 73, 4.7, 6.9),
    };

    [Theory]
    [MemberData(nameof(Loadable))]
    public void Load_then_definition_reproduces_the_profile_exactly(ProfileDefinition p)
    {
        var vm = new ProfileEditorViewModel();
        vm.Load(p);
        Assert.Equal(p, vm.Definition);
        Assert.True(vm.IsValid);
    }

    [Fact]
    public void Custom_profile_is_parsed_with_voids_and_roundtrips()
    {
        var vm = new ProfileEditorViewModel { Kind = ProfileKind.Custom };
        vm.CustomName = "Box";
        vm.CustomOuterText = "-20;-10\n20;-10\n20;10\n-20;10";
        vm.CustomVoidsText = "-10;-5\n10;-5\n10;5\n-10;5";
        Assert.True(vm.IsValid);
        Assert.Equal(800 - 200, vm.AreaMm2, 9);
        var copy = new ProfileEditorViewModel();
        copy.Load(vm.Definition!);
        Assert.Equal(ProfileKind.Custom, copy.Kind);
        Assert.Equal(vm.AreaMm2, copy.AreaMm2, 9);
    }

    [Theory]
    [InlineData("1;2\nabc;3\n4;5\n6;7", "input.point-invalid")]
    [InlineData("1;2\n3;4", "input.too-few-points")]
    [InlineData("", "input.outer-count")]
    public void Custom_profile_input_errors_are_reported(string outer, string code)
    {
        var vm = new ProfileEditorViewModel { Kind = ProfileKind.Custom, CustomOuterText = outer };
        Assert.False(vm.IsValid);
        Assert.Null(vm.Definition);
        Assert.Contains(vm.Issues, i => i.Code == code);
    }

    [Fact]
    public void Area_text_shows_value_or_dash()
    {
        var vm = new ProfileEditorViewModel();
        Assert.Equal("Querschnittsfläche: " + vm.AreaMm2.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " mm²", vm.AreaText);
        vm.Parameters.First(p => p.Name == "WallThickness").Value = 99;
        Assert.Equal("Querschnittsfläche: –", vm.AreaText);
    }

    [Fact]
    public void Point_parser_handles_separators_and_blank_line_loops()
    {
        var (loops, issues) = PointListParser.Parse("1;2\r\n3 4\n\n5;6\n7;8.5\n9;1", "t");
        Assert.Empty(issues);
        Assert.Equal(2, loops.Count);
        Assert.Equal(new Vector2(7, 8.5), loops[1][1]);
    }
}

public class OrbitCameraTests
{
    [Fact]
    public void Position_look_and_up_are_consistent_and_z_is_up()
    {
        var c = new OrbitCamera();
        c.SetPreset(ViewPreset.Side);                       // camera on -Y looking towards +Y
        Assert.Equal(1, c.LookDirection.Y, 6);
        Assert.True(c.Position.Y < 0 && Math.Abs(c.Position.Z) < 1e-9);
        Assert.Equal(new Vector3(0, 0, 1).Z, c.UpDirection.Z, 6);
        Assert.Equal(0, c.LookDirection.Dot(c.UpDirection), 9);
        Assert.Equal(1, c.LookDirection.Length, 9);
    }

    [Fact]
    public void Orbit_changes_angles_and_clamps_elevation()
    {
        var c = new OrbitCamera();
        double az = c.AzimuthDeg;
        c.Orbit(50, 0);
        Assert.NotEqual(az, c.AzimuthDeg);
        c.Orbit(0, 100000);
        Assert.Equal(OrbitCamera.MaxElevationDeg, c.ElevationDeg);
        c.Orbit(0, -1000000);
        Assert.Equal(OrbitCamera.MinElevationDeg, c.ElevationDeg);
    }

    [Fact]
    public void Zoom_scales_distance_and_respects_limits()
    {
        var c = new OrbitCamera();
        c.Fit(new Bounds3(new Vector3(0, -30, -30), new Vector3(1000, 30, 30)));
        double d = c.Distance;
        c.Zoom(120);
        Assert.True(c.Distance < d);
        c.Zoom(-120);
        Assert.Equal(d, c.Distance, 9);
        c.Zoom(1e9);
        Assert.Equal(c.MinDistance, c.Distance);
    }

    [Fact]
    public void Pan_moves_target_perpendicular_to_view()
    {
        var c = new OrbitCamera();
        c.Fit(new Bounds3(new Vector3(0, -30, -30), new Vector3(1000, 30, 30)));
        Vector3 t0 = c.Target;
        Vector3 look = c.LookDirection;
        c.Pan(100, 40, 600);
        Vector3 delta = c.Target - t0;
        Assert.True(delta.Length > 0);
        Assert.Equal(0, delta.Dot(look), 6);
    }

    [Fact]
    public void Fit_centres_on_bounds_and_sees_the_whole_model()
    {
        var c = new OrbitCamera();
        var b = new Bounds3(new Vector3(0, -30, -30), new Vector3(1000, 30, 30));
        c.Fit(b);
        Assert.Equal(b.Center, c.Target);
        Assert.True(c.Distance * Math.Sin(OrbitCamera.FieldOfViewDeg * Math.PI / 360) >= b.Radius);
    }

    [Fact]
    public void Changed_event_fires_for_every_interaction()
    {
        var c = new OrbitCamera();
        int n = 0;
        c.Changed += (_, _) => n++;
        c.Orbit(1, 1); c.Zoom(120); c.Pan(1, 1, 600); c.SetPreset(ViewPreset.Top); c.Fit(new Bounds3(Vector3.Zero, new Vector3(1, 1, 1)));
        Assert.Equal(5, n);
    }
}

public sealed class EditorViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tubecadcam-ui-" + Guid.NewGuid().ToString("N"));

    public EditorViewModelTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class FakeDialogs : IFileDialogService
    {
        public string? OpenPath { get; set; }
        public string? SavePath { get; set; }
        public string? PickFileToOpen() => OpenPath;
        public string? PickFileToSave(string suggestedName) => SavePath;
    }

    private MainViewModel NewVm(FakeDialogs? dialogs = null) =>
        new(new JsonModelDocumentStore(), new JsonProfileCatalogProvider(null, Path.Combine(_dir, "user.json")), dialogs ?? new FakeDialogs());

    [Fact]
    public void Initial_state_has_valid_model_and_3d_scene()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.Model);
        Assert.NotNull(vm.Scene);
        Assert.False(vm.HasErrors);
        Assert.True(vm.Is3D);
        Assert.False(vm.IsDirty);
    }

    [Theory]
    [InlineData(FeatureKind.Hole)]
    [InlineData(FeatureKind.RectangularCutout)]
    [InlineData(FeatureKind.Slot)]
    [InlineData(FeatureKind.ContourCutout)]
    [InlineData(FeatureKind.Slit)]
    [InlineData(FeatureKind.EndNotch)]
    [InlineData(FeatureKind.EndMiter)]
    public void Every_feature_kind_can_be_added_validated_and_appears_in_the_model(FeatureKind kind)
    {
        var vm = new MainViewModel();
        var f = vm.AddFeature(kind);
        Assert.Same(f, vm.SelectedFeature);
        Assert.True(f.IsSelected);
        Assert.False(vm.HasErrors);
        Assert.Equal(FeatureDescriptors.KindOf(Assert.Single(vm.Model!.Features)), kind);
        Assert.True(vm.IsDirty);
        if (kind != FeatureKind.EndMiter)
        {
            Assert.Contains(vm.Scene!.PreviewShapes, s => s.FeatureId == f.Id);
        }
    }

    [Fact]
    public void Editing_a_feature_parameter_updates_model_and_scene_immediately()
    {
        var vm = new MainViewModel();
        var f = vm.AddFeature(FeatureKind.Hole);
        double before = vm.Scene!.PreviewShapes.Single().Mesh.Bounds.Max.Y - vm.Scene.PreviewShapes.Single().Mesh.Bounds.Min.Y;
        f.Parameters.First(p => p.Name == "Diameter").Value = 20;
        double after = vm.Scene!.PreviewShapes.Single().Mesh.Bounds.Max.Y - vm.Scene.PreviewShapes.Single().Mesh.Bounds.Min.Y;
        Assert.True(after > before * 1.5);
        Assert.Equal(20, ((HoleFeature)vm.Model!.Features.Single()).DiameterMm);
    }

    [Fact]
    public void Invalid_feature_edit_is_reported_and_clears_when_fixed()
    {
        var vm = new MainViewModel();
        var f = vm.AddFeature(FeatureKind.Hole);
        f.Parameters.First(p => p.Name == "Diameter").Value = 500;
        Assert.True(vm.HasErrors);
        Assert.Contains(vm.Issues, i => i.Code == "feature.hole-too-large");
        f.Parameters.First(p => p.Name == "Diameter").Value = 10;
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public void Changing_length_and_profile_updates_scene_and_revalidates_features()
    {
        var vm = new MainViewModel();
        vm.AddFeature(FeatureKind.Hole);                 // at mid-length of 1000
        double v0 = vm.Scene!.Body.SignedVolume();
        vm.LengthMm = 1500;
        Assert.Equal(1500, vm.Scene!.Bounds.Max.X - vm.Scene.Bounds.Min.X, 6);
        Assert.True(vm.Scene.Body.SignedVolume() > v0);
        vm.LengthMm = 100;                                // hole at 500 now outside
        Assert.Contains(vm.Issues, i => i.Code == "feature.outside-length");
        vm.LengthMm = 1000;
        vm.ProfileEditor.Kind = ProfileKind.Rectangular;
        Assert.IsType<RectangularTubeProfile>(vm.Model!.Part.Profile);
        Assert.NotNull(vm.Scene);
    }

    [Fact]
    public void Invalid_profile_removes_model_and_scene_but_keeps_features()
    {
        var vm = new MainViewModel();
        vm.AddFeature(FeatureKind.Hole);
        vm.ProfileEditor.Parameters.First(p => p.Name == "WallThickness").Value = 99;
        Assert.Null(vm.Model);
        Assert.Null(vm.Scene);
        Assert.True(vm.HasErrors);
        Assert.Single(vm.Features);
        vm.ProfileEditor.Parameters.First(p => p.Name == "WallThickness").Value = 3;
        Assert.NotNull(vm.Scene);
    }

    [Fact]
    public void Contour_text_errors_are_reported_without_crashing()
    {
        var vm = new MainViewModel();
        var f = vm.AddFeature(FeatureKind.ContourCutout);
        f.ContourText = "1;1\nnonsense";
        Assert.Contains(vm.Issues, i => i.Code is "input.point-invalid" or "input.contour-points");
        Assert.Empty(vm.Model!.Features);
        f.ContourText = "-5;-5\n5;-5\n0;6";
        Assert.False(vm.HasErrors);
        Assert.Single(vm.Model.Features);
    }

    [Fact]
    public void Selection_from_the_3d_view_selects_feature_and_clears_on_empty_space()
    {
        var vm = new MainViewModel();
        var a = vm.AddFeature(FeatureKind.Hole);
        var b = vm.AddFeature(FeatureKind.Slot);
        vm.SelectFeature(a.Id);
        Assert.Same(a, vm.SelectedFeature);
        Assert.True(a.IsSelected);
        Assert.False(b.IsSelected);
        vm.SelectFeature(null);
        Assert.Null(vm.SelectedFeature);
        Assert.False(a.IsSelected);
        vm.SelectFeature(Guid.NewGuid());
        Assert.Null(vm.SelectedFeature);
    }

    [Fact]
    public void Remove_and_duplicate_features()
    {
        var vm = new MainViewModel();
        var a = vm.AddFeature(FeatureKind.Hole);
        vm.DuplicateSelectedFeature();
        Assert.Equal(2, vm.Features.Count);
        Assert.NotEqual(a.Id, vm.SelectedFeature!.Id);
        Assert.Equal("2. Bohrung", vm.SelectedFeature.DisplayName);
        Assert.Equal(2, vm.Model!.Features.Count);
        vm.RemoveSelectedFeature();
        Assert.Single(vm.Features);
        Assert.Single(vm.Model.Features);
        vm.RemoveSelectedFeature();
        Assert.Empty(vm.Features);
        Assert.True(((RelayCommandProbe)new RelayCommandProbe(vm)).CanRemove == false);
    }

    private sealed class RelayCommandProbe(MainViewModel vm)
    {
        public bool CanRemove => vm.RemoveFeatureCommand.CanExecute(null);
    }

    [Fact]
    public void View_mode_toggle_switches_between_2d_section_and_3d()
    {
        var vm = new MainViewModel();
        vm.ToggleViewModeCommand.Execute(null);
        Assert.True(vm.Is2D);
        Assert.False(vm.Is3D);
        Assert.NotEmpty(vm.ProfileEditor.PreviewLoops);
        vm.ToggleViewModeCommand.Execute(null);
        Assert.True(vm.Is3D);
    }

    [Fact]
    public async Task Save_and_reload_restores_the_complete_parametric_model()
    {
        string path = Path.Combine(_dir, "model.json");
        var dialogs = new FakeDialogs { SavePath = path };
        var vm = NewVm(dialogs);
        vm.Name = "Testrohr";
        vm.MaterialId = "1.4301";
        vm.LengthMm = 1800;
        vm.ProfileEditor.Kind = ProfileKind.Rectangular;
        vm.ProfileEditor.Parameters.First(p => p.Name == "Width").Value = 100;
        double[] axial = [200, 400, 600, 800];
        foreach (FeatureKind k in Enum.GetValues<FeatureKind>())
        {
            var f = vm.AddFeature(k);
            if (f.Parameters.FirstOrDefault(p => p.Name == "AxialPosition") is { } ap) { ap.Value = axial[(int)k % 4] + ((int)k / 4 * 0); }
            if (k == FeatureKind.EndNotch) { f.Parameters.First(p => p.Name == "Angle").Value = 270; }
            // The miter sits at the end; the slit at the start: a slit reaching a mitred end face is reported as unsupported (see SolidTests).
            if (k == FeatureKind.EndMiter) { f.End = TubeEnd.End; }
        }
        Assert.False(vm.HasErrors, string.Join(";", vm.Issues.Select(i => i.Message)));
        Assert.True(vm.IsDirty);

        await vm.SaveAsync(saveAs: true);
        Assert.False(vm.IsDirty);
        Assert.Equal(path, vm.FilePath);
        var expected = vm.Model!;

        dialogs.OpenPath = path;
        var other = NewVm(dialogs);
        await other.OpenAsync();
        Assert.False(other.IsDirty);
        Assert.Equal(expected.Part.Profile, other.Model!.Part.Profile);
        Assert.Equal(expected.Part.LengthMm, other.Model.Part.LengthMm);
        Assert.Equal("Testrohr", other.Model.Part.Name);
        Assert.Equal("1.4301", other.Model.Part.MaterialId);
        Assert.Equal(expected.Features.Select(f => f.Id), other.Model.Features.Select(f => f.Id));
        Assert.Equal(7, other.Features.Count);
        Assert.Equal(vm.Scene!.Body.Positions, other.Scene!.Body.Positions);
        Assert.Equal(vm.Scene.PreviewShapes.Count, other.Scene.PreviewShapes.Count);

        // editing after load still works
        other.Features[0].Parameters.First(p => p.Name == "Diameter").Value = 12;
        Assert.True(other.IsDirty);
        Assert.Equal(12, ((HoleFeature)other.Model!.Features[0]).DiameterMm);
    }

    [Fact]
    public async Task Open_of_broken_file_reports_status_and_keeps_state()
    {
        string bad = Path.Combine(_dir, "bad.json");
        await File.WriteAllTextAsync(bad, "garbage");
        var vm = NewVm(new FakeDialogs { OpenPath = bad });
        vm.AddFeature(FeatureKind.Hole);
        await vm.OpenAsync();
        Assert.StartsWith("Öffnen fehlgeschlagen", vm.Status);
        Assert.Single(vm.Features);
    }

    [Fact]
    public void New_document_resets_everything()
    {
        var vm = new MainViewModel();
        vm.LengthMm = 321;
        vm.ProfileEditor.Parameters.First(p => p.Name == "OuterDiameter").Value = 99;
        vm.AddFeature(FeatureKind.Slot);
        vm.NewDocument();
        Assert.Empty(vm.Features);
        Assert.Equal(1000, vm.LengthMm);
        Assert.Equal(60, ((RoundTubeProfile)vm.Model!.Part.Profile).OuterDiameterMm);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task Library_flow_empty_catalog_user_entry_apply()
    {
        var vm = NewVm();
        await vm.Library!.RefreshAsync();
        Assert.Empty(vm.Library.Entries);
        Assert.Contains("keine Normprofile", vm.Library.Status);

        vm.ProfileEditor.Kind = ProfileKind.L;
        vm.LibraryDesignation = "Mein Winkel";
        await vm.SaveToLibraryAsync();
        Assert.Single(vm.Library.Entries);
        Assert.StartsWith("⚠", vm.Library.Entries[0].Display);   // user entries are never "verified"

        vm.ProfileEditor.Kind = ProfileKind.Round;
        vm.Library.Selected = vm.Library.Entries[0];
        Assert.True(vm.ApplyLibraryEntryCommand.CanExecute(null));
        vm.ApplyLibraryEntryCommand.Execute(null);
        Assert.IsType<LProfile>(vm.Model!.Part.Profile);
        Assert.Contains("nicht verifiziert", vm.Status);

        vm.LibraryDesignation = "Mein Winkel";
        await vm.SaveToLibraryAsync();
        Assert.Contains("Bibliothek", vm.Status);        // duplicate designation is rejected
        Assert.Single(vm.Library.Entries);
    }

    [Fact]
    public void Model_built_by_the_editor_feeds_the_cam_core_unchanged()
    {
        var vm = new MainViewModel();
        vm.AddFeature(FeatureKind.Hole);
        vm.AddFeature(FeatureKind.Slit);
        var plan = new CamPlanner(new FeatureContourRecognizer()).Plan(vm.Model!).Value;
        Assert.Single(plan.Contours);
        Assert.Single(plan.UnrecognizedFeatureIds!);
        Assert.Equal(CamPlanStatus.Draft, plan.Status);
    }
}

public class LabelTests
{
    [Fact]
    public void Every_editable_parameter_has_a_german_label_with_unit()
    {
        var names = new HashSet<string>();
        foreach (ProfileKind k in Enum.GetValues<ProfileKind>())
        {
            foreach (var p in new ProfileEditorViewModel { Kind = k }.Parameters) { names.Add(p.Name); }
        }

        foreach (FeatureKind k in Enum.GetValues<FeatureKind>())
        {
            foreach (var (n, _) in FeatureDescriptors.Defaults(k, 1000)) { names.Add(n); }
        }

        Assert.All(names, n => Assert.True(ParameterLabels.Label(n) != n && (ParameterLabels.Label(n).EndsWith("[mm]") || ParameterLabels.Label(n).EndsWith("[°]")), n));
        Assert.Equal("Unbekannt", ParameterLabels.Label("Unbekannt"));
    }

    [Fact]
    public void Kinds_have_distinct_labels()
    {
        Assert.Equal(Enum.GetValues<ProfileKind>().Length, Enum.GetValues<ProfileKind>().Select(ProfileKindLabels.Label).Distinct().Count());
        Assert.Equal(Enum.GetValues<FeatureKind>().Length, Enum.GetValues<FeatureKind>().Select(FeatureDescriptors.Label).Distinct().Count());
    }

    [Fact]
    public void New_feature_kind_selection_is_used_by_add_command()
    {
        var vm = new MainViewModel { NewFeatureKind = FeatureKind.Slot };
        vm.AddFeatureCommand.Execute(vm.NewFeatureKind);
        Assert.Equal(FeatureKind.Slot, vm.SelectedFeature!.Kind);
    }
}
