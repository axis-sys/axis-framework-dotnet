# AxisSaga — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/2-ApplicationFlow/AxisSaga/README.md)

**Orchestration of long-running, multi-step processes with compensations** — declarative saga definitions (forward stages, error stages, success routing, route-to compensation), per-stage handlers returning `AxisResult<TPayload>`, a bundled storage adapter (Postgres, MySQL, …) over a shared core that persists every instance, every stage log and the definition catalogue, and a built-in poll-based resumer that recovers stuck sagas after a process restart.

```csharp
public static class OrderSagaDefinition
{
    public static void Configure(IAxisSagaConfigurator<OrderPayload> saga)
    {
        saga.AddStage("ReserveStock")
            .NextStageOnSuccess("ChargeCard")
            .RouteToOnError("CompensateOrder");

        saga.AddStage("ChargeCard")
            .FinishOnSuccess()
            .RouteToOnError("RefundStock", "CompensateOrder");

        saga.AddErrorStage("RefundStock")     /* … */;
        saga.AddErrorStage("CompensateOrder") /* … */;
    }
}
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### What a saga is

A **saga** is a long-running process composed of **stages** that run in order. Each stage runs an `IAxisSagaStageHandler<TPayload>` that returns `AxisResult<TPayload>` — success advances to the next stage; failure routes to one or more **error stages** (the classic *compensation* pattern). If a stage needs to notify the rest of the system, its own handler publishes the event on the `IAxisBus`, on the same unit of work that persists the stage's state change — the saga runtime itself does not publish anything.

### The five participating types

| Type | What it is |
|---|---|
| `IAxisSagaStageHandler<TPayload>` | a per-stage handler that runs one step and returns `AxisResult<TPayload>` |
| `IAxisSagaConfigurator<TPayload>` | the fluent builder that declares the stages and routes |
| `AxisSagaDefinition` | the immutable, validated definition that the registry exposes at runtime |
| `IAxisSagaMediator` | start a saga, fetch its state, or ask for it to resume |
| `IAxisSagaResumer` | a poll-based recovery worker that looks for stuck sagas and re-fires the engine |

→ **[Concepts · stages and routes](concepts.md)** · **[`IAxisSagaConfigurator<TPayload>` — the fluent builder](configuration.md)** · **[Writing a stage handler](stage-handlers.md)** · **[Driving a saga — `IAxisSagaMediator`](mediator.md)** · **[Resuming · `IAxisSagaResumer`](resumer.md)**

### Storage adapters — Postgres, MySQL, …

The schema is declared once, dialect-agnostically, in the core (`AxisSagaSchema`) and rendered per dialect: a single schema (`AXIS_SAGA`) with four tables — **saga_definitions**, **saga_instances**, **saga_stage_logs** and **saga_settings** — plus a `MIGRATIONS` control table created by the framework migration runner. The core supplies the `SagaEngine` that drives one instance forward (load → resolve stage → invoke handler → write log → route), the `SagaMediator` that inserts a new instance and fires the engine in the background, and the built-in resumer. The bundled storage adapters share that core — currently `AxisSaga.Postgres` (`AddAxisSagaPostgres`), `AxisSaga.MySql` (`AddAxisSagaMySql`), and more can follow — each supplying only its data source, store implementations and migration runner.

→ **[Postgres adapter](postgres-adapter.md)** · **[MySQL adapter](mysql-adapter.md)** · **[Database schema](database-schema.md)**

### Installation

```
dotnet add package AxisSaga              # contracts + configurator
dotnet add package AxisSaga.Postgres     # the Postgres storage adapter (or AxisSaga.MySql)
```

`AxisSaga` depends on `AxisResult`, `AxisLogger`, `AxisMediator.Contracts`. `AxisSaga.Postgres` adds `Npgsql`.

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Concepts · stages and routes** | understand the moving parts | [concepts.md](concepts.md) |
| **Configurator · `IAxisSagaConfigurator<TPayload>`** ⭐ | declare a saga end-to-end | [configuration.md](configuration.md) |
| **Stage handlers** | write a handler that runs one stage | [stage-handlers.md](stage-handlers.md) |
| **Mediator · `IAxisSagaMediator`** | start a saga, read its state, resume it | [mediator.md](mediator.md) |
| **Resumer · `IAxisSagaResumer`** | recover stuck instances after a restart | [resumer.md](resumer.md) |
| **Postgres adapter** | the storage + engine | [postgres-adapter.md](postgres-adapter.md) |
| **MySQL adapter** | the storage + engine | [mysql-adapter.md](mysql-adapter.md) |
| **Database schema** | what the adapter creates | [database-schema.md](database-schema.md) |
| **Why?** | the case for the abstraction | [why-axissaga.md](why-axissaga.md) |
| **Reference** | every member at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [Concepts](concepts.md) · [Why AxisSaga?](why-axissaga.md)

**Fundamentals:** [Configurator](configuration.md) · [Stage handlers](stage-handlers.md) · [Mediator](mediator.md) · [Resumer](resumer.md)

**Reference & extras:** [Postgres adapter](postgres-adapter.md) · [MySQL adapter](mysql-adapter.md) · [Database schema](database-schema.md) · [API reference](api-reference.md)

---

## Design principles

1. **Sagas are declarative.** Stages, next-on-success and route-to-on-error are *data* (`AxisSagaDefinition`), not handler-level conditionals.
2. **Compensation is first-class.** A failed forward stage routes to a sequence of error stages — there is no "throw an exception and hope for the best".
3. **Every step is logged.** Each stage's start / completion / failure is a row in `axis_saga.saga_stage_logs`. Forensics are a SQL query.
4. **The engine resumes itself.** The core ships a built-in resumer (an auto-hosted worker) that finds stuck instances and re-triggers the engine — process restart is not a saga restart.
5. **One storage per process.** `AddAxisSagaPostgres` refuses a second registration on purpose: every saga across every BC shares the `AXIS_SAGA` schema.

---

## License

Apache 2.0
