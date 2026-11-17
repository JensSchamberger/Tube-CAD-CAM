using System.IO;
using System.Windows;
using TubeCadCam.Infrastructure;
using TubeCadCam.Presentation;

namespace TubeCadCam.Host.Wpf;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Composition root: concrete infrastructure is chosen here and nowhere else.
        string userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TubeCadCam");
        var catalogs = new JsonProfileCatalogProvider(Path.Combine(AppContext.BaseDirectory, "catalogs"), Path.Combine(userDir, "user-profiles.json"));
        var viewModel = new MainViewModel(new JsonModelDocumentStore(), catalogs, new WpfFileDialogService());

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        _ = LoadLibraryAsync(viewModel);
    }

    private static async Task LoadLibraryAsync(MainViewModel vm)
    {
        try
        {
            if (vm.Library is not null)
            {
                await vm.Library.RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Profilbibliothek konnte nicht geladen werden: " + ex.Message, "Tube CAD/CAM");
        }
    }
}
