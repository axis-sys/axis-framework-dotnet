# AxisRepository — Documentação

> 🌐 [English (README principal)](../../../en-us/3-Infra/AxisRepository/README.md)

**Uma abstração de unit-of-work com `AxisResult` embutido** — `IAxisUnitOfWork` com três primitivas (`StartAsync`, `SaveChangesAsync`, `RollbackAsync`) e um `InTransactionAsync(work)` default-interface que orquestra as três. Os adapters embarcados `AxisRepository.Postgres` e `AxisRepository.MySql` trazem um builder de comando tipado, retry em erros transientes, detecção de conflito em violação de constraint única e um provider keyed-DI para setups multi-database.

```csharp
public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
    => uow.InTransactionAsync(() =>
        factory.CreateAsync(cmd)
            .ThenAsync(person => writer.CreateAsync(person))
            .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId }));
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### A interface em 60 segundos

```csharp
public interface IAxisUnitOfWork : IDisposable, IAsyncDisposable
{
    Task<AxisResult> StartAsync();
    Task<AxisResult> SaveChangesAsync();
    Task<AxisResult> RollbackAsync();

    Task<AxisResult>      InTransactionAsync(Func<Task<AxisResult>> work);
    Task<AxisResult<T>>   InTransactionAsync<T>(Func<Task<AxisResult<T>>> work);
}
```

`StartAsync` abre uma transação; `SaveChangesAsync` commita; `RollbackAsync` reverte. Os dois defaults `InTransactionAsync` embrulham as três primitivas — abrir, rodar o trabalho, commitar no sucesso, **fazer rollback na falha ou exceção** (e relançar a exceção). → **[O contrato `IAxisUnitOfWork`](iaxisunitofwork.md)**

### Por que um unit-of-work ciente de `Result`?

Um passo de negócio falho dentro de uma transação geralmente significa *rollback* — mas um `try/catch` não é o teste certo, porque falha aqui é um **valor** (`AxisResult.IsFailure`), não uma exceção. `InTransactionAsync` lê o resultado e decide: commit no `Ok`, rollback no `Error`, relançar numa exceção crua. → **[`InTransactionAsync` — o wrapper de transação certo](in-transaction.md)**

### Adapters embarcados — PostgreSQL e MySQL

`AxisRepository.Postgres` traz:

- **`PostgresUnitOfWork`** — implementa `IAxisUnitOfWork` via `NpgsqlConnection` + `NpgsqlTransaction`, traça cada operação via `IAxisTelemetry`.
- **`IPostgresUnitOfWork`** — `IAxisUnitOfWork` + `NewCommandAsync(sql)` para SQL cru.
- **`PostgresDbRepository`** — o executor pronto `IAxisDbRepository` que seus repositórios **compõem**: `ExecuteAsync`/`ExecuteCountAsync`/`GetAsync`/`ListAsync` com binding de parâmetros, retries em códigos `SqlState` transientes e violações de unique-key convertidas em `AxisError.Conflict`.
- **`PostgresRepositoryBase`** — a maquinaria por baixo do executor, também disponível como superfície de herança para um repositório deliberadamente preso ao dialeto.

```csharp
services.AddPostgresUnitOfWork(serviceKey: "main", connectionString: "Host=…");
services.AddPostgresDbRepository(serviceKey: "main");
```

`AxisRepository.MySql` traz a mesma forma sobre `MySqlConnector` — `MySqlUnitOfWork`, `IMySqlUnitOfWork`, `MySqlDbRepository`, `MySqlRepositoryBase` — compartilhando a maquinaria de retry/fault com o Postgres via `AxisRepositoryBase`:

```csharp
services.AddMySqlUnitOfWork(serviceKey: "main", connectionString: "Server=…");
services.AddMySqlDbRepository(serviceKey: "main");
```

→ **[Adapter Postgres](postgres-adapter.md)** · **[Adapter MySQL](mysql-adapter.md)** · **[Repository base](repository-base.md)** · **[Keyed multi-database](keyed-multi-database.md)**

### DDL de schema e migrations

Uma tabela é declarada **uma única vez**, agnóstica de dialeto, com o builder fluente `AxisTable` (`Axis.Ddl`); um `IAxisSqlDialect` injetado a renderiza no DDL concreto do Postgres ou do MySQL:

```csharp
public static AxisTable Define() => new AxisTable("AXIS_CACHE.CACHE_ENTRIES")
    .Column("CACHE_KEY", AxisDbType.Varchar(200), primaryKey: true)
    .Column("VALUE_JSON", AxisDbType.Json, notNull: true)
    .Index("IDX_CACHE_ENTRIES_EXPIRES_AT", "EXPIRES_AT");
