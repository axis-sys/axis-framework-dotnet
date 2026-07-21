# Why AxisSaga? · comparison

> There are other ways to orchestrate long-running workflows in .NET. This page tells you why AxisSaga is different — a direct comparison, no hand-waving.

---

## vs. MassTransit's saga / Automatonymous

`MassTransit.Saga` is the heavyweight option — state machines built on Automatonymous, plus a full distributed messaging substrate. It is excellent for complex, cross-service orchestration. AxisSaga is **declarative-first**, **in-process by default**, and **integrates with the rest of Axis** (`AxisResult`, `AxisBus`, `AxisLogger`, the `AXIS_SAGA` schema). If you don't need MassTransit's transport story, AxisSaga ships less ceremony.

## vs. NServiceBus sagas

Same trade-off. NServiceBus is a full messaging product; AxisSaga is a small saga engine. If your bus is already MassTransit / NServiceBus, you may not need AxisSaga at all. If your bus is `AxisBus`, AxisSaga slots in naturally.

## vs. Temporal / DTFx / orchestration services

Temporal and Azure DTFx are durable-execution engines: they record every step in their own runtime and replay history on resume. AxisSaga is a **state machine over a relational database** (currently Postgres and MySQL, …) — simpler, lighter, easier to inspect by hand. Use Temporal/DTFx when you genuinely need workflow versioning, signals, child-workflow fan-out, etc. AxisSaga is plenty for "a sequence of compensating steps".

## vs. a hand-rolled state machine

DIY. Same shape — forward stages, error stages, routing — but you re-derive the engine, the persistence, the resumer, the catalogue, the schema, the stage-log table. AxisSaga saves the cost.

---

## The comparison

| Feature | AxisSaga | MassTransit Saga | NServiceBus Saga | Temporal | Hand-rolled |
|---|:--:|:--:|:--:|:--:|:--:|
| Declarative stage + routing API | **Yes** | Yes (state machine) | Yes (state machine) | No (code) | Maybe |
| Returns `AxisResult` from each stage | **Yes** | No | No | No | Maybe |
| Compensation as first-class routing | **Yes** | Yes | Yes | Yes (code) | Maybe |
| Per-stage forensic log table | **Yes** (`saga_stage_logs`) | Audit via SDK | Audit via SDK | Built-in | Manual |
| Poll-based resumer for stuck instances | **Yes** | Built-in | Built-in | Built-in | Manual |
| Optimistic-concurrency rows | **Yes** | Yes | Yes | n/a | Manual |
| Bundled relational storage (Postgres, MySQL, …) | **Yes** | Configurable | Configurable | Self-hosted | Manual |
| In-process only (no broker) | **Yes** | No | No | No | Yes |
| Tiny abstraction (one configurator + one handler interface) | **Yes** | No | No | No | Yes |

---

## See also

- [Getting started](getting-started.md) — install and run
- [Concepts](concepts.md) — the moving parts
- [Configurator](configuration.md) — the declarative builder

---

↩ [Back to AxisSaga docs](README.md)
