# Adapter MySQL · `AxisSaga.MySql`

> O adapter de storage MySQL sobre o núcleo de saga compartilhado. Um único `MySqlDataSource` singleton contra o schema `AXIS_SAGA`, mais as quatro portas de store do MySQL; o núcleo agnóstico de dialeto fornece o `SagaEngine` que dirige uma instância para frente, o `SagaMediator` que insere novas linhas e dispara o engine em background, o `SagaResumer` para recovery e o `SagaDefinitionInitializer` que persiste definições no startup. O adapter irmão `AxisSaga.Postgres` (`AddAxisSagaPostgres`) compartilha esse mesmo núcleo e difere só no dialeto SQL e em alguns contornos de concorrência específicos do MySQL, documentados abaixo.

```csharp
services.AddAxisSagaMySql(new AxisSagaSettings
{
    ConnectionString    = "Server=…",
    ResumerPollInterval = TimeSpan.FromSeconds(30),
    ResumeAfter         = TimeSpan.FromSeconds(60),
    ResumeBatchSize     = 100,
});
```

---

## Quando usar

MySQL — seu próprio servidor, RDS, Aurora MySQL, Cloud SQL. O adapter espera um único banco para **todas** as sagas do processo; `AddAxisSagaMySql` recusa um segundo registro de propósito.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| mirar Postgres | `AddAxisSagaPostgres` do `AxisSaga.Postgres` — um adapter irmão sobre o mesmo núcleo compartilhado, settings e runtime idênticos |
| mirar SQL Server / Mongo | escreva um novo adapter contra as mesmas portas de store (`ISagaInstanceStore`, `ISagaStageLogStore`, `ISagaDefinitionStore`, `IAxisSagaStorageInitializer`); o núcleo agnóstico de dialeto (`AddAxisSagaCore`) fornece o resto |
| guardar sagas num topic Kafka | um adapter custom — os adapters embarcados assumem durabilidade relacional |
| compartilhar o storage de sagas com o resto do app | possível — aponte os dois para o mesmo banco; o schema `AXIS_SAGA` é isolado por nome |

---

## O que `AddAxisSagaMySql(settings)` registra

Lendo `DependencyInjection.AddAxisSagaMySql` direto: ele registra o data source do MySQL e as quatro portas de store específicas do dialeto, e então chama `AddAxisSagaCore(settings)` para o runtime agnóstico de dialeto (a mesma chamada que `AddAxisSagaPostgres` faz).

| Service | Lifetime | Descrição |
|---|---|---|
| `AxisSagaSettings` | singleton | o objeto de configuração (registrado pelo núcleo) |
| `AxisSagaMySqlDataSource` | singleton | embrulha um `MySqlDataSource` construído via `MySqlDataSourceBuilder`, com um callback de conexão-aberta que fixa toda conexão física nova em `READ COMMITTED` (específico do MySQL — veja abaixo) |
| `IAxisSagaDefinitionRegistry` | singleton | store em memória de `AxisSagaDefinition`s compiladas |
| `IAxisSagaMediator` | scoped | `SagaMediator` (start / get / resume) |
| `SagaEngine` | scoped | o driver por instância |
| `ISagaStageHandlerInvoker` | scoped | resolve e chama o `IAxisSagaStageHandler<TPayload>` que casa |
| `IAxisSagaResumer` | scoped | `SagaResumer` (o recovery por polling) |
| `IAxisSagaJanitor` | scoped | `SagaJanitor` (deleta sagas terminais cuja retenção expirou) |
| `IAxisSagaDefinitionInitializer` | scoped | `SagaDefinitionInitializer` (grava definições no catálogo no startup) |
| `AxisSagaResumerWorker` | hosted service | o loop de background embutido (registrado só quando `AxisSagaSettings.ResumerEnabled`, o default) |
| `ISagaInstanceStore`, `ISagaStageLogStore`, `ISagaDefinitionStore` | scoped | acesso row-level MySQL às tabelas — `MySqlSagaInstanceStore`, `MySqlSagaStageLogStore`, `MySqlSagaDefinitionStore` (específico do dialeto) |
| `IAxisSagaStorageInitializer` | singleton | `MySqlSagaStorageInitializer` — roda a migration do schema no startup (específico do dialeto) |

Uma segunda chamada a `AddAxisSagaMySql` **sem serviceKey** (ou a `AddAxisSagaPostgres`) lança — "um storage por processo por design". Para rodar **vários** stores independentes num só processo (um por subdomínio), use o overload keyed abaixo.

---

## Keyed por subdomínio — vários stores num processo

