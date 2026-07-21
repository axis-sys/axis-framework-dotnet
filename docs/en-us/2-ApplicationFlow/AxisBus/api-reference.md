# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## The contract — `IAxisBus`

| Method | Signature | Description |
|---|---|---|
| `PublishAsync<TEvent>` | `Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics) where TEvent : IAxisEvent` | fan out to every registered handler, aggregate their failures |

→ [The `IAxisBus` contract](iaxisbus.md) · [Publish · `PublishAsync`](publish.md)

---

## Event contracts (from `AxisMediator.Contracts.CQRS.Events`)

| Type | Shape | Description |
|---|---|---|
| `IAxisEvent` | `string? OrderingKey => null` (default-implemented) | identifies the payload as a bus event; `OrderingKey` opts it into the durable outbox's FIFO partition key |
| `IAxisEventHandler<TEvent>` | `Task<AxisResult> HandleAsync(TEvent @event)` | one handler per `TEvent` (zero or more — they all run) |

→ [Defining events and handlers](events-and-handlers.md)

---

## In-process adapter — `AxisMemoryBus`

| Member | Description |
|---|---|
| `MemoryBusAdapter(IServiceProvider)` | constructor; resolves handlers per publish |
| `services.AddAxisMemoryBus()` | DI extension; scans the calling assembly for handlers, registers `IAxisBus → MemoryBusAdapter` (scoped) |

→ [`AxisMemoryBus` adapter](memory-adapter.md)

---

## Behaviour contract (for adapters)

| Scenario | Returned `AxisResult` |
|---|---|
| zero handlers registered | `Ok()` |
| every handler returns `Ok()` | `Ok()` |
| K of N handlers return errors | `Combine`d — K error groups, no Ok contribution |
| a handler throws | (in-box adapter) exception escapes; a custom adapter may catch |
| cancellation | `Error(...)` if the adapter honours it; in-box adapter does not currently propagate |

→ [Custom adapter](custom-adapter.md)

---

## Durable outbox adapter — `AxisBus.Repository` (`AxisBus.Postgres` / `AxisBus.MySql`)

A production-ready `IAxisBus` adapter ships in the box: publishing enqueues on a request-scoped queue and the unit of work drains it into its own transaction at commit, so the event and the business state land atomically; a separate background dispatcher delivers it after the commit. There is no status column — a row's presence is its pending state, and delivery is its deletion (claim-by-lease, at-least-once).

| Service | Lifetime | Description |
|---|---|---|
| `AxisBusRepositorySettings` | singleton | connection string, poll interval, lease duration, batch size, dispatcher/migration opt-outs |
| `IAxisBus` | scoped | `RepositoryBusAdapter` — enqueues on the request-scoped queue, never touches the database directly |
| `IOutboxScopedQueue` | scoped | the request-scoped queue drained at commit |
| `IAxisRepositoryOutbox` | scoped | `RepositoryOutboxDrain` — the bridge the unit of work invokes at commit |
| `IBusDispatcher` | scoped | `BusDispatcher` — claims due partition heads, fans out to handlers, deletes/releases |
| `IBusEventDispatchStore` | scoped | `PostgresBusDispatchStore` / `MySqlBusDispatchStore` — claim/delete/release against the outbox table |
| `IAxisBusStorageInitializer` | singleton | schema bootstrap for `AXIS_OUTBOX.OUTBOX_EVENTS` (dialect-specific) |
| `AxisBusStorageInitializerWorker` | hosted service | runs the schema migration on startup (opt out via `RunStartupMigration`) |
| `AxisBusDispatcherWorker` | hosted service | the poll loop that drives `BusDispatcher` (opt out via `DispatcherEnabled`) |

Register with `services.AddAxisBusPostgres(settings)` or `services.AddAxisBusMySql(settings)` — one storage adapter per process (a second call throws).

→ [Custom adapter](custom-adapter.md)

---

## See also

- [Getting started](getting-started.md) — install, register, publish
- [Why AxisBus?](why-axisbus.md) — the case for the one-method port
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisBus docs](README.md)
