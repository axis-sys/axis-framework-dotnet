using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AxisTelemetry.UnitTests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class ActivitySpanTests : IDisposable
{
    private readonly ActivitySource _source = new("Test.ActivitySpan");
    private readonly ActivityListener _listener;

    public ActivitySpanTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Test.ActivitySpan",
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public void SetTag_SetsTagOnActivity()
    {
        using var activity = _source.StartActivity()!;
        using var span = CreateSpan(activity);

        span.SetTag("test.key", "test_value");

        Assert.Equal("test_value", activity.GetTagItem("test.key"));
    }

    [Fact]
    public void SetStatus_Ok_MapsToActivityStatusCodeOk()
    {
        using var activity = _source.StartActivity()!;
        using var span = CreateSpan(activity);

        span.SetStatus(AxisSpanStatus.Ok);

        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public void SetStatus_Error_MapsToActivityStatusCodeError()
    {
        using var activity = _source.StartActivity()!;
        using var span = CreateSpan(activity);

        span.SetStatus(AxisSpanStatus.Error, "something failed");

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("something failed", activity.StatusDescription);
    }

    [Fact]
    public void RecordException_AddsExceptionEvent()
    {
        using var activity = _source.StartActivity()!;
        using var span = CreateSpan(activity);

        var ex = new InvalidOperationException("test error");
        span.RecordException(ex);

        Assert.Single(activity.Events, e => e.Name == "exception");
        var eventTags = activity.Events.First().Tags.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal(typeof(InvalidOperationException).FullName, eventTags["exception.type"]);
        Assert.Equal("test error", eventTags["exception.message"]);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public void AddEvent_AddsNamedEvent()
    {
        using var activity = _source.StartActivity()!;
        using var span = CreateSpan(activity);

        span.AddEvent("custom.event", new KeyValuePair<string, object?>("detail", "value"));

        Assert.Single(activity.Events, e => e.Name == "custom.event");
    }

    [Fact]
    public void TraceId_ReturnsActivityTraceId()
    {
        using var activity = _source.StartActivity()!;
        using var span = CreateSpan(activity);

        Assert.Equal(activity.TraceId.ToString(), span.TraceId);
        Assert.Equal(activity.SpanId.ToString(), span.SpanId);
    }

    [Fact]
    public void SetStatus_Unset_MapsToActivityStatusCodeUnset()
    {
        using var activity = _source.StartActivity()!;
        using var span = CreateSpan(activity);

        span.SetStatus(AxisSpanStatus.Unset);

        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    [Fact]
    public void NullActivity_DoesNotThrow()
    {
        using var span = CreateSpan(null);

        var act = () => span.SetTag("key", "value")
                .SetStatus(AxisSpanStatus.Ok)
                .RecordException(new Exception("test"))
                .AddEvent("event");

        var exception = Record.Exception(act);
        Assert.Null(exception);
        Assert.Empty(span.TraceId);
        Assert.Empty(span.SpanId);
    }

    private static ActivityAxisSpan CreateSpan(Activity? activity)
        => new(activity);

    public void Dispose()
    {
        _source.Dispose();
        _listener.Dispose();
    }
}
