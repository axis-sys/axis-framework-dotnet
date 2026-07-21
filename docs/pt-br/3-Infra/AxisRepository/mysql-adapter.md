# Adapter MySQL · `AxisRepository.MySql`

> A implementação embarcada de `IAxisUnitOfWork` sobre `MySqlConnector`. Adiciona `IMySqlUnitOfWork` para construção crua de `MySqlCommand`, compartilha a mesma maquinaria de retry/fault do adapter Postgres via `AxisRepositoryBase`, e expõe um provider keyed-DI para setups multi-database.

```csharp
services.AddMySqlUnitOfWork(serviceKey: "main", connectionString: "Server=...");
```

---

## Quando usar

MySQL — seu próprio servidor, RDS, Aurora MySQL, PlanetScale, qualquer coisa que fale o protocolo via `MySqlConnector`. Pareie com `AddMySqlDbRepository` — o executor `IAxisDbRepository` pronto que seus repositórios compõem — para o boilerplate de binding de parâmetros + retry: a maquinaria compartilhada é a mesma do Postgres, então um repositório composto roda idêntico contra os dois dialetos.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| mirar SQL Server, SQLite, Mongo | um [adapter custom](custom-adapter.md) sobre o driver respectivo |
| usar um ORM | um [adapter custom](custom-adapter.md) sobre EF Core / Dapper / Linq2Db |
| compartilhar estado entre databases | um `IAxisUnitOfWork` por database, mais uma orquestração de nível mais alto |

---

## `IMySqlUnitOfWork`

```csharp
public interface IMySqlUnitOfWork : IDbUnitOfWork<MySqlCommand>;
```

`IDbUnitOfWork<TCommand>` é o seam de dialeto compartilhado — o mesmo que `IPostgresUnitOfWork` fecha sobre `NpgsqlCommand` — então `IMySqlUnitOfWork` só fixa o tipo do comando. Ele carrega `NewCommandAsync(string sql)`, `IsFaulted`/`MarkFaulted()` e `HasUncommittedWrites`/`MarkWrite()`, todos consumidos por `MySqlRepositoryBase`. Chame `NewCommandAsync` direto quando precisar de um comando cru (bulk load, statement específico do fornecedor).

---

## Como `StartAsync` / `SaveChangesAsync` / `RollbackAsync` / `ReleaseConnectionAsync` mapeiam para MySQL

Lendo `MySqlUnitOfWork` direto:

| Método | O que faz | Em falha |
|---|---|---|
| `StartAsync` | `dataSource.OpenConnectionAsync(ct)` (idempotente) + `connection.BeginTransactionAsync(ct)` | loga via `IAxisLogger`, retorna `Error("MYSQL_ERROR_STARTING_CONNECTION")` |
| `SaveChangesAsync` | `transaction.CommitAsync(ct)` + limpa o handle da transação em memória | loga + retorna `Error("MYSQL_SAVING_CHANGES_ERROR")` (ou `MYSQL_TRANSACTION_NOT_STARTED` / `MYSQL_TRANSACTION_FAULTED` se não tiver nenhuma / estiver abortada) |
| `RollbackAsync` | `transaction?.RollbackAsync(ct)` (no-op quando não tem) | loga + retorna `Error("MYSQL_ROLLBACK_ERROR")` |
| `ReleaseConnectionAsync` | reverte qualquer trabalho não commitado e devolve a conexão ao pool | best-effort; loga e ainda libera a conexão num `finally` se o próprio rollback lançar |

Todo método abre um span `IAxisTelemetry` tag-eado `db.system = "mysql"` e grava exceções nele. O cancelamento vem de `mediator.CancellationToken`.

`MySqlRepositoryBase` mapeia números de `MySqlException` específicos do MySQL para o contrato compartilhado de retry/fault: `1062` (duplicate entry) → `Conflict($"{prefix}_DUPLICATE_KEY_ERROR")`, `1146`/`1049` (no such table / unknown database) → um `ServiceUnavailable("MYSQL_SCHEMA_NOT_READY")` transiente (o estado esperado antes das migrations rodarem), qualquer outra coisa transiente (deadlock, lock-wait-timeout, instabilidade de conexão) é retentada até 4 vezes com backoff `[100, 200, 400, 1000]` ms — mas só enquanto o unit of work **ainda não tem escrita não commitada**; assim que uma escrita desembarcou, o erro transiente é exposto em vez de retentado no lugar. Veja [Repository base](repository-base.md) para a maquinaria completa de retry/fault compartilhada que os dois dialetos rodam.

---

## O que é registrado

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

- `MySqlDataSource` é registrado como **keyed singleton** (um pool por key), com units of work **scoped** por key.
- `MySqlUnitOfWorkProvider` cacheia um `MySqlUnitOfWork` por `serviceKey` para que `IAxisUnitOfWork` e `IMySqlUnitOfWork` resolvam para a **mesma** instância dentro de um scope — leitura e escrita pela mesma transação. Ele espelha `PostgresUnitOfWorkProvider` exatamente.
- `AddMySqlUnitOfWork` lança `InvalidOperationException` se `serviceKey` for nulo/vazio — uma key é obrigatória, mesmo para um único database.

> O provider é registrado por service key. Múltiplos databases usam múltiplas keys; cada provider tem seu próprio cache. Veja [Keyed multi-database](keyed-multi-database.md).

---

## Exemplo real — fiação de DI

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

**Por que compensa:** o handler depende de `IAxisUnitOfWork`, não de `MySqlUnitOfWork` ou `MySqlConnection`. Este é exatamente o mesmo handler mostrado na documentação do [adapter Postgres](postgres-adapter.md) — trocar de dialeto nunca toca o código de aplicação.

---

## Veja também

- [O contrato `IAxisUnitOfWork`](iaxisunitofwork.md) — a abstração que o adapter implementa
- [Adapter Postgres](postgres-adapter.md) — o adapter irmão; mesma base compartilhada de retry/fault, hooks de dialeto diferentes
- [Repository base](repository-base.md) — `ExecuteAsync` / `GetAsync` / `ListAsync` e a maquinaria compartilhada de retry/fault
- [DDL de schema](ddl.md) — este adapter embarca `MySqlSqlDialect`, o `IAxisSqlDialect` do MySQL
- [Migrations](migrations.md) — este adapter embarca `MySqlMigrationRunner`
- [Keyed multi-database](keyed-multi-database.md) — plugue dois ou mais databases
- [Adapter custom](custom-adapter.md) — escreva um para outro store

---

↩ [Voltar à documentação do AxisRepository](README.md)
