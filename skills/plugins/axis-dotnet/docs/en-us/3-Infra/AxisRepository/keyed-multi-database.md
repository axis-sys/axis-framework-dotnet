# Keyed multi-database

> `AddPostgresUnitOfWork(serviceKey, connectionString)` accepts a key, so you can wire **more than one database** into the same app and resolve each unit of work via `[FromKeyedServices(key)]`.

```csharp
services.AddPostgresUnitOfWork(serviceKey: "main",     connectionString: mainCs);
services.AddPostgresUnitOfWork(serviceKey: "auditing", connectionString: auditCs);

public class TransferHandler(
    [FromKeyedServices("main")]     IAxisUnitOfWork mainUow,
    [FromKeyedServices("auditing")] IAxisUnitOfWork auditUow) { /* ... */ }
```

---

## When to use

- A **separate database for auditing / read models / analytics**.
- A **per-tenant database** model where the key is the tenant id.
- A **migration** scenario where you read from the old database and write to the new one.

## When *not* to use

| You want to… | Use instead |
|---|---|
| stay on one database | a single `AddPostgresUnitOfWork(serviceKey: "main", …)` call |
| coordinate **distributed transactions** | a saga with compensations — no `IAxisUnitOfWork` can two-phase commit Postgres + Postgres |

---

## How keyed registration works

Reading `DependencyInjection.AddPostgresUnitOfWork`:

```csharp
services.AddNpgsqlDataSource(connectionString, …, serviceKey: serviceKey);
services.AddKeyedScoped<PostgresUnitOfWorkProvider>(serviceKey);
services.AddKeyedScoped<IAxisUnitOfWork>     (serviceKey, (sp, key) => sp.GetRequiredKeyedService<PostgresUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
services.AddKeyedScoped<IPostgresUnitOfWork> (serviceKey, (sp, key) => sp.GetRequiredKeyedService<PostgresUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
```

- Each `AddPostgresUnitOfWork(key, cs)` call registers an `NpgsqlDataSource` **keyed** by `key`.
- A keyed `PostgresUnitOfWorkProvider` caches a `PostgresUnitOfWork` per key inside the scope.
- Both `IAxisUnitOfWork` and `IPostgresUnitOfWork` resolve to the same instance for that key — read and write through the same connection.

> **Important:** the resulting `IAxisUnitOfWork` is **scoped**. Two databases mean two unit-of-works, and one `InTransactionAsync` can only commit **its own**.

---

## Picking keys

A few useful conventions:

| Convention | Example |
|---|---|
| Logical-database name | `"main"`, `"audit"`, `"reporting"` |
| Per-tenant | `tenantKey` (the tenant key as a string) |
| Per-feature | `"payments"`, `"identity"` |

Whatever the key, callers must use the **same string** when injecting (`[FromKeyedServices("main")]`).

---

## Real-world examples

### 1. Two databases, two transactions

```csharp
public class CreateAuditedOrderHandler(
    [FromKeyedServices("main")]     IAxisUnitOfWork mainUow,
    [FromKeyedServices("auditing")] IAxisUnitOfWork auditUow,
    OrderWriter orderWriter,
    AuditWriter auditWriter)
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
        => mainUow.InTransactionAsync(() =>
            orderWriter.CreateAsync(cmd)
                .ThenAsync(order => auditUow.InTransactionAsync(() => auditWriter.RecordCreateAsync(order)).Map(_ => order))
                .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId }));
}
```

**Why it pays off:** each database commits on its own. The order persists atomically in `main`; the audit row persists atomically in `auditing`. A failure on the audit side does **not** roll the order back — read the trade-off carefully.

> If you need both to be atomic, you cannot — that is two-phase commit territory. Use a saga, or write the audit through an **outbox** on the main database and drain it asynchronously.

### 2. Per-tenant slicing

```csharp
public class PerTenantUowFactory(IServiceProvider sp)
{
    public IAxisUnitOfWork For(string tenantKey)
        => sp.GetRequiredKeyedService<IAxisUnitOfWork>(tenantKey);
}

// registration
foreach (var tenantKey in tenantKeys)
    services.AddPostgresUnitOfWork(serviceKey: tenantKey, connectionString: ConnectionFor(tenantKey));
```

**Why it pays off:** the same handler code works against every tenant database; the routing is a single keyed-DI lookup.

---

## See also

- [Postgres adapter](postgres-adapter.md) — what each `AddPostgresUnitOfWork` call wires
- [`InTransactionAsync`](in-transaction.md) — runs against one unit of work at a time
- [The `IAxisUnitOfWork` contract](iaxisunitofwork.md) — the abstraction the caller depends on

---

↩ [Back to AxisRepository docs](README.md)
