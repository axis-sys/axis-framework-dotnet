# Stage handlers · `IAxisSagaStageHandler<TPayload>`

> Each stage in a saga has a handler that runs the work and returns `AxisResult<TPayload>`. The handler does not know about routing, events or persistence — it just transforms the payload and reports success or failure.

```csharp
public interface IAxisSagaStageHandler<TPayload> where TPayload : class
{
    string SagaName { get; }
    string StageName { get; }

    Task<AxisResult<TPayload>> ExecuteAsync(TPayload payload);
}
```

---

## When to use

One handler per `(SagaName, StageName)` pair. The engine never resolves handlers itself — it goes through the `ISagaStageHandlerInvoker`, which opens its **own fresh DI scope per stage**, resolves `GetServices(typeof(IAxisSagaStageHandler<>).MakeGenericType(payloadType))` and matches by `SagaName` + `StageName`. Those two names are read off the **interface** type, not the concrete class, so a handler that implements the contract **explicitly** (`string IAxisSagaStageHandler<T>.SagaName => …`) still matches.

## When *not* to use

| You want to… | Use instead |
|---|---|
| run **synchronous** business logic with no orchestration | a [`AxisMediator` command](../AxisMediator/README.md) |
| fan out to **many recipients** | publish an event on the [`IAxisBus`](../AxisBus/README.md) from the handler, on its unit of work |
| reach **across services** without local persistence | another saga, or a process manager with messaging |

---

## Anatomy

| Member | Purpose |
|---|---|
| `SagaName` | the saga this handler belongs to — must match the configurator's `Name` |
| `StageName` | the stage this handler implements — must match an `AddStage(...)` / `AddErrorStage(...)` call |
| `ExecuteAsync(payload)` | runs the stage; returns `AxisResult<TPayload>` |

### Why both names are properties (not generics)

A handler is matched **by content**, not by type. Two stages in two sagas can use the same `TPayload` type; the invoker still picks the right handler because each one identifies itself with `SagaName` + `StageName` (read off the interface type, so explicit interface implementations match too).

> `IAxisCache` and other Axis abstractions can be injected into the handler's constructor like any normal service. Each stage runs in its **own DI scope** that the invoker creates and disposes around the call — so scoped infrastructure (e.g. a unit of work owning one connection + transaction) is fresh per stage and never leaks a faulted transaction into the next stage or into compensation.

---

## What the handler must do

1. Apply any side effects the stage needs (reserve stock, charge a card, write a row).
2. Return an `AxisResult<TPayload>` whose **payload** is what the *next* stage will see. The engine persists the returned payload as the new `PayloadJson`.

> The payload can be mutated between stages. A typical pattern is to add fields as the saga progresses (e.g. `OrderPayload.PaymentToken` is filled in by `ChargeCard`, used by `Compensate`).

---

## Returning success vs. failure

| Return | Engine reaction |
|---|---|
| `AxisResult.Ok(payload)` | persist new payload, log `Completed`, advance to `NextStageOnSuccess` (or finish) |
| `AxisError.X(...).Map(...)` (or any failure) | persist current payload, log `Failed` with `LastErrorCode`/`LastErrorMessage`, walk `RouteToOnError` |
| **throw** | the invoker catches at the boundary, logs the exception, and turns it into a failure result (`AxisError.InternalServerError("STAGE_HANDLER_THREW_…")`). The engine then treats it like any stage failure — it walks `RouteToOnError` (compensation) when the stage has error routes, or fails the saga when it does not. A thrown exception is **not** a different code path from a returned failure. |

> Prefer returning a typed failure to throwing: wrap risky infra calls with `AxisResult.TryAsync` inside the handler so a thrown `DbException` becomes an explicit `AxisError.InternalServerError(...)` with a meaningful code, instead of the generic `STAGE_HANDLER_THREW_…` the invoker synthesises.

---

## Real-world examples

### 1. Reserve stock with a port

```csharp
public class ReserveStockHandler(IStockPort stock) : IAxisSagaStageHandler<OrderPayload>
{
    public string SagaName  => OrderSagaDefinition.Name;
    public string StageName => "ReserveStock";

    public Task<AxisResult<OrderPayload>> ExecuteAsync(OrderPayload payload)
        => stock.ReserveAsync(payload.OrderId, payload.Quantity)
            .MapAsync(reservationId => payload with { ReservationId = reservationId });
}
```

**Why it pays off:** the handler reads as `stock → reserve → store the reservation id back in the payload`. If the port returns `Conflict("OUT_OF_STOCK")`, the engine routes to compensation; if the call succeeds, the next stage receives the updated payload.

### 2. Charge a card with explicit failure-to-routing mapping

```csharp
public class ChargeCardHandler(IPaymentsPort payments) : IAxisSagaStageHandler<OrderPayload>
{
    public string SagaName  => OrderSagaDefinition.Name;
    public string StageName => "ChargeCard";

    public Task<AxisResult<OrderPayload>> ExecuteAsync(OrderPayload payload)
        => payments.ChargeAsync(payload.PaymentMethod, payload.Amount)
            .MapAsync(token => payload with { PaymentToken = token })
            .MapErrorAsync(errs => errs.Select(e =>
                e.Code is "CARD_DECLINED" ? AxisError.BusinessRule("CARD_DECLINED") : e).ToArray());
}
```

**Why it pays off:** the saga only knows the routing for the listed stages — the handler normalises the upstream error code so the rest of the system can pivot on a stable name.

### 3. A pure compensation handler

```csharp
public class RefundStockHandler(IStockPort stock) : IAxisSagaStageHandler<OrderPayload>
{
    public string SagaName  => OrderSagaDefinition.Name;
    public string StageName => "RefundStock";

    public Task<AxisResult<OrderPayload>> ExecuteAsync(OrderPayload payload)
        => string.IsNullOrEmpty(payload.ReservationId)
            ? Task.FromResult(AxisResult.Ok(payload))            // nothing to refund
            : stock.ReleaseAsync(payload.ReservationId).MapAsync(_ => payload);
}
```

**Why it pays off:** error stages are idempotent by design — `payload.ReservationId` may be empty if `ReserveStock` never ran. The handler simply returns `Ok` and the engine moves to the next error stage.

---

## Registering handlers

```csharp
services.AddAxisSagaHandlers(Assembly.GetExecutingAssembly());
```

Reading `DependencyInjection.AddAxisSagaHandlers` directly: the scanner finds every non-abstract, non-generic class implementing `IAxisSagaStageHandler<>` and registers it as **scoped** against each implementing interface variant.

---

## See also

- [Concepts · stages and routes](concepts.md) — the moving parts
- [Configurator](configuration.md) — the definition the handler is matched against
- [Mediator · `IAxisSagaMediator`](mediator.md) — `StartAsync` triggers the first handler
- [Resumer · `IAxisSagaResumer`](resumer.md) — the built-in worker that re-fires (and so re-runs) a handler after a crash or a lapsed lease — why handlers must be idempotent

---

↩ [Back to AxisSaga docs](README.md)