```

`IAxisMigrationRunner` então aplica o script renderizado num schema, idempotentemente — fazendo bootstrap do schema e de uma tabela de controle `MIGRATIONS`, serializando instâncias concorrentes com um lock com escopo no schema, e pulando versões já registradas.

→ **[DDL de schema](ddl.md)** · **[Migrations](migrations.md)**

### Instalação

```
dotnet add package AxisRepository                 # a abstração
dotnet add package AxisRepository.Postgres        # o adapter Postgres (Npgsql)
dotnet add package AxisRepository.MySql           # o adapter MySQL (MySqlConnector)
```

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Contrato · `IAxisUnitOfWork`** | as três primitivas + o wrapper | [iaxisunitofwork.md](iaxisunitofwork.md) |
| **`InTransactionAsync`** ⭐ | embrulhar uma ferrovia em uma transação | [in-transaction.md](in-transaction.md) |
| **Postgres · `IPostgresUnitOfWork`** | o builder embarcado de `NpgsqlCommand` | [postgres-adapter.md](postgres-adapter.md) |
| **MySQL · `IMySqlUnitOfWork`** | o builder embarcado de `MySqlCommand` | [mysql-adapter.md](mysql-adapter.md) |
| **Repository base** | o executor `IAxisDbRepository` que seus repositórios compõem, e as bases `PostgresRepositoryBase`/`MySqlRepositoryBase` por baixo | [repository-base.md](repository-base.md) |
| **DDL de schema · `AxisTable`** | declare uma tabela uma vez, agnóstica de dialeto | [ddl.md](ddl.md) |
| **Migrations · `IAxisMigrationRunner`** | aplique o DDL num schema, idempotentemente | [migrations.md](migrations.md) |
| **Multi-database** | keyed DI para dois ou mais databases | [keyed-multi-database.md](keyed-multi-database.md) |
| **Adapter custom** | implemente `IAxisUnitOfWork` para outro store | [custom-adapter.md](custom-adapter.md) |
| **Por quê?** | o argumento contra o `DbContext` do EF Core | [why-axisrepository.md](why-axisrepository.md) |
| **Referência** | cada membro num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [O contrato `IAxisUnitOfWork`](iaxisunitofwork.md) · [Por que AxisRepository?](why-axisrepository.md)

**Fundamentos:** [`InTransactionAsync`](in-transaction.md) · [Adapter Postgres](postgres-adapter.md) · [Adapter MySQL](mysql-adapter.md) · [Repository base](repository-base.md)

**Referência e extras:** [DDL de schema](ddl.md) · [Migrations](migrations.md) · [Keyed multi-database](keyed-multi-database.md) · [Adapter custom](custom-adapter.md) · [Referência da API](api-reference.md)

---

## Princípios de design

1. **Transações seguem a trilha.** Um `AxisResult` falho faz rollback; um bem-sucedido commita; uma exceção faz rollback e relança.
2. **Erros são tipados.** Violações de unique-key → `Conflict`. Falhas de conexão → `InternalServerError`. NotFound → `NotFound`. Sem string-matching nos call sites.
3. **Erros transientes retentam de forma transparente.** O repository base retenta em `SqlState` 40001 / 40P01 / 08006 / 08003 / 08001 / 57P03 (e nas condições equivalentes de deadlock/lock-wait-timeout/instabilidade de conexão do MySQL) com backoff.
4. **Sem cheiro de `DbContext`.** Repositories falam SQL ou ports, o unit-of-work é dono da conexão e da transação. Sem proxies, sem mágica de change tracking.
5. **Cancelamento flui via `IAxisMediator`.** Toda operação lê `mediator.CancellationToken` — sem parâmetros extras no contrato.
6. **Schema é declarado uma vez, renderizado por dialeto.** Um `AxisTable` é agnóstico de dialeto; `IAxisSqlDialect` o renderiza, `IAxisMigrationRunner` o aplica idempotentemente. Uma definição nunca pode divergir entre Postgres e MySQL porque só existe um lugar para editar.

---

## Licença

Apache 2.0
