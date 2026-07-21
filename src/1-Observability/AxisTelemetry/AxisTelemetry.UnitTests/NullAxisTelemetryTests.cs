namespace AxisTelemetry.UnitTests;

public class NullAxisTelemetryTests
{
    // ── NullAxisSpan ────────────────────────────────────────────────────────

    [Fact]
    public void NullAxisSpan_TraceId_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, NullAxisSpan.Instance.TraceId);
    }

    [Fact]
    public void NullAxisSpan_SpanId_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, NullAxisSpan.Instance.SpanId);
    }

    [Fact]
    public void NullAxisSpan_SetTag_ReturnsSelf()
    {
        var span = NullAxisSpan.Instance;

        var result = span.SetTag("key", "value");

        Assert.Same(span, result);
    }

    [Fact]
    public void NullAxisSpan_SetStatus_ReturnsSelf()
    {
        var span = NullAxisSpan.Instance;

        var result = span.SetStatus(AxisSpanStatus.Ok);

        Assert.Same(span, result);
    }

    [Fact]
    public void NullAxisSpan_SetStatus_WithDescription_ReturnsSelf()
    {
        var span = NullAxisSpan.Instance;

        var result = span.SetStatus(AxisSpanStatus.Error, "something went wrong");

        Assert.Same(span, result);
    }

    [Fact]
    public void NullAxisSpan_RecordException_ReturnsSelf()
    {
        var span = NullAxisSpan.Instance;

        var result = span.RecordException(new InvalidOperationException("test"));

        Assert.Same(span, result);
    }

    [Fact]
    public void NullAxisSpan_AddEvent_ReturnsSelf()
    {
        var span = NullAxisSpan.Instance;

        var result = span.AddEvent("event-name", new KeyValuePair<string, object?>("attr", 42));

        Assert.Same(span, result);
    }

    [Fact]
    public void NullAxisSpan_Dispose_DoesNotThrow()
    {
        var exception = Record.Exception(() => NullAxisSpan.Instance.Dispose());

        Assert.Null(exception);
    }

    // ── NullAxisTelemetry ───────────────────────────────────────────────────

    [Fact]
    public void StartSpan_ReturnsNullAxisSpanInstance()
    {
        var result = NullAxisTelemetry.Instance.StartSpan("operation");

        Assert.Same(NullAxisSpan.Instance, result);
    }

    [Fact]
    public void StartSpan_WithKind_ReturnsNullAxisSpanInstance()
    {
        var result = NullAxisTelemetry.Instance.StartSpan("operation", AxisSpanKind.Server);

        Assert.Same(NullAxisSpan.Instance, result);
    }

    [Fact]
    public void CurrentTraceId_ReturnsNull()
    {
        Assert.Null(NullAxisTelemetry.Instance.CurrentTraceId);
    }

    [Fact]
    public void CurrentSpanId_ReturnsNull()
    {
        Assert.Null(NullAxisTelemetry.Instance.CurrentSpanId);
    }

    [Fact]
    public void RecordHistogram_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            NullAxisTelemetry.Instance.RecordHistogram("metric", 1.5,
                new KeyValuePair<string, object?>("tag", "value")));

        Assert.Null(exception);
    }

    [Fact]
    public void IncrementCounter_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            NullAxisTelemetry.Instance.IncrementCounter("counter", 5,
                new KeyValuePair<string, object?>("tag", "value")));

        Assert.Null(exception);
    }

    [Fact]
    public void IncrementCounter_WithDefaultDelta_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            NullAxisTelemetry.Instance.IncrementCounter("counter"));

        Assert.Null(exception);
    }
}
