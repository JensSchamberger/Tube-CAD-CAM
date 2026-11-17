namespace TubeCadCam.Domain.Common;

public enum IssueSeverity { Warning, Error }

public sealed record ValidationIssue(string Code, string Message, IssueSeverity Severity = IssueSeverity.Error);
