# Repository base · `AxisRepositoryBase<TCommand,TReader,TParameters>`

> A maquinaria compartilhada de todo repositório SQL — binding de parâmetros, retry transparente em erros transientes, violações de unique-key viram `AxisError.Conflict(...)`. Ela é consumida por **duas superfícies**: o repositório da aplicação **compõe** o executor pronto `IAxisDbRepository` (`PostgresDbRepository`/`MySqlDbRepository`) — o padrão —, e `PostgresRepositoryBase`/`MySqlRepositoryBase` seguem disponíveis como superfície de **herança** para um repositório deliberadamente preso a um dialeto. As duas afunilam nos mesmos cores privados, então classificação, retry e faulting se comportam de forma idêntica.

```csharp
public class PersonRepository(IAxisDbRepository db)
{
    public Task<AxisResult> CreateAsync(Person person)
        => db.ExecuteAsync(
            sql: "INSERT INTO people (id, document, name) VALUES (@id, @doc, @name)",
            bind: b => b.Add("id",   person.PersonId)
                        .Add("doc",  person.Document)
                        .Add("name", person.DisplayName),
            duplicateKeyCode: "PERSON_DOCUMENT_ALREADY_EXISTS");
}
```

---

## As duas superfícies

**Composição (o padrão).** O repositório é agnóstico de provider: recebe `IAxisDbRepository` por construtor e fala a superfície ADO.NET comum — binder de parâmetros nomeados (`IDbParamBinder`) e `DbDataReader`. O executor concreto é registrado uma vez no composition root (`services.AddPostgresDbRepository("main")` ou `services.AddMySqlDbRepository("main")`), então o mesmo repositório roda em Postgres ou MySQL trocando só o registro.

**Herança (dialect-specific).** Um repositório que mira deliberadamente um provider e quer os tipos dele — `AddWithValue` na `NpgsqlParameterCollection`, o reader tipado — herda `PostgresRepositoryBase` (ou `MySqlRepositoryBase`) e usa os mesmos quatro helpers como métodos protected:

```csharp
public class PersonRepository(
    IAxisMediator mediator,
    IAxisLogger<PersonRepository> logger,
    [FromKeyedServices("main")] IPostgresUnitOfWork uow)
    : PostgresRepositoryBase(mediator, logger, uow)
{
    public Task<AxisResult> CreateAsync(Person person)
        => ExecuteAsync(
            sql: "INSERT INTO people (id, document, name) VALUES (@id, @doc, @name)",
            addParams: p =>
            {
                p.AddWithValue("id",   person.PersonId);
                p.AddWithValue("doc",  person.Document);
                p.AddWithValue("name", person.DisplayName);
            },
            duplicateKeyCode: "PERSON_DOCUMENT_ALREADY_EXISTS");
}
```

As peles de dialeto entregam quatro hooks à base (`IsTransient`, `IsDuplicateKey`, `IsSchemaMissing`, `ErrorPrefix`) — é assim que os próprios executors `PostgresDbRepository`/`MySqlDbRepository` são construídos por dentro.

## Quando usar

Componha `IAxisDbRepository` por padrão — é o que mantém o repositório neutro de provider e é o formato que os scaffolds e as convenções do Axis materializam. Herde de `PostgresRepositoryBase`/`MySqlRepositoryBase` apenas quando o repositório é deliberadamente dialect-specific e quer os tipos do provider.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| disparar um comando cru pontual a partir de um handler | chame `IPostgresUnitOfWork.NewCommandAsync(sql)` (ou `IMySqlUnitOfWork`) direto |
| rodar o mesmo repositório em outro banco embarcado | troque o registro (`AddPostgresDbRepository` ↔ `AddMySqlDbRepository`) — o repositório composto não muda |
| mirar um banco sem adapter embarcado | uma nova pele de dialeto sobre `AxisRepositoryBase<TCommand,TReader,TParameters>` |
| usar um ORM | um [adapter custom](custom-adapter.md) embrulhando ela |

---

## Os quatro métodos

Na superfície de composição (`IAxisDbRepository`):

| Método | Assinatura | O que faz |
|---|---|---|
| `ExecuteAsync` | `Task<AxisResult> ExecuteAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode = null)` | `ExecuteNonQueryAsync` com retry; violação unique-key → `AxisError.Conflict(duplicateKeyCode)` |
| `ExecuteCountAsync` | `Task<AxisResult<int>> ExecuteCountAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode = null)` | igual a `ExecuteAsync`, mas retorna a contagem de linhas afetadas |
| `GetAsync<T>` | `Task<AxisResult<T>> GetAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map, string notFoundCode)` | lê uma única linha; `notFoundCode` se nenhuma deu match |
| `ListAsync<T>` | `Task<AxisResult<IEnumerable<T>>> ListAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map)` | lê N linhas |

Na superfície de herança, os mesmos quatro existem como métodos protected provider-typed — `Action<TParameters>` e `Func<TReader, T>`, onde `TParameters`/`TReader` são `NpgsqlParameterCollection`/`NpgsqlDataReader` para Postgres e `MySqlParameterCollection`/`MySqlDataReader` para MySQL — mais irmãos sem `addParams` (`ExecuteAsync(sql, duplicateKeyCode)`, `GetAsync(sql, map, notFoundCode)`, `ListAsync(sql, map)`) para queries sem parâmetros.

