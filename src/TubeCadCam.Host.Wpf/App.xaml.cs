using System.Windows;
using TubeCadCam.Presentation;

namespace TubeCadCam.Host.Wpf;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Composition root: UI-independent services (repository, planner, catalog) are wired here as features arrive.
        var window = new MainWindow { DataContext = new MainViewModel() };
        window.Show();
    }
}
