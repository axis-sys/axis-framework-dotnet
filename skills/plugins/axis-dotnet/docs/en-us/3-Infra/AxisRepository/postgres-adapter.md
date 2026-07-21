# Postgres adapter · `AxisRepository.Postgres`

> The bundled implementation of `IAxisUnitOfWork` over `Npgsql`. Adds `IPostgresUnitOfWork` for raw `NpgsqlCommand` construction, traces every operation through `IAxisTelemetry`, and exposes a keyed-DI provider for multi-database setups.

```csharp
services.AddPostgresUnitOfWork(serviceKey: "main", connectionString: "Host=...");
```

---

## When to use

PostgreSQL — your own server, RDS, Aurora, Cloud SQL, anything that speaks the protocol. Pair with `AddPostgresDbRepository` — the ready-made `IAxisDbRepository` executor your repositories compose — for the parameter-binding + retry boilerplate.

## When *not* to use

| You want to… | Use instead |
|---|---|
| target SQL Server, SQLite | a [custom adapter](custom-adapter.md) over the respective ADO.NET driver |
| use an ORM | a [custom adapter](custom-adapter.md) over EF Core / Dapper / Linq2Db |
| share state across databases | one `IAxisUnitOfWork` per database, plus a higher-level orchestration |

---

## `IPostgresUnitOfWork`

```csharp
public interface IPostgresUnitOfWork : IAxisUnitOfWork
{
    Task<NpgsqlCommand> NewCommandAsync(string sql);
}
```

`NewCommandAsync` returns an `NpgsqlCommand` already attached to the current `NpgsqlConnection` and `NpgsqlTransaction`. If you call it before `StartAsync`, the implementation opens both transparently. This is what `PostgresRepositoryBase` consumes — but you can use it directly when you need a raw command (bulk insert, `COPY`, vendor-specific feature).

---

## How `StartAsync` / `SaveChangesAsync` / `RollbackAsync` map to Postgres

Reading `PostgresUnitOfWork` directly:

| Method | What it does | On failure |
|---|---|---|
| `StartAsync` | `dataSource.OpenConnectionAsync(ct)` (idempotent) + `connection.BeginTransactionAsync(ct)` | logs via `IAxisLogger`, returns `Error("POSTGRES_ERROR_STARTING_CONNECTION")` |
| `SaveChangesAsync` | `transaction.CommitAsync(ct)` + clears the in-memory transaction handle | logs + returns `Error("POSTGRES_SAVING_CHANGES_ERROR")` (or `POSTGRES_TRANSACTION_NOT_STARTED` if there is none) |
| `RollbackAsync` | `transaction?.RollbackAsync(ct)` (no-op when there is none) | logs + returns `Error("POSTGRES_ROLLBACK_ERROR")` |

Every method opens an `IAxisTelemetry` span tagged `db.system = "postgresql"` and records exceptions on it. Cancellation comes from `mediator.CancellationToken`.

---

## What gets registered

`DependencyInjection.AddPostgresUnitOfWork`:

```csharp
public static void AddPostgresUnitOfWork(this IServiceCollection services, string serviceKey, string connectionString)
{
    services.AddNpgsqlDataSource(connectionString,
        connectionLifetime: ServiceLifetime.Scoped,
        dataSourceLifetime: ServiceLifetime.Singleton,
        serviceKey: serviceKey);

    services.AddKeyedScoped<PostgresUnitOfWorkProvider>(serviceKey);
    services.AddKeyedScoped<IAxisUnitOfWork>(serviceKey, (sp, key) => sp.GetRequiredKeyedService<PostgresUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
    services.AddKeyedScoped<IPostgresUnitOfWork>(serviceKey, (sp, key) => sp.GetRequiredKeyedService<PostgresUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
}
```

- `NpgsqlDataSource` is registered as a **singleton** (one pool for the app), with **scoped** connections (one per request).
- `PostgresUnitOfWorkProvider` caches a `PostgresUnitOfWork` per `serviceKey` so `IAxisUnitOfWork` and `IPostgresUnitOfWork` resolve to the **same** instance inside a scope — read and write through the same transaction.

> The provider is registered per service key. Multiple databases use multiple keys; each provider has its own cache. See [Keyed multi-database](keyed-multi-database.md).

And `DependencyInjection.AddPostgresDbRepository(serviceKey)` registers the ready-made executor:

```csharp
public static void AddPostgresDbRepository(this IServiceCollection services, string serviceKey)
{
    services.AddScoped<IAxisDbRepository>(sp => new PostgresDbRepository(
        sp.GetRequiredService<IAxisMediator>(),
        sp.GetRequiredService<IAxisLogger<PostgresRepositoryBase>>(),
        sp.GetRequiredKeyedService<IPostgresUnitOfWork>(serviceKey)));
}
```

This `IAxisDbRepository` is what a provider-agnostic repository composes by constructor — the same class runs on MySQL by swapping the registration for `AddMySqlDbRepository`.

---

## Real-world example — DI wiring

```csharp
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddAxisTelemetry()
    .AddPostgresUnitOfWork(
        serviceKey:       "main",
        connectionString: builder.Configuration.GetConnectionString("Postgres")!);
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

**Why it pays off:** the handler depends on `IAxisUnitOfWork`, not on `PostgresUnitOfWork` or `NpgsqlConnection`. Swap to SQL Server with a custom adapter and the handler does not change.

---

## See also

- [The `IAxisUnitOfWork` contract](iaxisunitofwork.md) — the abstraction the adapter implements
- [MySQL adapter](mysql-adapter.md) — the sibling dialect, same shared retry/fault base
- [Repository base](repository-base.md) — `ExecuteAsync` / `GetAsync` / `ListAsync` on top of `NewCommandAsync`
- [Schema DDL](ddl.md) — this adapter ships `PostgresSqlDialect`, the Postgres `IAxisSqlDialect`
- [Migrations](migrations.md) — this adapter ships `PostgresMigrationRunner`
- [Keyed multi-database](keyed-multi-database.md) — wire two or more databases
- [Custom adapter](custom-adapter.md) — write one for another store

---

↩ [Back to AxisRepository docs](README.md)
