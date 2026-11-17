using Microsoft.Win32;
using TubeCadCam.Presentation;

namespace TubeCadCam.Host.Wpf;

public sealed class WpfFileDialogService : IFileDialogService
{
    private const string Filter = "Tube CAD/CAM Modell (*.tubecam.json)|*.tubecam.json|JSON (*.json)|*.json|Alle Dateien (*.*)|*.*";

    public string? PickFileToOpen()
    {
        var dlg = new OpenFileDialog { Filter = Filter, CheckFileExists = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickFileToSave(string suggestedName)
    {
        var dlg = new SaveFileDialog { Filter = Filter, FileName = suggestedName + ".tubecam.json", DefaultExt = ".tubecam.json", AddExtension = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
