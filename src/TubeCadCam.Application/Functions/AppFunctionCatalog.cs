using TubeCadCam.Domain.Common;

namespace TubeCadCam.Application.Functions;

public sealed class AppFunctionCatalog
{
    private readonly Dictionary<string, IAppFunction> _functions = new(StringComparer.Ordinal);

    public AppFunctionCatalog(IEnumerable<IAppFunction> functions)
    {
        foreach (IAppFunction f in functions)
        {
            if (!_functions.TryAdd(f.Name, f))
            {
                throw new ArgumentException($"Duplicate application function '{f.Name}'.");
            }
        }
    }

    public IReadOnlyList<IAppFunction> List() => _functions.Values.OrderBy(f => f.Name, StringComparer.Ordinal).ToList();

    public async Task<Result<object>> InvokeAsync(string name, IReadOnlyDictionary<string, string> arguments, CancellationToken ct = default)
    {
        if (!_functions.TryGetValue(name, out IAppFunction? function))
        {
            return Result<object>.Failure("function.unknown", $"Unknown application function '{name}'.");
        }

        string[] missing = function.RequiredArguments.Where(a => !arguments.ContainsKey(a)).ToArray();
        if (missing.Length > 0)
        {
            return Result<object>.Failure("function.arguments-missing", $"Missing arguments: {string.Join(", ", missing)}.");
        }

        return await function.ExecuteAsync(arguments, ct);
    }
}
