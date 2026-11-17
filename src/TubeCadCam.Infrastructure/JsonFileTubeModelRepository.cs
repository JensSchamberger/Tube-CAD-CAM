using System.Text.Json;
using TubeCadCam.Application.Ports;
using TubeCadCam.Cad;

namespace TubeCadCam.Infrastructure;

/// <summary>Local persistence: one indented JSON file per tube model in a directory.</summary>
public sealed class JsonFileTubeModelRepository : ITubeModelRepository
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _directory;

    public JsonFileTubeModelRepository(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    private string PathFor(Guid id) => Path.Combine(_directory, $"{id:D}.json");

    public async Task SaveAsync(TubeModel model, CancellationToken ct = default)
    {
        string tmp = PathFor(model.Id) + ".tmp";
        await using (FileStream fs = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(fs, model, Options, ct);
        }

        File.Move(tmp, PathFor(model.Id), overwrite: true);
    }

    public async Task<TubeModel?> GetAsync(Guid id, CancellationToken ct = default)
    {
        string path = PathFor(id);
        if (!File.Exists(path))
        {
            return null;
        }

        await using FileStream fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<TubeModel>(fs, Options, ct);
    }

    public async Task<IReadOnlyList<TubeModel>> ListAsync(CancellationToken ct = default)
    {
        var list = new List<TubeModel>();
        foreach (string file in Directory.EnumerateFiles(_directory, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            await using FileStream fs = File.OpenRead(file);
            TubeModel? m = await JsonSerializer.DeserializeAsync<TubeModel>(fs, Options, ct);
            if (m is not null)
            {
                list.Add(m);
            }
        }

        return list;
    }
}
