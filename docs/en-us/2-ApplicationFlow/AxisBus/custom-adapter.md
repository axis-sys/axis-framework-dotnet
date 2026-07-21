# Custom adapter · write your own `IAxisBus`

> Swap the in-process adapter for Kafka, RabbitMQ, Azure Service Bus, AWS SNS — or a transactional outbox. Implement one method, register your class for `IAxisBus`.

```csharp
public class KafkaBusAdapter(IProducer<string, byte[]> producer) : IAxisBus
{
    public async Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
        where TEvent : IAxisEvent
    {
        if (topics.Length == 0) topics = [DefaultTopic(typeof(TEvent))];

        var payload = JsonSerializer.SerializeToUtf8Bytes(@event);
        var deliveryResults = await Task.WhenAll(
            topics.Select(t => producer.ProduceAsync(t, new() { Value = payload })));

        return AxisResult.Ok();   // or aggregate broker errors via AxisResult.Combine
    }

    private static string DefaultTopic(Type t) => t.Name.ToLowerInvariant();
}
```

---

## When to use

- Producers and consumers live in **different processes**.
- You need **persistence**, **partitioning**, **broker-level retries**.
- You want a **transactional outbox** sitting in front of the broker.
- You want a **test double** that records every publish.

## When *not* to use

| You want to… | Use instead |
|---|---|
| run a single process | the in-box [`AxisMemoryBus`](memory-adapter.md) |
| add publish-time orchestration | a *mediator pipeline behaviour*, not a custom bus |

---

## The contract you must honour

| Behaviour | Required | Rationale |
|---|---|---|
| Return `Task<AxisResult>`, never throw cooperatively | yes | callers chain on the railway |
| `params string[] topics` interpreted as transport hints | yes | the in-box adapter ignores them; yours should give them meaning |
| Aggregate failures when fanning out (use `AxisResult.Combine`) | recommended | matches the in-box adapter and the `Combine` story everywhere in Axis |
| Honour cancellation from `IAxisMediatorAccessor.AxisMediator?.CancellationToken` | recommended | matches the rest of Axis |
| Log via `AxisLogger` (correlation / tenant enrichers) | recommended | structured logs across packages |

---

## Real-world example — transactional outbox

> **The framework already ships one of these.** `AxisBus.Repository` is a complete, production-ready durable outbox adapter — enqueue-then-drain-at-commit, a background dispatcher, claim-by-lease, the works — with `AxisBus.Postgres` and `AxisBus.MySql` storage adapters registered via `AddAxisBusPostgres` / `AddAxisBusMySql` (see [API reference](api-reference.md)). You do not need to build the adapter below yourself; it is kept here purely to illustrate how a *custom* outbox-shaped adapter would work.

A bus implementation that does not publish at all — it writes the event to an outbox table inside the current `UnitOfWork`, and a background worker drains the outbox into the real broker. The publisher does **not** change a line.

```csharp
public class OutboxBusAdapter(IOutboxStore outbox) : IAxisBus
{
    public Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
        where TEvent : IAxisEvent
        => outbox.EnqueueAsync(new OutboxEntry(
            EventType:   typeof(TEvent).FullName!,
            PayloadJson: JsonSerializer.Serialize(@event),
            Topics:      topics));
}

// composition
services.AddScoped<IAxisBus, OutboxBusAdapter>();
services.AddHostedService<OutboxDrainerWorker>();
```

**Why it pays off:** events become part of the same transaction as the persistence; the publish is atomic with the write. No double-write race, at-least-once delivery for free, and the *application* code is identical to the in-memory case.

---

## See also

- [The `IAxisBus` contract](iaxisbus.md) — what your adapter must satisfy
- [`AxisMemoryBus` adapter](memory-adapter.md) — the in-box reference
- [Publish · `PublishAsync`](publish.md) — fan-out semantics

---

↩ [Back to AxisBus docs](README.md)
