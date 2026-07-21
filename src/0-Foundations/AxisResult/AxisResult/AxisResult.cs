using System.Diagnostics;

namespace Axis;

/// <summary>
/// A valueless Result monad for Railway-Oriented Programming: a computation either <see cref="IsSuccess"/>
/// with nothing to carry, or <see cref="IsFailure"/> with one or more <see cref="AxisError"/>. Never
/// throws to signal an expected failure — compose with <c>Then</c>/<c>Tap</c>/<c>Match</c>/<c>Recover</c>
/// instead of branching on <see cref="IsSuccess"/>/<see cref="IsFailure"/> by hand. Create instances with
/// <see cref="Ok()"/>, <see cref="Error(AxisError)"/> or the implicit conversions from <see cref="AxisError"/>.
/// The value-carrying sibling is <see cref="AxisResult{TValue}"/>.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public abstract partial class AxisResult(List<AxisError>? initErrors = null)
{
    protected readonly List<AxisError>? _errors = initErrors is { Count: > 0 } ? initErrors : null;

    /// <summary>The failures carried by this result. Empty when <see cref="IsSuccess"/> is true.</summary>
    public IReadOnlyList<AxisError> Errors => _errors ?? (IReadOnlyList<AxisError>)Array.Empty<AxisError>();

    protected List<AxisError>? RawErrors => _errors;

    /// <summary>Joins every <see cref="AxisError.Code"/> with <paramref name="separator"/>; empty string on success. Handy for logging.</summary>
    /// <param name="separator">Separator placed between error codes. Defaults to <c>", "</c>.</param>
    public string JoinErrorCodes(string separator = ", ")
    {
        if (_errors == null || _errors.Count == 0) return string.Empty;
        return string.Join(separator, _errors.Select(e => e.Code));
    }

    /// <summary>True when this result carries no errors.</summary>
    public bool IsSuccess => _errors == null || _errors.Count == 0;

    /// <summary>True when this result carries at least one <see cref="AxisError"/>. The negation of <see cref="IsSuccess"/>.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// True when this is a failure and every error is transient (<see cref="AxisError.IsTransient"/>) —
    /// worth a silent retry/backoff instead of surfacing as a terminal failure. A classification axis
    /// distinct from IsSuccess/IsFailure (transient-vs-terminal, not success-vs-failure): the sanctioned
    /// way to drive retry/poll-loop control flow without re-deriving the check ad hoc at every call site.
    /// </summary>
    public bool IsTransientFailure => IsFailure && Errors.All(e => e.IsTransient);

    /// <summary>Enables <c>var (isSuccess, errors) = result;</c> pattern deconstruction.</summary>
    /// <param name="isSuccess"><see cref="IsSuccess"/>.</param>
    /// <param name="errors"><see cref="Errors"/>.</param>
    public void Deconstruct(out bool isSuccess, out IReadOnlyList<AxisError> errors)
    {
        isSuccess = IsSuccess;
        errors = Errors;
    }

    internal virtual string DebuggerDisplay =>
        IsSuccess ? "Ok" : $"Error[{_errors!.Count}]: {JoinErrorCodes()}";

    /// <summary>Lifts a single <see cref="AxisError"/> into a failed <see cref="AxisResult"/>. Equivalent to <see cref="Error(AxisError)"/>.</summary>
    public static implicit operator AxisResult(AxisError error) => Error(error);

    /// <summary>Lifts an error list into a failed <see cref="AxisResult"/>. Equivalent to <see cref="Error(IEnumerable{AxisError})"/>.</summary>
    public static implicit operator AxisResult(List<AxisError> errors) => Error(errors);

    /// <summary>Lifts an error array into a failed <see cref="AxisResult"/>. Equivalent to <see cref="Error(IEnumerable{AxisError})"/>.</summary>
    public static implicit operator AxisResult(AxisError[] errors) => Error(errors);
}
