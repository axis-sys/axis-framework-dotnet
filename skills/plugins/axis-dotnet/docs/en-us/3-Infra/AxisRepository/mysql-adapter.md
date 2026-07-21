# MySQL adapter · `AxisRepository.MySql`

> The bundled implementation of `IAxisUnitOfWork` over `MySqlConnector`. Adds `IMySqlUnitOfWork` for raw `MySqlCommand` construction, shares the same retry/fault machinery as the Postgres adapter through `AxisRepositoryBase`, and exposes a keyed-DI provider for multi-database setups.

```csharp
services.AddMySqlUnitOfWork(serviceKey: "main", connectionString: "Server=...");
```

---

## When to use

MySQL — your own server, RDS, Aurora MySQL, PlanetScale, anything that speaks the protocol via `MySqlConnector`. Pair with `AddMySqlDbRepository` — the ready-made `IAxisDbRepository` executor your repositories compose — for the parameter-binding + retry boilerplate: the shared machinery is the same as Postgres, so a composed repository runs identically against both dialects.

## When *not* to use

| You want to… | Use instead |
|---|---|
| target SQL Server, SQLite, Mongo | a [custom adapter](custom-adapter.md) over the respective driver |
| use an ORM | a [custom adapter](custom-adapter.md) over EF Core / Dapper / Linq2Db |
| share state across databases | one `IAxisUnitOfWork` per database, plus a higher-level orchestration |

---

## `IMySqlUnitOfWork`

```csharp
public interface IMySqlUnitOfWork : IDbUnitOfWork<MySqlCommand>;
```

`IDbUnitOfWork<TCommand>` is the shared dialect seam — the same one `IPostgresUnitOfWork` closes over `NpgsqlCommand` — so `IMySqlUnitOfWork` only fixes the command type. It carries `NewCommandAsync(string sql)`, `IsFaulted`/`MarkFaulted()` and `HasUncommittedWrites`/`MarkWrite()`, all consumed by `MySqlRepositoryBase`. Call `NewCommandAsync` directly when you need a raw command (bulk load, vendor-specific statement).

---

## How `StartAsync` / `SaveChangesAsync` / `RollbackAsync` / `ReleaseConnectionAsync` map to MySQL

Reading `MySqlUnitOfWork` directly:

