# Contract · `IAxisBus`

> A single-method publishing port. Routing details (topics, exchanges, partition keys) are concerns of the adapter, not of the contract. Callers think in terms of *events* and *handlers*, not transports.

```csharp
public interface IAxisBus
{
    Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
        where TEvent : IAxisEvent;
}
```

---

## When to use

Anywhere your code performs an action that other parts of the system **may want to react to** without coupling: order placed, customer updated, invoice paid. Publish first, react in handlers; the publisher does not know who listens.

## When *not* to use

| You want to… | Use instead |
|---|---|
| send a **command** (single recipient, expects a response) | `AxisMediator` |
| trigger a single in-process side effect tied to the publisher | a direct method call — events are over-engineering |
| consume external messages | the adapter (the bus is for *publishing*, not subscribing in code) |

---

## What the contract guarantees

| Behaviour | Guaranteed by |
|---|---|
| Returns an `AxisResult` (never throws cooperatively) | `IAxisBus` |
| Aggregates all handler failures into a single result | the adapter (the in-box adapter uses `AxisResult.Combine`) |
| Handlers receive the same event instance | the adapter |
| Routing semantics for `params string[] topics` | the adapter — interpreted as topic / routing key / partition key |

---

## What the contract does **not** guarantee

- **Delivery durability.** The in-memory adapter has none; a distributed adapter may have at-least-once or exactly-once depending on the broker.
- **Ordering.** Handlers may run concurrently. Cross-handler ordering is not guaranteed.
- **Retries.** Failures are reported, not retried. Add a pipeline behaviour or a broker-level retry policy if you need it.

---

## Real-world examples

### 1. Publish before the commit

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => orderFactory.CreateAsync(cmd)
        .ThenAsync(order => bus.PublishAsync(new OrderCreatedEvent(order.OrderId)))
        .ThenAsync(order => unitOfWork.SaveChangesAsync())
        .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId });
```

**Why it pays off:** the event is enqueued *before* the commit. With an outbox adapter (Example 3) the event row is written on the same connection as the state change; the single `SaveChangesAsync` commits both atomically, or rolls both back. There is no commit-then-publish dual-write, so the state can never commit while the event is lost.

### 2. Topic hint for a distributed adapter

```csharp
await bus.PublishAsync(
    new OrderShippedEvent(orderId),
    topics: ["orders", $"tenant:{tenant}"]);
```

**Why it pays off:** the publisher names two topics (a generic stream and a tenant-scoped one). The adapter decides how to map them — the application stays vendor-neutral.

### 3. Wiring an outbox

> Illustrative — the framework already ships a production-ready durable outbox adapter, `AxisBus.Repository`, with `AxisBus.Postgres` / `AxisBus.MySql` storage adapters registered via `AddAxisBusPostgres` / `AddAxisBusMySql` (see [API reference](api-reference.md) and [Custom adapter](custom-adapter.md)). Reach for the sketch below only if you need an outbox shape the bundled adapter does not cover.

```csharp
public class OutboxBusAdapter(IOutboxStore outbox) : IAxisBus
{
    public Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
        where TEvent : IAxisEvent
        => outbox.EnqueueAsync(@event, topics);
}
```

**Why it pays off:** the same `IAxisBus` becomes a transactional outbox — publishers do not change a line. A background worker drains the outbox into the real broker, with at-least-once delivery and no dual-write race.

---

## See also

- [Publish · `PublishAsync`](publish.md) — semantics in depth
- [Defining events and handlers](events-and-handlers.md) — modelling the surface
- [`AxisMemoryBus` adapter](memory-adapter.md) — the in-box implementation
- [Custom adapter](custom-adapter.md) — write one for your broker

---

↩ [Back to AxisBus docs](README.md)
