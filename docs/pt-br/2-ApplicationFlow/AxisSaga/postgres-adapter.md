# Adapter Postgres · `AxisSaga.Postgres`

> O adapter de storage Postgres sobre o núcleo de saga compartilhado. Um único `NpgsqlDataSource` singleton contra o schema `AXIS_SAGA`, mais as quatro portas de store do Postgres; o núcleo agnóstico de dialeto fornece o `SagaEngine` que dirige uma instância para frente, o `SagaMediator` que insere novas linhas e dispara o engine em background, o `SagaResumer` para recovery e o `SagaDefinitionInitializer` que persiste definições no startup. Adapters irmãos como o `AxisSaga.MySql` (`AddAxisSagaMySql`) — entre outros, à medida que forem lançados — compartilham esse mesmo núcleo e diferem só no dialeto SQL.

```csharp
services.AddAxisSagaPostgres(new AxisSagaSettings
{
    ConnectionString    = "Host=…",
    ResumerPollInterval = TimeSpan.FromSeconds(30),
    ResumeAfter         = TimeSpan.FromSeconds(60),
    ResumeBatchSize     = 100,
});
```

---

## Quando usar

PostgreSQL — seu próprio servidor, RDS, Aurora, Cloud SQL. O adapter espera um único banco para **todas** as sagas do processo; `AddAxisSagaPostgres` recusa um segundo registro de propósito.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| mirar MySQL | `AddAxisSagaMySql` do `AxisSaga.MySql` — um adapter irmão sobre o mesmo núcleo compartilhado, settings e runtime idênticos |
| mirar SQL Server / Mongo | escreva um novo adapter contra as mesmas portas de store (`ISagaInstanceStore`, `ISagaStageLogStore`, `ISagaDefinitionStore`, `IAxisSagaStorageInitializer`); o núcleo agnóstico de dialeto (`AddAxisSagaCore`) fornece o resto |
| guardar sagas num topic Kafka | um adapter custom — os adapters embarcados assumem durabilidade relacional |
| compartilhar o storage de sagas com o resto do app | possível — aponte os dois para o mesmo banco; o schema `AXIS_SAGA` é isolado por nome |

---

## O que `AddAxisSagaPostgres(settings)` registra

Lendo `DependencyInjection.AddAxisSagaPostgres` direto: ele registra o data source do Postgres e as quatro portas de store específicas do dialeto, e então chama `AddAxisSagaCore(settings)` para o runtime agnóstico de dialeto (a mesma chamada que `AddAxisSagaMySql` faz).

| Service | Lifetime | Descrição |
|---|---|---|
| `AxisSagaSettings` | singleton | o objeto de configuração (registrado pelo núcleo) |
| `AxisSagaPostgresDataSource` | singleton | embrulha `NpgsqlDataSource.Create(connectionString)` (específico do Postgres) |
| `IAxisSagaDefinitionRegistry` | singleton | store em memória de `AxisSagaDefinition`s compiladas |
| `IAxisSagaMediator` | scoped | `SagaMediator` (start / get / resume) |
| `SagaEngine` | scoped | o driver por instância |
| `ISagaStageHandlerInvoker` | scoped | resolve e chama o `IAxisSagaStageHandler<TPayload>` que casa |
| `IAxisSagaResumer` | scoped | `SagaResumer` (o recovery por polling) |
| `IAxisSagaJanitor` | scoped | `SagaJanitor` (deleta sagas terminais cuja retenção expirou) |
| `IAxisSagaDefinitionInitializer` | scoped | `SagaDefinitionInitializer` (grava definições no catálogo no startup) |
| `AxisSagaResumerWorker` | hosted service | o loop de background embutido (registrado só quando `AxisSagaSettings.ResumerEnabled`, o default) |
| `ISagaInstanceStore`, `ISagaStageLogStore`, `ISagaDefinitionStore` | scoped | acesso row-level Postgres às tabelas (específico do dialeto) |
| `IAxisSagaStorageInitializer` | singleton | `PostgresSagaStorageInitializer` — roda a migration do schema no startup (específico do dialeto) |

Uma segunda chamada a `AddAxisSagaPostgres` **sem serviceKey** (ou a `AddAxisSagaMySql`) lança — "um storage por processo por design". Para rodar **vários** stores independentes num só processo (um por subdomínio), use o overload keyed abaixo.

---

