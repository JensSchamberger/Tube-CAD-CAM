using System.Globalization;
using TubeCadCam.Application.Services;
using TubeCadCam.Cad;
using TubeCadCam.Domain.Common;

namespace TubeCadCam.Application.Functions;

/// <summary>State-changing function (IsReadOnly = false): adds a round hole after full geometric validation.</summary>
public sealed class AddHoleFeatureFunction(TubeModelService service) : IAppFunction
{
    public string Name => "add_hole_feature";
    public string Description => "Adds a round hole (axialPositionMm, angleDeg, diameterMm) to a tube model; rejected if geometrically invalid.";
    public bool IsReadOnly => false;
    public IReadOnlyList<string> RequiredArguments { get; } = ["modelId", "axialPositionMm", "angleDeg", "diameterMm"];

    public async Task<Result<object>> ExecuteAsync(IReadOnlyDictionary<string, string> arguments, CancellationToken ct = default)
    {
        if (!Guid.TryParse(arguments["modelId"], out Guid id))
        {
            return Result<object>.Failure("function.argument-invalid", "modelId must be a GUID.");
        }

        if (!TryNum(arguments["axialPositionMm"], out double pos) || !TryNum(arguments["angleDeg"], out double ang) || !TryNum(arguments["diameterMm"], out double dia))
        {
            return Result<object>.Failure("function.argument-invalid", "axialPositionMm, angleDeg and diameterMm must be numbers (invariant culture).");
        }

        Result<TubeModel> r = await service.AddFeatureAsync(id, new HoleFeature(Guid.NewGuid(), pos, ang, dia), ct);
        return r.IsSuccess ? Result<object>.Success(r.Value.Features[^1].Id, r.Issues) : Result<object>.Failure(r.Issues);
    }

    private static bool TryNum(string s, out double v) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && double.IsFinite(v);
}
