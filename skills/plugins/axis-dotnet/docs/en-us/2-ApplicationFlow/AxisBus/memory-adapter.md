# In-process adapter · `AxisMemoryBus`

> The ready-made implementation of `IAxisBus` for in-process fan-out. Registers `IAxisBus` as `MemoryBusAdapter`, scans the calling assembly for handlers, and runs them in parallel on publish.

```csharp
using AxisMemoryBus;

services.AddAxisMemoryBus();   // IAxisBus → MemoryBusAdapter + handler scanning
```

---

## When to use

- A single-process app where producers and consumers live together.
- Tests where you want fan-out without a real broker.
- A staging surface before you bolt on a distributed adapter — application code stays identical.

## When *not* to use

| You want to… | Use instead |
|---|---|
| share events between processes | a distributed adapter (Kafka, RabbitMQ, Service Bus) |
| survive process restart for in-flight events | a persistent outbox + distributed adapter |
| isolate handler failures from the publisher's transaction | a distributed adapter with at-least-once delivery |

---

## What gets registered

`DependencyInjection.AddAxisMemoryBus`:

```csharp
public static IServiceCollection AddAxisMemoryBus(this IServiceCollection services)
{
    services.AddCqrsMediator(Assembly.GetExecutingAssembly());  // discover handlers in caller asm
    services.AddScoped<IAxisBus, MemoryBusAdapter>();           // the IAxisBus binding
    return services;
}
```

- `AddCqrsMediator` scans the calling assembly for `IAxisEventHandler<>` implementations and registers them.
- `IAxisBus` is registered as **scoped** — one bus per request scope, sharing the scope's `IServiceProvider`.

If your handlers live in another assembly, call `AddCqrsMediator(typeof(MyHandler).Assembly)` yourself before `AddAxisMemoryBus`.

---

## How `PublishAsync` works

Reading `MemoryBusAdapter.PublishAsync` directly:

```csharp
public async Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
    where TEvent : IAxisEvent
{
    var handlers = serviceProvider.GetServices<IAxisEventHandler<TEvent>>().ToList();
    if (handlers.Count == 0) return AxisResult.Ok();

    var tasks = handlers.Select(h => h.HandleAsync(@event));
    var results = await Task.WhenAll(tasks);

    return AxisResult.Combine(results);
}
```

- **Zero handlers** → `Ok()`.
- **Multiple handlers** → all started concurrently with `Task.WhenAll`, results combined with `AxisResult.Combine` (errors aggregate, success on `Ok` everywhere).
- **A handler throws** → the exception escapes `PublishAsync` (the in-memory adapter does *not* catch — wrap risky code in the handler with `AxisResult.TryAsync`).
- **Topics** → accepted but ignored by the in-memory adapter; useful when you target a future distributed swap.

---

## Real-world example — wiring two handlers

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()       // mediator
    .AddAxisLogger()
    .AddAxisMemoryBus();     // bus + handler scan

// Handlers — discovered automatically
public class WarmCacheHandler(IAxisCache cache) : IAxisEventHandler<OrderCreatedEvent>
{
    public Task<AxisResult> HandleAsync(OrderCreatedEvent @event)
        => cache.RemoveAsync($"customer:{@event.CustomerId}");
}

public class SendEmailHandler(IAxisEmail email) : IAxisEventHandler<OrderCreatedEvent>
{
    public Task<AxisResult> HandleAsync(OrderCreatedEvent @event)
        => email.SendAsync(new OrderConfirmation(@event.OrderId));
}

// publisher
await bus.PublishAsync(new OrderCreatedEvent(orderId, customerId));
```

**Why it pays off:** moving to a distributed broker later means swapping `AddAxisMemoryBus()` for `AddAxisKafkaBus(...)` — the handlers and the publish call stay the same. Local development and CI keep the lightweight in-process path.

---

## See also

- [The `IAxisBus` contract](iaxisbus.md) — what every adapter must guarantee
- [Publish · `PublishAsync`](publish.md) — the semantics
- [Defining events and handlers](events-and-handlers.md) — how to model
- [Custom adapter](custom-adapter.md) — write one for your broker

---

↩ [Back to AxisBus docs](README.md)
