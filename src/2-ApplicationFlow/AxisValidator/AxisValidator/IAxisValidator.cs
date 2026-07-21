using Axis;

namespace AxisValidator;

public interface IAxisValidator<in T>
{
    AxisResult Validate(T instance);
    Task<AxisResult> ValidateAsync(T instance);
}
