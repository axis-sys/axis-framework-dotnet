# Why AxisBus? · comparison

> There are other event-bus libraries for .NET. This page tells you why AxisBus is different — a direct comparison, no hand-waving.

---

## vs. `MediatR` Notifications

`MediatR`'s `INotification` does in-process fan-out, but every handler call site needs to know about `IMediator`. There is no abstraction over the **transport** — moving from in-process to Kafka means rewriting your fan-out code. AxisBus *is* the abstraction: `IAxisBus` stays the same when the adapter changes.

## vs. `MassTransit`

`MassTransit` is a full-featured distributed messaging framework — sagas, conventions, transports, retries, scheduling. If you need all of that, use it. AxisBus is **a one-method port** that you can implement *on top of* MassTransit, or replace with a tiny adapter for a single broker. The application code never sees the framework choice.

## vs. `Microsoft.Extensions.DependencyInjection` callbacks

You could call `IEnumerable<IFoo>` services and `Task.WhenAll` them yourself. That is what `MemoryBusAdapter` does. AxisBus standardises the **publish-failures-aggregated-into-`AxisResult`** pattern across every package in Axis.

## vs. a bespoke `IEventBus<T>`

The DIY abstraction. Same shape as `IAxisBus`, but you write the contract, the in-memory adapter, the tests, and you re-discover the same trade-offs alone. `IAxisBus` saves the cost — and inherits the railway story from `AxisResult`.

---

## The comparison

| Feature | AxisBus | MediatR notifications | MassTransit | Custom `IEventBus` |
|---|:--:|:--:|:--:|:--:|
| One-method publish port | **Yes** | No (`Mediator`) | No | Yes |
| Returns `AxisResult` | **Yes** | No | No | Maybe |
| Aggregates handler failures | **Yes** | No | Per-policy | Maybe |
| Vendor-neutral application code | **Yes** | No | No | Yes |
| In-process adapter included | **Yes** | n/a | Yes | No |
| Outbox/Kafka/RabbitMQ swap without app changes | **Yes** | No | Yes | Yes |
| Tiny surface, no learning curve | **Yes** | Yes | No | Yes |
| Zero NuGet deps in the abstraction | **Yes** | Yes | No | Yes |

---

## See also

- [The `IAxisBus` contract](iaxisbus.md) — the surface
- [Publish · `PublishAsync`](publish.md) — semantics
- [Custom adapter](custom-adapter.md) — how to bolt on a broker

---

↩ [Back to AxisBus docs](README.md)
