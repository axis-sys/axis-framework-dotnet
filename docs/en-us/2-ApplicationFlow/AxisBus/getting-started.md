# Getting started · installation and usage

> Install the abstraction and the in-process adapter, register them in DI, define an event and a handler, and publish your first event in under five minutes.

---

## Installation

```
dotnet add package AxisBus           # the abstraction
dotnet add package AxisMemoryBus     # in-process adapter
```

`AxisBus` depends only on `AxisResult` (via `AxisMediator.Contracts`, for the return type). `AxisMemoryBus` wires the in-process bus and the CQRS mediator behaviour.

---

## Registering the adapter

```csharp
using AxisMemoryBus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAxisMemoryBus();      // IAxisBus → MemoryBusAdapter + handler scanning
```

`AddAxisMemoryBus()` also calls `services.AddCqrsMediator(Assembly.GetExecutingAssembly())` so that handlers in the **calling** assembly are discovered automatically.

---

## Defining an event and a handler

```csharp
using Axis;
using AxisMediator.Contracts.CQRS.Events;

public sealed record OrderCreatedEvent(AxisEntityId OrderId, AxisEntityId CustomerId) : IAxisEvent;

public class WarmCustomerCacheHandler(IAxisCache cache) : IAxisEventHandler<OrderCreatedEvent>
{
    public Task<AxisResult> HandleAsync(OrderCreatedEvent @event)
        => cache.RemoveAsync($"customer:{@event.CustomerId}");
}
```

Every handler returns an `AxisResult`. A failure becomes an aggregate entry on the publish result — see [Publish · `PublishAsync`](publish.md).

---

## Publishing

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => orderFactory.CreateAsync(cmd)
        .ThenAsync(order => bus.PublishAsync(new OrderCreatedEvent(order.OrderId, cmd.CustomerId)))
        .ThenAsync(order => unitOfWork.SaveChangesAsync())
        .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId });
```

**Why it pays off:** the publish comes *before* the commit. With an outbox adapter the event is written on the same connection as the state change, and the single `SaveChangesAsync` commits both atomically — the state and the event land together or not at all. That closes the dual-write gap a commit-then-publish sequence leaves open (state committed, then the process dies before the event ever ships). With the in-memory adapter, keep `PublishAsync` for post-commit side effects that tolerate loss — see [`PublishAsync`](publish.md).

---

## See also

- [The `IAxisBus` contract](iaxisbus.md) — the publishing port
- [Publish · `PublishAsync`](publish.md) — fan-out semantics, error aggregation, topics
- [Defining events and handlers](events-and-handlers.md) — modelling and registration
- [`AxisMemoryBus` adapter](memory-adapter.md) — the in-process implementation
- [Custom adapter](custom-adapter.md) — Kafka, RabbitMQ, Service Bus
- [Why AxisBus?](why-axisbus.md) — the case for a one-method port

---

↩ [Back to AxisBus docs](README.md)
