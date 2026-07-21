# Primeiros passos · instalação e uso

> Instale a abstração e o adapter Postgres, registre uma conexão, escreva um pequeno repositório e rode seu primeiro pipeline transacional em cinco minutos.

---

## Instalação

```
dotnet add package AxisRepository                 # a abstração
dotnet add package AxisRepository.Postgres        # o adapter Postgres
```

`AxisRepository` depende de `AxisResult`. `AxisRepository.Postgres` adiciona `Npgsql` e depende de `AxisLogger` e `AxisTelemetry` (toda operação de banco é traçada).

---

## Registrando o Postgres

```csharp
using AxisRepository.Postgres;

builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddAxisTelemetry()
    .AddPostgresUnitOfWork(
        serviceKey:       "main",
        connectionString: builder.Configuration.GetConnectionString("Postgres")!);

builder.Services.AddPostgresDbRepository(serviceKey: "main");
```

`AddPostgresUnitOfWork`:

- Chama `services.AddNpgsqlDataSource(...)` com data source singleton e conexões scoped.
- Registra um `PostgresUnitOfWorkProvider` **keyed**.
- Amarra `IAxisUnitOfWork` e `IPostgresUnitOfWork` como keyed-scoped contra o provider.

`AddPostgresDbRepository` registra o executor pronto `PostgresDbRepository` como o `IAxisDbRepository` scoped, amarrado ao unit of work daquela key — é ele que seus repositórios compõem.

O registro keyed deixa você plugar **múltiplos databases** sob `serviceKey` diferentes — veja [Keyed multi-database](keyed-multi-database.md).

---

## Escrevendo um repositório

```csharp
using Axis;

public class PersonRepository(IAxisDbRepository db)
{
    public Task<AxisResult> CreateAsync(Person person)
        => db.ExecuteAsync(
            sql: "INSERT INTO people (id, document, name) VALUES (@id, @doc, @name)",
            bind: b => b.Add("id",   person.PersonId)
                        .Add("doc",  person.Document)
                        .Add("name", person.DisplayName),
            duplicateKeyCode: "PERSON_DOCUMENT_ALREADY_EXISTS");

    public Task<AxisResult<Person>> GetByIdAsync(AxisEntityId personId)
        => db.GetAsync(
            sql: "SELECT id, document, name FROM people WHERE id = @id",
            bind: b => b.Add("id", personId),
            map: r => new Person(r.GetString(0), r.GetString(1), r.GetString(2)),
            notFoundCode: "PERSON_NOT_FOUND");
}
```

O repositório **compõe** o executor `IAxisDbRepository` — nenhum tipo de driver aparece na classe, então o mesmo repositório roda em Postgres ou MySQL trocando só o registro. Os métodos do executor (`ExecuteAsync`, `ExecuteCountAsync`, `GetAsync`, `ListAsync`) embrulham o binding de parâmetros, retries em erros transientes e convertem violações de unique-key em `AxisError.Conflict(...)`.

> Um adapter MySQL também é embarcado — `AxisRepository.MySql`, registrado com `AddMySqlUnitOfWork` + `AddMySqlDbRepository`. Mesma forma, `MySqlConnector` por baixo. Veja [Adapter MySQL](mysql-adapter.md). E se um repositório for deliberadamente preso a um dialeto, a superfície de herança (`PostgresRepositoryBase`/`MySqlRepositoryBase`) continua disponível — veja [Repository base](repository-base.md).

---

## Rodando dentro de uma transação

```csharp
public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
    => uow.InTransactionAsync(() =>
        factory.CreateAsync(cmd)
            .ThenAsync(person => writer.CreateAsync(person))
            .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId }));
```

**Por que compensa:** `InTransactionAsync` lê a ferrovia. Um passo falho faz rollback, um bem-sucedido commita, uma exceção faz rollback e relança. O handler lê como uma descrição em linha reta do caso de uso.

---

## Veja também

- [O contrato `IAxisUnitOfWork`](iaxisunitofwork.md) — as três primitivas
- [`InTransactionAsync`](in-transaction.md) — o wrapper
- [Adapter Postgres](postgres-adapter.md) — o que `AddPostgresUnitOfWork` registra
- [Adapter MySQL](mysql-adapter.md) — a alternativa MySQL embarcada
- [Repository base](repository-base.md) — os helpers `ExecuteAsync`/`GetAsync`/`ListAsync`
- [DDL de schema](ddl.md) — declare uma tabela uma vez, agnóstica de dialeto
- [Migrations](migrations.md) — aplique o DDL num schema, idempotentemente
- [Keyed multi-database](keyed-multi-database.md) — rodar contra mais de um database
- [Adapter custom](custom-adapter.md) — implemente `IAxisUnitOfWork` para outro store
- [Por que AxisRepository?](why-axisrepository.md) — o argumento contra EF Core
- [Referência da API](api-reference.md) — cada membro num só lugar

---

↩ [Voltar à documentação do AxisRepository](README.md)
