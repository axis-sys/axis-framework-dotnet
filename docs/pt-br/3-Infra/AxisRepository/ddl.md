# DDL de schema · `AxisTable`

> Declare uma tabela **uma única vez** — colunas, chave primária, índices, foreign keys, checks de nível de tabela e um seed idempotente opcional — com o modelo `Axis.Ddl`, agnóstico de dialeto, e depois entregue para um `IAxisSqlDialect` renderizar o `CREATE TABLE` concreto para Postgres ou MySQL. Substitui escrever (e sincronizar) à mão uma string de DDL por banco.

```csharp
namespace Axis.Persistence.Scripts;

internal static class WidgetsTable
{
    public const string Table    = $"{WidgetsSchema.Schema}.WIDGETS";
    public const string WidgetId = "WIDGET_ID";
    public const string OwnerId  = "OWNER_ID";
    public const string Title    = "TITLE";
    public const string IsActive = "IS_ACTIVE";
    public const string CreatedAt = "CREATED_AT";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(WidgetId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(OwnerId, AxisDbType.Varchar(50), notNull: true)
        .Column(Title, AxisDbType.Varchar(120), notNull: true, collation: AxisCollation.CaseAccentSensitive)
        .Column(IsActive, AxisDbType.Bool, notNull: true, @default: AxisDefault.Bool(true))
        .Column(CreatedAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
        .Index("IDX_WIDGETS_OWNER", OwnerId)
        .Unique("UX_WIDGETS_OWNER_TITLE", OwnerId, Title)
        .ForeignKey("FK_WIDGETS_OWNER", OwnerId, OwnersTable.Table, OwnersTable.OwnerId, onDeleteCascade: true);
}
```

`Define().Render(dialect)` — chamado uma vez por adapter, com `new PostgresSqlDialect()` ou `new MySqlSqlDialect()` — é o que uma classe `{BC}DbInit`/`{Package}Schema` entrega ao [`IAxisMigrationRunner`](migrations.md) como script de migration. Esta página cobre o modelo; [Migrations](migrations.md) cobre aplicá-lo.

---

## Quando usar

