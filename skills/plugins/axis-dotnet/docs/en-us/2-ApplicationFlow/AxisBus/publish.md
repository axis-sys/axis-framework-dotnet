# Publish · `PublishAsync`

> Fan-out an event to **every** registered `IAxisEventHandler<TEvent>`, run them concurrently, and combine the results. A single handler's failure does **not** stop the others — every error surfaces in the same `AxisResult`.

```csharp
var result = await bus.PublishAsync(new OrderCreatedEvent(orderId));

if (result.IsFailure)
    foreach (var error in result.Errors)
        logger.LogWarning("Handler failed: {Code}", error.Code);
```

---

## When to use

Whenever the publisher does not need a value back. Fan-out is the right tool when "more than one thing might want to know about this", or when handlers carry side effects (cache invalidation, email, projection updates).

## When *not* to use

| You want to… | Use instead |
|---|---|
| ask a question and get an answer | `IAxisMediator.QueryAsync` (CQRS query) |
| run a single command with a typed response | `IAxisMediator.ExecuteAsync` (CQRS command) |
| short-circuit on the first failure | iterate handlers manually with `Then`/`Map` |

---

## Behaviour table

Reading `MemoryBusAdapter.PublishAsync` directly:

| Handlers registered | Each handler returns | Returned `AxisResult` |
|---|---|---|
| zero | n/a | `Ok()` |
| N, all succeed | `Ok()` × N | `Ok()` |
| N, K fail with `Error(errors)` | mixed | `Combine`d — only failed handlers contribute errors |
| handler **throws** | `InvalidOperationException` | the exception **escapes** `PublishAsync` |

> **Sharp edge:** a handler that throws (uncooperative failure) is **not** caught by the in-memory adapter. Wrap risky work in `AxisResult.TryAsync` inside the handler — or write an adapter that catches and converts.

---

## Topics

```csharp
await bus.PublishAsync(@event, "orders", $"tenant:{tenant}");
```

The `params string[] topics` are **hints for the adapter**. The in-memory adapter ignores them (every handler is invoked regardless). A Kafka/RabbitMQ adapter uses them as topic / routing key / partition key — the publisher need not know which.

---

## Real-world examples

### 1. Cache invalidation after a write

```csharp
public sealed record CustomerUpdatedEvent(AxisEntityId CustomerId) : IAxisEvent;

public class InvalidateCustomerCacheHandler(IAxisCache cache) : IAxisEventHandler<CustomerUpdatedEvent>
{
    public Task<AxisResult> HandleAsync(CustomerUpdatedEvent @event)
        => cache.RemoveAsync($"customer:{@event.CustomerId}");
}

// publisher
await bus.PublishAsync(new CustomerUpdatedEvent(cmd.CustomerId));
```

**Why it pays off:** the command handler stays focused on persistence; the cache layer wires itself to events. Adding a second cache (Redis L2) is one more handler — no changes to the command handler.

### 2. Multiple side effects in parallel

```csharp
// All three handlers run concurrently; their failures aggregate.
public class SendOrderEmailHandler(IAxisEmail email)         : IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
public class WarmOrderProjectionHandler(IAxisCache cache)    : IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
public class UpdateAnalyticsHandler(IAnalyticsPort analytics): IAxisEventHandler<OrderCreatedEvent> { /* ... */ }

// publisher
var result = await bus.PublishAsync(new OrderCreatedEvent(orderId));
```

**Why it pays off:** three side effects, one publish call, errors aggregated. If the email handler fails the projection still warms — and the publisher sees both outcomes in the same `AxisResult`.

### 3. Partial failure → telemetry, not retry

```csharp
return await bus.PublishAsync(new OrderShippedEvent(orderId))
    .TapErrorAsync(errors => telemetry.RecordFanOutFailure("OrderShipped", errors))
    .Recover(AxisResult.Ok());   // we already persisted; don't fail the command
```

**Why it pays off:** the publisher records the partial failure for observability, then **recovers** to keep the command successful. The shipped order is committed; ops sees the handler degradation.

---

## See also

- [The `IAxisBus` contract](iaxisbus.md) — what the port guarantees
- [Defining events and handlers](events-and-handlers.md) — how to write them
- [`AxisMemoryBus` adapter](memory-adapter.md) — the in-box fan-out implementation
- [Custom adapter](custom-adapter.md) — how a distributed adapter handles topics

---

↩ [Back to AxisBus docs](README.md)
