# Why AxisRepository? · comparison

> There are other ways to handle persistence in .NET. This page tells you why AxisRepository is different — a direct comparison, no hand-waving.

---

## vs. `DbContext` (EF Core) directly

`DbContext` is excellent for CRUD-shaped domains. Three pain points show up at scale:

1. **Change tracking is leaky.** `SaveChangesAsync` commits *whatever you mutated*; tracing a stray side effect is a forensic exercise.
2. **`SaveChangesAsync` throws.** You need a `try/catch` around every command pipeline — or you wrap it in your own `Result`. `AxisRepository` already returns `AxisResult`.
3. **Transactions are not first-class on the railway.** EF's `IDbContextTransaction` is fine, but composing it with `Result`-returning helpers means writing the same wrapper everyone writes.

`AxisRepository` is **not** an ORM replacement. You can wrap a `DbContext` inside a custom `IAxisUnitOfWork` and keep the `AxisResult` flow at the application layer — best of both worlds.

## vs. Dapper directly

Dapper is also excellent — and AxisRepository's `IAxisDbRepository` executor is a thin layer over the driver that fills the same niche. The difference: AxisRepository adds **typed error codes**, **transient retry**, and **transaction orchestration via `InTransactionAsync`**. If you only need one-shot queries, Dapper is plenty; if you ship multi-step pipelines, the executor saves the boilerplate.

## vs. a bespoke `IUnitOfWork`

DIY. Same shape (`Start`/`Commit`/`Rollback`), but you re-derive: how to return errors, how to handle exceptions inside `InTransaction`, how to plug in retries, how to register keyed providers. `IAxisUnitOfWork` saves the cost — and integrates cleanly with `AxisResult`, `AxisMediator` (cancellation), `AxisLogger`, `AxisTelemetry`.

## vs. EventSourcing libraries

Different problem. AxisRepository is for **state-stored** systems with relational integrity. Event sourcing replaces both the persistence and the model.

---

## The comparison

| Feature | AxisRepository | EF Core direct | Dapper direct | Bespoke `IUnitOfWork` |
|---|:--:|:--:|:--:|:--:|
| Returns `AxisResult` | **Yes** | No | No | Maybe |
| `InTransactionAsync(railway)` wrapper | **Yes** | No | No | Maybe |
| Implicit cancellation via `IAxisMediator` | **Yes** | No | No | Maybe |
| Unique-key violation → typed `Conflict` | **Yes (in box)** | No | No | Maybe |
| Transient `SqlState` retry with backoff | **Yes (in box)** | No | No | Maybe |
| Keyed multi-database registration | **Yes** | Manual | Manual | Maybe |
| Bundled Postgres adapter | **Yes** | n/a | n/a | No |
| Plays with `AxisLogger` / `AxisTelemetry` | **Yes** | Indirect | Indirect | Manual |
| Zero NuGet deps in the abstraction | **Yes** | n/a | n/a | Yes |

---

## See also

- [The `IAxisUnitOfWork` contract](iaxisunitofwork.md) — the surface
- [`InTransactionAsync`](in-transaction.md) — the operator that justifies the abstraction
- [Postgres adapter](postgres-adapter.md) — the in-box implementation

---

↩ [Back to AxisRepository docs](README.md)
