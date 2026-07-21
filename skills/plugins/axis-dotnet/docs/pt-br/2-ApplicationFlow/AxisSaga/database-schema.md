# Schema do banco · `AXIS_SAGA`

> O schema é declarado **uma única vez** no core do AxisSaga (`Axis.Persistence.Scripts.AxisSagaSchema`) e renderizado por dialeto por um `IAxisSqlDialect` injetado. Um adapter de storage cria e é dono de um único schema: `AXIS_SAGA`. Quatro tabelas — `SAGA_DEFINITIONS`, `SAGA_INSTANCES`, `SAGA_STAGE_LOGS`, `SAGA_SETTINGS` — cobrem o catálogo, o estado vivo, o log forense por stage e os parâmetros de runtime do processo. O runner de migration do framework também cria uma tabela de controle `MIGRATIONS` que rastreia quais versões de DDL já foram aplicadas.

```
AXIS_SAGA
├── SAGA_DEFINITIONS
├── SAGA_INSTANCES
├── SAGA_STAGE_LOGS
├── SAGA_SETTINGS        (parâmetros de runtime do processo)
└── MIGRATIONS           (bookkeeping de migrations, criada pelo runner do framework)
```

> O storage **não** é mais só Postgres. As quatro definições de tabela vivem no core (`SagaInstancesTable` / `SagaStageLogsTable` / `SagaDefinitionsTable` / `SagaSettingsTable` como defs `AxisTable`); os adapters de storage embarcados as compartilham — hoje **AxisSaga.Postgres**, **AxisSaga.MySql**, entre outros — cada um passando o próprio dialeto. Qualquer banco com um `IAxisSqlDialect` e as portas de store pode ser adicionado como mais um adapter. O schema é compartilhado por **toda saga no processo** — não há slicing por BC. É deliberado: um schema, um resumer, uma tabela forense para consultar.

> Todo identificador nesta página é escrito exatamente como declarado na fonte — em maiúsculas. O Postgres dobra identificadores não-quotados para minúsculas, então `SELECT * FROM AXIS_SAGA.SAGA_INSTANCES` e seu equivalente todo em minúsculas nomeiam o mesmo objeto lá; o MySQL, por outro lado, preserva a caixa exatamente como escrita. A forma em maiúsculas usada ao longo desta página é, portanto, segura de usar literalmente em ambos.

---

## Quando isso importa

O schema é invisível ao código da aplicação — handlers nunca leem. Você vê quando:

- Desenhando migrations / backups de banco.
- Escrevendo dashboards admin que leem o estado das sagas direto.
- Debugando instâncias travadas.
- Auditando o que aconteceu numa saga dada, stage por stage.

---

## `AXIS_SAGA.SAGA_DEFINITIONS`

O catálogo. Uma linha por saga conhecida pelo processo.

| Coluna | Tipo | Propósito |
|---|---|---|
| `SAGA_NAME` | `varchar(100)` (PK) | nome lógico da saga (casa com `IAxisSagaStageHandler.SagaName`) |
| `DEFINITION_HASH` | `varchar(64)` | hash SHA-256 da definição serializada; usado para detectar mudanças e evitar reescritas redundantes |
| `DEFINITION_JSON` | `jsonb` | a `AxisSagaDefinition` inteira serializada (inclui o type name de `TPayload`); usado por dashboards ops |
| `UPDATED_AT` | `timestamptz` | bookkeeping |

O engine em runtime lê do `IAxisSagaDefinitionRegistry` em memória, não desta tabela. A tabela existe para que um processo separado (ou um dashboard) possa responder "que sagas o código deployado conhece?" sem ter uma referência .NET ao assembly.

---

## `AXIS_SAGA.SAGA_INSTANCES`

Uma linha por instância de saga. Este é o estado vivo.