## Keyed por subdomínio — vários stores num processo

`AddAxisSagaPostgres(serviceKey, settings)` registra o **mesmo** runtime, porém keyed por `serviceKey` — a mesma convenção do `AddPostgresUnitOfWork(serviceKey, connectionString)` do `AxisRepository`. Assim dois subdomínios independentes (ex.: e-commerce e receita×despesa) rodam **sagas independentes contra bancos independentes** no mesmo monólito, sem esperar o split em microsserviços. As APIs sem serviceKey seguem idênticas e podem coexistir.

```csharp
// um store por subdomínio, cada um com sua connection string
services.AddPostgresUnitOfWork("ecommerce", ecommerceConn);       // repositório do BC (opcional)
services.AddAxisSagaPostgres("ecommerce", new AxisSagaSettings { ConnectionString = ecommerceConn });

services.AddPostgresUnitOfWork("financas", financasConn);
services.AddAxisSagaPostgres("financas", new AxisSagaSettings { ConnectionString = financasConn });

// defina cada saga sob a key do seu subdomínio
services.AddKeyedSingleton("ecommerce", AxisSagaDefinitions.Define<OrderPayload>(OrderSaga.Name, OrderSaga.Configure));
services.AddAxisSagaHandlers(typeof(OrderSaga).Assembly);   // handlers continuam globais, casados por (payload, saga, stage)
```

O consumidor injeta o mediator do subdomínio com `[FromKeyedServices("ecommerce")] IAxisSagaMediator`.

Diferenças em relação ao caminho não-keyed:

- **Guard por-key.** Uma segunda chamada com a **mesma** key lança; keys distintas convivem.
- **Reuso do datasource.** Se o BC já registrou um `NpgsqlDataSource` keyed para aquela key (via `AddPostgresUnitOfWork`), a saga **reusa a mesma pool** em vez de abrir outra; senão cria e possui a sua. Postgres opera em `READ COMMITTED` por padrão, então o claim de lease é seguro na pool compartilhada.
- **Definições isoladas por key.** Cada subdomínio registra suas definições com `AddKeyedSingleton<AxisSagaDefinition>(serviceKey, Define(...))`; o registry keyed enxerga só as suas — dois subdomínios podem ter sagas de mesmo nome.
- **Um resumer worker por key.** Cada store keyed hospeda seu próprio `AxisSagaResumerWorker` (quando `ResumerEnabled`).

> Decisão registrada em [ADR-0003](../../../adr/0003-axis-saga-keyed-per-subdomain-storage.md).

---

## O pipeline de um stage

Quando o engine roda um stage (em `SagaEngine.ExecuteAsync(sagaId)`):

1. **Carrega** a instância de `axis_saga.saga_instances` (`ISagaInstanceStore`).
2. **Resolve** o `AxisSagaStageDefinition` atual no registry por `(SagaName, CurrentStage ?? FirstForwardStage)`.
3. **Loga** `Started` em `axis_saga.saga_stage_logs`.
4. **Invoca** o handler via `ISagaStageHandlerInvoker` (que acha o `IAxisSagaStageHandler<TPayload>` certo e chama `ExecuteAsync(payload)`).
5. Em `Ok`: serializa o novo payload, **atualiza** a linha da instância com o novo payload + version + `CurrentStage`, **loga** `Completed`, define o próximo stage (ou `Completed` / `Compensated`).
6. Em `Error`: **atualiza** com `LastErrorCode`/`LastErrorMessage`, **loga** `Failed`, troca para `Compensating` e percorre `RouteToOnError` (ou define `Failed`).

> Concorrência: cada update que muda estado guarda na version **e** na posse do lease — `WHERE version = @currentVersion AND CLAIMED_BY = @runner AND CLAIMED_UNTIL > NOW()` — e incrementa a version. Uma execução que perdeu o lease, ou um segundo engine concorrente, vê o mismatch e falha com `AxisSagaErrors.ConcurrencyConflict` — a saga **não** é dirigida em dose dupla. A execução única é garantida por esse lease sem conexão (`CLAIMED_BY` / `CLAIMED_UNTIL`, renovado por heartbeat) em vez de um advisory lock segurado.

---

## Bootstrap — `SagaDefinitionInitializer`

No startup, o initializer:

1. Lê cada `AxisSagaDefinition` registrada no registry em memória (a saída do configurator).
2. Faz **upsert** de cada uma como uma linha em `axis_saga.saga_definitions`. O catálogo é só um snapshot JSON da definição; o engine em runtime ainda lê do registry em memória, mas o catálogo dá ao ops uma visão consultável do que o processo deployado sabe.

Você **não** fia isso você mesmo: o resumer worker embutido roda a migration do storage e então dispara `IAxisSagaDefinitionInitializer.InitializeAsync` uma vez na sua primeira passada, antes de começar o polling (veja abaixo). O `SagaDefinitionInitializer` agnóstico de dialeto é o mesmo no Postgres e no MySQL; só o `ISagaDefinitionStore` por onde ele faz upsert é específico do dialeto.

---

## Resumer — embutido, sem worker para escrever à mão

O resumer **não** é algo que você hospeda. `AddAxisSagaPostgres` (e `AddAxisSagaMySql`) auto-registram o `AxisSagaResumerWorker`, um `BackgroundService`, sempre que `AxisSagaSettings.ResumerEnabled` é `true` (o default). No startup ele:

1. Roda a migration idempotente do schema via `IAxisSagaStorageInitializer` (no-op se já aplicada);
2. Inicializa as definições de saga registradas uma vez;
3. Faz polling de `IAxisSagaResumer.RunOnceAsync` a cada `ResumerPollInterval`, reivindicando e re-disparando instâncias travadas.

Ponha `ResumerEnabled = false` só num processo que precise iniciar/aguardar sagas mas não rodar o loop (recovery de responsabilidade de outro processo, ou um teste sem banco vivo).

Cada poll reivindica sagas travadas via `ISagaInstanceStore.ClaimStaleSagaIdsAsync` — um `SELECT … FOR UPDATE SKIP LOCKED` puro, chaveado num **lease expirado** (`CLAIMED_UNTIL IS NULL OR CLAIMED_UNTIL < NOW()`) com `status IN ('Pending','Running','Compensating')` — e re-dispara cada uma pelo mediator, que re-adquire o lease via `AcquireLeaseAsync` (a mesma chamada aplica o cap global de concorrência). `ResumeAfter` também serve como a duração do lease: uma execução reivindica a instância por esse tempo e um heartbeat o renova a cada `ResumeAfter / 4` enquanto os stages rodam, então uma saga só é considerada travada quando seu lease vence.

Veja [`IAxisSagaResumer`](resumer.md) para a semântica.

---

## Exemplo real — fiação de produção

```csharp
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddAxisMemoryBus()
    .AddAxisSagaPostgres(new AxisSagaSettings
    {
        ConnectionString    = builder.Configuration.GetConnectionString("Postgres")!,
        ResumerPollInterval = TimeSpan.FromSeconds(30),
        ResumeAfter         = TimeSpan.FromSeconds(60),
        ResumeBatchSize     = 100,
    })
    .AddAxisSagaHandlers(Assembly.GetExecutingAssembly());

// registre cada definição de saga para que o engine consiga resolvê-la
builder.Services.AddSingleton(
    AxisSagaDefinitions.Define<OrderPayload>(OrderSagaDefinition.Name, OrderSagaDefinition.Configure));
```

Nenhum hosted service para adicionar à mão: `AddAxisSagaPostgres` já registrou o resumer worker embutido, que migra o schema, inicializa as definições e roda o recovery. (Troque `AddAxisSagaPostgres` por `AddAxisSagaMySql` — mesmos settings, mesma fiação — para rodar no MySQL no lugar.)

**Por que compensa:** a aplicação só fala com `IAxisSagaMediator`. Storage, engine e recovery são fiados uma vez na raiz de composição.

---

## Veja também

- [Adapter MySQL](mysql-adapter.md) — o adapter irmão sobre o mesmo núcleo compartilhado
- [Schema do banco](database-schema.md) — as quatro tabelas de negócio (+ a tabela de controle `MIGRATIONS` que o runner de migrations do framework mantém) que o adapter cria
- [Mediator · `IAxisSagaMediator`](mediator.md) — a superfície de API
- [Resumer · `IAxisSagaResumer`](resumer.md) — o loop de recovery
- [Conceitos](concepts.md) — o que o engine está dirigindo
- [Stage handlers](stage-handlers.md) — o que o engine invoca

---

↩ [Voltar à documentação do AxisSaga](README.md)
