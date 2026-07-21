# Mediator · `IAxisSagaMediator`

> The application-facing surface for sagas. Start an instance (with a generated or caller-supplied id, optionally with a retention window), fetch its state (typed or untyped), or ask for it to resume.

```csharp
public interface IAxisSagaMediator
{
    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload)
        where TPayload : class;

    // Optional overloads: supply your own correlation id and/or a retention window.
    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, string sagaId)
        where TPayload : class;
    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, TimeSpan? retainedFor)
        where TPayload : class;
    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, string sagaId, TimeSpan? retainedFor)
        where TPayload : class;

    Task<AxisResult<AxisSagaInstance>>             GetByIdAsync(string sagaId);
    Task<AxisResult<AxisSagaInstance<TPayload>>>   GetByIdAsync<TPayload>(string sagaId)
        where TPayload : class;

    Task<AxisResult> ResumeAsync(string sagaId);
}
```

The id is **generated** (UUID v7) unless you pass `sagaId` — supply your own when you want to reuse it as a correlation key in your own domain (a batch/run id). `retainedFor` sets a retention window: once the saga reaches a terminal status the built-in janitor may delete the row after that window elapses.

---

## When to use

The mediator is what you inject into command handlers and controllers when you want to **start** or **inspect** a saga. The engine itself is invoked from inside `StartAsync` (and from `ResumeAsync` / the resumer) — application code never talks to `SagaEngine` directly.

## When *not* to use

| You want to… | Use instead |
|---|---|
| dispatch a regular command / query | [`IAxisMediator.Cqrs`](../AxisMediator/README.md) — the saga mediator only knows about sagas |
| publish a saga-related event from outside the saga | [`IAxisBus`](../AxisBus/README.md) |
| drive the engine forward yourself | call `ResumeAsync(sagaId)` — never call the engine directly |

---

## `StartAsync<TPayload>(sagaName, payload[, sagaId][, retainedFor])`

Reading the dialect-agnostic `SagaMediator.StartAsync` directly (the core implementation, shared by every storage adapter):

| Validation | Returned |
|---|---|
| `sagaName` empty | `AxisError.ValidationRule("SAGA_NAME_REQUIRED")` |
| `sagaId` empty | `AxisError.ValidationRule("SAGA_ID_REQUIRED")` |
| `sagaName` not in the registry | `AxisError.NotFound(AxisSagaErrors.SagaDefinitionNotFound)` |
| `JsonSerializer.Serialize` throws | `AxisError.InternalServerError(AxisSagaErrors.PayloadSerializationFailed)` |
| `InsertAsync` succeeds | `AxisResult.Ok(sagaId)`, then **fires the engine in the background** |
| `InsertAsync` hits a duplicate id | `AxisError.Conflict("SAGA_ID_ALREADY_EXISTS")` |
| any other `InsertAsync` failure | `AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed)` |

> `sagaName` is validated **before** `sagaId`. The conflict (`SAGA_ID_ALREADY_EXISTS`) and persistence (`PersistenceFailed`) outcomes are produced by the dialect store's `ISagaInstanceStore.InsertAsync` — the mediator just surfaces them.

The instance is persisted as `Status = Pending`, `Version = 1`, `CurrentStage = null`. The engine picks it up in the background, advances to the first forward stage, and runs from there.

> **Sharp edge:** the caller gets `Ok(sagaId)` *as soon as the row is in the database*. The engine runs **asynchronously** via `Task.Run`. If you need to wait until the saga finishes, poll `GetByIdAsync` for `Status = Completed` / `Failed` / `Compensated`.

## `GetByIdAsync(sagaId)` and `GetByIdAsync<TPayload>(sagaId)`

| Overload | Returns | Use when |
|---|---|---|
| untyped | `AxisResult<AxisSagaInstance>` (with `PayloadJson` as raw string) | you do not have `TPayload` handy (logging, admin endpoints) |
| typed | `AxisResult<AxisSagaInstance<TPayload>>` (with `Payload` deserialised) | you have the payload type and want to read its fields |

| Failure | Reason |
|---|---|
| `AxisError.NotFound(SagaInstanceNotFound)` | no row with this id |
| `AxisError.InternalServerError(PayloadDeserializationFailed)` | the stored JSON cannot be deserialised into `TPayload` |
| `AxisError.InternalServerError(PersistenceFailed)` | unexpected DB exception |

`AxisSagaInstance` carries the full state: `Status`, `CurrentStage`, `PayloadJson`, `LastErrorCode`/`LastErrorMessage`, `Version`, `CreatedAt`, `UpdatedAt`.

## `ResumeAsync(sagaId)`

A fire-and-forget signal that says "please drive this saga forward". The mediator schedules the engine to run (`Task.Run` → a fresh DI scope → `SagaEngine.ExecuteAsync`) and the call returns `Ok` immediately — it does not wait for the engine. A re-fire is harmless for a saga that is already `Completed` / `Compensated` / `Failed`: the engine re-acquires the execution lease via `AcquireLeaseAsync`, which excludes terminal sagas, so the run simply finds nothing to claim and stops.

> This is what [`IAxisSagaResumer`](resumer.md) calls under the hood for each instance whose lease has expired.

---

## Real-world examples

### 1. Start a saga from a controller

```csharp
public class OrdersController(IAxisSagaMediator sagas) : ControllerBase
{
    [HttpPost]
    public Task<AxisResult<string>> CreateAsync(CreateOrderRequest req)
        => sagas.StartAsync(
            sagaName: OrderSagaDefinition.Name,
            payload:  new OrderPayload(req.OrderId, req.Amount, req.CustomerEmail),
            sagaId:   $"order-{Guid.CreateVersion7()}");
}
```

**Why it pays off:** the controller returns immediately with the `sagaId`. The orchestration runs in the background — clients poll `GET /orders/{sagaId}/status` or react to the bus events.

### 2. Read the current state from an admin endpoint

```csharp
public class SagaAdminController(IAxisSagaMediator sagas) : ControllerBase
{
    [HttpGet("{sagaId}")]
    public async Task<IResult> GetAsync(string sagaId)
    {
        var result = await sagas.GetByIdAsync(sagaId);
        return result.Match(
            onSuccess: i      => Results.Ok(new { i.Status, i.CurrentStage, i.LastErrorCode, i.Version }),
            onFailure: errors => Results.NotFound());
    }
}
```

**Why it pays off:** admin and support staff can inspect any saga's status without reading the database directly. The typed overload (`GetByIdAsync<TPayload>`) makes the payload available for richer dashboards.

### 3. Force a resume after fixing a downstream issue

```csharp
public Task<AxisResult> RetryAsync(string sagaId)
    => sagas.ResumeAsync(sagaId);
```

**Why it pays off:** when a transient external failure is fixed (a queue draining, a vendor coming back online), an operator can re-trigger the engine without touching the database.

---

## See also

- [Configurator](configuration.md) — declare the saga before starting it
- [Stage handlers](stage-handlers.md) — what runs at each step
- [Resumer · `IAxisSagaResumer`](resumer.md) — automatic resumption
- Storage adapters (Postgres, MySQL, …) — what `StartAsync` writes and what the engine does; see the [Postgres adapter](postgres-adapter.md) for a concrete walk-through

---

↩ [Back to AxisSaga docs](README.md)
