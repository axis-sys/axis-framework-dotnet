# Database schema · `AXIS_SAGA`

> The schema is declared **once** in AxisSaga core (`Axis.Persistence.Scripts.AxisSagaSchema`) and rendered per dialect by an injected `IAxisSqlDialect`. A storage adapter owns a single schema: `AXIS_SAGA`. Four tables — `SAGA_DEFINITIONS`, `SAGA_INSTANCES`, `SAGA_STAGE_LOGS`, `SAGA_SETTINGS` — cover the catalogue, the live state, the per-stage forensic log and the process-wide runtime knobs. The framework migration runner also creates a `MIGRATIONS` control table that tracks which DDL versions have been applied.

```
AXIS_SAGA
├── SAGA_DEFINITIONS
├── SAGA_INSTANCES
├── SAGA_STAGE_LOGS
├── SAGA_SETTINGS        (process-wide runtime knobs)
└── MIGRATIONS           (migration bookkeeping, created by the framework runner)
```

> Storage is **not** Postgres-only. The four table definitions live in core (`SagaInstancesTable` / `SagaStageLogsTable` / `SagaDefinitionsTable` / `SagaSettingsTable` as `AxisTable` defs); the bundled storage adapters share them — currently **AxisSaga.Postgres**, **AxisSaga.MySql**, … — each passing its own dialect. Any database with an `IAxisSqlDialect` and the store ports can be added as another adapter. The schema is shared by **every saga in the process** — there is no per-BC slicing. That is deliberate: one schema, one resumer, one forensic table to query.

> Every identifier on this page is written exactly as declared in source — upper-case. Postgres folds unquoted identifiers to lower-case, so `SELECT * FROM AXIS_SAGA.SAGA_INSTANCES` and its all-lower-case equivalent name the same object there; MySQL, on the other hand, preserves case as written. The upper-case form shown throughout this page is therefore safe to use verbatim on both.

---

## When this matters

The schema is invisible to application code — handlers never read it. You see it when:

- Designing migrations / database backups.
- Writing admin dashboards that read saga state directly.
- Debugging stuck instances.
- Auditing what happened on a given saga, stage by stage.

---

## `AXIS_SAGA.SAGA_DEFINITIONS`

The catalogue. One row per saga known to the process.

| Column | Type | Purpose |
|---|---|---|
| `SAGA_NAME` | `varchar(100)` (PK) | the saga's logical name (matches `IAxisSagaStageHandler.SagaName`) |
| `DEFINITION_HASH` | `varchar(64)` | SHA-256 hash of the serialised definition; used to detect changes and skip redundant writes |
| `DEFINITION_JSON` | `jsonb` | the full `AxisSagaDefinition` serialised (includes the `TPayload` type name); used by ops dashboards |
| `UPDATED_AT` | `timestamptz` | bookkeeping |

The runtime engine reads from the in-memory `IAxisSagaDefinitionRegistry`, not from this table. The table exists so a separate process (or a dashboard) can answer "what sagas does the deployed code know about?" without having a .NET reference to the assembly.

---

## `AXIS_SAGA.SAGA_INSTANCES`

One row per saga instance. This is the live state.

| Column | Type | Purpose |
|---|---|---|
| `SAGA_ID` | `varchar(50)` (PK) | the caller-provided id (`Guid`-style) |
| `SAGA_NAME` | `varchar(100)` | the definition this instance is bound to |
| `STATUS` | `varchar(30)` | `Pending` / `Running` / `Completed` / `Failed` / `Compensating` / `Compensated` |
| `CURRENT_STAGE` | `varchar(50) NULL` | the stage the engine is on (NULL before the first stage runs) |
| `PAYLOAD_JSON` | `jsonb` | the latest payload returned by the most recent handler |
| `LAST_ERROR_CODE` | `varchar(100) NULL` | the `AxisError.Code` of the most recent failure |
| `LAST_ERROR_MESSAGE` | `text NULL` | the human-friendly version |
| `VERSION` | `int` | optimistic-concurrency token; bumped on every update |
| `CREATED_AT` / `UPDATED_AT` | `timestamptz` | bookkeeping |
| `RETAIN_FOR_SECONDS` | `int NULL` | retention window: how long to keep the row after it reaches a terminal status before the janitor may delete it (`NULL` = keep forever) |
| `DELETE_NOT_BEFORE` | `timestamptz NULL` | set when the saga goes terminal (from `RETAIN_FOR_SECONDS`); the janitor deletes the row once `NOW()` passes this instant |
| `CLAIMED_BY` | `varchar(50) NULL` | the execution **lease** owner (the runner token) — replaces the held advisory lock |
| `CLAIMED_UNTIL` | `timestamptz NULL` | when the current lease expires; a run is the owner only while `CLAIMED_BY` matches and `CLAIMED_UNTIL > NOW()` |

