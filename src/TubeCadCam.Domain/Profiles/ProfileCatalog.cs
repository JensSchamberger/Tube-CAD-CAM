using TubeCadCam.Domain.Common;

namespace TubeCadCam.Domain.Profiles;

/// <summary>
/// One library entry. <see cref="Source"/> must name where the dimensions come from (standard edition/table or "user-defined").
/// <see cref="Verified"/> is true only if the dimensions were checked against that source by a person.
/// </summary>
public sealed record ProfileCatalogEntry(string Designation, string Standard, string Source, bool Verified, ProfileDefinition Profile)
{
    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(Designation))
        {
            issues.Add(new ValidationIssue("catalog.designation-missing", "Catalog entry needs a designation."));
        }

        if (string.IsNullOrWhiteSpace(Standard))
        {
            issues.Add(new ValidationIssue("catalog.standard-missing", $"Entry '{Designation}': standard is required (use \"user-defined\" for own profiles)."));
        }

        if (string.IsNullOrWhiteSpace(Source))
        {
            issues.Add(new ValidationIssue("catalog.source-missing", $"Entry '{Designation}': source of the dimensions is required."));
        }

        issues.AddRange(Profile.Validate().Select(i => i with { Message = $"Entry '{Designation}': {i.Message}" }));
        return issues;
    }
}

/// <summary>Immutable, validated collection of catalog entries with unique designations (case-insensitive).</summary>
public sealed class ProfileCatalog
{
    private readonly Dictionary<string, ProfileCatalogEntry> _byName;

    private ProfileCatalog(IReadOnlyList<ProfileCatalogEntry> entries)
    {
        Entries = entries;
        _byName = entries.ToDictionary(e => e.Designation, StringComparer.OrdinalIgnoreCase);
    }

    public static ProfileCatalog Empty { get; } = new([]);

    public IReadOnlyList<ProfileCatalogEntry> Entries { get; }

    public ProfileCatalogEntry? Find(string designation) =>
        _byName.TryGetValue(designation, out ProfileCatalogEntry? e) ? e : null;

    public static Result<ProfileCatalog> Create(IEnumerable<ProfileCatalogEntry> entries)
    {
        var list = entries.ToList();
        var issues = new List<ValidationIssue>();
        foreach (ProfileCatalogEntry e in list)
        {
            issues.AddRange(e.Validate());
        }

        foreach (var dup in list.GroupBy(e => e.Designation, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            issues.Add(new ValidationIssue("catalog.duplicate", $"Designation '{dup.Key}' occurs {dup.Count()} times."));
        }

        return issues.Count > 0
            ? Result<ProfileCatalog>.Failure(issues)
            : Result<ProfileCatalog>.Success(new ProfileCatalog(list.OrderBy(e => e.Designation, StringComparer.OrdinalIgnoreCase).ToArray()));
    }

    /// <summary>Merges catalogs; duplicate designations are an error (no silent override of standard data).</summary>
    public static Result<ProfileCatalog> Merge(params ProfileCatalog[] catalogs) =>
        Create(catalogs.SelectMany(c => c.Entries));
}
