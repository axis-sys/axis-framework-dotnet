# Postgres adapter · `AxisSaga.Postgres`

> The Postgres storage adapter over the shared saga core. One singleton `NpgsqlDataSource` against the `AXIS_SAGA` schema, plus the four Postgres store ports; the dialect-agnostic core supplies the `SagaEngine` that drives a single instance forward, the `SagaMediator` that inserts new rows and fires the engine in the background, the `SagaResumer` for recovery and the `SagaDefinitionInitializer` that persists definitions at startup. Sibling adapters like `AxisSaga.MySql` (`AddAxisSagaMySql`) — and more as they ship — share that same core and differ only in the SQL dialect.

```csharp
services.AddAxisSagaPostgres(new AxisSagaSettings
{
    ConnectionString    = "Host=…",
    ResumerPollInterval = TimeSpan.FromSeconds(30),
    ResumeAfter         = TimeSpan.FromSeconds(60),
    ResumeBatchSize     = 100,
});
```

---

## When to use

PostgreSQL — your own server, RDS, Aurora, Cloud SQL. The adapter expects a single database for **all** sagas in the process; `AddAxisSagaPostgres` refuses a second registration on purpose.

## When *not* to use

| You want to… | Use instead |
|---|---|
| target MySQL | `AddAxisSagaMySql` from `AxisSaga.MySql` — a sibling adapter over the same shared core, identical settings and runtime |
| target SQL Server / Mongo | write a new adapter against the same store ports (`ISagaInstanceStore`, `ISagaStageLogStore`, `ISagaDefinitionStore`, `IAxisSagaStorageInitializer`); the dialect-agnostic core (`AddAxisSagaCore`) supplies the rest |
| store sagas in a Kafka topic | a custom adapter — the in-box adapters assume relational durability |
| share the saga storage with the rest of the app | possible — point both at the same database; the `AXIS_SAGA` schema is isolated by name |

---

## What `AddAxisSagaPostgres(settings)` registers

Reading `DependencyInjection.AddAxisSagaPostgres` directly: it registers the Postgres data source and the four dialect-specific store ports, then calls `AddAxisSagaCore(settings)` for the dialect-agnostic runtime (the same call `AddAxisSagaMySql` makes).

| Service | Lifetime | Description |
|---|---|---|
| `AxisSagaSettings` | singleton | the configuration object (registered by the core) |
| `AxisSagaPostgresDataSource` | singleton | wraps `NpgsqlDataSource.Create(connectionString)` (Postgres-specific) |
| `IAxisSagaDefinitionRegistry` | singleton | in-memory store of compiled `AxisSagaDefinition`s |
| `IAxisSagaMediator` | scoped | `SagaMediator` (start / get / resume) |
| `SagaEngine` | scoped | the per-instance driver |
| `ISagaStageHandlerInvoker` | scoped | resolves and calls the matching `IAxisSagaStageHandler<TPayload>` |
| `IAxisSagaResumer` | scoped | `SagaResumer` (the poll-based recovery) |
| `IAxisSagaJanitor` | scoped | `SagaJanitor` (deletes retention-expired terminal sagas) |
| `IAxisSagaDefinitionInitializer` | scoped | `SagaDefinitionInitializer` (writes definitions to the catalogue at startup) |
| `AxisSagaResumerWorker` | hosted service | the built-in background loop (registered only when `AxisSagaSettings.ResumerEnabled`, the default) |
| `ISagaInstanceStore`, `ISagaStageLogStore`, `ISagaDefinitionStore` | scoped | Postgres row-level access to the tables (dialect-specific) |
| `IAxisSagaStorageInitializer` | singleton | `PostgresSagaStorageInitializer` — runs the schema migration on startup (dialect-specific) |

A second call to `AddAxisSagaPostgres` **without a serviceKey** (or to `AddAxisSagaMySql`) throws — "one storage per process by design". To run **several** independent stores in one process (one per subdomain), use the keyed overload below.

---

## Keyed per subdomain — several stores in one process

`AddAxisSagaPostgres(serviceKey, settings)` registers the **same** runtime, but keyed by `serviceKey` — the same convention as `AxisRepository`'s `AddPostgresUnitOfWork(serviceKey, connectionString)`. Two independent subdomains (e.g. e-commerce and revenue-vs-expense) then run **independent sagas against independent databases** in the same monolith, without waiting for the microservices split. The keyless APIs stay identical and can coexist.

```csharp
// one store per subdomain, each with its own connection string
services.AddPostgresUnitOfWork("ecommerce", ecommerceConn);       // the BC's repository (optional)
services.AddAxisSagaPostgres("ecommerce", new AxisSagaSettings { ConnectionString = ecommerceConn });

services.AddPostgresUnitOfWork("finance", financeConn);
services.AddAxisSagaPostgres("finance", new AxisSagaSettings { ConnectionString = financeConn });

// register each saga under its subdomain's key
services.AddKeyedSingleton("ecommerce", AxisSagaDefinitions.Define<OrderPayload>(OrderSaga.Name, OrderSaga.Configure));
services.AddAxisSagaHandlers(typeof(OrderSaga).Assembly);   // handlers stay global, matched by (payload, saga, stage)
```

The consumer injects the subdomain's mediator with `[FromKeyedServices("ecommerce")] IAxisSagaMediator`.

Differences from the keyless path:

