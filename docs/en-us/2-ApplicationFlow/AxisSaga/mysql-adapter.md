# MySQL adapter · `AxisSaga.MySql`

> The MySQL storage adapter over the shared saga core. One singleton `MySqlDataSource` against the `AXIS_SAGA` schema, plus the four MySQL store ports; the dialect-agnostic core supplies the `SagaEngine` that drives a single instance forward, the `SagaMediator` that inserts new rows and fires the engine in the background, the `SagaResumer` for recovery and the `SagaDefinitionInitializer` that persists definitions at startup. The sibling `AxisSaga.Postgres` (`AddAxisSagaPostgres`) shares that same core and differs only in the SQL dialect and a couple of MySQL-specific concurrency workarounds documented below.

```csharp
services.AddAxisSagaMySql(new AxisSagaSettings
{
    ConnectionString    = "Server=…",
    ResumerPollInterval = TimeSpan.FromSeconds(30),
    ResumeAfter         = TimeSpan.FromSeconds(60),
    ResumeBatchSize     = 100,
});
```

---

## When to use

MySQL — your own server, RDS, Aurora MySQL, Cloud SQL. The adapter expects a single database for **all** sagas in the process; `AddAxisSagaMySql` refuses a second registration on purpose.

## When *not* to use

| You want to… | Use instead |
|---|---|
| target Postgres | `AddAxisSagaPostgres` from `AxisSaga.Postgres` — a sibling adapter over the same shared core, identical settings and runtime |
| target SQL Server / Mongo | write a new adapter against the same store ports (`ISagaInstanceStore`, `ISagaStageLogStore`, `ISagaDefinitionStore`, `IAxisSagaStorageInitializer`); the dialect-agnostic core (`AddAxisSagaCore`) supplies the rest |
| store sagas in a Kafka topic | a custom adapter — the in-box adapters assume relational durability |
| share the saga storage with the rest of the app | possible — point both at the same database; the `AXIS_SAGA` schema is isolated by name |

---

## What `AddAxisSagaMySql(settings)` registers

Reading `DependencyInjection.AddAxisSagaMySql` directly: it registers the MySQL data source and the four dialect-specific store ports, then calls `AddAxisSagaCore(settings)` for the dialect-agnostic runtime (the same call `AddAxisSagaPostgres` makes).

| Service | Lifetime | Description |
|---|---|---|
| `AxisSagaSettings` | singleton | the configuration object (registered by the core) |
| `AxisSagaMySqlDataSource` | singleton | wraps a `MySqlDataSource` built via `MySqlDataSourceBuilder`, with a connection-opened callback that pins every new physical connection to `READ COMMITTED` (MySQL-specific — see below) |
| `IAxisSagaDefinitionRegistry` | singleton | in-memory store of compiled `AxisSagaDefinition`s |
| `IAxisSagaMediator` | scoped | `SagaMediator` (start / get / resume) |
| `SagaEngine` | scoped | the per-instance driver |
| `ISagaStageHandlerInvoker` | scoped | resolves and calls the matching `IAxisSagaStageHandler<TPayload>` |
| `IAxisSagaResumer` | scoped | `SagaResumer` (the poll-based recovery) |
| `IAxisSagaJanitor` | scoped | `SagaJanitor` (deletes retention-expired terminal sagas) |
| `IAxisSagaDefinitionInitializer` | scoped | `SagaDefinitionInitializer` (writes definitions to the catalogue at startup) |
| `AxisSagaResumerWorker` | hosted service | the built-in background loop (registered only when `AxisSagaSettings.ResumerEnabled`, the default) |
| `ISagaInstanceStore`, `ISagaStageLogStore`, `ISagaDefinitionStore` | scoped | MySQL row-level access to the tables — `MySqlSagaInstanceStore`, `MySqlSagaStageLogStore`, `MySqlSagaDefinitionStore` (dialect-specific) |
| `IAxisSagaStorageInitializer` | singleton | `MySqlSagaStorageInitializer` — runs the schema migration on startup (dialect-specific) |

