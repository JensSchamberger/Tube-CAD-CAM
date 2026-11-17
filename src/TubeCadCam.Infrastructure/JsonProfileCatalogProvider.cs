using System.Text.Json;
using TubeCadCam.Application.Ports;
using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;

namespace TubeCadCam.Infrastructure;

public sealed record ProfileCatalogDocument(int SchemaVersion, string Name, IReadOnlyList<ProfileCatalogEntry> Entries);

/// <summary>
/// Reads every <c>*.json</c> catalog in a directory of shipped catalogs plus one user catalog file.
/// No catalog data is built in: dimensions come from files that name their source.
/// </summary>
public sealed class JsonProfileCatalogProvider(string? catalogDirectory, string userCatalogFile) : IProfileCatalogProvider
{
    public const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<Result<ProfileCatalog>> LoadAsync(CancellationToken ct = default)
    {
        var entries = new List<ProfileCatalogEntry>();
        var issues = new List<ValidationIssue>();
        var files = new List<string>();
        if (catalogDirectory is not null && Directory.Exists(catalogDirectory))
        {
            files.AddRange(Directory.EnumerateFiles(catalogDirectory, "*.json").OrderBy(f => f, StringComparer.Ordinal));
        }

        if (File.Exists(userCatalogFile))
        {
            files.Add(userCatalogFile);
        }

        foreach (string file in files)
        {
            (ProfileCatalogDocument? doc, string? error) = await ReadAsync(file, ct);
            if (doc is null)
            {
                issues.Add(new ValidationIssue("catalog.file-invalid", $"{Path.GetFileName(file)}: {error}"));
                continue;
            }

            entries.AddRange(doc.Entries);
        }

        if (issues.Count > 0)
        {
            return Result<ProfileCatalog>.Failure(issues);
        }

        return ProfileCatalog.Create(entries);
    }

    public async Task<Result> AddUserEntryAsync(ProfileCatalogEntry entry, CancellationToken ct = default)
    {
        Result<ProfileCatalog> current = await LoadAsync(ct);
        if (!current.IsSuccess)
        {
            return Result.Failure([.. current.Issues]);
        }

        Result<ProfileCatalog> merged = ProfileCatalog.Create(current.Value.Entries.Append(entry));
        if (!merged.IsSuccess)
        {
            return Result.Failure([.. merged.Issues]);
        }

        List<ProfileCatalogEntry> userEntries = [];
        if (File.Exists(userCatalogFile))
        {
            (ProfileCatalogDocument? doc, string? error) = await ReadAsync(userCatalogFile, ct);
            if (doc is null)
            {
                return Result.Failure("catalog.file-invalid", $"{Path.GetFileName(userCatalogFile)}: {error}");
            }

            userEntries.AddRange(doc.Entries);
        }

        userEntries.Add(entry);
        try
        {
            string? dir = Path.GetDirectoryName(userCatalogFile);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using FileStream fs = File.Create(userCatalogFile);
            await JsonSerializer.SerializeAsync(fs, new ProfileCatalogDocument(CurrentSchemaVersion, "user", userEntries), Options, ct);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure("catalog.save-failed", ex.Message);
        }
    }

    private static async Task<(ProfileCatalogDocument?, string?)> ReadAsync(string file, CancellationToken ct)
    {
        try
        {
            await using FileStream fs = File.OpenRead(file);
            ProfileCatalogDocument? doc = await JsonSerializer.DeserializeAsync<ProfileCatalogDocument>(fs, Options, ct);
            if (doc?.Entries is null)
            {
                return (null, "not a profile catalog");
            }

            return doc.SchemaVersion == CurrentSchemaVersion ? (doc, null) : (null, $"unsupported schema version {doc.SchemaVersion}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return (null, ex.Message);
        }
    }
}
