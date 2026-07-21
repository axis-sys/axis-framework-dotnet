namespace Axis;

internal sealed class AxisResultImpl(List<AxisError>? errors = null) : AxisResult(errors);

internal sealed class AxisResultImpl<TValue>(TValue? value, List<AxisError>? errors = null) : AxisResult<TValue>(value, errors);
