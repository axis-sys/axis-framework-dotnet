namespace Axis;

public interface IAxisEmailService
{
    Task<AxisResult> SendAsync(AxisEmailData data);
}
