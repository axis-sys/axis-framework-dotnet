# The mediator and the accessors · `IAxisMediator`, `IAxisMediatorAccessor`, `IAxisMediatorContextAccessor`

> Three layers. `IAxisMediator` is the ambient context every handler reads. `IAxisMediatorAccessor` is the **last** `IAxisMediator` that ran (singleton, used by adapters that don't have DI scope). `IAxisMediatorContextAccessor` is the **`AsyncLocal`-backed source of truth** for `TraceId`/`OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken`.

```csharp
public class CreateOrderHandler(IAxisMediator mediator, ...)
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    {
        // mediator carries everything you need
        logger.LogInformation("Traced as {TraceId}", mediator.TraceId);
        // …
    }
}
```

---

## When to use which

| Type | Lifetime | Use when |
|---|---|---|
| `IAxisMediator` | scoped | the **happy path** — inject this in handlers / behaviours / domain services |
| `IAxisMediatorAccessor` | singleton | adapters that need the *currently running* mediator and **cannot** depend on the scope (e.g. `MemoryCacheAdapter.CancellationToken`) |
| `IAxisMediatorContextAccessor` | singleton | the **edge** sets the ambient values (`OriginId`, `JourneyId`, etc.) at request entry |

---

## `IAxisMediator`

```csharp
public interface IAxisMediator
{
    CancellationToken CancellationToken { get; }

    string TraceId { get; }
    string? OriginId { get; }
    string? JourneyId { get; }

    AxisEntityId? AxisEntityId { get; }

    IAxisMediatorHandler Cqrs { get; }
}
```

Reading `AxisMediator` directly:

- Properties delegate to `IAxisMediatorContextAccessor`.
- `TraceId` is captured **once** on construction: if `Activity.Current` exists, its `TraceId`; otherwise a fresh `Guid.NewGuid().ToString()`.
- The constructor sets `_accessor.AxisMediator = this`; `Dispose()` clears it.

## `IAxisMediatorAccessor`

```csharp
public interface IAxisMediatorAccessor
{
    IAxisMediator? AxisMediator { get; set; }
}
```

The accessor holds the **last constructed** `IAxisMediator`. Adapters that are singletons (e.g. `MemoryCacheAdapter`) read `accessor.AxisMediator?.CancellationToken` instead of injecting `IAxisMediator` directly (which is scoped).

## `IAxisMediatorContextAccessor`

```csharp
public interface IAxisMediatorContextAccessor
{
    string? OriginId { get; set; }
    string? JourneyId { get; set; }
    AxisEntityId? AxisEntityId { get; set; }
    CancellationToken CancellationToken { get; set; }
    bool IsAuthenticated => AxisEntityId != null;
}
```

The default implementation stores each property in an `AsyncLocal<T>`, so a per-request middleware that sets `OriginId = "rest"` carries that value to every awaited continuation.

---

## Real-world examples

### 1. Setting ambient context at the HTTP edge

```csharp
app.Use(async (ctx, next) =>
{
    var contextAccessor = ctx.RequestServices.GetRequiredService<IAxisMediatorContextAccessor>();

    contextAccessor.OriginId = "rest";
    contextAccessor.CancellationToken = ctx.RequestAborted;

    if (ctx.User.Identity?.IsAuthenticated == true
        && AxisEntityId.TryParse(ctx.User.FindFirst("sub")?.Value, out var id))
    {
        contextAccessor.AxisEntityId = id;
    }

    await next();
});
```

**Why it pays off:** one middleware fills the ambient context once. Every downstream handler, validator, adapter and behaviour reads from it — no parameter threading, no `HttpContext` leaking into domain code.

### 2. Singleton adapter reading the ambient token

```csharp
public class MemoryCacheAdapter(IMemoryCache memoryCache, IAxisMediatorAccessor accessor) : IAxisCache
{
    private readonly CancellationToken _ct = accessor.AxisMediator?.CancellationToken ?? CancellationToken.None;
    // … every method calls _ct.ThrowIfCancellationRequested()
}
```

**Why it pays off:** the cache adapter is a singleton (one per app), but each call still respects the **request's** cancellation token. The accessor bridges the lifetime mismatch.

---

## See also

- [Dispatching · `IAxisMediatorHandler`](dispatching.md) — what `mediator.Cqrs` does
- [Registration & scanning](registration.md) — what `AddAxisMediator()` registers
- [CQRS · commands, queries, streams, events](cqrs.md) — the shapes the dispatcher handles
- [`AxisLogger`](../../1-Observability/AxisLogger/README.md) — uses `OriginId`/`TraceId`/`JourneyId` for log enrichment

---

↩ [Back to AxisMediator docs](README.md)
