namespace Axis;

/// <summary>
/// A value-carrying Result monad for Railway-Oriented Programming: a computation either
/// <see cref="AxisResult.IsSuccess"/> with a <see cref="Value"/>, or <see cref="AxisResult.IsFailure"/>
/// with one or more <see cref="AxisError"/>. Never throws to signal an expected failure — compose with
/// <c>Map</c>/<c>Then</c>/<c>Ensure</c>/<c>Tap</c>/<c>Recover</c>/<c>Match</c> instead of reading
/// <see cref="Value"/> directly or branching on <see cref="AxisResult.IsSuccess"/> by hand (<see cref="Value"/>
/// throws <see cref="NoAccessValueOnErrorResultException"/> on failure). Create instances with
/// <see cref="AxisResult.Ok{TValue}(TValue)"/>, <see cref="AxisResult.Error{TValue}(AxisError)"/> or the
/// implicit conversions.
/// </summary>
public abstract partial class AxisResult<TValue>(TValue? value, List<AxisError>? errors = null) : AxisResult(errors)
{
    protected readonly TValue? _value = value;

    /// <summary>
    /// The success value. Throws <see cref="NoAccessValueOnErrorResultException"/> when
    /// <see cref="AxisResult.IsFailure"/> is true — read it only after confirming success (or, preferably,
    /// never read it directly: compose with <c>Map</c>/<c>Then</c>/<c>Match</c> instead).
    /// </summary>
    public virtual TValue Value => IsSuccess
        ? _value!
        : throw new NoAccessValueOnErrorResultException(Errors);

    /// <summary>Enables <c>var (isSuccess, value, errors) = result;</c> pattern deconstruction.</summary>
    /// <param name="isSuccess"><see cref="AxisResult.IsSuccess"/>.</param>
    /// <param name="value">The success value, or the type's default when this is a failure.</param>
    /// <param name="errors"><see cref="AxisResult.Errors"/>.</param>
    public void Deconstruct(out bool isSuccess, out TValue? value, out IReadOnlyList<AxisError> errors)
    {
        isSuccess = IsSuccess;
        value = _value;
        errors = Errors;
    }

    internal override string DebuggerDisplay =>
        IsSuccess ? $"Ok({_value})" : $"Error[{_errors!.Count}]: {JoinErrorCodes()}";

    /// <summary>Lifts a plain value into a successful <see cref="AxisResult{TValue}"/>. Equivalent to <see cref="AxisResult.Ok{TValue}(TValue)"/>.</summary>
    public static implicit operator AxisResult<TValue>(TValue value) => Ok(value);

    /// <summary>Lifts a single <see cref="AxisError"/> into a failed <see cref="AxisResult{TValue}"/>. Equivalent to <see cref="AxisResult.Error{TValue}(AxisError)"/>.</summary>
    public static implicit operator AxisResult<TValue>(AxisError error) => Error<TValue>(error);

    /// <summary>Lifts an error list into a failed <see cref="AxisResult{TValue}"/>. Equivalent to <see cref="AxisResult.Error{TValue}(IEnumerable{AxisError})"/>.</summary>
    public static implicit operator AxisResult<TValue>(List<AxisError> errors) => Error<TValue>(errors);

    /// <summary>Lifts an error array into a failed <see cref="AxisResult{TValue}"/>. Equivalent to <see cref="AxisResult.Error{TValue}(IEnumerable{AxisError})"/>.</summary>
    public static implicit operator AxisResult<TValue>(AxisError[] errors) => Error<TValue>(errors);
}
