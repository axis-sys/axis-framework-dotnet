# Adapter Postgres · `AxisRepository.Postgres`

> A implementação embarcada de `IAxisUnitOfWork` sobre `Npgsql`. Adiciona `IPostgresUnitOfWork` para construção crua de `NpgsqlCommand`, traça cada operação via `IAxisTelemetry` e expõe um provider keyed-DI para setups multi-database.

```csharp
services.AddPostgresUnitOfWork(serviceKey: "main", connectionString: "Host=...");
```

---

## Quando usar

PostgreSQL — seu próprio servidor, RDS, Aurora, Cloud SQL, qualquer coisa que fale o protocolo. Pareie com `AddPostgresDbRepository` — o executor `IAxisDbRepository` pronto que seus repositórios compõem — para o boilerplate de binding de parâmetros + retry.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| mirar SQL Server, SQLite | um [adapter custom](custom-adapter.md) sobre o driver ADO.NET respectivo |
| usar um ORM | um [adapter custom](custom-adapter.md) sobre EF Core / Dapper / Linq2Db |
| compartilhar estado entre databases | um `IAxisUnitOfWork` por database, mais uma orquestração de nível mais alto |

---

## `IPostgresUnitOfWork`

```csharp
public interface IPostgresUnitOfWork : IAxisUnitOfWork
{
    Task<NpgsqlCommand> NewCommandAsync(string sql);
}
```

`NewCommandAsync` retorna um `NpgsqlCommand` já anexado à `NpgsqlConnection` e `NpgsqlTransaction` atuais. Se você chamar antes de `StartAsync`, a implementação abre os dois transparentemente. É isso que `PostgresRepositoryBase` consome — mas você pode usar direto quando precisar de um comando cru (bulk insert, `COPY`, feature específica do fornecedor).

---

## Como `StartAsync` / `SaveChangesAsync` / `RollbackAsync` mapeiam para Postgres

Lendo `PostgresUnitOfWork` direto:

| Método | O que faz | Em falha |
|---|---|---|
| `StartAsync` | `dataSource.OpenConnectionAsync(ct)` (idempotente) + `connection.BeginTransactionAsync(ct)` | loga via `IAxisLogger`, retorna `Error("POSTGRES_ERROR_STARTING_CONNECTION")` |
| `SaveChangesAsync` | `transaction.CommitAsync(ct)` + limpa o handle da transação em memória | loga + retorna `Error("POSTGRES_SAVING_CHANGES_ERROR")` (ou `POSTGRES_TRANSACTION_NOT_STARTED` se não tiver nenhum) |
| `RollbackAsync` | `transaction?.RollbackAsync(ct)` (no-op quando não tem) | loga + retorna `Error("POSTGRES_ROLLBACK_ERROR")` |

Todo método abre um span `IAxisTelemetry` tag-eado `db.system = "postgresql"` e grava exceções nele. O cancelamento vem de `mediator.CancellationToken`.

---

## O que é registrado

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

- `NpgsqlDataSource` é registrado como **singleton** (um pool para o app), com conexões **scoped** (uma por requisição).
- `PostgresUnitOfWorkProvider` cacheia um `PostgresUnitOfWork` por `serviceKey` para que `IAxisUnitOfWork` e `IPostgresUnitOfWork` resolvam para a **mesma** instância dentro de um scope — leitura e escrita pela mesma transação.

> O provider é registrado por service key. Múltiplos databases usam múltiplas keys; cada provider tem seu próprio cache. Veja [Keyed multi-database](keyed-multi-database.md).

E `DependencyInjection.AddPostgresDbRepository(serviceKey)` registra o executor pronto:

```csharp
public static void AddPostgresDbRepository(this IServiceCollection services, string serviceKey)
{
    services.AddScoped<IAxisDbRepository>(sp => new PostgresDbRepository(
        sp.GetRequiredService<IAxisMediator>(),
        sp.GetRequiredService<IAxisLogger<PostgresRepositoryBase>>(),
        sp.GetRequiredKeyedService<IPostgresUnitOfWork>(serviceKey)));
}
```

É esse `IAxisDbRepository` que um repositório agnóstico de provider compõe por construtor — a mesma classe roda em MySQL trocando o registro por `AddMySqlDbRepository`.

---

## Exemplo real — fiação de DI

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

**Por que compensa:** o handler depende de `IAxisUnitOfWork`, não de `PostgresUnitOfWork` ou `NpgsqlConnection`. Troque para SQL Server com um adapter custom e o handler não muda.

---

## Veja também

- [O contrato `IAxisUnitOfWork`](iaxisunitofwork.md) — a abstração que o adapter implementa
- [Adapter MySQL](mysql-adapter.md) — o dialeto irmão, mesma base compartilhada de retry/fault
- [Repository base](repository-base.md) — `ExecuteAsync` / `GetAsync` / `ListAsync` em cima do `NewCommandAsync`
- [DDL de schema](ddl.md) — este adapter embarca `PostgresSqlDialect`, o `IAxisSqlDialect` do Postgres
- [Migrations](migrations.md) — este adapter embarca `PostgresMigrationRunner`
- [Keyed multi-database](keyed-multi-database.md) — plugue dois ou mais databases
- [Adapter custom](custom-adapter.md) — escreva um para outro store

---

↩ [Voltar à documentação do AxisRepository](README.md)
