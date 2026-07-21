# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## O contrato — `IAxisUnitOfWork`

| Método | Assinatura | Descrição |
|---|---|---|
| `StartAsync` | `Task<AxisResult> StartAsync()` | abre a conexão (se necessário) e `BEGIN` na transação |
| `SaveChangesAsync` | `Task<AxisResult> SaveChangesAsync()` | `COMMIT` e limpa o handle da transação |
| `RollbackAsync` | `Task<AxisResult> RollbackAsync()` | `ROLLBACK`; no-op se nenhuma transação está ativa |
| `ReleaseConnectionAsync` | `Task ReleaseConnectionAsync()` | reverte qualquer trabalho não commitado e devolve a conexão ao pool; sem default de interface — implementações sem conexão pooled simplesmente retornam `Task.CompletedTask` |
| `InTransactionAsync` | `Task<AxisResult> InTransactionAsync(Func<Task<AxisResult>> work)` | wrapper default-interface; commit no `Ok`, rollback no `Error`, rollback-e-relança em exceção |
| `InTransactionAsync<T>` | `Task<AxisResult<T>> InTransactionAsync<T>(Func<Task<AxisResult<T>>> work)` | wrapper tipado com mesma semântica; em falha do `Save`, retorna os erros do **save** |
| `Dispose` | `void Dispose()` | dispõe a conexão por baixo |
| `DisposeAsync` | `ValueTask DisposeAsync()` | dispose async |

→ [O contrato `IAxisUnitOfWork`](iaxisunitofwork.md) · [`InTransactionAsync`](in-transaction.md)

---

## Adapter Postgres — `AxisRepository.Postgres`

| Tipo | Descrição |
|---|---|
| `IPostgresUnitOfWork` | `IAxisUnitOfWork + Task<NpgsqlCommand> NewCommandAsync(string sql)` |
| `PostgresUnitOfWork` | a implementação: abre uma `NpgsqlConnection`, gerencia `NpgsqlTransaction`, traça cada primitiva via `IAxisTelemetry` |
| `PostgresUnitOfWorkProvider` | cacheia um `PostgresUnitOfWork` por service key dentro do scope |
| `PostgresDbRepository` | o executor pronto `IAxisDbRepository` que um repositório agnóstico de provider compõe |
| `PostgresRepositoryBase` | a maquinaria por baixo do executor (`ExecuteAsync`/`GetAsync`/`ListAsync` + retry); superfície de herança para repositórios dialect-specific |

| Extensão | Efeito |
|---|---|
| `services.AddPostgresUnitOfWork(serviceKey, connectionString)` | registra um `NpgsqlDataSource` keyed, `PostgresUnitOfWorkProvider` keyed, `IAxisUnitOfWork` keyed e `IPostgresUnitOfWork` keyed |
| `services.AddPostgresDbRepository(serviceKey)` | registra `IAxisDbRepository` apoiado em `PostgresDbRepository` para aquela key |

→ [Adapter Postgres](postgres-adapter.md) · [Keyed multi-database](keyed-multi-database.md)

---

## Adapter MySQL — `AxisRepository.MySql`

| Tipo | Descrição |
|---|---|
| `IMySqlUnitOfWork` | `IAxisUnitOfWork + Task<MySqlCommand> NewCommandAsync(string sql)` (via `IDbUnitOfWork<MySqlCommand>`) |
| `MySqlUnitOfWork` | a implementação: abre uma `MySqlConnection` a partir de um `MySqlDataSource`, gerencia a `MySqlTransaction`, traça cada primitiva via `IAxisTelemetry` |
| `MySqlUnitOfWorkProvider` | cacheia um `MySqlUnitOfWork` por service key dentro do scope |
| `MySqlDbRepository` | o executor pronto `IAxisDbRepository` que um repositório agnóstico de provider compõe |
| `MySqlRepositoryBase` | a maquinaria por baixo do executor (`ExecuteAsync`/`GetAsync`/`ListAsync` + retry), compartilhando `AxisRepositoryBase` com o Postgres; superfície de herança para repositórios dialect-specific |

| Extensão | Efeito |
|---|---|
| `services.AddMySqlUnitOfWork(serviceKey, connectionString)` | registra um `MySqlDataSource` keyed, `MySqlUnitOfWorkProvider` keyed, `IAxisUnitOfWork` keyed e `IMySqlUnitOfWork` keyed |
| `services.AddMySqlDbRepository(serviceKey)` | registra `IAxisDbRepository` apoiado em `MySqlDbRepository` para aquela key |

→ [Adapter MySQL](mysql-adapter.md) · [Keyed multi-database](keyed-multi-database.md)

---

## O executor — `IAxisDbRepository`

| Método | Assinatura | Descrição |
|---|---|---|
| `ExecuteAsync` | `Task<AxisResult> ExecuteAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode = null)` | `ExecuteNonQueryAsync` com retry; violação unique-key → `AxisError.Conflict(duplicateKeyCode)` |
| `ExecuteCountAsync` | `Task<AxisResult<int>> ExecuteCountAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode = null)` | igual a `ExecuteAsync`, mas retorna a contagem de linhas afetadas |
| `GetAsync<T>` | `Task<AxisResult<T>> GetAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map, string notFoundCode)` | lê uma linha; ausente → `AxisError.NotFound(notFoundCode)` |
| `ListAsync<T>` | `Task<AxisResult<IEnumerable<T>>> ListAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map)` | lê N linhas |

