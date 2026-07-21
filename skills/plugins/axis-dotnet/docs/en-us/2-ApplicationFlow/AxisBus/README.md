# AxisBus — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/2-ApplicationFlow/AxisBus/README.md)

**A one-method event bus port** — `IAxisBus.PublishAsync<TEvent>(@event, params topics)` returning an `AxisResult` that **aggregates every handler failure**. Drop in `AxisMemoryBus` for in-process broadcasting; swap it for a Kafka, RabbitMQ, Service Bus or transactional-outbox adapter without touching application code.

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => orderFactory.CreateAsync(cmd)
        .ThenAsync(order => bus.PublishAsync(new OrderCreatedEvent(order.OrderId)))   // enqueue in the outbox…
        .ThenAsync(order => unitOfWork.SaveChangesAsync())                            // …then one commit persists the order AND the event
        .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId });
```

> **The event never leaves the unit of work.** With an outbox adapter (see [Custom adapter](custom-adapter.md)), `PublishAsync` enqueues the event on the unit of work's connection *before* the commit — the single `SaveChangesAsync` then persists the state change and the event together, atomically, or neither. There is no commit-then-publish dual-write, so the classic race — state committed but the event lost, or the event broadcast for a state change that then rolls back — cannot happen. In-memory fan-out for tolerant side effects (cache, email) is a *separate* role → see [`PublishAsync`](publish.md).

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### The interface in 60 seconds

```csharp
public interface IAxisBus
{
    Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
        where TEvent : IAxisEvent;
}
```

One method. The bus fans the event out to every registered `IAxisEventHandler<TEvent>`, runs them **in parallel**, and aggregates the results with `AxisResult.Combine` — every handler's errors are surfaced together. Routing details (topics, partition keys, exchanges) are the **adapter's** problem, not the caller's. → **[The `IAxisBus` contract](iaxisbus.md)**

### Events and handlers

The contracts come from `AxisMediator`:

```csharp
public interface IAxisEvent
{
    string? OrderingKey => null;   // optional; the durable outbox's FIFO partition key
}

public interface IAxisEventHandler<in TEvent> where TEvent : IAxisEvent
{
    Task<AxisResult> HandleAsync(TEvent @event);
}
```

Define a record that implements `IAxisEvent`, write a handler for it, register it in DI, and the bus picks it up. → **[Defining events and handlers](events-and-handlers.md)**

### In-memory adapter

`AxisMemoryBus` registers `IAxisBus` against in-process handlers, runs them in parallel and aggregates their results:

```csharp
services.AddAxisMemoryBus();   // wires IAxisBus → MemoryBusAdapter + scans for handlers
```

→ **[`AxisMemoryBus` adapter](memory-adapter.md)**

### Durable outbox adapter (bundled)

`AxisBus.Repository` ships a production-ready transactional outbox over Postgres/MySQL: publishing enqueues on the unit of work, a commit drains it atomically with the business state, and a background dispatcher delivers it after the commit — no status column, claim-by-lease, at-least-once:

```csharp
services.AddAxisBusPostgres(new AxisBusRepositorySettings { ConnectionString = "..." });   // or AddAxisBusMySql
```

→ **[API reference](api-reference.md)** (see "Durable outbox adapter")

### Installation

```
dotnet add package AxisBus           # the abstraction (depends on AxisResult)
dotnet add package AxisMemoryBus     # in-process adapter
```

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Contract · `IAxisBus`** | the publishing port and its semantics | [iaxisbus.md](iaxisbus.md) |
| **Publish · `PublishAsync`** ⭐ | fan-out an event to every handler | [publish.md](publish.md) |
| **Events · `IAxisEvent`** | define events and write handlers | [events-and-handlers.md](events-and-handlers.md) |
| **In-process · `AxisMemoryBus`** | the ready-made adapter | [memory-adapter.md](memory-adapter.md) |
| **Durable outbox · `AxisBus.Repository`** | the bundled transactional-outbox adapter (Postgres/MySQL) | [api-reference.md](api-reference.md) |
| **Custom adapter** | write your own (Kafka, RabbitMQ, Service Bus) | [custom-adapter.md](custom-adapter.md) |
| **Why?** | the case for a one-method port | [why-axisbus.md](why-axisbus.md) |
| **Reference** | every member at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [The `IAxisBus` contract](iaxisbus.md) · [Why AxisBus?](why-axisbus.md)

**Fundamentals:** [Publish · `PublishAsync`](publish.md) · [Defining events and handlers](events-and-handlers.md) · [`AxisMemoryBus` adapter](memory-adapter.md)

**Reference & extras:** [Custom adapter](custom-adapter.md) · [API reference](api-reference.md)

---

## Design principles

1. **One method.** A publish-only port keeps the application out of the transport's vocabulary. Subscriptions live in the adapter or the broker, not in the abstraction.
2. **Errors aggregate, not crash.** Every handler's failure becomes an entry in the combined `AxisResult`. The publisher sees the *whole* picture, not the first thing that blew up.
3. **Handlers run in parallel.** The in-memory adapter awaits all handlers concurrently. Side effects must be independent.
4. **Routing belongs to the adapter.** Topics, partition keys and exchanges are vendor concepts. The port accepts a `params string[] topics` so callers can hint, but interpretation is the adapter's call.
5. **Events are dumb records.** `IAxisEvent` is a marker interface. Add the fields the consumers need; nothing more.
6. **Domain events ride the unit of work.** A domain event is staged *inside* the transaction that persists the state change — an outbox adapter writes the event row on the same connection, and the commit makes both durable atomically. Never `SaveChanges` first and `Publish` after: that dual-write can commit the state while losing the event. In-memory fan-out is reserved for post-commit side effects that tolerate loss.

---

## License

Apache 2.0