Qualquer que seja a superfície, cada método abre um comando fresco via `uow.NewCommandAsync(sql)` (que anexa a conexão + transação atuais), invoca seu callback de binding para amarrar parâmetros, depois roda o método de execução apropriado com cancelamento de `mediator.CancellationToken`.

Mais dois caminhos de falha são tratados centralmente, antes de qualquer base de dialeto ver a exceção:

- **Schema não pronto** — quando `IsSchemaMissing` dá match (ex.: Postgres `42P01`/`3F000`, MySQL `1146`/`1049`), a chamada retorna um `AxisError.ServiceUnavailable("{PREFIX}_SCHEMA_NOT_READY")` transiente sem log de erro — este é um estado *esperado* antes das migrations rodarem, não um bug.
- **Transação faultada** — assim que um comando falha, `uow.MarkFaulted()` é chamado e toda chamada posterior no mesmo unit of work curto-circuita com `AxisError.InternalServerError("{PREFIX}_TRANSACTION_FAULTED")` em vez de executar (alguns engines abortam a transação inteira em qualquer erro, então retentar só falharia de novo).

---

## Retry transiente

Lendo `AxisRepositoryBase.WithRetryAsync` direto:

| Gatilho | Espera entre tentativas | Tentativas |
|---|---|---|
| o hook `IsTransient(exception)` do dialeto retorna `true` **e** o unit of work ainda não tem escrita não commitada (`!uow.HasUncommittedWrites`) | 100 ms, 200 ms, 400 ms, 1000 ms | 5 no total (a chamada + 4 retries) |

Para Postgres, `IsTransient` dá match em `NpgsqlException.SqlState` em `40001` (serialization failure), `40P01` (deadlock), `08006` (connection failure), `08003` (connection does not exist), `08001` (unable to connect), `57P03` (cannot connect now). Para MySQL, `MySqlTransientErrors.IsTransient` dá match nas condições equivalentes de deadlock/lock-wait-timeout/instabilidade de conexão.

O gate `!uow.HasUncommittedWrites` importa: um erro transiente aborta a transação inteira, então retentar o mesmo comando no lugar só bateria num erro de "transação abortada" — e se uma escrita anterior na mesma transação já tivesse desembarcado, retentar perderia ela silenciosamente. Sem escrita ainda, o retry libera a conexão (`uow.ReleaseConnectionAsync()`) e começa fresco; assim que uma escrita desembarcou, o transiente é exposto em vez de retentado para que o chamador possa reproduzir o unit of work inteiro (isso é exatamente o que o resumer da saga faz numa nova tentativa).

Qualquer outra coisa **não** é retentada — vai direto para o bloco catch e vira um `AxisError.InternalServerError(...)`, ou, para exceções de unique-key/schema-missing, o `Conflict`/`ServiceUnavailable` mapeado acima.

---

## Exemplos reais

### 1. `INSERT` idempotente com mapeamento de conflito

```csharp
public Task<AxisResult> CreateAsync(Person person)
    => db.ExecuteAsync(
        sql: "INSERT INTO people (id, document, name) VALUES (@id, @doc, @name)",
        bind: b => b.Add("id",   person.PersonId)
                    .Add("doc",  person.Document)
                    .Add("name", person.DisplayName),
        duplicateKeyCode: "PERSON_DOCUMENT_ALREADY_EXISTS");
```

Se a coluna `document` tem unique constraint e o insert viola, o chamador vê `AxisError.Conflict("PERSON_DOCUMENT_ALREADY_EXISTS")` — um código de erro tipado e previsível, não uma exceção string-matched.

### 2. `GetAsync` tipado com mapeamento customizado

```csharp
public Task<AxisResult<Person>> GetByIdAsync(AxisEntityId personId)
    => db.GetAsync(
        sql: "SELECT id, document, name FROM people WHERE id = @id",
        bind: b => b.Add("id", personId),
        map: r => new Person(r.GetString(0), r.GetString(1), r.GetString(2)),
        notFoundCode: "PERSON_NOT_FOUND");
```

Se nenhuma linha bate, o resultado é `AxisError.NotFound("PERSON_NOT_FOUND")` — encadeie `.RequireNotFoundAsync(...)` do `AxisResult` para o padrão de guard "não deve existir".

### 3. `ListAsync` paginado

```csharp
public Task<AxisResult<IEnumerable<Person>>> ListByTenantAsync(string tenantKey, int limit, int offset)
    => db.ListAsync(
        sql: "SELECT id, document, name FROM people WHERE tenant = @tenant ORDER BY id LIMIT @limit OFFSET @offset",
        bind: b => b.Add("tenant", tenantKey)
                    .Add("limit",  limit)
                    .Add("offset", offset),
        map: r => new Person(r.GetString(0), r.GetString(1), r.GetString(2)));
```

**Por que compensa:** cada método de repositório lê como uma tupla (parâmetros → SQL → projeção). O boilerplate que geralmente sepulta essa intenção — `using`, retry, `try/catch`, mapeamento de erro — vive uma vez na base.

---

## Veja também

- [Adapter Postgres](postgres-adapter.md) — uma pele de dialeto sob a qual a base se senta
- [Adapter MySQL](mysql-adapter.md) — a outra pele de dialeto, mesma base compartilhada
- [O contrato `IAxisUnitOfWork`](iaxisunitofwork.md) — as primitivas de transação
- [DDL de schema](ddl.md) — declara as tabelas que estes repositórios leem e escrevem
- [Adapter custom](custom-adapter.md) — quando você precisa de uma base diferente

---

↩ [Voltar à documentação do AxisRepository](README.md)
