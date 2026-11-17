namespace TubeCadCam.Domain.Common;

public class Result
{
    protected Result(IReadOnlyList<ValidationIssue> issues) => Issues = issues;

    public IReadOnlyList<ValidationIssue> Issues { get; }
    public bool IsSuccess => Issues.All(i => i.Severity != IssueSeverity.Error);

    public static Result Success() => new([]);
    public static Result Failure(params ValidationIssue[] issues) => new(issues);
    public static Result Failure(string code, string message) => new([new ValidationIssue(code, message)]);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, IReadOnlyList<ValidationIssue> issues) : base(issues) => _value = value;

    public T Value => IsSuccess && _value is not null
        ? _value
        : throw new InvalidOperationException("Result has no value because it failed.");

    public static Result<T> Success(T value, IReadOnlyList<ValidationIssue>? warnings = null) => new(value, warnings ?? []);
    public static new Result<T> Failure(string code, string message) => new(default, [new ValidationIssue(code, message)]);
    public static Result<T> Failure(IReadOnlyList<ValidationIssue> issues) => new(default, issues);
}
