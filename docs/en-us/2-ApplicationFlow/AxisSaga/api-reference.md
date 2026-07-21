# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## Contracts

| Type | Description |
|---|---|
| `IAxisSagaStageHandler<TPayload>` | `string SagaName`, `string StageName`, `Task<AxisResult<TPayload>> ExecuteAsync(TPayload)` |
| `IAxisSagaConfigurator<TPayload>` | `AddStage(name)`, `AddErrorStage(name)` |
| `IAxisSagaStageBuilder<TPayload>` | `NextStageOnSuccess(name)`, `FinishOnSuccess()`, `RouteToOnError(params names)`, `RetryOnTransient(maxAttempts, baseDelay?)` |
| `AxisSagaDefinition` | immutable: `SagaName`, `PayloadType`, `ForwardStages`, `ErrorStages`, `FirstForwardStage`, `GetStage(name)` |
| `AxisSagaStageDefinition` | `StageName`, `IsErrorStage`, `NextStageOnSuccess?`, `RouteToOnError` |

→ [Configurator](configuration.md) · [Stage handlers](stage-handlers.md)

---

## Ports

| Port | Members |
|---|---|
| `IAxisSagaMediator` | `StartAsync<TPayload>(sagaName, payload)` (+ overloads adding `sagaId` and/or `retainedFor`), `GetByIdAsync(sagaId)`, `GetByIdAsync<TPayload>(sagaId)`, `ResumeAsync(sagaId)` |
| `IAxisSagaResumer` | `Task<int> RunOnceAsync(CancellationToken)` |
| `IAxisSagaJanitor` | `Task<int> RunOnceAsync(CancellationToken)` (deletes retention-expired terminal sagas) |
| `IAxisSagaDefinitionRegistry` | `AxisSagaDefinition? Get(string sagaName)`, `IReadOnlyCollection<AxisSagaDefinition> All` |
| `IAxisSagaDefinitionInitializer` | `Task<int> InitializeAsync(CancellationToken)` |

→ [Mediator](mediator.md) · [Resumer](resumer.md)

---

## Shared kernel

| Type | Members |
|---|---|
| `AxisSagaInstance` | `SagaId`, `SagaName`, `Status`, `CurrentStage?`, `PayloadJson`, `LastErrorCode?`, `LastErrorMessage?`, `Version`, `CreatedAt`, `UpdatedAt` |
| `AxisSagaInstance<TPayload>` | inherits `AxisSagaInstance` + `TPayload Payload` |
| `AxisSagaStatus` (enum) | `Pending`, `Running`, `Completed`, `Failed`, `Compensating`, `Compensated` |
| `AxisSagaStageStatus` (enum) | `Started`, `Completed`, `Failed` |
| `AxisSagaErrors` (constants) | `SagaDefinitionNotFound`, `SagaInstanceNotFound`, `StageHandlerNotFound`, `StageNotFound`, `ConcurrencyConflict`, `PayloadDeserializationFailed`, `PayloadSerializationFailed`, `PersistenceFailed` |

→ [Concepts](concepts.md)

---

## Core runtime — `AxisSaga` (dialect-agnostic)

The engine, mediator, resumer, janitor, definition initializer and invoker are **dialect-agnostic** and live in the core project. The bundled storage adapters (`AxisSaga.Postgres`, `AxisSaga.MySql`, …) share them via `AddAxisSagaCore`.

| Type | Description |
|---|---|
| `AxisSagaSettings` | `ConnectionString`, `ResumerPollInterval` (default 30s), `ResumeAfter` (default 60s), `ResumeBatchSize` (default 100), `ResumerEnabled` (default `true`) |
| `SagaMediator` | implements `IAxisSagaMediator` |
| `SagaResumer` | implements `IAxisSagaResumer` |
| `SagaJanitor` | implements `IAxisSagaJanitor` |
| `SagaDefinitionInitializer` | implements `IAxisSagaDefinitionInitializer` |
| `SagaEngine` | the per-instance driver (load → invoke → log → advance) |
| `SagaStageHandlerInvoker`, `SagaInstanceMapper` | the engine's collaborators |
| `AxisSagaResumerWorker` | the built-in `BackgroundService` (auto-hosted when `ResumerEnabled`) |

→ [Mediator](mediator.md) · [Resumer](resumer.md) · [Configurator](configuration.md)

---

