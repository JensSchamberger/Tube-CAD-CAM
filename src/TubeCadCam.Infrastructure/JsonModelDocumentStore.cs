using System.Text.Json;
using TubeCadCam.Application.Ports;
using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Infrastructure;

/// <summary>Self-contained model document (profile parameters, length, material and all features) with a schema version.</summary>
public sealed record TubeModelDocument(int SchemaVersion, TubeModel Model);

public sealed class JsonModelDocumentStore : IModelDocumentStore
{
    public const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<Result> SaveAsync(string path, TubeModel model, CancellationToken ct = default)
    {
        try
        {
            string tmp = path + ".tmp";
            await using (FileStream fs = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(fs, new TubeModelDocument(CurrentSchemaVersion, model), Options, ct);
            }

            File.Move(tmp, path, overwrite: true);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Result.Failure("document.save-failed", ex.Message);
        }
    }

    public async Task<Result<TubeModel>> LoadAsync(string path, CancellationToken ct = default)
    {
        try
        {
            await using FileStream fs = File.OpenRead(path);
            TubeModelDocument? doc = await JsonSerializer.DeserializeAsync<TubeModelDocument>(fs, Options, ct);
            if (doc is null || doc.Model is null)
            {
                return Result<TubeModel>.Failure("document.invalid", "The file does not contain a tube model.");
            }

            return doc.SchemaVersion switch
            {
                CurrentSchemaVersion => Result<TubeModel>.Success(doc.Model),
                > CurrentSchemaVersion => Result<TubeModel>.Failure("document.version-too-new", $"Schema version {doc.SchemaVersion} is newer than supported ({CurrentSchemaVersion})."),
                _ => Result<TubeModel>.Failure("document.version-unsupported", $"Schema version {doc.SchemaVersion} is not supported."),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return Result<TubeModel>.Failure("document.load-failed", ex.Message);
        }
    }
}
