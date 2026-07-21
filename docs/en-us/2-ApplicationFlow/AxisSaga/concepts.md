# Concepts · stages and routes

> Before reading any code, build the model. A saga is a small state machine in data form: a list of forward stages, a list of error stages, and where to route on success or failure.

```
ReserveStock ──ok──▶ ChargeCard ──ok──▶  (Completed)
     │                    │
   error                error
     │                    │
     ▼                    ▼
CompensateOrder      RefundStock ──ok──▶ CompensateOrder
```

---

## Status machine — `AxisSagaStatus`

| Value | Meaning |
|---|---|
| `Pending` | the instance row has been inserted; the engine has not started yet |
| `Running` | the engine is executing forward stages |
| `Completed` | the last forward stage finished with `FinishOnSuccess()` |
| `Failed` | a forward stage failed and there were no error stages routed |
| `Compensating` | a forward stage failed; the engine is walking the routed error stages |
| `Compensated` | every routed error stage finished successfully |

## Stage status — `AxisSagaStageStatus`

| Value | Where it is written |
|---|---|
| `Started` | row appended to `saga_stage_logs` when the engine picks the stage up |
| `Completed` | row appended after `IAxisSagaStageHandler` returns `Ok` |
| `Failed` | row appended after `IAxisSagaStageHandler` returns `Error` |

---

## A stage's anatomy

Each stage in the definition has four configurable parts:

| Part | Purpose |
|---|---|
| `StageName` | unique within the saga |
| `IsErrorStage` | `false` for forward stages, `true` for error/compensation stages |
| `NextStageOnSuccess` | where to go when `Ok` |
| `RouteToOnError` | where to go when `Error` |

When a stage succeeds:

1. The engine writes a `Completed` row in `saga_stage_logs`.
2. If `NextStageOnSuccess` is set, the engine moves there. Otherwise the saga completes (`FinishOnSuccess`).

When a stage fails:

1. The engine writes a `Failed` row in `saga_stage_logs`.
2. If `RouteToOnError` is non-empty, the engine sets status to `Compensating` and walks the listed error stages in order. Otherwise the saga status becomes `Failed`.

---

## Forward stages vs. error stages

| Aspect | Forward (`AddStage`) | Error (`AddErrorStage`) |
|---|---|---|
| Where it appears | `ForwardStages` | `ErrorStages` |
| Default purpose | move the business state forward | undo or notify (compensation) |
| Routed to on error | yes (configurable per stage) | also possible, but typically the chain ends here |
| Counts toward `Compensated` status | no | yes |

> Error stages are still implemented by an `IAxisSagaStageHandler` — same interface, same `AxisResult<TPayload>` shape. The only difference is **where** they sit in the definition and **how** the engine treats their failures.

---

## A complete shape — `AxisSagaDefinition`

```csharp
public record AxisSagaDefinition
{
    public required string SagaName { get; init; }
    public required Type PayloadType { get; init; }
    public required IReadOnlyList<AxisSagaStageDefinition> ForwardStages { get; init; }
    public required IReadOnlyList<AxisSagaStageDefinition> ErrorStages   { get; init; }

    public AxisSagaStageDefinition FirstForwardStage => ForwardStages[0];
    public AxisSagaStageDefinition? GetStage(string stageName); // searches both lists
}
```

The configurator builds and **validates** this object: at least one forward stage; unique stage names; every `NextStageOnSuccess` and `RouteToOnError` references a stage that actually exists. Invalid definitions throw at startup, not at runtime.

---

## Real-world example — an "OrderSaga" walked through

| Step | Status | Stage | `saga_stage_logs` |
|---|---|---|---|
| 1. `StartAsync` | `Pending` | (none) | (none) |
| 2. engine picks up | `Running` | `ReserveStock` | `Started(ReserveStock)` |
| 3. handler returns `Ok` | `Running` | `ReserveStock` | `Completed(ReserveStock)` |
| 4. engine advances | `Running` | `ChargeCard` | `Started(ChargeCard)` |
| 5. handler returns `Ok` | `Completed` | (none) | `Completed(ChargeCard)` |

If step 5 had failed:

| Step | Status | Stage | `saga_stage_logs` |
|---|---|---|---|
| 5'. handler returns `Error` | `Compensating` | `RefundStock` | `Failed(ChargeCard)` then `Started(RefundStock)` |
| 6'. handler returns `Ok` | `Compensating` | `CompensateOrder` | `Completed(RefundStock)`, `Started(CompensateOrder)` |
| 7'. handler returns `Ok` | `Compensated` | (none) | `Completed(CompensateOrder)` |

---

## See also

- [Configurator · `IAxisSagaConfigurator<TPayload>`](configuration.md) — the fluent builder that produces the definition
- [Stage handlers](stage-handlers.md) — the code that runs each stage
- [Mediator · `IAxisSagaMediator`](mediator.md) — start a saga and read its state
- [Resumer · `IAxisSagaResumer`](resumer.md) — the built-in worker that re-fires stale instances
- [Postgres adapter](postgres-adapter.md) — one of the bundled storage adapters (Postgres, MySQL, …) that persists this machine
- [Database schema](database-schema.md) — what gets persisted

---

↩ [Back to AxisSaga docs](README.md)
