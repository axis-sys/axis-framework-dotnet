# Migrations · `IAxisMigrationRunner`

> Aplica as migrations pendentes de um Bounded Context num schema, idempotentemente — a porta de infra trocável que faz par com a metade "renderizar" do [`IAxisSqlDialect`](ddl.md#o-dialeto--iaxissqldialect-e-axissqldialectbase). Cada adapter entrega sua própria implementação, dona do próprio bootstrap, lock de concorrência e semântica de transação; um chamador agnóstico de dialeto migra qualquer provedor trocando o runner injetado junto com o dialeto correspondente.

```csharp
namespace AxisSaga.Postgres.Persistence;

public static class AxisSagaMigrations
{
    // O schema é declarado UMA VEZ no core (AxisSagaSchema); aqui é renderizado com o
    // dialeto Postgres e aplicado pelo runner do framework.
    public static Task InitializePostgresAsync(string connectionString)
        => new PostgresMigrationRunner().RunAsync(
            connectionString,
            AxisSagaSchema.Schema,
            AxisSagaSchema.Migrations(new PostgresSqlDialect()));
}
```

---

## Quando usar

Qualquer pacote ou Bounded Context que seja dono de um schema Postgres ou MySQL e precise dele criado e evoluído no startup — idempotentemente, e com segurança sob múltiplas instâncias migrando o mesmo schema ao mesmo tempo. Pareie com [`AxisTable`](ddl.md) para o DDL em si.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| declarar o formato da tabela (colunas, índices, FKs) | [`AxisTable`](ddl.md) — esta página só **aplica** um script já renderizado |
| usar a ferramenta de migration de um ORM (EF Core Migrations, Flyway, Liquibase) | não suportado — escreva o DDL com `AxisTable` (ou SQL cru) e aplique com `IAxisMigrationRunner`; `AxisRepository` nunca embrulha um ORM |
| mirar um banco sem `IAxisMigrationRunner` ainda | implemente um, pareado com um novo [`IAxisSqlDialect`](ddl.md#o-dialeto--iaxissqldialect-e-axissqldialectbase) para aquele banco |

---

## O contrato

```csharp
public interface IAxisMigrationRunner
{
    Task RunAsync(string connectionString, string schema, (string Version, string Script)[] migrations);
}
```

Um método. `migrations` é um array ordenado de tuplas `(Version, Script)` — tipicamente `{Package}Schema.Migrations(dialect)` ou `{BC}DbInit.Migrations`, cada `Script` produzido por [`AxisTable.Render(dialect)`](ddl.md) ou uma string SQL cru.

## O que `RunAsync` faz, passo a passo

1. **Bootstrap idempotente, fora de qualquer transação** — `CREATE SCHEMA IF NOT EXISTS {schema}`, depois `CREATE TABLE IF NOT EXISTS {schema}.MIGRATIONS (VERSION VARCHAR(50) PRIMARY KEY, APPLIED_AT ... NOT NULL DEFAULT ...)`.
2. **Adquire um lock com escopo no nome do schema** — para que duas instâncias migrando o mesmo schema ao mesmo tempo serializem em vez de correr uma contra a outra.
3. **Para cada `(Version, Script)`, na ordem do array** — pula se `VERSION` já está em `MIGRATIONS`; senão executa `Script`, depois insere a versão.
4. **Libera o lock.**

Os dois runners embarcados seguem esses quatro passos; a mecânica do lock e a fronteira da transação em torno do passo 3 são onde eles divergem — veja abaixo.

---

## Postgres vs MySQL — dois modelos diferentes de concorrência e atomicidade

| | `PostgresMigrationRunner` | `MySqlMigrationRunner` |
|---|---|---|
| **Lock de concorrência** | advisory lock transacional — `SELECT pg_advisory_xact_lock(hashtext(schema))` como primeiro statement da transação; liberado automaticamente no commit ou rollback | named lock com escopo de sessão — `SELECT GET_LOCK(schema, 30)`; liberado explicitamente com `RELEASE_LOCK` num `finally`, então é liberado mesmo se um script lançar |
| **Escopo da transação** | o **lote pendente inteiro** roda dentro de uma única transação; qualquer script que falhe faz rollback de toda migration tentada naquela execução | **sem transação envolvente** — o DDL do MySQL causa um commit implícito e não pode fazer rollback. Cada versão é registrada em `MIGRATIONS` imediatamente após o próprio script ter sucesso, então uma falha no meio do lote deixa as versões já aplicadas registradas; uma nova execução retoma da primeira pendente em vez de tentar de novo o que já desembarcou |
| **Conexão de bootstrap** | abre direto contra o database da connection string | conecta no **nível do servidor** primeiro (`Database` limpo no builder) — o MySQL recusa uma conexão cujo database default ainda não existe (`ERROR 1049`), então o runner cria o próprio database da conexão (quando um é nomeado) e o schema alvo a partir de uma conexão sem database antes de qualquer outra coisa rodar |
| **`MIGRATIONS.APPLIED_AT`** | `TIMESTAMPTZ NOT NULL DEFAULT NOW()` | `DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)` |
| **Falha na aquisição do lock** | bloqueia até o lock ficar disponível (semântica de lock transacional) | lança `InvalidOperationException` se o lock não for adquirido em 30 segundos |

Os dois runners são implementações `sealed class` de `IAxisMigrationRunner`, uma por pacote adapter (`AxisRepository.Postgres` / `AxisRepository.MySql`) — troque o runner e o dialeto correspondente juntos para migrar um banco diferente; o chamador (`{Package}Schema.Migrations`, `{BC}DbInit`) nunca muda.

---

## Idempotência — seguro para chamar em todo startup

`MIGRATIONS.VERSION` é a chave primária da tabela de controle, então "já aplicada" é uma busca de linha, não um palpite. Chamar `InitializePostgresAsync`/`InitializeMySqlAsync` duas vezes seguidas é um no-op na segunda vez — toda versão já está registrada, então `RunAsync` faz o bootstrap (ambos `CREATE ... IF NOT EXISTS`, portanto também um no-op), pega o lock, não encontra nada pendente e retorna. É exatamente isso que deixa um hosted worker ou um test fixture chamar o inicializador incondicionalmente em todo startup em vez de rastrear "eu já migrei?" por conta própria.

**Nunca modifique uma versão já enviada para produção.** Uma `VERSION` registrada nunca é reaplicada, mesmo que você edite a `const string` por trás dela — um script renderizado a partir de um `AxisTable` mudado sob a mesma chave `"V1"` silenciosamente nunca alcança um banco que já tem o `V1` registrado. Em desenvolvimento, editar `V1` diretamente é aceitável (nada rodou contra ele ainda); em produção, sempre acrescente uma nova versão:

```csharp
public static (string Version, string Script)[] Migrations(IAxisSqlDialect dialect) =>
[
    ("V1", /* as chamadas originais de AxisTable.Render(dialect) */),
    ("V2", /* uma tabela adicional, ou SQL cru ALTER/INSERT para a nova */),
];
```

Para a evolução mais comum — uma coluna nova numa tabela existente — [`IAxisSqlDialect.RenderAddColumn`](ddl.md#adicionar-uma-coluna--renderaddcolumn) renderiza o `ALTER TABLE … ADD COLUMN` portável para o script da versão nova, então o `V2` dispensa tokens de engine escritos à mão.

---

## Exemplos reais

### 1. Dois adapters, um schema — `AxisSaga`

```csharp
// AxisSaga.Postgres
public static class AxisSagaMigrations
{
    public static Task InitializePostgresAsync(string connectionString)
        => new PostgresMigrationRunner().RunAsync(
            connectionString, AxisSagaSchema.Schema, AxisSagaSchema.Migrations(new PostgresSqlDialect()));
}

// AxisSaga.MySql
public static class AxisSagaMySqlMigrations
{
    public static Task InitializeMySqlAsync(string connectionString)
        => new MySqlMigrationRunner().RunAsync(
            connectionString, AxisSagaSchema.Schema, AxisSagaSchema.Migrations(new MySqlSqlDialect()));
}
```

Ambos chamam `AxisSagaSchema.Migrations(dialect)`, que renderiza as mesmas quatro definições `AxisTable` — `SagaInstancesTable` primeiro, já que `SagaStageLogsTable` a referencia via FK — num único `"V1"` consolidado. O inicializador de startup de cada adapter de storage (`PostgresSagaStorageInitializer.InitializeAsync` / `MySqlSagaStorageInitializer.InitializeAsync`) chama o `InitializeXAsync` do seu dialeto, e o resumer worker embarcado chama esse inicializador no startup — idempotente, então é um no-op quando uma execução anterior (ou um test fixture) já migrou o schema. Nunca destrói dados.

**Por que compensa:** o schema é definido uma única vez; escolher Postgres ou MySQL para um deploy é a troca de um adapter numa linha, não dois scripts de DDL para manter sincronizados.

### 2. Um schema de pacote com tabela única — `AxisCache`

```csharp
public static class AxisCacheSchema
{
    public const string Schema = CacheEntriesTable.Schema;

    public static (string Version, string Script)[] Migrations(IAxisSqlDialect dialect) =>
        [("V1", CacheEntriesTable.Define().Render(dialect))];
}
```

O inicializador de um adapter roda `new PostgresMigrationRunner().RunAsync(connectionString, AxisCacheSchema.Schema, AxisCacheSchema.Migrations(new PostgresSqlDialect()))` (ou o equivalente MySQL) da mesma forma — o padrão não muda com o número de tabelas.

**Por que compensa:** um pacote de uma tabela paga exatamente o mesmo custo de fiação que um de quatro tabelas — não há cerimônia extra para reduzir de escala.

---

## Veja também

- [DDL de schema · `AxisTable`](ddl.md) — declara o que `RunAsync` aplica
- [Adapter Postgres](postgres-adapter.md) — embarca `PostgresMigrationRunner` e `PostgresSqlDialect`
- [Adapter MySQL](mysql-adapter.md) — embarca `MySqlMigrationRunner` e `MySqlSqlDialect`
- [Schema do banco · `AXIS_SAGA`](../../2-ApplicationFlow/AxisSaga/database-schema.md) — o schema real completo que estes exemplos referenciam

---

↩ [Voltar à documentação do AxisRepository](README.md)