Qualquer tabela cujo DDL precise rodar de forma idêntica — estruturalmente — em todo adapter de banco que seu pacote ou Bounded Context embarca. Uma definição `AxisTable`, renderizada por dialeto, é a fonte única de verdade para nomes de coluna, tipos e constraints; os dois adapters nunca podem divergir porque só existe um lugar para editar.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| **alterar** uma tabela já criada por um `Define()` anterior | um novo script de migration versionado — [`RenderAddColumn`](#adicionar-uma-coluna--renderaddcolumn) para um `ADD COLUMN` portável; SQL cru para qualquer outro `ALTER` (o próprio `AxisTable` só renderiza `CREATE TABLE IF NOT EXISTS`) |
| aplicar o DDL renderizado num banco real | [`IAxisMigrationRunner`](migrations.md) — esta página só constrói a string |
| mirar um banco sem `IAxisSqlDialect` ainda | implemente um sobre [`AxisSqlDialectBase`](#o-dialeto--iaxissqldialect-e-axissqldialectbase) |

---

## O builder — métodos fluentes de `AxisTable`

Todo método retorna `this`, então uma definição lê como uma única expressão encadeada. `Name` (o `{SCHEMA}.{TABELA}` qualificado) é fixado na construção.

| Método | Adiciona | Notas |
|---|---|---|
| `Column(name, dbType, notNull, default, primaryKey, check, collation)` | uma coluna | `dbType` via [`AxisDbType.*`](#tipos-de-coluna--axisdbtype), `default` via [`AxisDefault.*`](#defaults-de-coluna--axisdefault) |
| `Index(name, params columns)` | um índice simples, não-único | renderizado como um `CREATE INDEX` avulso (Postgres) ou inline no corpo da tabela (MySQL) |
| `Unique(name, params columns)` | um índice único | mesma divisão de renderização de `Index` |
| `PartialIndex(name, predicate, params columns)` | um índice não-único condicional | Postgres: `CREATE INDEX ... WHERE {predicate}`; MySQL não tem índices parciais, então o predicado é descartado e vira um índice simples |
| `PartialUnique(name, predicate, params columns)` | um índice único condicional | Postgres: `WHERE {predicate}`; MySQL emula com uma coluna `GENERATED ALWAYS ... STORED` que é `NULL` fora do predicado (chaves únicas ignoram `NULL`, então linhas fora do predicado nunca colidem) |
| `ForeignKey(name, column, referencedTable, referencedColumn, onDeleteCascade)` | uma FK de nível de tabela | todo dialeto renderiza de forma idêntica: `CONSTRAINT {name} FOREIGN KEY (...) REFERENCES ...` |
| `Check(name, expression)` | um `CHECK` de nível de tabela | `expression` é SQL cru, portável (ex.: um XOR entre colunas) renderizado verbatim por todo dialeto — veja também o [`AxisCheck.IsTrue`](#checks-de-nível-de-coluna-e-collation) de nível de coluna |
| `WithSeed(columns, conflictColumns, rows)` | um `INSERT` idempotente | no-op num conflito de `conflictColumns` — veja [Seeding](#seeding--axisseed) |
| `Render(dialect)` | — | retorna a string de DDL completa (`CREATE TABLE` + índices + seed) para o `IAxisSqlDialect` dado |

`Columns`, `Indexes`, `ForeignKeys`, `Checks` e `Seed` são expostos como propriedades somente-leitura — um dialeto (ou um teste) inspeciona o modelo registrado sem reparsear SQL.

---

## Tipos de coluna · `AxisDbType`

Um conjunto fechado de tipos lógicos; o dialeto mapeia cada um para o tipo de coluna concreto.

| Factory | Postgres | MySQL |
|---|---|---|
| `Varchar(length)` | `VARCHAR(length)` | `VARCHAR(length)` |
| `Text` | `TEXT` | `TEXT` |
| `Int` | `INT` | `INT` |
| `Bool` | `BOOLEAN` | `TINYINT(1)` |
| `Json` | `JSONB` | `JSON` |
| `TimestampUtc` | `TIMESTAMPTZ` | `DATETIME(6)` |
| `Decimal(precision, scale)` | `NUMERIC(precision,scale)` | `DECIMAL(precision,scale)` |

As factories escalares (`Text`, `Int`, `Bool`, `Json`, `TimestampUtc`) são singletons; `Varchar`/`Decimal` carregam seus argumentos.

## Defaults de coluna · `AxisDefault`

| Factory | Renderiza para |
|---|---|
| `NowUtc` | Postgres `NOW()` · MySQL `(UTC_TIMESTAMP(6))` |
| `Bool(value)` | Postgres `TRUE`/`FALSE` · MySQL `1`/`0` |
| `Int(value)` | o literal, sempre culture-invariant |
| `Raw(sql)` | `sql` verbatim — a válvula de escape para um default específico de fornecedor que o modelo não tem factory (ex.: `"gen_random_uuid()"`) |

## Checks de nível de coluna e collation

`AxisCheck.IsTrue` prende uma coluna booleana em `true` — o padrão single-row-guard para uma tabela de configurações com exatamente uma linha (`ONLY_ROW BOOLEAN PRIMARY KEY CHECK (...)`, como `AXIS_SAGA.SAGA_SETTINGS` usa). Postgres renderiza `CHECK (col)`; MySQL renderiza `CHECK (col = 1)`.

`AxisCollation` prende a intenção de comparação de string por coluna, porque os dois bancos discordam nos defaults: Postgres compara sensível a caixa e acento via `=`; a collation default do MySQL (`utf8mb4_0900_ai_ci`) dobra tanto caixa quanto acento. Defina a intenção explicitamente para que o MySQL bata com a semântica do Postgres:

| Valor | Use para |
|---|---|
| `Default` | sem collation explícita — aceita o default do próprio dialeto |
| `CaseAccentSensitive` | igualdade exata / chaves únicas (MySQL: `COLLATE utf8mb4_0900_as_cs`) |
| `CaseInsensitiveAccentSensitive` | busca estilo `ILIKE`/`lower()` (MySQL: `COLLATE utf8mb4_0900_as_ci`) |

O Postgres ignora `AxisCollation` (seu default já bate com `CaseAccentSensitive`); só o dialeto MySQL renderiza uma cláusula `COLLATE`.

## Seeding · `AxisSeed`

`WithSeed(columns, conflictColumns, rows)` registra um `INSERT` idempotente, aplicado na primeira vez que o script de migration da tabela roda:

```csharp
.WithSeed(
    columns:         [OnlyRow, MaxConcurrentSagas],
    conflictColumns: [OnlyRow],
    new object?[] { true, 20 });
```

Renderiza como um `ON CONFLICT (...) DO NOTHING` no Postgres e `ON DUPLICATE KEY UPDATE col = col` no MySQL — nunca `INSERT IGNORE`, que também engoliria silenciosamente violações de FK e `NOT NULL` em vez de só pular a duplicata pretendida. Os valores das linhas passam pelo mesmo `RenderValue` usado em todo lugar: literais numéricos são sempre culture-invariant (um `ToString()` sensível a cultura emitiria uma vírgula decimal e corromperia o valor), e valores `DateTime`/`DateTimeOffset` são normalizados para UTC antes de renderizar.

---

## O dialeto — `IAxisSqlDialect` e `AxisSqlDialectBase`

```csharp
public interface IAxisSqlDialect
{
    string RenderCreateTable(AxisTable table);
    string RenderAddColumn(string table, AxisColumn column);
}
```

Dois métodos: `RenderCreateTable` transforma um `AxisTable` no DDL completo para um banco; `RenderAddColumn` renderiza um `ALTER TABLE … ADD COLUMN` portável para uma migration incremental — veja [Adicionar uma coluna](#adicionar-uma-coluna--renderaddcolumn). `AxisSqlDialectBase` implementa `RenderCreateTable` uma única vez — ordem de montagem da coluna (tipo + collation → `PRIMARY KEY`/`NOT NULL` → `DEFAULT` → `CHECK`), o wrapper `CREATE TABLE IF NOT EXISTS`, depois statements pós-tabela e o seed — e pede a cada dialeto concreto nove tokens:

| Hook | Responde |
|---|---|
| `RenderType` | o tipo de coluna concreto para um `AxisDbType` |
| `RenderDefault` | a expressão `DEFAULT` concreta para um `AxisDefault` |
| `RenderCheck` | o corpo do `CHECK` de nível de coluna concreto |
| `RenderCollation` | a cláusula `COLLATE` (ou `""`) para um `AxisCollation` |
| `RenderBoolLiteral` | como um literal `true`/`false` cru é escrito |
| `RenderSeedConflict` | a cláusula de insert idempotente (`ON CONFLICT` / `ON DUPLICATE KEY UPDATE`) |
| `RenderInlineIndexLines` | linhas de índice para colocar inline **dentro** do corpo do `CREATE TABLE` (MySQL) — `[]` se o dialeto os emite separadamente |
| `RenderPostTableStatements` | statements a emitir **depois** do `CREATE TABLE` (os `CREATE INDEX` avulsos do Postgres) — `[]` se o dialeto os coloca inline |
| `RenderForeignKey` / `RenderTimestampLiteral` | abstratos de propósito: Postgres e MySQL hoje concordam, mas mantidos abstratos para que um dialeto futuro com sintaxe de FK ou tratamento de timestamp diferente não fique silenciosamente preso a um default que não serve |

`PostgresSqlDialect` e `MySqlSqlDialect` (em `AxisRepository.Postgres` / `AxisRepository.MySql`) são as duas implementações de `IAxisSqlDialect` embarcadas — leia-as lado a lado para ver exatamente onde Postgres e MySQL divergem: o **layout de indexação** é o grande — Postgres retorna `[]` de `RenderInlineIndexLines` e emite statements `CREATE INDEX IF NOT EXISTS ... ON table (...)` avulsos a partir de `RenderPostTableStatements`; MySQL faz o oposto, colocando linhas `INDEX`/`UNIQUE KEY` inline (e, para um índice único parcial, uma coluna `GENERATED ALWAYS ... STORED`) no corpo do `CREATE TABLE` e retornando `[]` de `RenderPostTableStatements`.

Helpers compartilhados, não-abstratos, na classe base: `ForeignKeyConstraint` (a renderização padrão `CONSTRAINT ... FOREIGN KEY ... REFERENCES ...` que os dois dialetos reusam tal como é), `Quote` (escaping de aspa simples), `FormatUtcTimestamp` (texto UTC com precisão de microssegundo) e `RenderNull` (default `"NULL"`, sobrescrevível).

### Adicionar uma coluna — `RenderAddColumn`

```csharp
dialect.RenderAddColumn(WidgetsTable.Table, new AxisColumn("UPLOADED_AT", AxisDbType.TimestampUtc));
// Postgres: ALTER TABLE {schema}.WIDGETS ADD COLUMN UPLOADED_AT TIMESTAMPTZ;
// MySQL:    ALTER TABLE {schema}.WIDGETS ADD COLUMN UPLOADED_AT DATETIME(6);
```

Um statement `ALTER TABLE {table} ADD COLUMN …;` por chamada, para o script da versão nova que evolui uma tabela já publicada — veja [Migrations](migrations.md). A linha da coluna passa pelo **mesmo pipeline** que `RenderCreateTable` usa, então o dialeto é dono do mapeamento de tipos e você nunca escreve tokens de engine à mão. `virtual` em `AxisSqlDialectBase`, para que um dialeto futuro com sintaxe de `ALTER` divergente possa sobrescrevê-lo. Três ressalvas:

- **Sem `IF NOT EXISTS`** — deliberado: o MySQL não tem essa forma para `ADD COLUMN`. A idempotência vem do ledger de migrations, que nunca reaplica uma versão registrada — coloque o statement numa versão nova e ele roda exatamente uma vez.
- **Uma coluna `PRIMARY KEY` lança `ArgumentException`** — adicionar chave primária via `ALTER TABLE` não é portável; declare-a no `CREATE TABLE`.
- **`NOT NULL` sem `DEFAULT`** falha no Postgres quando a tabela tem linhas (o MySQL preenche um default implícito) — dê um default à coluna, ou adicione-a como nullable.

---

## Exemplos reais

### 1. Uma tabela com múltiplos índices compartilhada por dois adapters — `AxisSaga`

```csharp
public static AxisTable Define() => new AxisTable(Table)
    .Column(SagaId, AxisDbType.Varchar(50), primaryKey: true)
    .Column(SagaName, AxisDbType.Varchar(100), notNull: true)
    .Column(Status, AxisDbType.Varchar(30), notNull: true)
    .Column(PayloadJson, AxisDbType.Json, notNull: true)
    .Column(Version, AxisDbType.Int, notNull: true, @default: AxisDefault.Int(1))
    .Column(ClaimedUntil, AxisDbType.TimestampUtc)
    // …
    .Index("IDX_SAGA_INSTANCES_STATUS_UPDATED", Status, UpdatedAt)
    .Index("IDX_SAGA_INSTANCES_LEASE", Status, ClaimedUntil)
    .PartialIndex("IDX_SAGA_INSTANCES_ACTIVE_LEASE",
        $"{Status} NOT IN ('Completed','Failed','Compensated')", ClaimedUntil);
```

`AxisSagaSchema.Tables` lista esta ao lado de três tabelas irmãs; `AxisSagaMigrations.InitializePostgresAsync` as renderiza com `new PostgresSqlDialect()`, `AxisSagaMySqlMigrations.InitializeMySqlAsync` renderiza exatamente as mesmas instâncias `AxisTable` com `new MySqlSqlDialect()`. Veja [Schema do banco · `AXIS_SAGA`](../../2-ApplicationFlow/AxisSaga/database-schema.md) para a referência completa coluna a coluna.

**Por que compensa:** o índice parcial que mantém a query de contagem de lease seletiva é declarado uma única vez. Renomear uma coluna ou adicionar um índice é uma edição que alcança os dois adapters — não existe uma segunda string de DDL para lembrar de atualizar.

### 2. Um schema de tabela única — `AxisCache`

```csharp
internal static class CacheEntriesTable
{
    public const string Schema = "AXIS_CACHE";
    public const string Table  = $"{Schema}.CACHE_ENTRIES";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(CacheKey, AxisDbType.Varchar(200), primaryKey: true)
        .Column(ValueJson, AxisDbType.Json, notNull: true)
        .Column(ExpiresAt, AxisDbType.TimestampUtc)
        .Column(UpdatedAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
        .Index("IDX_CACHE_ENTRIES_EXPIRES_AT", ExpiresAt);
}

public static class AxisCacheSchema
{
    public const string Schema = CacheEntriesTable.Schema;

    public static (string Version, string Script)[] Migrations(IAxisSqlDialect dialect) =>
        [("V1", CacheEntriesTable.Define().Render(dialect))];
}
```

**Por que compensa:** `{Package}Schema.Migrations(dialect)` é a única linha que o inicializador de todo adapter chama — veja [Migrations](migrations.md) para o que a roda.

---

## Veja também

- [Migrations · `IAxisMigrationRunner`](migrations.md) — aplica o DDL que esta página constrói, idempotentemente
- [Adapter Postgres](postgres-adapter.md) — embarca `PostgresSqlDialect` e `PostgresMigrationRunner`
- [Adapter MySQL](mysql-adapter.md) — embarca `MySqlSqlDialect` e `MySqlMigrationRunner`
- [Repository base](repository-base.md) — o repositório que lê/escreve as linhas que este schema define
- [Schema do banco · `AXIS_SAGA`](../../2-ApplicationFlow/AxisSaga/database-schema.md) — um exemplo completo de um schema definido com `AxisTable`

---

↩ [Voltar à documentação do AxisRepository](README.md)