A second call to `AddAxisSagaMySql` **without a serviceKey** (or to `AddAxisSagaPostgres`) throws — "one storage per process by design". To run **several** independent stores in one process (one per subdomain), use the keyed overload below.

---

## Keyed per subdomain — several stores in one process

`AddAxisSagaMySql(serviceKey, settings)` registers the **same** runtime, but keyed by `serviceKey` — the same convention as `AxisRepository`'s `AddMySqlUnitOfWork(serviceKey, connectionString)`. Two independent subdomains then run **independent sagas against independent databases** in the same monolith, without waiting for the microservices split. The keyless APIs stay identical and can coexist.

```csharp
services.AddAxisSagaMySql("ecommerce", new AxisSagaSettings { ConnectionString = ecommerceConn });
services.AddAxisSagaMySql("finance",   new AxisSagaSettings { ConnectionString = financeConn });

// register each saga under its subdomain's key
services.AddKeyedSingleton("ecommerce", AxisSagaDefinitions.Define<OrderPayload>(OrderSaga.Name, OrderSaga.Configure));
services.AddAxisSagaHandlers(typeof(OrderSaga).Assembly);   // handlers stay global, matched by (payload, saga, stage)
```

The consumer injects the subdomain's mediator with `[FromKeyedServices("ecommerce")] IAxisSagaMediator`.

Differences from the keyless path — and from the Postgres adapter:

- **Per-key guard.** A second call with the **same** key throws; distinct keys coexist.
- **Always its own datasource.** Unlike Postgres, the MySQL adapter does **not** reuse the repository's `MySqlDataSource`: each key builds its own via `BuildDataSource`, which pins every connection to `READ COMMITTED` (the repository's plain `MySqlDataSource` does not, and the lease claim gap-locks under InnoDB's default `REPEATABLE READ` — see the isolation section above).
- **Definitions isolated per key.** Each subdomain registers its definitions with `AddKeyedSingleton<AxisSagaDefinition>(serviceKey, Define(...))`; the keyed registry sees only its own — two subdomains may have same-named sagas.
- **One resumer worker per key.** Each keyed store hosts its own `AxisSagaResumerWorker` (when `ResumerEnabled`).

> Decision recorded in [ADR-0003](../../../adr/0003-axis-saga-keyed-per-subdomain-storage.md).

---

## Isolation level — every connection is pinned to `READ COMMITTED`

This is the one behavior with **no Postgres equivalent**. `DependencyInjection.BuildDataSource` registers a `UseConnectionOpenedCallback` that runs on every brand-new physical connection:

```csharp
// The store pins every saga-store connection to READ COMMITTED. The lease claim
// (MySqlSagaInstanceStore.AcquireLeaseAsync) gates on a COUNT over SAGA_INSTANCES inside the
// UPDATE; under InnoDB's default REPEATABLE READ that scan takes next-key/gap locks, which
// deadlock against concurrent claims and block concurrent INSERTs under the import fan-out.
// READ COMMITTED drops the gap locks. The global cap is already a soft cap by design, so the
// looser isolation does not change its semantics. The SET runs only on brand-new physical
// connections; the session setting persists across pool reuse.
private static MySqlDataSource BuildDataSource(string connectionString)
{
    MySqlDataSourceBuilder builder = new(connectionString);
    builder.UseConnectionOpenedCallback(async (context, cancellationToken) =>
    {
        if ((context.Conditions & MySqlConnectionOpenedConditions.New) == 0)
            return;

        await using MySqlCommand cmd = context.Connection.CreateCommand();
        cmd.CommandText = "SET SESSION TRANSACTION ISOLATION LEVEL READ COMMITTED";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    });
    return builder.Build();
}
```

(`AxisSaga.MySql/DependencyInjection.cs`.) Under InnoDB's default `REPEATABLE READ`, the global-concurrency-cap gate that `MySqlSagaInstanceStore.AcquireLeaseAsync` runs — a `COUNT(*)` scan over `AXIS_SAGA.SAGA_INSTANCES` — takes next-key/gap locks that deadlock against other concurrent lease claims and block concurrent `INSERT`s during import fan-out. `READ COMMITTED` drops those gap locks. Because the concurrency cap is already a *soft* cap by design (see [Database schema](database-schema.md#axis_sagasaga_settings)), the looser isolation does not change its semantics — it only removes a deadlock vector that is specific to MySQL/InnoDB's locking model. The `SET SESSION` statement runs once per brand-new physical connection; the session setting then persists for the lifetime of that connection across pool reuse.

