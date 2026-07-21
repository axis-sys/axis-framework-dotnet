namespace AxisMediator.Contracts.Pipelines;

/// <summary>
/// Carries state shared across pipeline behaviors for a single request execution.
/// Items set by an upstream behavior (e.g. TelemetryBehavior) can be consumed by
/// downstream behaviors (e.g. LoggingBehavior) through typed keys.
/// </summary>
public sealed class AxisPipelineContext
{
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public T? Get<T>(string key)
        => Items.TryGetValue(key, out var value) && value is T typed ? typed : default;

    public void Set<T>(string key, T value) => Items[key] = value;
}
