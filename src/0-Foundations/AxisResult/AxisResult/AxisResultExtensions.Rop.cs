namespace Axis;

/// <summary>
/// Entry point for Railway-Oriented pipelines: lifts a plain value into a successful
/// <see cref="AxisResult{TValue}"/> so the fluent chain can start from the value itself.
/// </summary>
public static class AxisResultRopExtensions
{
    /// <summary>
    /// Wraps <paramref name="value"/> in a successful <see cref="AxisResult{TValue}"/>.
    /// Equivalent to <c>AxisResult.Ok(value)</c>, but composable: <c>user.Email.Rop().Ensure(...)</c>.
    /// </summary>
    public static AxisResult<TValue> Rop<TValue>(this TValue value) => AxisResult.Ok(value);
}
