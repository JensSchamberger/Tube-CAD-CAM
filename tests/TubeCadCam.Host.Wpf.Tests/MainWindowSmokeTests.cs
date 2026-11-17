using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using TubeCadCam.Host.Wpf;
using TubeCadCam.Infrastructure;
using TubeCadCam.Presentation;

namespace TubeCadCam.Host.Wpf.Tests;

/// <summary>
/// Drives the real MainWindow on an STA thread. Requires Windows (WPF). Fails on any WPF data-binding error,
/// which would otherwise only show up as a silently empty control.
/// </summary>
public sealed class MainWindowSmokeTests
{
    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { if (!string.IsNullOrWhiteSpace(message)) { lock (Errors) { Errors.Add(message); } } }
    }

    private sealed class NoDialogs : IFileDialogService
    {
        public string? PickFileToOpen() => null;
        public string? PickFileToSave(string suggestedName) => null;
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) { throw new Xunit.Sdk.XunitException(error.ToString()); }
    }

    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject c = VisualTreeHelper.GetChild(root, i);
            if (c is T t) { return t; }
            if (Find<T>(c) is { } r) { return r; }
        }

        return null;
    }

    private static void Pump(Window w) => w.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

    [Fact]
    public void Window_renders_3d_scene_features_and_switches_to_2d_without_binding_errors()
    {
        RunSta(() =>
        {
            var listener = new BindingErrorListener();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
            if (System.Windows.Application.Current is null) { _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; }

            var vm = new MainViewModel(new JsonModelDocumentStore(), new JsonProfileCatalogProvider(null, Path.Combine(Path.GetTempPath(), "tubecadcam-ui-test-user.json")), new NoDialogs());
            var window = new MainWindow { DataContext = vm, Width = 1280, Height = 820 };
            window.Show();
            Pump(window);

            var view = (SceneView)window.FindName("Scene3D");
            Assert.NotNull(Find<Viewport3D>(view));
            Assert.Equal(0, view.FeatureVisualCount);
            Assert.Equal(vm.Scene!.Body.TriangleCount, view.BodyTriangleCount);
            int bodyWithoutFeatures = view.BodyTriangleCount;

            vm.AddFeature(FeatureKind.Hole);
            vm.AddFeature(FeatureKind.Slot);
            Pump(window);
            Assert.Empty(vm.Scene!.Failures);
            Assert.Equal(vm.Scene.Body.TriangleCount, view.BodyTriangleCount);
            Assert.True(view.BodyTriangleCount != bodyWithoutFeatures);   // the openings are part of the displayed body
            Assert.Equal(1, view.FeatureVisualCount);                    // only the selected feature is previewed by default

            vm.ShowFeaturePreview = true;
            Pump(window);
            Assert.Equal(2, view.FeatureVisualCount);

            vm.AddFeature(FeatureKind.ContourCutout);   // exercises the contour text editor binding
            vm.AddFeature(FeatureKind.EndMiter);        // exercises the end selector binding
            Pump(window);
            Assert.Equal(3, view.FeatureVisualCount);   // miter is part of the body, not a cutter
            vm.ShowFeaturePreview = false;
            Pump(window);
            Assert.Equal(0, view.FeatureVisualCount);   // the miter is selected and has no preview volume

            vm.ToggleViewModeCommand.Execute(null);
            Pump(window);
            Assert.False(view.IsVisible);
            vm.ToggleViewModeCommand.Execute(null);
            Pump(window);
            Assert.True(view.IsVisible);

            vm.ProfileEditor.Kind = ProfileKind.I;
            vm.ProfileEditor.Kind = ProfileKind.Custom;
            Pump(window);

            window.Close();
            lock (listener.Errors) { Assert.True(listener.Errors.Count == 0, string.Join(Environment.NewLine, listener.Errors)); }
        });
    }
}