> Every state-modifying update guards on **both** optimistic concurrency and lease ownership: `WHERE SAGA_ID = @id AND VERSION = @currentVersion AND CLAIMED_BY = @runner AND CLAIMED_UNTIL > NOW()`. A run mutates the row only while it owns a live lease; if a second engine somehow runs the same instance concurrently, the one without the matching version/lease sees zero rows updated and aborts with `AxisSagaErrors.ConcurrencyConflict`. The lease is acquired by `AcquireLeaseAsync` (which also enforces the global concurrency cap) and renewed by a heartbeat every `ResumeAfter / 4`.

### How the resumer queries this

The resumer is a **built-in hosted worker** (`AxisSagaResumerWorker`, auto-registered by the storage adapter when `AxisSagaSettings.ResumerEnabled` is set — the default). Its claim query (`SagaInstanceStore.ClaimStaleSagaIdsAsync`) is a pure read that selects stale sagas — non-terminal and with an expired (or never-set) lease:

```sql
SELECT SAGA_ID
FROM AXIS_SAGA.SAGA_INSTANCES
WHERE STATUS IN ('Pending', 'Running', 'Compensating')
  AND (CLAIMED_UNTIL IS NULL OR CLAIMED_UNTIL < NOW())
ORDER BY CLAIMED_UNTIL NULLS FIRST
LIMIT @batch
FOR UPDATE SKIP LOCKED;
```

`FOR UPDATE SKIP LOCKED` locks the candidate rows so a concurrent resumer on another node simply skips the ones already taken. The select itself does not mutate state: the resumer re-fires each returned saga through `mediator.ResumeAsync`, and the engine re-acquires the lease via `AcquireLeaseAsync` (which also enforces the global concurrency cap). That is what makes the resumer safe to run on every node — once a saga's lease is live again, other nodes' claim queries skip it, so multiple resumers do not double-fire the same saga. `@batch` is `ResumeBatchSize` (default 100), the maximum number of sagas claimed per poll; when a global cap is set the resumer further trims it to the number of free lease slots.

---

## `AXIS_SAGA.SAGA_STAGE_LOGS`

One row per stage transition. The forensic log.

| Column | Type | Purpose |
|---|---|---|
| `LOG_ID` | `varchar(50)` (PK) | unique log id (UUID v7) |
| `SAGA_ID` | `varchar(50)` | foreign key into `SAGA_INSTANCES` (`ON DELETE CASCADE`) |
| `STAGE_NAME` | `varchar(50)` | the stage at this event |
| `ATTEMPT` | `int` | attempt number for this stage (defaults to `1`) |
| `STATUS` | `varchar(30)` | `Started` / `Completed` / `Failed` |
| `ERROR_CODE` | `varchar(100) NULL` | filled when `STATUS = 'Failed'` |
| `ERROR_MESSAGE` | `text NULL` | the human-friendly version |
| `STARTED_AT` | `timestamptz` | when the stage started (UTC) |
| `FINISHED_AT` | `timestamptz NULL` | when it finished (UTC; `NULL` while in progress) |

### What you get to do with it

```sql
-- list every stage ever run for a saga, in order
SELECT STAGE_NAME, STATUS, STARTED_AT, FINISHED_AT
FROM AXIS_SAGA.SAGA_STAGE_LOGS
WHERE SAGA_ID = 'order-01927a8b-…'
ORDER BY STARTED_AT;

-- failures-per-stage for the last week
SELECT STAGE_NAME, count(*) failures
FROM AXIS_SAGA.SAGA_STAGE_LOGS
WHERE STATUS = 'Failed' AND STARTED_AT > NOW() - INTERVAL '7 days'
GROUP BY STAGE_NAME
ORDER BY failures DESC;
```

> Combine `SAGA_STAGE_LOGS` with `SAGA_INSTANCES.STATUS` for dashboards: "show me every saga that started compensating in the last hour and never reached `Compensated`".

---

## `AXIS_SAGA.SAGA_SETTINGS`

Process-wide runtime knobs, held in a **single row** shared by every application instance that points at this database.

| Column | Type | Purpose |
|---|---|---|
| `ONLY_ROW` | `boolean` (PK, `CHECK (ONLY_ROW)`) | pins the table to exactly one row |
| `MAX_CONCURRENT_SAGAS` | `int NULL` | global cap on how many sagas may hold a **live lease** (be executing) at once across all instances; `NULL` = unbounded |

### Why it lives here and not in app config

`MAX_CONCURRENT_SAGAS` is a **global** limit. If it lived in each application's configuration, two instances could be deployed with different values and the "global" cap would silently stop being global. Storing it in the shared database makes it a single source of truth that every instance reads on each lease claim.

The lease claim refuses to admit a saga once the live-lease count reaches the cap; the deferred saga stays `Pending` and the resumer retries it when a slot frees, so nothing is dropped. It is a *soft* cap — a burst of concurrent claims can transiently exceed it by a small, self-correcting amount, which is fine for its purpose (bounding load on the connection pool).

### How to change it

It takes effect on the next lease claim — no redeploy, no migration.

**From code (recommended) — `IAxisSagaSettingsStore`.** The storage adapter registers this consumer-facing port, so an application reads and adjusts the cap without hand-rolled SQL. A single dialect-agnostic implementation over ADO.NET serves every storage, so the behaviour is identical on Postgres and MySQL. Every method returns an `AxisResult` and never throws.

