# Getting started · installation and usage

> Install the packages, register a storage adapter (currently Postgres, MySQL, …), define a saga, write the stage handlers, dispatch it — five-minute path from zero to a running orchestration with compensations.

---

## Installation

```
dotnet add package AxisSaga              # the contracts + the dialect-agnostic runtime
dotnet add package AxisSaga.Postgres     # the Postgres storage adapter
# or:
dotnet add package AxisSaga.MySql        # the MySQL storage adapter (same runtime, MySQL DDL/SQL)
```

`AxisSaga` carries the whole dialect-agnostic runtime (engine, mediator, resumer worker, janitor, the store schema declared once); it depends on `AxisResult`, `AxisLogger`, `AxisMediator.Contracts`. A storage adapter only adds the provider-specific SQL: `AxisSaga.Postgres` depends on `Npgsql`, `AxisSaga.MySql` on `MySqlConnector`. The saga runtime itself never publishes events — a stage handler that needs to notify the rest of the system depends on `AxisBus` directly and publishes on its own unit of work.

---

## Registering

```csharp
using Axis;
using AxisSaga.Postgres;
using System.Reflection;

builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddAxisMemoryBus()
    .AddAxisSagaPostgres(new AxisSagaSettings
    {
        ConnectionString    = builder.Configuration.GetConnectionString("Postgres")!,
        ResumerPollInterval = TimeSpan.FromSeconds(30),
        ResumeAfter         = TimeSpan.FromSeconds(60),
        ResumeBatchSize     = 100,
    })
    .AddAxisSagaHandlers(Assembly.GetExecutingAssembly());
```

| Extension | What it does |
|---|---|
| `AddAxisSagaPostgres(settings)` | wires the `Npgsql` data source and the Postgres store ports, then calls the shared `AddAxisSagaCore` — registering the dialect-agnostic runtime (`IAxisSagaMediator`, `IAxisSagaResumer`, `IAxisSagaJanitor`, the engine, the stage-handler invoker, the in-memory definition registry) and, when `ResumerEnabled` is set, the built-in resumer worker that migrates the schema and polls — see [Postgres adapter](postgres-adapter.md) |
| `AddAxisSagaHandlers(assembly)` | scans the assembly for `IAxisSagaStageHandler<>` implementations and registers each as scoped |

`AxisSaga.MySql` exposes the mirror `AddAxisSagaMySql(settings)` — same runtime, MySQL storage ports.

Whichever storage adapter you pick, the **keyless** call refuses a second one — *one storage per process by design* (all sagas across all BCs share the same `AXIS_SAGA` schema). To run **several** independent stores per process (one per subdomain, each with its own database), use the keyed overload `AddAxisSagaPostgres(serviceKey, settings)` / `AddAxisSagaMySql(serviceKey, settings)` — see [Postgres adapter · Keyed per subdomain](postgres-adapter.md#keyed-per-subdomain--several-stores-in-one-process).

---

## Defining a saga

```csharp
public record OrderPayload(AxisEntityId OrderId, decimal Amount, string CustomerEmail);

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

→ **[Configurator · `IAxisSagaConfigurator<TPayload>`](configuration.md)**

---

## Registering the definition

The configured definition has to be added to the container so the engine can resolve it — without this, `StartAsync` fails with `SAGA_DEFINITION_NOT_FOUND`. Register the compiled `AxisSagaDefinition` produced by `AxisSagaDefinitions.Define<TPayload>(name, configure)` as a singleton:

```csharp
builder.Services.AddSingleton(
    AxisSagaDefinitions.Define<OrderPayload>(OrderSagaDefinition.Name, OrderSagaDefinition.Configure));
```

Add one `AddSingleton(...)` per saga. The in-memory registry picks up every registered `AxisSagaDefinition` at startup, and the initializer upserts each into the catalogue.

---

## Writing a stage handler

```csharp
public class ReserveStockHandler(IStockPort stock) : IAxisSagaStageHandler<OrderPayload>
{
    public string SagaName  => OrderSagaDefinition.Name;
    public string StageName => "ReserveStock";

    public Task<AxisResult<OrderPayload>> ExecuteAsync(OrderPayload payload)
        => stock.ReserveAsync(payload.OrderId, payload.Amount)
            .MapAsync(_ => payload);
}
```

Both `SagaName` and `StageName` must match the definition exactly (case-sensitive). → **[Stage handlers](stage-handlers.md)**

---

## Starting the saga

```csharp
public class OrdersController(IAxisSagaMediator sagaMediator) : ControllerBase
{
    [HttpPost]
    public Task<AxisResult<string>> CreateAsync(CreateOrderRequest req)
        => sagaMediator.StartAsync(
            sagaId:   $"order-{Guid.CreateVersion7()}",
            sagaName: OrderSagaDefinition.Name,
            payload:  new OrderPayload(req.OrderId, req.Amount, req.CustomerEmail));
}
```

`StartAsync` inserts the instance row, then **fires the engine in the background** — the controller returns immediately with the `sagaId`. The engine drives the saga from `ReserveStock` to `Completed` (or the compensation chain) asynchronously.

→ **[Mediator · `IAxisSagaMediator`](mediator.md)**

---

## What you get for free

- Every stage start / completion / failure is logged in `axis_saga.saga_stage_logs`.
- Success → advance to the next stage (or finish). Error → **route** to the configured compensation stages.
- A built-in resumer worker is hosted automatically by the storage adapter (auto-registered by `AddAxisSagaPostgres`/`AddAxisSagaMySql` while `AxisSagaSettings.ResumerEnabled` is `true`) — you no longer hand-roll a `BackgroundService`. If the process restarts (or a run hangs) mid-saga, its execution **lease** (`CLAIMED_UNTIL`) expires; the worker reclaims the stale instance and re-fires the engine, which re-acquires the lease and so respects the global concurrency cap.

→ **[Resumer · `IAxisSagaResumer`](resumer.md)**

---

## See also

- [Concepts · stages and routes](concepts.md) — the moving parts
- [Configurator · `IAxisSagaConfigurator<TPayload>`](configuration.md) — the fluent builder
- [Stage handlers](stage-handlers.md) — `IAxisSagaStageHandler<TPayload>`
- [Mediator · `IAxisSagaMediator`](mediator.md) — start, read, resume
- [Resumer · `IAxisSagaResumer`](resumer.md) — recover after a restart
- [Postgres adapter](postgres-adapter.md) — engine + storage + bootstrap
- [Database schema](database-schema.md) — `AXIS_SAGA` tables
- [Why AxisSaga?](why-axissaga.md) — the case for the abstraction
- [API reference](api-reference.md) — every member in one place

---

↩ [Back to AxisSaga docs](README.md)
