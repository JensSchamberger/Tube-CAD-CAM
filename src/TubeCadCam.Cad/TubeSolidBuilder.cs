using TubeCadCam.Domain.Common;
using TubeCadCam.Geometry;

namespace TubeCadCam.Cad;

/// <summary>
/// Builds the real 3D body of a tube with all features subtracted (openings, notches, slits) and the miter end faces.
/// Features that cannot be subtracted safely are listed in <see cref="TubeSolid.Failures"/>; they are never replaced by a
/// substitute shape. The parametric <see cref="TubeModel"/> stays the single source of truth; the solid is derived from it.
/// </summary>
public static class TubeSolidBuilder
{
    public static TubeSolid Build(TubeModel model, ProfileSection section, SolidOptions? options = null)
    {
        options ??= SolidOptions.Default;
        (List<Cutter> cutters, List<FeatureFailure> failures) = SolidCutters.Create(model, section, options);

        CoreResult all = new SolidAssembler(model, section, options).Build(cutters);
        if (all.Solid is not null)
        {
            return all.Solid with { Failures = failures.Concat(all.Failures).ToList() };
        }

        // The combination produced an invalid body: add the features one by one and name the ones that break it.
        var applied = new List<Cutter>();
        foreach (Cutter c in cutters)
        {
            CoreResult trial = new SolidAssembler(model, section, options).Build([.. applied, c]);
            FeatureFailure[] own = trial.Failures.Where(f => f.FeatureId == c.FeatureId).ToArray();
            if (own.Length > 0)
            {
                failures.AddRange(own);
            }
            else if (trial.Solid is null)
            {
                failures.Add(new FeatureFailure(c.FeatureId, "solid.topology", "Subtracting this feature does not yield a valid closed solid: " + trial.TopologyError));
            }
            else
            {
                applied.Add(c);
            }
        }

        CoreResult final = new SolidAssembler(model, section, options).Build(applied);
        if (final.Solid is null)
        {
            throw new InvalidOperationException("The tube body could not be built: " + final.TopologyError);
        }

        return final.Solid with { Failures = failures };
    }
}
