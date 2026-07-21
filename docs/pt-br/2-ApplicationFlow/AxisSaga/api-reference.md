# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## Contratos

| Tipo | Descrição |
|---|---|
| `IAxisSagaStageHandler<TPayload>` | `string SagaName`, `string StageName`, `Task<AxisResult<TPayload>> ExecuteAsync(TPayload)` |
| `IAxisSagaConfigurator<TPayload>` | `AddStage(name)`, `AddErrorStage(name)` |
| `IAxisSagaStageBuilder<TPayload>` | `NextStageOnSuccess(name)`, `FinishOnSuccess()`, `RouteToOnError(params names)`, `RetryOnTransient(maxAttempts, baseDelay?)` |
| `AxisSagaDefinition` | imutável: `SagaName`, `PayloadType`, `ForwardStages`, `ErrorStages`, `FirstForwardStage`, `GetStage(name)` |
| `AxisSagaStageDefinition` | `StageName`, `IsErrorStage`, `NextStageOnSuccess?`, `RouteToOnError` |

→ [Configurator](configuration.md) · [Stage handlers](stage-handlers.md)

---

## Portas

| Port | Membros |
|---|---|
| `IAxisSagaMediator` | `StartAsync<TPayload>(sagaName, payload)` (+ overloads que adicionam `sagaId` e/ou `retainedFor`), `GetByIdAsync(sagaId)`, `GetByIdAsync<TPayload>(sagaId)`, `ResumeAsync(sagaId)` |
| `IAxisSagaResumer` | `Task<int> RunOnceAsync(CancellationToken)` |
| `IAxisSagaJanitor` | `Task<int> RunOnceAsync(CancellationToken)` (deleta sagas terminais cuja retenção expirou) |
| `IAxisSagaDefinitionRegistry` | `AxisSagaDefinition? Get(string sagaName)`, `IReadOnlyCollection<AxisSagaDefinition> All` |
| `IAxisSagaDefinitionInitializer` | `Task<int> InitializeAsync(CancellationToken)` |

→ [Mediator](mediator.md) · [Resumer](resumer.md)

---

## Shared kernel

| Tipo | Membros |
|---|---|
| `AxisSagaInstance` | `SagaId`, `SagaName`, `Status`, `CurrentStage?`, `PayloadJson`, `LastErrorCode?`, `LastErrorMessage?`, `Version`, `CreatedAt`, `UpdatedAt` |
| `AxisSagaInstance<TPayload>` | herda `AxisSagaInstance` + `TPayload Payload` |
| `AxisSagaStatus` (enum) | `Pending`, `Running`, `Completed`, `Failed`, `Compensating`, `Compensated` |
| `AxisSagaStageStatus` (enum) | `Started`, `Completed`, `Failed` |
| `AxisSagaErrors` (constantes) | `SagaDefinitionNotFound`, `SagaInstanceNotFound`, `StageHandlerNotFound`, `StageNotFound`, `ConcurrencyConflict`, `PayloadDeserializationFailed`, `PayloadSerializationFailed`, `PersistenceFailed` |

→ [Conceitos](concepts.md)

---

## Runtime do núcleo — `AxisSaga` (agnóstico de dialeto)

O engine, mediator, resumer, janitor, definition initializer e invoker são **agnósticos de dialeto** e vivem no projeto do núcleo. Os adapters de storage inclusos (`AxisSaga.Postgres`, `AxisSaga.MySql`, …) os compartilham via `AddAxisSagaCore`.

| Tipo | Descrição |
|---|---|
| `AxisSagaSettings` | `ConnectionString`, `ResumerPollInterval` (padrão 30s), `ResumeAfter` (padrão 60s), `ResumeBatchSize` (padrão 100), `ResumerEnabled` (padrão `true`) |
| `SagaMediator` | implementa `IAxisSagaMediator` |
| `SagaResumer` | implementa `IAxisSagaResumer` |
| `SagaJanitor` | implementa `IAxisSagaJanitor` |
| `SagaDefinitionInitializer` | implementa `IAxisSagaDefinitionInitializer` |
| `SagaEngine` | o driver por instância (load → invoca → loga → avança) |
| `SagaStageHandlerInvoker`, `SagaInstanceMapper` | colaboradores do engine |
| `AxisSagaResumerWorker` | o `BackgroundService` embutido (auto-hospedado quando `ResumerEnabled`) |

→ [Mediator](mediator.md) · [Resumer](resumer.md) · [Configurator](configuration.md)

---

## Adapters de storage — `AxisSaga.Postgres`, `AxisSaga.MySql`, …

