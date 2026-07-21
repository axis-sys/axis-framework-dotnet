# Contract · `IAxisUnitOfWork`

> Four primitives — open the transaction, commit, roll back, release the connection — plus two default-interface helpers (`InTransactionAsync`) that orchestrate the trio against an `AxisResult`-aware delegate.

```csharp
public interface IAxisUnitOfWork : IDisposable, IAsyncDisposable
{
    Task<AxisResult> StartAsync();
    Task<AxisResult> SaveChangesAsync();
    Task<AxisResult> RollbackAsync();
    Task ReleaseConnectionAsync();

    Task<AxisResult>    InTransactionAsync(Func<Task<AxisResult>> work);
    Task<AxisResult<T>> InTransactionAsync<T>(Func<Task<AxisResult<T>>> work);
}
```

---

## When to use

Whenever a use case writes to the database and you want the write to be atomic with the rest of the pipeline. Pair it with command handlers, integration handlers, sagas — anywhere `SaveChangesAsync` would belong in EF Core.

## When *not* to use

| You want to… | Use instead |
|---|---|
| **read** without writing | call the repository directly; no transaction needed for a single read |
| run a workflow that **spans services** | a [`Saga`](../../2-ApplicationFlow/AxisSaga/README.md) with compensations |
| share a transaction between two stores (Postgres + Redis) | a custom adapter that coordinates them |

---

## The four primitives

| Method | What it does | Returns `IsFailure` when |
|---|---|---|
| `StartAsync` | opens (or reuses) the connection and begins a transaction | the connection / `BEGIN` failed |
| `SaveChangesAsync` | commits the transaction and forgets it | the `COMMIT` failed or the transaction was already cleared |
| `RollbackAsync` | rolls back the transaction (no-op if it was never started) | the `ROLLBACK` failed |
| `ReleaseConnectionAsync` | returns the currently held connection (rolling back any uncommitted work) to the pool, so a slow external call mid-unit-of-work doesn't pin a pooled connection and an open transaction idle across it — the next command transparently reopens a fresh connection and transaction | never fails; it's a `Task`, not an `AxisResult` — has no interface default, so every implementer must supply one; implementations that don't pool a connection simply return `Task.CompletedTask` |

Disposing the unit of work disposes the underlying connection — there is no leftover state across requests because the DI scope is per-request.

---

## The default-interface wrappers

Reading `IAxisUnitOfWork.InTransactionAsync` directly:

```csharp
async Task<AxisResult> InTransactionAsync(Func<Task<AxisResult>> work)
{
    var start = await StartAsync();
    if (start.IsFailure) return start;

    try
    {
        var result = await work();
        if (result.IsFailure)
        {
            await RollbackAsync();
            return result;
        }
        return await SaveChangesAsync();
    }
    catch
    {
        await RollbackAsync();
        throw;
    }
}
```

| Outcome of `work()` | Result | Side effect |
|---|---|---|
| `Ok()` (or `Ok(value)`) | `await SaveChangesAsync()` | commit |
| `Error(errors)` | the same `Error(errors)` | rollback (return value is the work's failure, not the commit's) |
| an exception | rethrows the exception | rollback then rethrow |
| `StartAsync` itself fails | propagated as `Error` | no `work` is run |

The generic overload `InTransactionAsync<T>` adds one extra wrinkle: when `SaveChangesAsync` fails after a successful `work()`, it returns the **save's errors** (because the value never reached durability).

---

## Real-world examples

### 1. Persist + publish — *inside* the transaction

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => uow.InTransactionAsync(() =>
        factory.CreateAsync(cmd)
            .ThenAsync(order => writer.CreateAsync(order))
            .ThenAsync(_     => outboxBus.PublishAsync(new OrderCreatedEvent(cmd.OrderId)))
            .MapAsync(_      => new CreateOrderResponse { OrderId = cmd.OrderId }));
```

**Why it pays off:** the `OutboxBusAdapter` writes to an outbox table inside the same transaction. Commit = both the order and the event land atomically; rollback = neither persists.

### 2. Read, validate, then write — short-circuit on the failure rail

```csharp
public Task<AxisResult> HandleAsync(UpdatePersonCommand cmd)
    => uow.InTransactionAsync(() =>
        reader.GetByIdAsync(cmd.PersonId)
            .ThenAsync(person => person.UpdateAsync(cmd))
            .ThenAsync(person => writer.UpdateAsync(person)));
```

**Why it pays off:** if `GetByIdAsync` returns `NotFound`, the pipeline short-circuits and `InTransactionAsync` rolls back — the empty transaction simply commits nothing.

### 3. Catch an unexpected exception cleanly

```csharp
public Task<AxisResult> HandleAsync(BackfillProjectionCommand cmd)
    => uow.InTransactionAsync(() => projection.RebuildAsync(cmd));
```

If `RebuildAsync` throws (`OutOfMemoryException`, a Postgres exception that escapes the repository base, whatever), `InTransactionAsync` rolls back and rethrows. The exception still travels up the stack — but at least the database is consistent.

---

## See also

- [`InTransactionAsync`](in-transaction.md) — the wrapper, in depth
- [Postgres adapter](postgres-adapter.md) — a bundled implementation
- [MySQL adapter](mysql-adapter.md) — the other bundled implementation
- [Repository base](repository-base.md) — `ExecuteAsync`/`GetAsync`/`ListAsync`

---

↩ [Back to AxisRepository docs](README.md)
