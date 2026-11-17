using TubeCadCam.Domain.Common;
using TubeCadCam.Domain.Profiles;

namespace TubeCadCam.Domain;

public sealed record TubePart(Guid Id, string Name, string MaterialId, ProfileDefinition Profile, double LengthMm)
{
    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            issues.Add(new ValidationIssue("part.name-missing", "Part name is required."));
        }

        if (string.IsNullOrWhiteSpace(MaterialId))
        {
            issues.Add(new ValidationIssue("part.material-missing", "Material is required."));
        }

        if (!(LengthMm > 0) || double.IsInfinity(LengthMm))
        {
            issues.Add(new ValidationIssue("part.length-invalid", "Length must be a finite number greater than 0."));
        }

        issues.AddRange(Profile.Validate());
        return issues;
    }
}
