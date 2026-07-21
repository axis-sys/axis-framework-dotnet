# `InTransactionAsync` — the right transaction wrapper

> Default-interface helpers on `IAxisUnitOfWork` that orchestrate `StartAsync` / `SaveChangesAsync` / `RollbackAsync` against an `AxisResult`-aware delegate. Commit on `Ok`, rollback on `Error`, rollback-then-rethrow on an exception.

```csharp
return uow.InTransactionAsync(() =>
    factory.CreateAsync(cmd)
        .ThenAsync(person => writer.CreateAsync(person))
        .MapAsync(_       => new CreatePersonResponse { PersonId = cmd.PersonId }));
```

---

## When to use

Any pipeline that **writes** and that should be atomic. Pair with `Then`/`Map`/`Tap` from `AxisResult` to chain the steps; let `InTransactionAsync` watch the result.

## When *not* to use

| You want to… | Use instead |
|---|---|
| run a read-only pipeline | call the repository directly; no transaction needed |
| commit a partial result on failure | the three primitives manually (`StartAsync` / `SaveChangesAsync` / `RollbackAsync`) — be very deliberate |
| span the transaction across two stores | a custom adapter; one `IAxisUnitOfWork` cannot coordinate Postgres + Mongo |

---

## The two overloads

| Overload | Returns | Notes |
|---|---|---|
| `InTransactionAsync(Func<Task<AxisResult>>)` | `Task<AxisResult>` | for pipelines that yield no value (commands without a typed response) |
| `InTransactionAsync<T>(Func<Task<AxisResult<T>>>)` | `Task<AxisResult<T>>` | preserves the typed value on the way out; if `SaveChangesAsync` fails it returns the **save's** errors |

---

## The state machine

| State | Returns | Side effect |
|---|---|---|
| `StartAsync` fails | the start's `Error(errors)` | no `work` runs |
| `work()` returns `Ok` and `SaveChangesAsync` succeeds | `Ok` (or `Ok(value)`) | committed |
| `work()` returns `Ok` and `SaveChangesAsync` fails | the save's `Error(errors)` (value is lost in the typed overload) | nothing committed |
| `work()` returns `Error` | the work's `Error(errors)` | rollback |
| `work()` throws | rethrows | rollback, then rethrow |

---

## Real-world examples

### 1. Two writes in one transaction

```csharp
public Task<AxisResult<CreateInvoiceResponse>> HandleAsync(CreateInvoiceCommand cmd)
    => uow.InTransactionAsync(() =>
        invoiceFactory.CreateAsync(cmd)
            .ThenAsync(invoice => invoiceWriter.CreateAsync(invoice))
            .ThenAsync(invoice => ledgerWriter.PostAsync(invoice))
            .MapAsync(invoice => new CreateInvoiceResponse { InvoiceId = invoice.InvoiceId }));
```

**Why it pays off:** the invoice and its ledger entry land atomically. A failure in `ledgerWriter.PostAsync` rolls the invoice back, no orphaned rows.

### 2. Transactional outbox

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => uow.InTransactionAsync(() =>
        factory.CreateAsync(cmd)
            .ThenAsync(order => writer.CreateAsync(order))
            .ThenAsync(_     => outboxBus.PublishAsync(new OrderCreatedEvent(cmd.OrderId)))
            .MapAsync(_      => new CreateOrderResponse { OrderId = cmd.OrderId }));
```

**Why it pays off:** the bus is an outbox adapter — `PublishAsync` writes a row in the same connection. Commit = both rows; rollback = neither. The classic dual-write race vanishes.

### 3. Validate inside the transaction (cheap reads + writes)

```csharp
public Task<AxisResult> HandleAsync(TransferFundsCommand cmd)
    => uow.InTransactionAsync(() =>
        accountReader.GetForUpdateAsync(cmd.FromAccountId)
            .ThenAsync(from => from.DebitAsync(cmd.Amount))
            .ThenAsync(_    => accountReader.GetForUpdateAsync(cmd.ToAccountId))
            .ThenAsync(to   => to.CreditAsync(cmd.Amount))
            .ThenAsync(to   => accountWriter.UpdateAsync(to)));
```

**Why it pays off:** the `FOR UPDATE` locks travel inside one transaction. A failed debit (insufficient funds) rolls back the entire workflow — and the credit never lands without the debit.

---

## See also

- [The `IAxisUnitOfWork` contract](iaxisunitofwork.md) — the primitives the wrapper calls
- [Postgres adapter](postgres-adapter.md) — how `StartAsync`/`SaveChangesAsync`/`RollbackAsync` map to Postgres

---

↩ [Back to AxisRepository docs](README.md)
