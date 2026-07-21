# Pipeline context · `AxisPipelineContext`

> A per-call dictionary shared between every behaviour in the pipeline. Use it to pass a value computed by an upstream behaviour to a downstream one — without inventing new injection points.

```csharp
public sealed class AxisPipelineContext
{
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public T? Get<T>(string key)
        => Items.TryGetValue(key, out var value) && value is T typed ? typed : default;

    public void Set<T>(string key, T value) => Items[key] = value;
}
```

---

## When to use

When a behaviour produces a value the next behaviour wants — e.g. `TelemetryBehavior` opens an `IAxisSpan` and a downstream behaviour wants to set extra tags. Stick the span in the context with a typed key; the downstream reads it back without re-creating the span.

## When *not* to use

| You want to… | Use instead |
|---|---|
| pass values to the **handler** | the request itself or the mediator's ambient context |
| share values across **multiple requests** | a singleton service |
| store cross-cutting state (current user, tenant) | [`IAxisMediator`](mediator-and-accessors.md) — the ambient context, not this |

---

## The well-known keys — `AxisPipelineContextKeys`

```csharp
public static class AxisPipelineContextKeys
{
    public const string Span = "axis.pipeline.span";   // the IAxisSpan opened by TelemetryBehavior
}
```

Add to this class when you publish a key from an in-box behaviour. For your own behaviours, define a `const string` in your own type — and keep it under your namespace (`"my-app.audit.actor"`).

---

## Real-world examples

### 1. Reading the span set by `TelemetryBehavior`

```csharp
public class AddIdentityTagBehavior<TRequest>(IAxisMediator mediator)
    : IAxisPipelineBehavior<TRequest> where TRequest : IAxisRequest
{
    public async Task<AxisResult> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var span = context.Get<IAxisSpan>(AxisPipelineContextKeys.Span);
        span?.SetTag(TelemetryTagNames.AxisIdentity, mediator.AxisEntityId);

        return await next();
    }
}
```

**Why it pays off:** the identity tag rides the existing span. No second span, no risk of orphaned tags, no `IAxisSpan` injection in this behaviour.

### 2. Passing an `IDisposable` from one behaviour to another

```csharp
public class StartScopeBehavior<TRequest>(IServiceProvider sp) : IAxisPipelineBehavior<TRequest>
    where TRequest : IAxisRequest
{
    public async Task<AxisResult> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var scope = sp.CreateScope();
        context.Set("my-app.scope", scope);
        try { return await next(); }
        finally { scope.Dispose(); }
    }
}

public class UseScopedRepoBehavior<TRequest>(IAxisMediator mediator) : IAxisPipelineBehavior<TRequest>
    where TRequest : IAxisRequest
{
    public Task<AxisResult> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var scope = context.Get<IServiceScope>("my-app.scope");
        var repo = scope?.ServiceProvider.GetRequiredService<IMyRepo>();
        // … use repo, then call next()
        return next();
    }
}
```

**Why it pays off:** the scope is opened once at the outer behaviour, used by inner behaviours, disposed safely on the way out — without any "ambient" globals.

### 3. Storing a started timer for a later log line

```csharp
public class StartTimerBehavior<TRequest> : IAxisPipelineBehavior<TRequest> where TRequest : IAxisRequest
{
    public async Task<AxisResult> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var sw = Stopwatch.StartNew();
        context.Set("my-app.sw", sw);

        var result = await next();
        return result;
    }
}

public class TrailingLogBehavior<TRequest>(IAxisLogger<TRequest> logger) : IAxisPipelineBehavior<TRequest>
    where TRequest : IAxisRequest
{
    public async Task<AxisResult> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var result = await next();
        var sw = context.Get<Stopwatch>("my-app.sw");
        logger.LogInformation("Took {Ms}ms", ("Ms", sw?.ElapsedMilliseconds ?? 0));
        return result;
    }
}
```

**Why it pays off:** the timer starts in the outermost behaviour and the trailing log uses the elapsed value — even though the two behaviours don't know each other.

---

## See also

- [Pipeline behaviours](pipeline-behaviors.md) — how a behaviour reads and writes the context
- [Dispatching · `IAxisMediatorHandler`](dispatching.md) — when the context is created (once per dispatch)
- [Telemetry behaviour](../../1-Observability/AxisTelemetry/telemetry-behavior.md) — the producer of the `Span` key

---

↩ [Back to AxisMediator docs](README.md)
