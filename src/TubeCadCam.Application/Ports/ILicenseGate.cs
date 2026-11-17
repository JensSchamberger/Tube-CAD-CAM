namespace TubeCadCam.Application.Ports;

/// <summary>Seam for the future licensing architecture. No implementation exists yet (by design).</summary>
public interface ILicenseGate
{
    bool IsFeatureLicensed(string featureKey);
}
