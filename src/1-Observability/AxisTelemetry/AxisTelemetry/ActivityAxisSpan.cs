using System.Diagnostics;

namespace Axis;

internal class ActivityAxisSpan(Activity? activity) : IAxisSpan
{
    public string TraceId => activity?.TraceId.ToString() ?? string.Empty;
    public string SpanId => activity?.SpanId.ToString() ?? string.Empty;

    public IAxisSpan SetTag(string key, object? value)
    {
        activity?.SetTag(key, value);
        return this;
    }

    public IAxisSpan SetStatus(AxisSpanStatus status, string? description = null)
    {
        activity?.SetStatus(MapStatus(status), description);
        return this;
    }

    public IAxisSpan RecordException(Exception exception)
    {
        activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", exception.GetType().FullName },
            { "exception.message", exception.Message },
            { "exception.stacktrace", exception.StackTrace }
        }));
        return SetStatus(AxisSpanStatus.Error, exception.Message);
    }

    public IAxisSpan AddEvent(string name, params KeyValuePair<string, object?>[] attributes)
    {
        activity?.AddEvent(new ActivityEvent(name, tags: [.. attributes]));
        return this;
    }

    public void Dispose() => activity?.Dispose();

    private static ActivityStatusCode MapStatus(AxisSpanStatus status) => status switch
    {
        AxisSpanStatus.Ok => ActivityStatusCode.Ok,
        AxisSpanStatus.Error => ActivityStatusCode.Error,
        _ => ActivityStatusCode.Unset
    };
}
