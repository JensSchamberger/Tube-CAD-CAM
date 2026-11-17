using TubeCadCam.Application.Services;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Application.Functions;

public sealed class AnalyzeTubeModelFunction(TubeModelService service) : IAppFunction
{
    public string Name => "analyze_tube_model";
    public string Description => "Validates a tube model and reports section area, feature and contour counts. Read-only.";
    public bool IsReadOnly => true;
    public IReadOnlyList<string> RequiredArguments { get; } = ["modelId"];

    public async Task<Result<object>> ExecuteAsync(IReadOnlyDictionary<string, string> arguments, CancellationToken ct = default)
    {
        if (!Guid.TryParse(arguments["modelId"], out Guid id))
        {
            return Result<object>.Failure("function.argument-invalid", "modelId must be a GUID.");
        }

        Result<TubeModelAnalysis> r = await service.AnalyzeAsync(id, ct);
        return r.IsSuccess ? Result<object>.Success(r.Value, r.Issues) : Result<object>.Failure(r.Issues);
    }
}
