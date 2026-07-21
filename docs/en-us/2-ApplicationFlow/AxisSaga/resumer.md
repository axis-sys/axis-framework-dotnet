# Resumer · `IAxisSagaResumer`

> A built-in, poll-based recovery worker. Periodically scans the database for **stuck** instances (non-terminal sagas whose execution **lease** has expired) and re-fires the engine so the process can pick up where the previous one left off. The framework hosts this worker for you — you do not write one.

```csharp
public interface IAxisSagaResumer
{
    Task<int> RunOnceAsync(CancellationToken cancellationToken);
}
```

---

## When to use

In a production deployment you do not host the resumer yourself — `AddAxisSagaPostgres` / `AddAxisSagaMySql` register a built-in hosted worker (`AxisSagaResumerWorker`) that calls `RunOnceAsync` every `ResumerPollInterval` while `AxisSagaSettings.ResumerEnabled` is `true` (the default). The dialect-agnostic `SagaResumer` (registered under this interface in the saga core) does the work; the worker just drives its loop. You interact with `IAxisSagaResumer` directly only for the niche cases below (a health probe, a one-shot drain).

## When *not* to use

| You want to… | Use instead |
|---|---|
| immediately resume after a fix | [`IAxisSagaMediator.ResumeAsync(sagaId)`](mediator.md) |
| run only on a specific node | gate the hosted service behind a leader-election check (the resumer itself is safe to run on every node, but you may not want it competing) |
| drain *all* sagas on shutdown | a graceful-shutdown handler that calls `RunOnceAsync` once then waits |

---

## What "stuck" means

A saga is **stuck** when it is non-terminal **and its execution lease has expired** — `CLAIMED_UNTIL IS NULL OR CLAIMED_UNTIL < NOW()`. Each engine run holds a lease (stamped on `CLAIMED_BY` / `CLAIMED_UNTIL`) and a heartbeat renews it every `ResumeAfter / 4` while stages execute; a crashed or hung owner stops renewing, the lease lapses, and the next resumer pass reclaims it:

| Status | Lease | Diagnosis |
|---|---|---|
| `Pending` | absent or expired | started but never claimed by a run (or the claimer died before stamping a live lease) |
| `Running` | expired | the engine was driving it but stopped (process crash, OOM kill) |
| `Compensating` | expired | same, mid-compensation |

The resumer claims `Pending` / `Running` / `Compensating` rows whose lease has lapsed; it **does not** touch `Completed` / `Compensated` / `Failed` (those are terminal), nor any saga whose lease is still live (an owner is actively driving it).

> `ResumeAfter` (default 60 seconds) is the lease duration. Tune it to be larger than the longest single stage's worst-case duration plus a safety margin — otherwise a legitimately-running stage could have its lease lapse and be reclaimed mid-flight.

---

## What it does

For each stuck saga the resumer finds:

1. It calls the engine (the same code path as `IAxisSagaMediator.ResumeAsync(sagaId)`), which first **re-acquires the lease** via `AcquireLeaseAsync` — an atomic claim that also enforces the **global concurrency cap** (see below). If the lease is already held by a live run or the cap is full, the re-fire is skipped.
2. The engine reads the current state, locates the `CurrentStage`, resolves the matching handler, and runs it.
3. If the stage was already half-run (a log row `Started` but no `Completed` / `Failed`), the handler is expected to be **idempotent**: re-running it on the same payload should produce the same result or be a no-op.

> Idempotency is the handler's responsibility. A typical pattern is to check upstream state first (`stock.GetReservationAsync` before `ReserveAsync`), or to use idempotency keys derived from `(SagaId, StageName)`.

`RunOnceAsync` returns the **number of sagas it kicked**.

---

## The loop is hosted for you

You do **not** write a `BackgroundService`. The storage adapter registers a built-in `AxisSagaResumerWorker` (an internal `BackgroundService`) the moment you call `AddAxisSagaPostgres` / `AddAxisSagaMySql` with `ResumerEnabled = true`. On startup it migrates the saga schema and upserts the in-process definitions, then polls `RunOnceAsync` every `ResumerPollInterval` — roughly equivalent to:

```csharp
// Inside the framework — you do not write this.
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await storageInitializer.InitializeAsync();          // idempotent schema migration
    while (!stoppingToken.IsCancellationRequested)
    {
        using var scope = scopeFactory.CreateScope();    // a fresh scope per pass
        await scope.ServiceProvider.GetRequiredService<IAxisSagaResumer>().RunOnceAsync(stoppingToken);
        await Task.Delay(settings.ResumerPollInterval, stoppingToken);
    }
}
```

