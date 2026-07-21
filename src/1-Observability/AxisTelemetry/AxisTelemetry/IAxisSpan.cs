namespace Axis;

public interface IAxisSpan : IDisposable
{
    string TraceId { get; }
    string SpanId { get; }

    IAxisSpan SetTag(string key, object? value);
    IAxisSpan SetStatus(AxisSpanStatus status, string? description = null);
    IAxisSpan RecordException(Exception exception);
    IAxisSpan AddEvent(string name, params KeyValuePair<string, object?>[] attributes);
}