`AddAxisSagaMySql(serviceKey, settings)` registra o **mesmo** runtime, porém keyed por `serviceKey` — a mesma convenção do `AddMySqlUnitOfWork(serviceKey, connectionString)` do `AxisRepository`. Assim dois subdomínios independentes rodam **sagas independentes contra bancos independentes** no mesmo monólito, sem esperar o split em microsserviços. As APIs sem serviceKey seguem idênticas e podem coexistir.

```csharp
services.AddAxisSagaMySql("ecommerce", new AxisSagaSettings { ConnectionString = ecommerceConn });
services.AddAxisSagaMySql("financas",  new AxisSagaSettings { ConnectionString = financasConn });

// defina cada saga sob a key do seu subdomínio
services.AddKeyedSingleton("ecommerce", AxisSagaDefinitions.Define<OrderPayload>(OrderSaga.Name, OrderSaga.Configure));
services.AddAxisSagaHandlers(typeof(OrderSaga).Assembly);   // handlers continuam globais, casados por (payload, saga, stage)
```

O consumidor injeta o mediator do subdomínio com `[FromKeyedServices("ecommerce")] IAxisSagaMediator`.

Diferenças em relação ao caminho não-keyed — e ao adapter Postgres:

- **Guard por-key.** Uma segunda chamada com a **mesma** key lança; keys distintas convivem.
- **Datasource sempre próprio.** Diferente do Postgres, o adapter MySQL **não reusa** o `MySqlDataSource` do repositório: cada key constrói o seu via `BuildDataSource`, que fixa toda conexão em `READ COMMITTED` (o `MySqlDataSource` simples do repositório não faz isso, e o claim de lease sofre gap-lock sob o `REPEATABLE READ` padrão do InnoDB — veja a seção de isolation acima).
- **Definições isoladas por key.** Cada subdomínio registra suas definições com `AddKeyedSingleton<AxisSagaDefinition>(serviceKey, Define(...))`; o registry keyed enxerga só as suas — dois subdomínios podem ter sagas de mesmo nome.
- **Um resumer worker por key.** Cada store keyed hospeda seu próprio `AxisSagaResumerWorker` (quando `ResumerEnabled`).

> Decisão registrada em [ADR-0003](../../../adr/0003-axis-saga-keyed-per-subdomain-storage.md).

---

## Isolation level — toda conexão é fixada em `READ COMMITTED`

Este é o único comportamento **sem equivalente no Postgres**. `DependencyInjection.BuildDataSource` registra um `UseConnectionOpenedCallback` que roda em toda conexão física nova:

```csharp
// The store pins every saga-store connection to READ COMMITTED. The lease claim
// (MySqlSagaInstanceStore.AcquireLeaseAsync) gates on a COUNT over SAGA_INSTANCES inside the
// UPDATE; under InnoDB's default REPEATABLE READ that scan takes next-key/gap locks, which
// deadlock against concurrent claims and block concurrent INSERTs under the import fan-out.
// READ COMMITTED drops the gap locks. The global cap is already a soft cap by design, so the
// looser isolation does not change its semantics. The SET runs only on brand-new physical
// connections; the session setting persists across pool reuse.
private static MySqlDataSource BuildDataSource(string connectionString)
{
    MySqlDataSourceBuilder builder = new(connectionString);
    builder.UseConnectionOpenedCallback(async (context, cancellationToken) =>
    {
        if ((context.Conditions & MySqlConnectionOpenedConditions.New) == 0)
            return;

        await using MySqlCommand cmd = context.Connection.CreateCommand();
        cmd.CommandText = "SET SESSION TRANSACTION ISOLATION LEVEL READ COMMITTED";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    });
    return builder.Build();
}
```

