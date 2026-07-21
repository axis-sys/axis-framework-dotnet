using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Axis.OpenTelemetry;

public class OpenTelemetryAdapter : IAxisTelemetry, IAxisMetrics
{
    public const string SourceName = "Axis.AxisMediator";

    internal static readonly ActivitySource ActivitySource = new(SourceName);
    internal static readonly Meter Meter = new(SourceName);

    private readonly ConcurrentDictionary<string, Counter<long>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();

    public IAxisSpan StartSpan(string operationName, AxisSpanKind kind = AxisSpanKind.Internal)
    {
        var activity = ActivitySource.StartActivity(operationName, MapKind(kind));
        return new ActivityAxisSpan(activity);
    }

    public string? CurrentTraceId => Activity.Current?.TraceId.ToString();
    public string? CurrentSpanId => Activity.Current?.SpanId.ToString();

    public void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags)
    {
        var histogram = _histograms.GetOrAdd(name, static n => Meter.CreateHistogram<double>(n));
        histogram.Record(value, tags);
    }

    public void IncrementCounter(string name, long delta = 1, params KeyValuePair<string, object?>[] tags)
    {
        var counter = _counters.GetOrAdd(name, static n => Meter.CreateCounter<long>(n));
        counter.Add(delta, tags);
    }

    private static ActivityKind MapKind(AxisSpanKind kind) => kind switch
    {
        AxisSpanKind.Server => ActivityKind.Server,
        AxisSpanKind.Client => ActivityKind.Client,
        AxisSpanKind.Producer => ActivityKind.Producer,
        AxisSpanKind.Consumer => ActivityKind.Consumer,
        _ => ActivityKind.Internal
    };
}