| Coluna | Tipo | Propósito |
|---|---|---|
| `SAGA_ID` | `varchar(50)` (PK) | o id fornecido pelo chamador (estilo `Guid`) |
| `SAGA_NAME` | `varchar(100)` | a definição a que esta instância está ligada |
| `STATUS` | `varchar(30)` | `Pending` / `Running` / `Completed` / `Failed` / `Compensating` / `Compensated` |
| `CURRENT_STAGE` | `varchar(50) NULL` | o stage onde o engine está (NULL antes do primeiro stage rodar) |
| `PAYLOAD_JSON` | `jsonb` | o payload mais recente retornado pelo handler mais recente |
| `LAST_ERROR_CODE` | `varchar(100) NULL` | o `AxisError.Code` da falha mais recente |
| `LAST_ERROR_MESSAGE` | `text NULL` | a versão amigável humana |
| `VERSION` | `int` | token de concorrência otimista; incrementado em cada update |
| `CREATED_AT` / `UPDATED_AT` | `timestamptz` | bookkeeping |
| `RETAIN_FOR_SECONDS` | `int NULL` | janela de retenção: por quanto tempo manter a linha após chegar a um status terminal antes de o janitor poder deletá-la (`NULL` = manter para sempre) |
| `DELETE_NOT_BEFORE` | `timestamptz NULL` | preenchido quando a saga vai a terminal (a partir de `RETAIN_FOR_SECONDS`); o janitor deleta a linha quando `NOW()` passa deste instante |
| `CLAIMED_BY` | `varchar(50) NULL` | o dono do **lease** de execução (o token do runner) — substitui o advisory lock segurado |
| `CLAIMED_UNTIL` | `timestamptz NULL` | quando o lease atual expira; um run é o dono apenas enquanto `CLAIMED_BY` casa e `CLAIMED_UNTIL > NOW()` |

> Cada update que modifica o estado guarda em **ambos** concorrência otimista e posse do lease: `WHERE SAGA_ID = @id AND VERSION = @currentVersion AND CLAIMED_BY = @runner AND CLAIMED_UNTIL > NOW()`. Um run só muta a linha enquanto detém um lease vivo; se um segundo engine de alguma forma rodar a mesma instância concorrentemente, aquele sem a version/lease correspondente vê zero linhas atualizadas e aborta com `AxisSagaErrors.ConcurrencyConflict`. O lease é adquirido por `AcquireLeaseAsync` (que também aplica o teto global de concorrência) e renovado por um heartbeat a cada `ResumeAfter / 4`.

### Como o resumer consulta isso

O resumer é um **worker hospedado embutido** (`AxisSagaResumerWorker`, auto-registrado pelo adapter de storage quando `AxisSagaSettings.ResumerEnabled` está ligado — o padrão). Sua query de claim (`SagaInstanceStore.ClaimStaleSagaIdsAsync`) é uma leitura pura que seleciona sagas stale — não-terminais e com lease expirado (ou nunca setado):

```sql
SELECT SAGA_ID
FROM AXIS_SAGA.SAGA_INSTANCES
WHERE STATUS IN ('Pending', 'Running', 'Compensating')
  AND (CLAIMED_UNTIL IS NULL OR CLAIMED_UNTIL < NOW())
ORDER BY CLAIMED_UNTIL NULLS FIRST
LIMIT @batch
FOR UPDATE SKIP LOCKED;
```

`FOR UPDATE SKIP LOCKED` trava as linhas candidatas para que um resumer concorrente em outro node simplesmente pule as já tomadas. O select em si não muta estado: o resumer re-dispara cada saga retornada via `mediator.ResumeAsync`, e o engine re-adquire o lease via `AcquireLeaseAsync` (que também aplica o teto global de concorrência). É isso que torna o resumer seguro para rodar em cada node — uma vez que o lease de uma saga está vivo de novo, as queries de claim dos outros nodes a pulam, então múltiplos resumers não disparam de novo a mesma saga. `@batch` é o `ResumeBatchSize` (padrão 100), o número máximo de sagas reivindicadas por poll; quando há um teto global setado, o resumer ainda o reduz ao número de slots de lease livres.

---

## `AXIS_SAGA.SAGA_STAGE_LOGS`

Uma linha por transição de stage. O log forense.

| Coluna | Tipo | Propósito |
|---|---|---|
| `LOG_ID` | `varchar(50)` (PK) | id único do registro (UUID v7) |
| `SAGA_ID` | `varchar(50)` | chave estrangeira em `SAGA_INSTANCES` (`ON DELETE CASCADE`) |
| `STAGE_NAME` | `varchar(50)` | o stage neste evento |
| `ATTEMPT` | `int` | número da tentativa deste stage (padrão `1`) |
| `STATUS` | `varchar(30)` | `Started` / `Completed` / `Failed` |
| `ERROR_CODE` | `varchar(100) NULL` | preenchido quando `STATUS = 'Failed'` |
| `ERROR_MESSAGE` | `text NULL` | a versão amigável humana |
| `STARTED_AT` | `timestamptz` | quando o stage começou (UTC) |
| `FINISHED_AT` | `timestamptz NULL` | quando terminou (UTC; `NULL` enquanto em andamento) |