- **Per-key guard.** A second call with the **same** key throws; distinct keys coexist.
- **Datasource reuse.** If the BC already registered a keyed `NpgsqlDataSource` for that key (via `AddPostgresUnitOfWork`), the saga **reuses that same pool** instead of opening another; otherwise it creates and owns its own. Postgres defaults to `READ COMMITTED`, so the lease claim is safe on the shared pool.
- **Definitions isolated per key.** Each subdomain registers its definitions with `AddKeyedSingleton<AxisSagaDefinition>(serviceKey, Define(...))`; the keyed registry sees only its own — two subdomains may have same-named sagas.
- **One resumer worker per key.** Each keyed store hosts its own `AxisSagaResumerWorker` (when `ResumerEnabled`).

> Decision recorded in [ADR-0003](../../../adr/0003-axis-saga-keyed-per-subdomain-storage.md).

---

## The pipeline of one stage

When the engine runs a stage (in `SagaEngine.ExecuteAsync(sagaId)`):

1. **Load** the instance from `axis_saga.saga_instances` (`ISagaInstanceStore`).
2. **Resolve** the current `AxisSagaStageDefinition` from the registry by `(SagaName, CurrentStage ?? FirstForwardStage)`.
3. **Log** `Started` in `axis_saga.saga_stage_logs`.
4. **Invoke** the handler via `ISagaStageHandlerInvoker` (which finds the right `IAxisSagaStageHandler<TPayload>` and calls `ExecuteAsync(payload)`).
5. On `Ok`: serialise the new payload, **update** the instance row with the new payload + version + `CurrentStage`, **log** `Completed`, set the next stage (or `Completed` / `Compensated`).
6. On `Error`: **update** with `LastErrorCode`/`LastErrorMessage`, **log** `Failed`, switch to `Compensating` and walk `RouteToOnError` (or set `Failed`).

> Concurrency: every state-mutating update guards on the version **and** lease ownership — `WHERE version = @currentVersion AND CLAIMED_BY = @runner AND CLAIMED_UNTIL > NOW()` — and bumps the version. A run that lost its lease, or a second concurrent engine, sees the mismatch and fails with `AxisSagaErrors.ConcurrencyConflict` — the saga is **not** double-driven. Single execution is enforced by this connection-less lease (`CLAIMED_BY` / `CLAIMED_UNTIL`, heartbeat-refreshed) rather than a held advisory lock.

---

## Bootstrap — `SagaDefinitionInitializer`

On startup, the initializer:

1. Reads every `AxisSagaDefinition` registered in the in-memory registry (the configurator output).
2. **Upserts** each one as a row in `axis_saga.saga_definitions`. The catalogue is just a JSON snapshot of the definition; the runtime engine still reads from the in-memory registry, but the catalogue gives ops a queryable view of what the deployed process knows about.

You do **not** wire this yourself: the built-in resumer worker runs the storage migration and then drives `IAxisSagaDefinitionInitializer.InitializeAsync` once on its first pass before it begins polling (see below). The dialect-agnostic `SagaDefinitionInitializer` is the same under Postgres and MySQL; only the `ISagaDefinitionStore` it upserts through is dialect-specific.

---

## Resumer — built in, no worker to hand-roll

The resumer is **not** something you host yourself. `AddAxisSagaPostgres` (and `AddAxisSagaMySql`) auto-register `AxisSagaResumerWorker`, a `BackgroundService`, whenever `AxisSagaSettings.ResumerEnabled` is `true` (the default). On startup it:

1. Runs the idempotent schema migration via `IAxisSagaStorageInitializer` (no-op if already applied);
2. Initializes the registered saga definitions once;
3. Polls `IAxisSagaResumer.RunOnceAsync` every `ResumerPollInterval`, reclaiming and re-firing stale instances.

Set `ResumerEnabled = false` only on a process that must start/await sagas but not run the loop (recovery owned elsewhere, or a test with no live database).

Each poll claims stale sagas through `ISagaInstanceStore.ClaimStaleSagaIdsAsync` — a pure `SELECT … FOR UPDATE SKIP LOCKED` keyed on an **expired lease** (`CLAIMED_UNTIL IS NULL OR CLAIMED_UNTIL < NOW()`) with `status IN ('Pending','Running','Compensating')` — and re-fires each through the mediator, which re-acquires the lease via `AcquireLeaseAsync` (the same call enforces the global concurrency cap). `ResumeAfter` doubles as the lease duration: a run claims the instance for that long and a heartbeat renews it every `ResumeAfter / 4` while stages execute, so a saga is treated as stale only once its lease lapses.

See [`IAxisSagaResumer`](resumer.md) for the semantics.

---

## Real-world example — production wiring

```csharp
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

// register each saga definition so the engine can resolve it
builder.Services.AddSingleton(
    AxisSagaDefinitions.Define<OrderPayload>(OrderSagaDefinition.Name, OrderSagaDefinition.Configure));
```

No hosted services to add by hand: `AddAxisSagaPostgres` already registered the built-in resumer worker, which migrates the schema, initializes the definitions and runs recovery. (Swap `AddAxisSagaPostgres` for `AddAxisSagaMySql` — same settings, same wiring — to run on MySQL instead.)

**Why it pays off:** the application only talks to `IAxisSagaMediator`. Storage, engine and recovery are wired once at the composition root.

---

## See also

- [MySQL adapter](mysql-adapter.md) — the sibling adapter over the same shared core
- [Database schema](database-schema.md) — the four business tables (plus the `MIGRATIONS` control table the framework migration runner maintains) the adapter creates
- [Mediator · `IAxisSagaMediator`](mediator.md) — the API surface
- [Resumer · `IAxisSagaResumer`](resumer.md) — the recovery loop
- [Concepts](concepts.md) — what the engine is driving
- [Stage handlers](stage-handlers.md) — what the engine invokes

---

↩ [Back to AxisSaga docs](README.md)