| Method | What it does | On failure |
|---|---|---|
| `StartAsync` | `dataSource.OpenConnectionAsync(ct)` (idempotent) + `connection.BeginTransactionAsync(ct)` | logs via `IAxisLogger`, returns `Error("MYSQL_ERROR_STARTING_CONNECTION")` |
| `SaveChangesAsync` | `transaction.CommitAsync(ct)` + clears the in-memory transaction handle | logs + returns `Error("MYSQL_SAVING_CHANGES_ERROR")` (or `MYSQL_TRANSACTION_NOT_STARTED` / `MYSQL_TRANSACTION_FAULTED` if there is none / it's aborted) |
| `RollbackAsync` | `transaction?.RollbackAsync(ct)` (no-op when there is none) | logs + returns `Error("MYSQL_ROLLBACK_ERROR")` |
| `ReleaseConnectionAsync` | rolls back any uncommitted work and disposes the connection back to the pool | best-effort; logs and still releases the connection in a `finally` if the rollback itself throws |

Every method opens an `IAxisTelemetry` span tagged `db.system = "mysql"` and records exceptions on it. Cancellation comes from `mediator.CancellationToken`.

`MySqlRepositoryBase` maps MySQL-specific `MySqlException` numbers to the shared retry/fault contract: `1062` (duplicate entry) → `Conflict($"{prefix}_DUPLICATE_KEY_ERROR")`, `1146`/`1049` (no such table / unknown database) → a transient `ServiceUnavailable("MYSQL_SCHEMA_NOT_READY")` (the expected state before migrations run), anything else transient (deadlock, lock-wait-timeout, connection blips) is retried up to 4 times with `[100, 200, 400, 1000]` ms backoff — but only while the unit of work has **no uncommitted write yet**; once a write has landed, the transient error is surfaced instead of retried in place. See [Repository base](repository-base.md) for the full shared retry/fault machinery both dialects run through.

---

## What gets registered

`DependencyInjection.AddMySqlUnitOfWork` + `AddMySqlDbRepository`:

```csharp
public static void AddMySqlUnitOfWork(this IServiceCollection services, string serviceKey, string connectionString)
{
    services.AddKeyedSingleton<MySqlDataSource>(serviceKey, (_, _) => new MySqlDataSource(connectionString));
    services.AddKeyedScoped<MySqlUnitOfWorkProvider>(serviceKey);
    services.AddKeyedScoped<IAxisUnitOfWork>(serviceKey, (sp, key) => sp.GetRequiredKeyedService<MySqlUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
    services.AddKeyedScoped<IMySqlUnitOfWork>(serviceKey, (sp, key) => sp.GetRequiredKeyedService<MySqlUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
}

public static void AddMySqlDbRepository(this IServiceCollection services, string serviceKey)
{
    services.AddScoped<IAxisDbRepository>(sp => new MySqlDbRepository(
        sp.GetRequiredService<IAxisMediator>(),
        sp.GetRequiredService<IAxisLogger<MySqlRepositoryBase>>(),
        sp.GetRequiredKeyedService<IMySqlUnitOfWork>(serviceKey)));
}
```

- `MySqlDataSource` is registered as a **keyed singleton** (one pool per key), with **scoped** per-key units of work.
- `MySqlUnitOfWorkProvider` caches a `MySqlUnitOfWork` per `serviceKey` so `IAxisUnitOfWork` and `IMySqlUnitOfWork` resolve to the **same** instance inside a scope — read and write through the same transaction. It mirrors `PostgresUnitOfWorkProvider` exactly.
- `AddMySqlUnitOfWork` throws `InvalidOperationException` if `serviceKey` is null/blank — a key is mandatory, even for a single database.

> The provider is registered per service key. Multiple databases use multiple keys; each provider has its own cache. See [Keyed multi-database](keyed-multi-database.md).

---

## Real-world example — DI wiring

```csharp
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddAxisTelemetry();

builder.Services.AddMySqlUnitOfWork(
    serviceKey:       "main",
    connectionString: builder.Configuration.GetConnectionString("MySql")!);

builder.Services.AddMySqlDbRepository(serviceKey: "main");
```

```csharp
public class CreatePersonHandler(
    [FromKeyedServices("main")] IAxisUnitOfWork uow,
    PersonFactory factory,
    PersonRepository writer)
{
    public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
        => uow.InTransactionAsync(() =>
            factory.CreateAsync(cmd)
                .ThenAsync(person => writer.CreateAsync(person))
                .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId }));
}
```

**Why it pays off:** the handler depends on `IAxisUnitOfWork`, not on `MySqlUnitOfWork` or `MySqlConnection`. This is the exact same handler shown in the [Postgres adapter](postgres-adapter.md) docs — swapping dialects never touches application code.

---

## See also

- [The `IAxisUnitOfWork` contract](iaxisunitofwork.md) — the abstraction the adapter implements
- [Postgres adapter](postgres-adapter.md) — the sibling adapter; same shared retry/fault base, different dialect hooks
- [Repository base](repository-base.md) — `ExecuteAsync` / `GetAsync` / `ListAsync` and the shared retry/fault machinery
- [Schema DDL](ddl.md) — this adapter ships `MySqlSqlDialect`, the MySQL `IAxisSqlDialect`
- [Migrations](migrations.md) — this adapter ships `MySqlMigrationRunner`
- [Keyed multi-database](keyed-multi-database.md) — wire two or more databases
- [Custom adapter](custom-adapter.md) — write one for another store

---

↩ [Back to AxisRepository docs](README.md)