Cada adapter fornece só o storage específico do dialeto: um data source, as implementações de store por trás das quatro portas de store, o runner de migration e o storage initializer. Postgres e MySQL são os adapters entregues hoje; qualquer banco com um `IAxisSqlDialect` mais as portas de store pode ser adicionado como outro. O schema em si é declarado uma vez no núcleo (`Axis.Persistence.Scripts.AxisSagaSchema`) e renderizado por dialeto.

| Tipo (Postgres / MySQL) | Descrição |
|---|---|
| `AxisSagaPostgresDataSource` / `AxisSagaMySqlDataSource` | embrulha um `NpgsqlDataSource` / `MySqlDataSource` singleton |
| `SagaInstanceStore` / `MySqlSagaInstanceStore` | implementa `ISagaInstanceStore` |
| `SagaStageLogStore` / `MySqlSagaStageLogStore` | implementa `ISagaStageLogStore` |
| `PostgresSagaDefinitionStore` / `MySqlSagaDefinitionStore` | implementa `ISagaDefinitionStore` |
| `PostgresSagaStorageInitializer` / `MySqlSagaStorageInitializer` | implementa `IAxisSagaStorageInitializer` (roda a migration do schema no startup) |
| `AxisSagaMigrations` / `AxisSagaMySqlMigrations` | renderiza `AxisSagaSchema` com o dialeto e aplica via o runner de migration do framework |

→ [Adapter Postgres](postgres-adapter.md) · [Adapter MySQL](mysql-adapter.md) · [Schema do banco](database-schema.md)

---

## Extensões DI

| Extensão | Efeito |
|---|---|
| `services.AddAxisSagaCore(AxisSagaSettings)` | registra o runtime agnóstico de dialeto: settings, registry, mediator, engine, invoker, resumer, janitor, definition initializer — mais o hosted service `AxisSagaResumerWorker` quando `ResumerEnabled`. Chamado uma vez por cada adapter de storage. |
| `services.AddAxisSagaPostgres(AxisSagaSettings)` | registra o data source do Postgres + as quatro portas de store, e então chama `AddAxisSagaCore`. Lança num segundo registro de storage. |
| `services.AddAxisSagaMySql(AxisSagaSettings)` | o gêmeo MySQL — registra o data source do MySQL + as quatro portas de store, e então chama `AddAxisSagaCore`. Lança num segundo registro de storage. |
| `services.AddAxisSagaCore(string serviceKey, AxisSagaSettings)` | versão **keyed** do runtime — todos os serviços keyed por `serviceKey`, para vários stores por processo. O registry keyed resolve `GetKeyedServices<AxisSagaDefinition>(serviceKey)`. |
| `services.AddAxisSagaPostgres(string serviceKey, AxisSagaSettings)` | store Postgres **keyed por subdomínio**; reusa o `NpgsqlDataSource` keyed do repositório (`AddPostgresUnitOfWork`) se presente, senão cria o próprio. Lança só se a **mesma** key já foi registrada. |
| `services.AddAxisSagaMySql(string serviceKey, AxisSagaSettings)` | store MySQL **keyed por subdomínio**; datasource sempre próprio (fixado em `READ COMMITTED`). Lança só se a **mesma** key já foi registrada. |
| `services.AddAxisSagaHandlers(Assembly)` | scaneia o assembly por implementações de `IAxisSagaStageHandler<>` e registra cada uma como **scoped** |

→ [Adapter Postgres](postgres-adapter.md) · [Adapter MySQL](mysql-adapter.md) · [Configurator](configuration.md) · [Stage handlers](stage-handlers.md)

---

## Contrato de comportamento (engine de saga)

| Desfecho do stage | Persistência | Roteamento |
|---|---|---|
| handler retorna `Ok(payload)` | atualiza payload + version + loga `Completed` | seta `NextStageOnSuccess`, ou `Completed` (forward) / `Compensated` (error stage) |
| handler retorna `Error(errs)` | atualiza `LastErrorCode`/`LastErrorMessage` + loga `Failed` | se `RouteToOnError` não vazio: seta `Compensating` e percorre; caso contrário `Failed` |
| handler lança | engine grava a exceção, marca `Failed` | (sem compensação; exceções = erros de programação) |
| engine concorrente vê mismatch de version | aborta com `AxisSagaErrors.ConcurrencyConflict` | nenhum |

→ [Adapter Postgres](postgres-adapter.md) · [Adapter MySQL](mysql-adapter.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — instale, defina, despache
- [Por que AxisSaga?](why-axissaga.md) — o argumento pela abstração
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisSaga](README.md)