### O que você ganha para fazer com isso

```sql
-- liste cada stage que já rodou para uma saga, em ordem
SELECT STAGE_NAME, STATUS, STARTED_AT, FINISHED_AT
FROM AXIS_SAGA.SAGA_STAGE_LOGS
WHERE SAGA_ID = 'order-01927a8b-…'
ORDER BY STARTED_AT;

-- falhas-por-stage na última semana
SELECT STAGE_NAME, count(*) failures
FROM AXIS_SAGA.SAGA_STAGE_LOGS
WHERE STATUS = 'Failed' AND STARTED_AT > NOW() - INTERVAL '7 days'
GROUP BY STAGE_NAME
ORDER BY failures DESC;
```

> Combine `SAGA_STAGE_LOGS` com `SAGA_INSTANCES.STATUS` para dashboards: "mostre cada saga que começou a compensar na última hora e nunca alcançou `Compensated`".

---

## `AXIS_SAGA.SAGA_SETTINGS`

Parâmetros de runtime do processo, mantidos numa **única linha** compartilhada por toda instância da aplicação que aponta para este banco.

| Coluna | Tipo | Propósito |
|---|---|---|
| `ONLY_ROW` | `boolean` (PK, `CHECK (ONLY_ROW)`) | fixa a tabela em exatamente uma linha |
| `MAX_CONCURRENT_SAGAS` | `int NULL` | teto global de quantas sagas podem segurar um **lease vivo** (estar executando) ao mesmo tempo, somando todas as instâncias; `NULL` = ilimitado |

### Por que mora aqui e não na config da app

`MAX_CONCURRENT_SAGAS` é um limite **global**. Se ele morasse na configuração de cada aplicação, duas instâncias poderiam ser deployadas com valores diferentes e o teto "global" silenciosamente deixaria de ser global. Guardá-lo no banco compartilhado o torna fonte única de verdade, lida por toda instância a cada claim de lease.

O claim de lease recusa admitir uma saga assim que a contagem de leases vivos atinge o teto; a saga adiada fica `Pending` e o resumer a retoma quando abre vaga, então nada é perdido. É um teto *soft* — uma rajada de claims concorrentes pode excedê-lo transitoriamente por uma margem pequena e auto-corretiva, o que é aceitável para o propósito (limitar a carga no pool de conexões).

### Como alterar

Tem efeito no próximo claim de lease — sem redeploy, sem migration.

**Por código (recomendado) — `IAxisSagaSettingsStore`.** O adapter de storage registra esse port voltado ao consumidor, então a aplicação lê e ajusta o teto sem SQL na mão. Uma única implementação agnóstica de dialeto sobre ADO.NET serve todos os storages, então o comportamento é idêntico em Postgres e MySQL. Todos os métodos devolvem um `AxisResult` e nunca lançam.

```csharp
public interface IAxisSagaSettingsStore
{
    // Ok(null) = ilimitado.
    Task<AxisResult<int?>> GetMaxConcurrentSagasAsync(CancellationToken ct = default);

    // Set incondicional; null = ilimitado; um teto zero/negativo é erro de validação.
    Task<AxisResult> SetMaxConcurrentSagasAsync(int? maxConcurrentSagas, CancellationToken ct = default);

    // Set condicional race-safe: grava newValue só se o teto armazenado ainda for igual a expectedCurrent.
    // Ok(true) = alterou; Ok(false) = o guard não bateu (alguém já mudou / nunca foi expectedCurrent).
    // Nunca sobrescreve um valor ajustado manualmente.
    Task<AxisResult<bool>> TrySetMaxConcurrentSagasAsync(int expectedCurrent, int? newValue, CancellationToken ct = default);
}
```

`TrySetMaxConcurrentSagasAsync` é a operação idiomática "elevar o default semeado para um teto operacional maior logo após uma migration, mas apenas enquanto ele ainda valer o seed — e nunca sobrescrever um valor que um operador ajustou à mão", feita como uma única instrução atômica:

