namespace TubeCadCam.Presentation;

public sealed class MainViewModel : ViewModelBase
{
    public string Title => "Tube CAD/CAM";

    public ProfileEditorViewModel ProfileEditor { get; } = new();
}
