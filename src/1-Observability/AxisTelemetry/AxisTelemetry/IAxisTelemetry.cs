namespace Axis;

public interface IAxisTelemetry
{
    IAxisSpan StartSpan(string operationName, AxisSpanKind kind = AxisSpanKind.Internal);

    string? CurrentTraceId { get; }
    string? CurrentSpanId { get; }
}