```csharp
// ex.: logo após o endpoint de migrations rodar: eleva 20 → 75, mas só se ainda for o seed 20.
await settingsStore.TrySetMaxConcurrentSagasAsync(expectedCurrent: 20, newValue: 75);
```

**Por SQL (equivalente).** Como é uma linha única simples, um `UPDATE` manual também funciona:

```sql
-- limita o total de sagas concorrentes, em todas as instâncias, em 20
UPDATE AXIS_SAGA.SAGA_SETTINGS SET MAX_CONCURRENT_SAGAS = 20;

-- desliga o teto (ilimitado)
UPDATE AXIS_SAGA.SAGA_SETTINGS SET MAX_CONCURRENT_SAGAS = NULL;
```

A migration `V1` consolidada faz o seed da linha com `20`.

---

## Migrations

As quatro definições de tabela vivem uma única vez no core (`AxisSagaSchema.Tables`). O adapter as renderiza com seu dialeto e as aplica pelo runner de migration do framework — `AxisSagaMigrations.InitializePostgresAsync` chama `PostgresMigrationRunner.RunAsync`; o `AxisSagaMySqlMigrations.InitializeMySqlAsync` do adapter MySQL chama `MySqlMigrationRunner.RunAsync`. O runner cria o schema (`CREATE SCHEMA IF NOT EXISTS`) e a tabela de bookkeeping de forma idempotente.

Essa tabela de controle é `AXIS_SAGA.MIGRATIONS` (`VERSION` PK, `APPLIED_AT TIMESTAMPTZ`), criada pelo runner do framework — e **não** uma tabela `schema_migrations` criada pelo adapter. O runner registra nela cada versão de DDL aplicada (sob um advisory lock transacional por schema) e pula versões já registradas em vez de reemiti-las. Hoje o schema inteiro é entregue como uma única `V1` consolidada — `AxisSagaSchema.Migrations(dialect)` renderiza todas as tabelas nessa única versão (o framework ainda não tem deploys em produção); alterar essa `V1` exige recriar o banco, já que uma versão registrada nunca é reaplicada.

No startup, o `AxisSagaResumerWorker` embutido chama o storage initializer para rodar essa migration (idempotente — versões já aplicadas são puladas), então é um no-op quando um test fixture ou um run anterior já migrou o schema. Nunca destrói dados.

---

## Notas de indexação

As definições de tabela criam:

- `PRIMARY KEY` em `SAGA_NAME` (SAGA_DEFINITIONS), `SAGA_ID` (SAGA_INSTANCES), `LOG_ID` (SAGA_STAGE_LOGS), e a PK booleana `ONLY_ROW` em `SAGA_SETTINGS`.
- Em `SAGA_INSTANCES`: um índice em `(STATUS, UPDATED_AT)`, um em `SAGA_NAME`, um em `(STATUS, CLAIMED_UNTIL)` para o claim do resumer keyed no lease, mais dois índices parciais — um em `DELETE_NOT_BEFORE` (onde não-nulo) para o janitor, e um de lease-ativo em `CLAIMED_UNTIL` (onde status é não-terminal) para a contagem de leases vivos do teto global de concorrência. No MySQL os índices parciais são renderizados como índices comuns.
- Um índice em `SAGA_STAGE_LOGS.(SAGA_ID, STAGE_NAME, STATUS)` (a query forense).

Para workloads maiores considere particionar `SAGA_STAGE_LOGS` por `STARTED_AT` — cresce muito mais rápido que `SAGA_INSTANCES`.

---

## Veja também

- [Adapter Postgres](postgres-adapter.md) — o que escreve / lê nessas tabelas
- [Adapter MySQL](mysql-adapter.md) — o comportamento específico do MySQL nessas mesmas tabelas
- [Mediator · `IAxisSagaMediator`](mediator.md) — a API user-facing
- [Resumer · `IAxisSagaResumer`](resumer.md) — usa o índice de `SAGA_INSTANCES`
- [Conceitos](concepts.md) — o que as colunas significam
- [AxisRepository · DDL de schema](../../3-Infra/AxisRepository/ddl.md) — o modelo `AxisTable` do qual estas quatro tabelas são construídas
- [AxisRepository · Migrations](../../3-Infra/AxisRepository/migrations.md) — o runner que as aplica idempotentemente

---

↩ [Voltar à documentação do AxisSaga](README.md)
