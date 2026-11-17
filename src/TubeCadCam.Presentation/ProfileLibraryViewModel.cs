using System.Collections.ObjectModel;
using TubeCadCam.Application.Ports;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;

namespace TubeCadCam.Presentation;

public sealed record CatalogEntryItem(ProfileCatalogEntry Entry)
{
    public string Display => $"{(Entry.Verified ? "✓" : "⚠")} {Entry.Designation} ({Entry.Standard})";
}

/// <summary>Browses the extensible profile library. Entries flagged ⚠ are not verified against their source.</summary>
public sealed class ProfileLibraryViewModel(IProfileCatalogProvider provider) : ViewModelBase
{
    private CatalogEntryItem? _selected;
    private string _status = "Bibliothek nicht geladen.";

    public ObservableCollection<CatalogEntryItem> Entries { get; } = [];
    public CatalogEntryItem? Selected { get => _selected; set => Set(ref _selected, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        Result<ProfileCatalog> r = await provider.LoadAsync(ct);
        Entries.Clear();
        if (!r.IsSuccess)
        {
            Status = "Bibliothek konnte nicht geladen werden: " + string.Join(" | ", r.Issues.Select(i => i.Message));
            return;
        }

        foreach (ProfileCatalogEntry e in r.Value.Entries)
        {
            Entries.Add(new CatalogEntryItem(e));
        }

        Status = Entries.Count == 0
            ? "Bibliothek leer: Es sind keine Normprofile installiert (siehe catalogs/README.md). Eigene Profile können gespeichert werden."
            : $"{Entries.Count} Profil(e) geladen.";
    }

    public async Task<Result> AddUserEntryAsync(string designation, ProfileDefinition profile, CancellationToken ct = default)
    {
        var entry = new ProfileCatalogEntry(designation.Trim(), "user-defined", "user-defined", false, profile);
        Result r = await provider.AddUserEntryAsync(entry, ct);
        if (r.IsSuccess)
        {
            await RefreshAsync(ct);
        }

        return r;
    }
}