To opt out — a process that starts/awaits sagas but must not run the loop, or a test with no live database — set `ResumerEnabled = false`:

```csharp
builder.Services.AddAxisSagaPostgres(new AxisSagaSettings
{
    ConnectionString = "…",
    ResumerEnabled   = false,
});
```

**Why it pays off:** crash recovery is on by default, with no per-application boilerplate to forget. The claim is safe to run on every node — the store *claims* stale instances with a single `SELECT … FOR UPDATE SKIP LOCKED` keyed on the expired lease (`CLAIMED_UNTIL IS NULL OR CLAIMED_UNTIL < NOW()`), so a concurrent resumer on another node skips rows already locked. The re-fire then re-acquires the lease atomically via `AcquireLeaseAsync`, so multiple resumers do not double-fire the same saga.

---

## Tuning `AxisSagaSettings`

```csharp
public class AxisSagaSettings
{
    public required string  ConnectionString    { get; init; }
    public TimeSpan         ResumerPollInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan         ResumeAfter         { get; init; } = TimeSpan.FromSeconds(60);
    public int              ResumeBatchSize     { get; init; } = 100;
    public bool             ResumerEnabled      { get; init; } = true;
}
```

| Setting | Meaning | Default |
|---|---|---|
| `ResumerPollInterval` | how often the worker calls `RunOnceAsync` | 30s |
| `ResumeAfter` | the execution **lease** duration; also how long a lease must be expired before the resumer reclaims the saga | 60s |
| `ResumeBatchSize` | maximum number of stale sagas claimed per poll (the `LIMIT` of the claim query) | 100 |
| `ResumerEnabled` | whether the storage adapter hosts the built-in resumer worker | `true` |

> Aim for `ResumeAfter >= 2 × ResumerPollInterval` — that way the worker never reclaims a saga whose lease the engine just renewed.

### Global concurrency cap

`RunOnceAsync` is also **cap-aware**. A single process-wide ceiling lives in the database, not in settings: the `AXIS_SAGA.SAGA_SETTINGS` singleton row holds `MAX_CONCURRENT_SAGAS` (seeded to `20`, `NULL` = unbounded), adjustable at runtime with a plain `UPDATE`, no redeploy. Before claiming, the resumer reads the cap and the count of **live leases** and fetches at most the number of free slots, so it never fires a batch that would immediately bounce off the gate. The cap is ultimately enforced atomically inside `AcquireLeaseAsync` (a claim succeeds only while fewer than `MAX_CONCURRENT_SAGAS` sagas hold a live lease), so the resumer's pre-trim is just an optimization — the engine's atomic gate stays the authority.

---

## Real-world examples

### 1. The built-in worker (default)

Just register a storage adapter — the resumer loop comes with it. Nothing else to write:

```csharp
builder.Services.AddAxisSagaPostgres(new AxisSagaSettings { ConnectionString = "…" });
// ResumerEnabled defaults to true → AxisSagaResumerWorker is hosted automatically.
```

### 2. Resume on demand from a command

```csharp
public class RetrySagaHandler(IAxisSagaMediator sagas) : IAxisCommandHandler<RetrySagaCommand>
{
    public Task<AxisResult> HandleAsync(RetrySagaCommand cmd)
        => sagas.ResumeAsync(cmd.SagaId);
}
```

**Why it pays off:** ops can ask the system to re-try a saga manually via an admin command, without restarting the process.

### 3. A health probe via `RunOnceAsync`

```csharp
public class SagaResumerHealthProbe(IAxisSagaResumer resumer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext _, CancellationToken ct)
    {
        var resumed = await resumer.RunOnceAsync(ct);
        return HealthCheckResult.Healthy($"resumed {resumed} sagas");
    }
}
```

**Why it pays off:** a single liveness probe both **proves** the database is reachable and **drives** recovery. The number of resumed sagas is a useful metric.

---

## See also

- [Mediator · `IAxisSagaMediator`](mediator.md) — manual `ResumeAsync`
- [Postgres adapter](postgres-adapter.md) — what a bundled storage adapter (Postgres, MySQL, …) does under the hood
- [Database schema](database-schema.md) — which columns the resumer queries (the lease columns `CLAIMED_BY` / `CLAIMED_UNTIL`)
- [Concepts · stages and routes](concepts.md) — the state machine the engine drives

---

↩ [Back to AxisSaga docs](README.md)
