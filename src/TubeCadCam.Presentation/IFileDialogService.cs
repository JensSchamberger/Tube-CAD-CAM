namespace TubeCadCam.Presentation;

/// <summary>UI-framework neutral file picking; implemented by the host.</summary>
public interface IFileDialogService
{
    string? PickFileToOpen();
    string? PickFileToSave(string suggestedName);
}
