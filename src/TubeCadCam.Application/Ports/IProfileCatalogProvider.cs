using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;

namespace TubeCadCam.Application.Ports;

public interface IProfileCatalogProvider
{
    /// <summary>All available entries (shipped catalogs + user catalog). Failures name the offending file/entry.</summary>
    Task<Result<ProfileCatalog>> LoadAsync(CancellationToken ct = default);

    /// <summary>Stores an entry in the user catalog (never in shipped standard catalogs).</summary>
    Task<Result> AddUserEntryAsync(ProfileCatalogEntry entry, CancellationToken ct = default);
}