Implementado por `PostgresDbRepository`/`MySqlDbRepository` (registrados via `AddPostgresDbRepository`/`AddMySqlDbRepository`). Na superfície de herança (`PostgresRepositoryBase`/`MySqlRepositoryBase`), os mesmos métodos existem como protected provider-typed — `Action<TParameters>` e `Func<TReader, T>`, onde `TParameters`/`TReader` são `NpgsqlParameterCollection`/`NpgsqlDataReader` para Postgres e `MySqlParameterCollection`/`MySqlDataReader` para MySQL.

→ [Repository base](repository-base.md)

---

## DDL de schema — `Axis.Ddl`

| Tipo | Descrição |
|---|---|
| `AxisTable` | builder fluente de tabela, agnóstico de dialeto — `Column`/`Index`/`Unique`/`PartialIndex`/`PartialUnique`/`ForeignKey`/`Check`/`WithSeed`, cada um retornando `this`; `Render(dialect)` produz a string de DDL |
| `AxisColumn` | um record de coluna — `Name`, `DbType`, `NotNull`, `Default`, `PrimaryKey`, `Check`, `Collation` |
| `AxisDbType` | tipo lógico de coluna — `Varchar(length)` / `Text` / `Int` / `Bool` / `Json` / `TimestampUtc` / `Decimal(precision, scale)` |
| `AxisDefault` | default de coluna — `NowUtc` / `Bool(value)` / `Int(value)` / `Raw(sql)` |
| `AxisCheck` | check de nível de coluna — `IsTrue` (padrão single-row-guard) |
| `AxisCollation` | intenção de collation por coluna — `Default` / `CaseAccentSensitive` / `CaseInsensitiveAccentSensitive` |
| `AxisIndex` | um record de índice — `Name`, `Columns`, `Unique`, `PartialPredicate` |
| `AxisForeignKey` | um record de FK de nível de tabela — `Name`, `Column`, `ReferencedTable`, `ReferencedColumn`, `OnDeleteCascade` |
| `AxisTableCheck` | um record de `CHECK` de nível de tabela — `Name`, `Expression` (SQL portável) |
| `AxisSeed` | um record de seed idempotente — `Columns`, `ConflictColumns`, `Rows` |
| `IAxisSqlDialect` | renderiza o modelo de DDL no SQL de um banco — `RenderCreateTable(table)` para a tabela completa, `RenderAddColumn(table, column)` para um `ALTER TABLE … ADD COLUMN` portável |
| `AxisSqlDialectBase` | esqueleto de renderização compartilhado; nove hooks abstratos (`RenderType`, `RenderDefault`, `RenderCheck`, `RenderCollation`, `RenderBoolLiteral`, `RenderSeedConflict`, `RenderInlineIndexLines`, `RenderPostTableStatements`, `RenderForeignKey`, `RenderTimestampLiteral`) mais helpers compartilhados (`ForeignKeyConstraint`, `Quote`, `FormatUtcTimestamp`, `RenderNull`) |
| `PostgresSqlDialect` / `MySqlSqlDialect` | as duas implementações de `IAxisSqlDialect` embarcadas, uma por pacote adapter |

→ [DDL de schema](ddl.md)

---

## Migrations — `IAxisMigrationRunner`

| Tipo | Descrição |
|---|---|
| `IAxisMigrationRunner` | `Task RunAsync(string connectionString, string schema, (string Version, string Script)[] migrations)` — faz bootstrap do schema + tabela de controle `MIGRATIONS`, aplica versões pendentes em ordem, pulando as já registradas |
| `PostgresMigrationRunner` | a implementação Postgres — uma transação para o lote inteiro, advisory lock transacional (`pg_advisory_xact_lock`) |
| `MySqlMigrationRunner` | a implementação MySQL — sem transação envolvente (o DDL do MySQL faz commit implícito), named lock de sessão (`GET_LOCK`/`RELEASE_LOCK`), cada versão registrada imediatamente após rodar |

→ [Migrations](migrations.md)

---

## Códigos transientes que a base retenta

| Dialeto | Códigos | Significado |
|---|---|---|
| Postgres `SqlState` | `40001` | serialization failure |
| Postgres `SqlState` | `40P01` | deadlock detected |
| Postgres `SqlState` | `08006` | connection failure |
| Postgres `SqlState` | `08003` | connection does not exist |
| Postgres `SqlState` | `08001` | unable to connect |
| Postgres `SqlState` | `57P03` | cannot connect now |
| MySQL `MySqlException.Number` | `1062` | duplicate entry → mapeado para `Conflict`, não retentado |
| MySQL `MySqlException.Number` | `1146` / `1049` | no such table / unknown database → mapeado para `ServiceUnavailable`, não retentado |
| MySQL (via `MySqlTransientErrors.IsTransient`) | deadlock / lock-wait-timeout / instabilidade de conexão | retentado |

Delays de retry: `100`, `200`, `400`, `1000` ms (4 retries, 5 tentativas no total).

→ [Repository base](repository-base.md)

---

## Contrato de comportamento (para adapters)

| Desfecho do `work()` (em `InTransactionAsync`) | Retornado | Side effect |
|---|---|---|
| `Ok` e `SaveChangesAsync` ok | `Ok` (com valor, no overload `<T>`) | commitado |
| `Ok` e `SaveChangesAsync` falha | o `Error(errors)` do save | nada commitado |
| `Error(errors)` | o `Error(errors)` do work | rollback feito |
| lança | relança | rollback feito, depois relançado |

→ [Adapter custom](custom-adapter.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — instale, registre, persista
- [Por que AxisRepository?](why-axisrepository.md) — o argumento pela abstração
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisRepository](README.md)