To further avoid gap locks, the lease claim itself (`AcquireLeaseAsync`) reads the concurrency gate as a separate, non-locking `SELECT` and then claims strictly **by primary key** (`WHERE SAGA_ID = @id AND …`) — a single-row equality match that cannot range-scan or gap-lock, unlike Postgres's combined `FOR UPDATE SKIP LOCKED` batch claim.

---

## Transient retry — `MySqlTransientRetry`

Every saga-store write goes through `MySqlTransientRetry.ExecuteAsync`, which retries up to **5 attempts** with a jittered backoff (`20ms * attempt + random(0–25ms)`) whenever the underlying `MySqlException` is transient — the same classification (`MySqlTransientErrors.IsTransient`) shared with `MySqlRepositoryBase`: deadlocks, lock-wait timeouts, and connection blips.

```csharp
internal static class MySqlTransientRetry
{
    private const int MaxAttempts = 5;

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (MySqlException ex) when (MySqlTransientErrors.IsTransient(ex) && attempt < MaxAttempts)
            {
                // Brief backoff with jitter so the racing runners desynchronize before retrying.
                await Task.Delay(TimeSpan.FromMilliseconds(20 * attempt + Random.Shared.Next(0, 25)));
            }
        }
    }
}
```

Unlike `MySqlRepositoryBase` — which must surface a transient once its unit-of-work already holds an uncommitted write — the saga store writes in autocommit (one statement per connection), so a transient never strands a durable write and retrying in place is always safe. This is why the saga adapter retries internally instead of bubbling the exception up to the engine.

---

## The pipeline of one stage

When the engine runs a stage (in `SagaEngine.ExecuteAsync(sagaId)`), the steps are identical to the Postgres adapter's — load, resolve, log `Started`, invoke, then update + log + route — with two MySQL-specific differences:

1. **No `RETURNING`.** MySQL has no `RETURNING` clause, so `AcquireLeaseAsync` claims the row with an `UPDATE` and then reads it back with a follow-up `SELECT … WHERE SAGA_ID = @id AND CLAIMED_BY = @runner`. Because the runner token is unique to that run, this reads exactly the row just claimed even without a surrounding transaction.
2. **Timestamps.** Every "now" comparison uses `UTC_TIMESTAMP(6)` instead of Postgres's `NOW()`; columns render as `DATETIME(6)` rather than `timestamptz`.

> Concurrency: every state-mutating update still guards on the version **and** lease ownership — `WHERE VERSION = @currentVersion AND CLAIMED_BY = @runner AND CLAIMED_UNTIL > UTC_TIMESTAMP(6)` — and bumps the version. A run that lost its lease, or a second concurrent engine, sees the mismatch and fails with `AxisSagaErrors.ConcurrencyConflict` — the saga is **not** double-driven, exactly as under Postgres.

---

## Bootstrap — `SagaDefinitionInitializer`

