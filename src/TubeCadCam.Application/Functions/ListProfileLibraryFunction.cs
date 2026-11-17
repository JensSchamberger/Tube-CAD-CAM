using TubeCadCam.Application.Ports;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;

namespace TubeCadCam.Application.Functions;

public sealed record ProfileLibraryItem(string Designation, string Standard, string Source, bool Verified, string ProfileKey);

public sealed class ListProfileLibraryFunction(IProfileCatalogProvider provider) : IAppFunction
{
    public string Name => "list_profile_library";
    public string Description => "Lists the profile library entries (designation, standard, source, verified flag). Read-only.";
    public bool IsReadOnly => true;
    public IReadOnlyList<string> RequiredArguments { get; } = [];

    public async Task<Result<object>> ExecuteAsync(IReadOnlyDictionary<string, string> arguments, CancellationToken ct = default)
    {
        Result<ProfileCatalog> r = await provider.LoadAsync(ct);
        if (!r.IsSuccess)
        {
            return Result<object>.Failure(r.Issues);
        }

        return Result<object>.Success(r.Value.Entries
            .Select(e => new ProfileLibraryItem(e.Designation, e.Standard, e.Source, e.Verified, e.Profile.ProfileKey)).ToList());
    }
}