```csharp
public interface IAxisSagaSettingsStore
{
    // Ok(null) = unbounded.
    Task<AxisResult<int?>> GetMaxConcurrentSagasAsync(CancellationToken ct = default);

    // Unconditional set; null = unbounded; a zero/negative cap is a validation error.
    Task<AxisResult> SetMaxConcurrentSagasAsync(int? maxConcurrentSagas, CancellationToken ct = default);

    // Race-safe conditional set: writes newValue only if the stored cap still equals expectedCurrent.
    // Ok(true) = changed; Ok(false) = the guard did not match (someone already changed it / it was
    // never expectedCurrent). Never clobbers a manually-tuned value.
    Task<AxisResult<bool>> TrySetMaxConcurrentSagasAsync(int expectedCurrent, int? newValue, CancellationToken ct = default);
}
```

`TrySetMaxConcurrentSagasAsync` is the idiomatic "raise the seeded default to a higher operating cap right after a migration, but only while it still holds the seed — and never overwrite a value an operator tuned by hand" operation, done as one atomic statement:

```csharp
// e.g. right after the migrations endpoint runs: raise 20 → 75, but only if it is still the seeded 20.
await settingsStore.TrySetMaxConcurrentSagasAsync(expectedCurrent: 20, newValue: 75);
```

**From SQL (equivalent).** Since it is a plain single row, a manual `UPDATE` works too:

```sql
-- cap total concurrent sagas across every instance at 20
UPDATE AXIS_SAGA.SAGA_SETTINGS SET MAX_CONCURRENT_SAGAS = 20;

-- disable the cap (unbounded)
UPDATE AXIS_SAGA.SAGA_SETTINGS SET MAX_CONCURRENT_SAGAS = NULL;
```

The consolidated `V1` migration seeds the row with `20`.

---

## Migrations

The four table definitions live once in core (`AxisSagaSchema.Tables`). The adapter renders them with its dialect and applies them through the framework migration runner — `AxisSagaMigrations.InitializePostgresAsync` calls `PostgresMigrationRunner.RunAsync`; the MySQL adapter's `AxisSagaMySqlMigrations.InitializeMySqlAsync` calls `MySqlMigrationRunner.RunAsync`. The runner creates the schema (`CREATE SCHEMA IF NOT EXISTS`) and the bookkeeping table idempotently.

That control table is `AXIS_SAGA.MIGRATIONS` (`VERSION` PK, `APPLIED_AT TIMESTAMPTZ`), created by the framework runner — **not** a `schema_migrations` table created by the adapter. The runner records each applied DDL version inside it (under a transactional advisory lock per schema) and skips versions already recorded instead of re-issuing them. The whole schema currently ships as a single consolidated `V1` — `AxisSagaSchema.Migrations(dialect)` renders every table into that one version (the framework has no production deployments yet); changing that `V1` requires recreating the database, since a recorded version is never re-applied.

On startup the built-in `AxisSagaResumerWorker` calls the storage initializer to run this migration (idempotent — already-applied versions are skipped), so it is a no-op when a test fixture or a prior run already migrated the schema. It never destroys data.

---

## Indexing notes

The table definitions create:

- `PRIMARY KEY` on `SAGA_NAME` (SAGA_DEFINITIONS), `SAGA_ID` (SAGA_INSTANCES), `LOG_ID` (SAGA_STAGE_LOGS), and the `ONLY_ROW` boolean PK on `SAGA_SETTINGS`.
- On `SAGA_INSTANCES`: an index on `(STATUS, UPDATED_AT)`, one on `SAGA_NAME`, one on `(STATUS, CLAIMED_UNTIL)` for the lease-keyed resumer claim, plus two partial indexes — one on `DELETE_NOT_BEFORE` (where not null) for the janitor, and an active-lease one on `CLAIMED_UNTIL` (where status is non-terminal) for the global concurrency-cap live-lease count. On MySQL the partial indexes are rendered as plain indexes.
- An index on `SAGA_STAGE_LOGS.(SAGA_ID, STAGE_NAME, STATUS)` (the forensic query).

For larger workloads consider partitioning `SAGA_STAGE_LOGS` by `STARTED_AT` — it grows much faster than `SAGA_INSTANCES`.

---

## See also

- [Postgres adapter](postgres-adapter.md) — what writes to / reads from these tables
- [MySQL adapter](mysql-adapter.md) — the MySQL-specific behavior around these same tables
- [Mediator · `IAxisSagaMediator`](mediator.md) — the user-facing API
- [Resumer · `IAxisSagaResumer`](resumer.md) — uses the `SAGA_INSTANCES` index
- [Concepts](concepts.md) — what the columns mean
- [AxisRepository · Schema DDL](../../3-Infra/AxisRepository/ddl.md) — the `AxisTable` model these four tables are built from
- [AxisRepository · Migrations](../../3-Infra/AxisRepository/migrations.md) — the runner that applies them idempotently

---

↩ [Back to AxisSaga docs](README.md)
