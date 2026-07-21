# Custom adapter · write your own `IAxisUnitOfWork`

> Swap the Postgres adapter for SQL Server, Mongo, EF Core — or write a test double that records every transaction. Implement four methods (plus dispose), register your class for `IAxisUnitOfWork`.

```csharp
public class SqlServerUnitOfWork(IAxisMediator mediator, SqlConnection conn, IAxisLogger<SqlServerUnitOfWork> log)
    : IAxisUnitOfWork
{
    private SqlTransaction? _tx;

    public async Task<AxisResult> StartAsync()
    {
        try
        {
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(mediator.CancellationToken);
            _tx = (SqlTransaction)await conn.BeginTransactionAsync(mediator.CancellationToken);
            return AxisResult.Ok();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to start SQL Server transaction");
            return AxisError.InternalServerError("SQLSERVER_ERROR_STARTING_CONNECTION");
        }
    }

    // SaveChangesAsync, RollbackAsync, ReleaseConnectionAsync, Dispose, DisposeAsync …
}
```

---

## When to use

- Another relational database (SQL Server, Oracle, SQLite).
- A non-relational store with transaction semantics (Mongo with multi-document transactions).
- An ORM stack (EF Core, Linq2Db) — wrap the `DbContext` lifecycle.
- A test double that captures every commit/rollback for assertion.

## When *not* to use

| You want to… | Use instead |
|---|---|
| stay on Postgres | the bundled [`AxisRepository.Postgres`](postgres-adapter.md) |
| stay on MySQL | the bundled [`AxisRepository.MySql`](mysql-adapter.md) |
| add cross-store transactions | a saga with compensations |

---

## The contract you must honour

| Behaviour | Required | Rationale |
|---|---|---|
| Return `Task<AxisResult>` from every primitive; never throw cooperatively | yes | the railway depends on it |
| `StartAsync` is idempotent within a scope | yes | `InTransactionAsync` may call it after another caller already started; do not blow up |
| `SaveChangesAsync` clears the transaction handle | yes | so a follow-up call without re-starting fails cleanly |
| `RollbackAsync` is no-op when there is no transaction | yes | otherwise `InTransactionAsync` can double-rollback during stack unwind |
| Cancellation comes from `IAxisMediator.CancellationToken` | recommended | matches the bundled adapter and every other Axis package |
| Trace via `IAxisTelemetry` and log via `IAxisLogger` | recommended | observability stays uniform |

---

## Real-world example — an in-memory test double

```csharp
public class FakeUnitOfWork : IAxisUnitOfWork
{
    public bool Started   { get; private set; }
    public bool Committed { get; private set; }
    public bool RolledBack{ get; private set; }

    public Task<AxisResult> StartAsync()       { Started   = true;  return Task.FromResult(AxisResult.Ok()); }
    public Task<AxisResult> SaveChangesAsync() { Committed = true;  return Task.FromResult(AxisResult.Ok()); }
    public Task<AxisResult> RollbackAsync()    { RolledBack = true; return Task.FromResult(AxisResult.Ok()); }
    public Task ReleaseConnectionAsync()       => Task.CompletedTask;

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

// in a test
services.AddScoped<IAxisUnitOfWork, FakeUnitOfWork>();
// later
Assert.True(uow.Started);
Assert.True(uow.Committed);
Assert.False(uow.RolledBack);
```

**Why it pays off:** the handler is `InTransactionAsync`-driven, but the test does not need a database. The fake unit of work records every call for assertion — and you can flip a property to simulate a `Save` failure if you want to test the rollback path.

---

## See also

- [The `IAxisUnitOfWork` contract](iaxisunitofwork.md) — the abstraction you must implement
- [`InTransactionAsync`](in-transaction.md) — the state machine your primitives have to satisfy
- [Postgres adapter](postgres-adapter.md) — the in-box reference
- [MySQL adapter](mysql-adapter.md) — the other in-box reference, same shared base

---

↩ [Back to AxisRepository docs](README.md)
