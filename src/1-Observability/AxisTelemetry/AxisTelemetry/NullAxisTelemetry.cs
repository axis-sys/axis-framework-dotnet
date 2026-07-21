namespace Axis;

public sealed class NullAxisTelemetry : IAxisTelemetry, IAxisMetrics
{
    public static readonly NullAxisTelemetry Instance = new();

    public IAxisSpan StartSpan(string operationName, AxisSpanKind kind = AxisSpanKind.Internal)
        => NullAxisSpan.Instance;

    public string? CurrentTraceId => null;
    public string? CurrentSpanId => null;

    public void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags) { }
    public void IncrementCounter(string name, long delta = 1, params KeyValuePair<string, object?>[] tags) { }
}

public sealed class NullAxisSpan : IAxisSpan
{
    public static readonly NullAxisSpan Instance = new();

    public string TraceId => string.Empty;
    public string SpanId => string.Empty;

    public IAxisSpan SetTag(string key, object? value) => this;
    public IAxisSpan SetStatus(AxisSpanStatus status, string? description = null) => this;
    public IAxisSpan RecordException(Exception exception) => this;
    public IAxisSpan AddEvent(string name, params KeyValuePair<string, object?>[] attributes) => this;

    public void Dispose() { }
}