Identical to the Postgres adapter: on startup, the initializer reads every `AxisSagaDefinition` registered in the in-memory registry and **upserts** each one as a row in `AXIS_SAGA.SAGA_DEFINITIONS` (via `ON DUPLICATE KEY UPDATE` under MySQL's dialect). You do **not** wire this yourself — the built-in resumer worker runs the storage migration and then drives `IAxisSagaDefinitionInitializer.InitializeAsync` once on its first pass before it begins polling. The dialect-agnostic `SagaDefinitionInitializer` is the same under Postgres and MySQL; only the `ISagaDefinitionStore` it upserts through (`MySqlSagaDefinitionStore`) is dialect-specific.

---

## Resumer — built in, no worker to hand-roll

The resumer is **not** something you host yourself. `AddAxisSagaMySql` (and `AddAxisSagaPostgres`) auto-register `AxisSagaResumerWorker`, a `BackgroundService`, whenever `AxisSagaSettings.ResumerEnabled` is `true` (the default). On startup it:

1. Runs the idempotent schema migration via `IAxisSagaStorageInitializer` (`AxisSagaMySqlMigrations.InitializeMySqlAsync`, no-op if already applied);
2. Initializes the registered saga definitions once;
3. Polls `IAxisSagaResumer.RunOnceAsync` every `ResumerPollInterval`, reclaiming and re-firing stale instances.

Set `ResumerEnabled = false` only on a process that must start/await sagas but not run the loop (recovery owned elsewhere, or a test with no live database).

Each poll claims stale sagas through `ISagaInstanceStore.ClaimStaleSagaIdsAsync`. Unlike Postgres's `FOR UPDATE SKIP LOCKED` batch claim, the MySQL implementation runs a plain `SELECT` (`STATUS IN ('Pending','Running','Compensating')` and `CLAIMED_UNTIL IS NULL OR CLAIMED_UNTIL < UTC_TIMESTAMP(6)`, ordered so `NULL` leases sort first, matching Postgres's `NULLS FIRST`) with no row-level locking — the real de-duplication happens later, at the engine's atomic per-row lease acquire (`AcquireLeaseAsync`, claimed strictly by primary key), so a re-fire that races another resumer is a harmless no-op rather than a locking concern.

See [`IAxisSagaResumer`](resumer.md) for the semantics.

---

## Indexes — partial indexes render as plain indexes

MySQL has no `WHERE` predicate on `CREATE INDEX`. The two partial indexes that Postgres renders on `AXIS_SAGA.SAGA_INSTANCES` — `IDX_SAGA_INSTANCES_DELETE_NOT_BEFORE` (on `DELETE_NOT_BEFORE`, where not null) and `IDX_SAGA_INSTANCES_ACTIVE_LEASE` (on `CLAIMED_UNTIL`, where `STATUS` is non-terminal) — come out as plain, full-table indexes under MySQL's dialect (`MySqlSqlDialect.RenderInlineIndexLines` drops the predicate for non-unique indexes and emits a normal `INDEX name (cols)`). They still serve the same queries; they are simply less selective than their Postgres counterparts, since MySQL indexes every row rather than only the qualifying ones. See [Database schema](database-schema.md#indexing-notes) for the full indexing note.

---

## Real-world example — production wiring

```csharp
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddAxisMemoryBus()
    .AddAxisSagaMySql(new AxisSagaSettings
    {
        ConnectionString    = builder.Configuration.GetConnectionString("MySql")!,
        ResumerPollInterval = TimeSpan.FromSeconds(30),
        ResumeAfter         = TimeSpan.FromSeconds(60),
        ResumeBatchSize     = 100,
    })
    .AddAxisSagaHandlers(Assembly.GetExecutingAssembly());

// register each saga definition so the engine can resolve it
builder.Services.AddSingleton(
    AxisSagaDefinitions.Define<OrderPayload>(OrderSagaDefinition.Name, OrderSagaDefinition.Configure));
```

No hosted services to add by hand: `AddAxisSagaMySql` already registered the built-in resumer worker, which migrates the schema, initializes the definitions and runs recovery. (Swap `AddAxisSagaMySql` for `AddAxisSagaPostgres` — same settings, same wiring — to run on Postgres instead.)

**Why it pays off:** the application only talks to `IAxisSagaMediator`. Storage, engine and recovery are wired once at the composition root — and the two MySQL-specific quirks above (isolation level, transient retry) are handled inside the adapter, invisible to application code.

---

## See also

- [Postgres adapter](postgres-adapter.md) — the sibling adapter over the same shared core
- [Database schema](database-schema.md) — the four business tables (plus the `MIGRATIONS` control table the framework migration runner maintains) the adapter creates
- [Mediator · `IAxisSagaMediator`](mediator.md) — the API surface
- [Resumer · `IAxisSagaResumer`](resumer.md) — the recovery loop
- [Concepts](concepts.md) — what the engine is driving
- [Stage handlers](stage-handlers.md) — what the engine invokes

---

↩ [Back to AxisSaga docs](README.md)
