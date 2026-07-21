# Events and handlers · `IAxisEvent`, `IAxisEventHandler<T>`

> Events are records that implement `IAxisEvent`. Handlers are classes that implement `IAxisEventHandler<TEvent>` and return an `AxisResult`. Register them in DI, publish via `IAxisBus`, and the adapter takes care of the fan-out.

```csharp
public sealed record CustomerUpdatedEvent(AxisEntityId CustomerId) : IAxisEvent;

public class InvalidateCustomerCacheHandler(IAxisCache cache) : IAxisEventHandler<CustomerUpdatedEvent>
{
    public Task<AxisResult> HandleAsync(CustomerUpdatedEvent @event)
        => cache.RemoveAsync($"customer:{@event.CustomerId}");
}
```

---

## When to use

Model an event whenever a *fact* has just become true and other code may want to react: an order was created, a payment was confirmed, a tenant was provisioned. Events are **past tense** — they record what already happened.

## When *not* to use

| You want to… | Use instead |
|---|---|
| ask for something to be done | a *command* via `AxisMediator.ExecuteAsync` |
| return data to the publisher | a *query* via `AxisMediator.QueryAsync` |
| reuse one handler across event types | publish a base event, or split into two handlers |

---

## The two interfaces

| Type | Shape | Purpose |
|---|---|---|
| `IAxisEvent` | `public interface IAxisEvent { string? OrderingKey => null; }` | identifies the payload as a bus event; `OrderingKey` (default `null`) lets it opt into the durable outbox's FIFO partition key |
| `IAxisEventHandler<TEvent>` | `Task<AxisResult> HandleAsync(TEvent @event)` | one handler per `TEvent` (or several — they all run) |

Events should be **records** (or readonly structs): immutable, value-equal, easy to log.

---

## Registration

`AddAxisMemoryBus` calls `services.AddCqrsMediator(Assembly.GetExecutingAssembly())`, which scans the **calling assembly** for `IAxisEventHandler<>` implementations and registers them.

For handlers in other assemblies:

```csharp
services.AddCqrsMediator(typeof(MyHandlerInOtherAssembly).Assembly);
services.AddScoped<IAxisEventHandler<MyEvent>, MyHandler>();   // or register one by one
```

> The in-memory adapter resolves handlers from `IServiceProvider` per publish — so handlers can be scoped, transient or singleton at your choice.

---

## Real-world examples

### 1. A single handler doing one thing

```csharp
public sealed record InvoicePaidEvent(AxisEntityId InvoiceId) : IAxisEvent;

public class ReleaseGoodsHandler(IShippingPort shipping) : IAxisEventHandler<InvoicePaidEvent>
{
    public Task<AxisResult> HandleAsync(InvoicePaidEvent @event)
        => shipping.ReleaseForInvoiceAsync(@event.InvoiceId);
}
```

**Why it pays off:** the payment side never knows about shipping, and shipping reacts to the *fact* (invoice paid) without coupling back to the payment service.

### 2. Multiple handlers per event (fan-out)

```csharp
public class WarmProjectionHandler(IAxisCache cache)    : IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
public class SendOrderEmailHandler(IAxisEmail email)    : IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
public class PublishToAnalyticsHandler(IAnalyticsPort a): IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
```

**Why it pays off:** three independent reactions to one event. Adding a fourth is a class — not a refactor.

### 3. A handler that itself fails gracefully

```csharp
public class SendOrderEmailHandler(IAxisEmail email, IAxisLogger logger) : IAxisEventHandler<OrderCreatedEvent>
{
    public Task<AxisResult> HandleAsync(OrderCreatedEvent @event)
        => email.SendAsync(new OrderConfirmationMessage(@event.OrderId))
            .TapErrorAsync(errs => logger.LogWarningAsync("EMAIL_SEND_FAILED", errs))
            .RecoverAsync(AxisResult.Ok());        // recover to Ok — we don't fail the publish on email errors
}
```

**Why it pays off:** the publish result stays `Ok` for non-critical handlers, but the error is logged and observable. The handler decides locally whether to bubble up or absorb.

---

## See also

- [The `IAxisBus` contract](iaxisbus.md) — the publishing surface
- [Publish · `PublishAsync`](publish.md) — fan-out semantics and aggregation
- [`AxisMemoryBus` adapter](memory-adapter.md) — handler registration

---

↩ [Back to AxisBus docs](README.md)