(`AxisSaga.MySql/DependencyInjection.cs`.) Sob o `REPEATABLE READ` padrão do InnoDB, o gate do teto global de concorrência que `MySqlSagaInstanceStore.AcquireLeaseAsync` roda — um scan `COUNT(*)` sobre `AXIS_SAGA.SAGA_INSTANCES` — toma locks next-key/gap que causam deadlock contra outros claims de lease concorrentes e bloqueiam `INSERT`s concorrentes durante o fan-out de importação. `READ COMMITTED` remove esses gap locks. Como o teto de concorrência já é um teto *soft* por design (veja [Schema do banco](database-schema.md#axis_sagasaga_settings)), o isolamento mais frouxo não muda sua semântica — ele só remove um vetor de deadlock específico do modelo de locking do MySQL/InnoDB. O `SET SESSION` roda uma vez por conexão física nova; a configuração de sessão então persiste pela vida daquela conexão através do reuso do pool.

Para evitar ainda mais gap locks, o próprio claim de lease (`AcquireLeaseAsync`) lê o gate de concorrência como um `SELECT` separado e não-locking, e então reivindica estritamente **por chave primária** (`WHERE SAGA_ID = @id AND …`) — um match de igualdade de uma única linha que não pode fazer range-scan nem gap-lock, diferente do claim em lote do Postgres com `FOR UPDATE SKIP LOCKED`.

---

## Retry transiente — `MySqlTransientRetry`

Toda escrita no store de sagas passa por `MySqlTransientRetry.ExecuteAsync`, que tenta novamente até **5 tentativas** com backoff com jitter (`20ms * tentativa + random(0–25ms)`) sempre que a `MySqlException` subjacente é transiente — a mesma classificação (`MySqlTransientErrors.IsTransient`) compartilhada com `MySqlRepositoryBase`: deadlocks, lock-wait timeouts e soluços de conexão.

```csharp
internal static class MySqlTransientRetry
{
    private const int MaxAttempts = 5;

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (MySqlException ex) when (MySqlTransientErrors.IsTransient(ex) && attempt < MaxAttempts)
            {
                // Brief backoff with jitter so the racing runners desynchronize before retrying.
                await Task.Delay(TimeSpan.FromMilliseconds(20 * attempt + Random.Shared.Next(0, 25)));
            }
        }
    }
}
```

Diferente do `MySqlRepositoryBase` — que precisa deixar um transiente subir quando sua unit-of-work já segura uma escrita não commitada —, o store de sagas escreve em autocommit (uma instrução por conexão), então um transiente nunca deixa uma escrita durável pela metade e refazer a operação no lugar é sempre seguro. É por isso que o adapter de saga tenta de novo internamente em vez de deixar a exceção subir até o engine.

---

## O pipeline de um stage

Quando o engine roda um stage (em `SagaEngine.ExecuteAsync(sagaId)`), os passos são idênticos aos do adapter Postgres — carrega, resolve, loga `Started`, invoca, então atualiza + loga + roteia — com duas diferenças específicas do MySQL:

1. **Sem `RETURNING`.** O MySQL não tem cláusula `RETURNING`, então `AcquireLeaseAsync` reivindica a linha com um `UPDATE` e depois a lê de volta com um `SELECT … WHERE SAGA_ID = @id AND CLAIMED_BY = @runner` subsequente. Como o token do runner é único para aquele run, isso lê exatamente a linha recém-reivindicada mesmo sem uma transação ao redor.
2. **Timestamps.** Toda comparação de "agora" usa `UTC_TIMESTAMP(6)` em vez do `NOW()` do Postgres; colunas são renderizadas como `DATETIME(6)` em vez de `timestamptz`.

> Concorrência: cada update que muda estado ainda guarda na version **e** na posse do lease — `WHERE VERSION = @currentVersion AND CLAIMED_BY = @runner AND CLAIMED_UNTIL > UTC_TIMESTAMP(6)` — e incrementa a version. Uma execução que perdeu o lease, ou um segundo engine concorrente, vê o mismatch e falha com `AxisSagaErrors.ConcurrencyConflict` — a saga **não** é dirigida em dose dupla, exatamente como no Postgres.

---

## Bootstrap — `SagaDefinitionInitializer`

Idêntico ao adapter Postgres: no startup, o initializer lê cada `AxisSagaDefinition` registrada no registry em memória e faz **upsert** de cada uma como uma linha em `AXIS_SAGA.SAGA_DEFINITIONS` (via `ON DUPLICATE KEY UPDATE` sob o dialeto do MySQL). Você **não** fia isso você mesmo — o resumer worker embutido roda a migration do storage e então dispara `IAxisSagaDefinitionInitializer.InitializeAsync` uma vez na sua primeira passada, antes de começar o polling. O `SagaDefinitionInitializer` agnóstico de dialeto é o mesmo no Postgres e no MySQL; só o `ISagaDefinitionStore` por onde ele faz upsert (`MySqlSagaDefinitionStore`) é específico do dialeto.

---

## Resumer — embutido, sem worker para escrever à mão

O resumer **não** é algo que você hospeda. `AddAxisSagaMySql` (e `AddAxisSagaPostgres`) auto-registram o `AxisSagaResumerWorker`, um `BackgroundService`, sempre que `AxisSagaSettings.ResumerEnabled` é `true` (o default). No startup ele:

1. Roda a migration idempotente do schema via `IAxisSagaStorageInitializer` (`AxisSagaMySqlMigrations.InitializeMySqlAsync`, no-op se já aplicada);
2. Inicializa as definições de saga registradas uma vez;
3. Faz polling de `IAxisSagaResumer.RunOnceAsync` a cada `ResumerPollInterval`, reivindicando e re-disparando instâncias travadas.

Ponha `ResumerEnabled = false` só num processo que precise iniciar/aguardar sagas mas não rodar o loop (recovery de responsabilidade de outro processo, ou um teste sem banco vivo).

Cada poll reivindica sagas travadas via `ISagaInstanceStore.ClaimStaleSagaIdsAsync`. Diferente do claim em lote do Postgres com `FOR UPDATE SKIP LOCKED`, a implementação MySQL roda um `SELECT` simples (`STATUS IN ('Pending','Running','Compensating')` e `CLAIMED_UNTIL IS NULL OR CLAIMED_UNTIL < UTC_TIMESTAMP(6)`, ordenado de forma que leases `NULL` venham primeiro, casando com o `NULLS FIRST` do Postgres) sem lock em nível de linha — a deduplicação real acontece depois, no claim de lease atômico por linha do engine (`AcquireLeaseAsync`, reivindicado estritamente por chave primária), então um re-disparo que corre contra outro resumer é um no-op inofensivo em vez de uma questão de locking.

Veja [`IAxisSagaResumer`](resumer.md) para a semântica.

---

## Índices — índices parciais viram índices comuns

O MySQL não tem predicado `WHERE` em `CREATE INDEX`. Os dois índices parciais que o Postgres renderiza em `AXIS_SAGA.SAGA_INSTANCES` — `IDX_SAGA_INSTANCES_DELETE_NOT_BEFORE` (em `DELETE_NOT_BEFORE`, onde não-nulo) e `IDX_SAGA_INSTANCES_ACTIVE_LEASE` (em `CLAIMED_UNTIL`, onde `STATUS` é não-terminal) — saem como índices comuns, de tabela inteira, sob o dialeto do MySQL (`MySqlSqlDialect.RenderInlineIndexLines` descarta o predicado para índices não-únicos e emite um `INDEX nome (cols)` normal). Eles ainda atendem às mesmas queries; são simplesmente menos seletivos que seus equivalentes no Postgres, já que o MySQL indexa toda linha em vez de só as que qualificam. Veja [Schema do banco](database-schema.md#notas-de-indexação) para a nota de indexação completa.

---

## Exemplo real — fiação de produção

```csharp
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddAxisMemoryBus()
    .AddAxisSagaMySql(new AxisSagaSettings
    {
        ConnectionString    = builder.Configuration.GetConnectionString("MySql")!,
        ResumerPollInterval = TimeSpan.FromSeconds(30),
        ResumeAfter         = TimeSpan.FromSeconds(60),
        ResumeBatchSize     = 100,
    })
    .AddAxisSagaHandlers(Assembly.GetExecutingAssembly());

// registre cada definição de saga para que o engine consiga resolvê-la
builder.Services.AddSingleton(
    AxisSagaDefinitions.Define<OrderPayload>(OrderSagaDefinition.Name, OrderSagaDefinition.Configure));
```

Nenhum hosted service para adicionar à mão: `AddAxisSagaMySql` já registrou o resumer worker embutido, que migra o schema, inicializa as definições e roda o recovery. (Troque `AddAxisSagaMySql` por `AddAxisSagaPostgres` — mesmos settings, mesma fiação — para rodar no Postgres no lugar.)

**Por que compensa:** a aplicação só fala com `IAxisSagaMediator`. Storage, engine e recovery são fiados uma vez na raiz de composição — e os dois contornos específicos do MySQL acima (isolation level, retry transiente) são tratados dentro do adapter, invisíveis ao código da aplicação.

---

## Veja também

- [Adapter Postgres](postgres-adapter.md) — o adapter irmão sobre o mesmo núcleo compartilhado
- [Schema do banco](database-schema.md) — as quatro tabelas de negócio (+ a tabela de controle `MIGRATIONS` que o runner de migrations do framework mantém) que o adapter cria
- [Mediator · `IAxisSagaMediator`](mediator.md) — a superfície de API
- [Resumer · `IAxisSagaResumer`](resumer.md) — o loop de recovery
- [Conceitos](concepts.md) — o que o engine está dirigindo
- [Stage handlers](stage-handlers.md) — o que o engine invoca

---

↩ [Voltar à documentação do AxisSaga](README.md)