## Storage adapters — `AxisSaga.Postgres`, `AxisSaga.MySql`, …

Each adapter supplies only the dialect-specific storage: a data source, the store implementations behind the four store ports, the migration runner and the storage initializer. Postgres and MySQL are the adapters shipped today; any database with an `IAxisSqlDialect` plus the store ports can be added as another. The schema itself is declared once in core (`Axis.Persistence.Scripts.AxisSagaSchema`) and rendered per dialect.

| Type (Postgres / MySQL) | Description |
|---|---|
| `AxisSagaPostgresDataSource` / `AxisSagaMySqlDataSource` | wraps a singleton `NpgsqlDataSource` / `MySqlDataSource` |
| `SagaInstanceStore` / `MySqlSagaInstanceStore` | implements `ISagaInstanceStore` |
| `SagaStageLogStore` / `MySqlSagaStageLogStore` | implements `ISagaStageLogStore` |
| `PostgresSagaDefinitionStore` / `MySqlSagaDefinitionStore` | implements `ISagaDefinitionStore` |
| `PostgresSagaStorageInitializer` / `MySqlSagaStorageInitializer` | implements `IAxisSagaStorageInitializer` (runs the schema migration on startup) |
| `AxisSagaMigrations` / `AxisSagaMySqlMigrations` | renders `AxisSagaSchema` with the dialect and applies it via the framework migration runner |

→ [Postgres adapter](postgres-adapter.md) · [MySQL adapter](mysql-adapter.md) · [Database schema](database-schema.md)

---

## DI extensions

| Extension | Effect |
|---|---|
| `services.AddAxisSagaCore(AxisSagaSettings)` | registers the dialect-agnostic runtime: settings, registry, mediator, engine, invoker, resumer, janitor, definition initializer — plus the `AxisSagaResumerWorker` hosted service when `ResumerEnabled`. Called once by each storage adapter. |
| `services.AddAxisSagaPostgres(AxisSagaSettings)` | registers the Postgres data source + the four store ports, then calls `AddAxisSagaCore`. Throws on a second storage registration. |
| `services.AddAxisSagaMySql(AxisSagaSettings)` | the MySQL twin — registers the MySQL data source + the four store ports, then calls `AddAxisSagaCore`. Throws on a second storage registration. |
| `services.AddAxisSagaCore(string serviceKey, AxisSagaSettings)` | the **keyed** runtime — every service keyed by `serviceKey`, for several stores per process. The keyed registry resolves `GetKeyedServices<AxisSagaDefinition>(serviceKey)`. |
| `services.AddAxisSagaPostgres(string serviceKey, AxisSagaSettings)` | **per-subdomain keyed** Postgres store; reuses the repository's keyed `NpgsqlDataSource` (`AddPostgresUnitOfWork`) when present, otherwise creates its own. Throws only if the **same** key is already registered. |
| `services.AddAxisSagaMySql(string serviceKey, AxisSagaSettings)` | **per-subdomain keyed** MySQL store; always its own datasource (pinned to `READ COMMITTED`). Throws only if the **same** key is already registered. |
| `services.AddAxisSagaHandlers(Assembly)` | scans the assembly for `IAxisSagaStageHandler<>` implementations and registers each as **scoped** |

→ [Postgres adapter](postgres-adapter.md) · [MySQL adapter](mysql-adapter.md) · [Configuration](configuration.md) · [Stage handlers](stage-handlers.md)

---

## Behaviour contract (saga engine)

| Stage outcome | Persistence | Routing |
|---|---|---|
| handler returns `Ok(payload)` | update payload + version + log `Completed` | set `NextStageOnSuccess`, or `Completed` (forward) / `Compensated` (error stage) |
| handler returns `Error(errs)` | update `LastErrorCode`/`LastErrorMessage` + log `Failed` | if `RouteToOnError` non-empty: set `Compensating` and walk it; otherwise `Failed` |
| handler throws | engine records the exception, marks `Failed` | (no compensation; exceptions = programming errors) |
| concurrent engine sees version mismatch | abort with `AxisSagaErrors.ConcurrencyConflict` | none |

→ [Postgres adapter](postgres-adapter.md) · [MySQL adapter](mysql-adapter.md)

---

## See also

- [Getting started](getting-started.md) — install, define, dispatch
- [Why AxisSaga?](why-axissaga.md) — the case for the abstraction
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisSaga docs](README.md)
