# Configurator · `IAxisSagaConfigurator<TPayload>`

> A fluent builder that produces an `AxisSagaDefinition`. Two methods to declare stages (`AddStage`, `AddErrorStage`); a small chain to wire success routing and compensation on each.

```csharp
public interface IAxisSagaConfigurator<out TPayload> where TPayload : class
{
    IAxisSagaStageBuilder<TPayload> AddStage(string stageName);
    IAxisSagaStageBuilder<TPayload> AddErrorStage(string stageName);
}

public interface IAxisSagaStageBuilder<out TPayload> where TPayload : class
{
    IAxisSagaStageBuilder<TPayload> NextStageOnSuccess(string nextStageName);
    IAxisSagaStageBuilder<TPayload> FinishOnSuccess();

    IAxisSagaStageBuilder<TPayload> RouteToOnError(params string[] errorStageNames);
}
```

---

## When to use

Define every saga in a small static method whose only job is to call into this configurator. The result — a compiled `AxisSagaDefinition` — is loaded by the registry at startup and persisted in `axis_saga.saga_definitions` by the initializer.

## When *not* to use

| You want to… | Use instead |
|---|---|
| run **conditional** branching inside a stage | the stage handler — it returns `AxisResult<TPayload>`, the `Error` path triggers the routes |
| run a single-stage workflow | a [`AxisMediator` command](../AxisMediator/README.md) — sagas pay for what they are |
| react to events from outside the saga | a regular `IAxisEventHandler` — sagas drive themselves forward |

---

## The fluent API in depth

### `AddStage(name)` / `AddErrorStage(name)`

| Method | Adds to | Notes |
|---|---|---|
| `AddStage(name)` | `ForwardStages` | the first forward stage is the saga's entry point |
| `AddErrorStage(name)` | `ErrorStages` | only reachable via `RouteToOnError(...)` on another stage |

Names must be **non-empty** and **unique** across the whole definition. The configurator throws at build-time if either rule is broken.

To publish domain events, do it from the stage handler itself — inside its `ExecuteAsync`, on the same unit of work that persists the state change (see [Stage handlers](stage-handlers.md)). The configurator only wires flow: routing and compensation.

### `NextStageOnSuccess(nextStageName)` vs. `FinishOnSuccess()`

Mutually exclusive — call one or the other.

| Call | Effect |
|---|---|
| `NextStageOnSuccess(name)` | the engine sets `CurrentStage = name` after success and runs that handler |
| `FinishOnSuccess()` | the engine sets `Status = Completed` (or `Compensated` if the stage was an error stage) |

### `RouteToOnError(params errorStageNames)`

Lists the error stages to walk in order when the current stage fails. If the list is empty, the saga ends in `Failed` and stops there.

```csharp
saga.AddStage("ChargeCard")
    .RouteToOnError("RefundStock", "CompensateOrder");
// on failure: run RefundStock → CompensateOrder
```

---

## What the configurator validates

Reading `AxisSagaConfigurator<TPayload>.Build()` directly:

| Check | Throws when |
|---|---|
| `ForwardStages` is non-empty | no `AddStage(...)` was called |
| `StageName` is non-empty | any `AddStage("")` or `AddErrorStage("")` |
| Names are unique across forward + error stages | duplicate `StageName` (case-sensitive `Ordinal`) |
| Every `NextStageOnSuccess` references a known stage | unknown name |
| Every `RouteToOnError` entry references a known stage | unknown name |

The errors come out as `InvalidOperationException` with a clear message at app startup — not at saga-execution time.

---

## Real-world example — the "OrderSaga"

```csharp
public static class OrderSagaDefinition
{
    public const string Name = "OrderSaga";

    public static void Configure(IAxisSagaConfigurator<OrderPayload> saga)
    {
        saga.AddStage("ReserveStock")
            .NextStageOnSuccess("ChargeCard")
            .RouteToOnError("CompensateOrder");

        saga.AddStage("ChargeCard")
            .FinishOnSuccess()
            .RouteToOnError("RefundStock", "CompensateOrder");

        saga.AddErrorStage("RefundStock")
            .NextStageOnSuccess("CompensateOrder");

        saga.AddErrorStage("CompensateOrder")
            .FinishOnSuccess();
    }
}
```

**Why it pays off:** the entire orchestration reads as a checklist. Adding a third forward stage is one method call; changing the compensation chain is one line in `RouteToOnError`. The handlers don't know about the routing — they just return `Ok` or `Error`.

---

## See also

- [Concepts · stages and routes](concepts.md) — the moving parts
- [Stage handlers](stage-handlers.md) — what runs at each stage
- [Mediator · `IAxisSagaMediator`](mediator.md) — start the saga once configured
- [Postgres adapter](postgres-adapter.md) — how a bundled storage adapter (Postgres, MySQL, …) wires the (dialect-agnostic core) engine that consumes the definition

---

↩ [Back to AxisSaga docs](README.md)
