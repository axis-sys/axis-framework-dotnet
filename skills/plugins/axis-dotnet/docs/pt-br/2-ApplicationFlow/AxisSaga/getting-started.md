# Primeiros passos · instalação e uso

> Instale as packages, registre um adapter de storage (atualmente Postgres, MySQL, …), defina uma saga, escreva os stage handlers, despache — caminho de cinco minutos de zero a uma orquestração rodando com compensações.

---

## Instalação

```
dotnet add package AxisSaga              # contratos + o runtime agnóstico de dialeto
dotnet add package AxisSaga.Postgres     # o adapter de storage Postgres
# ou:
dotnet add package AxisSaga.MySql        # o adapter de storage MySQL (mesmo runtime, DDL/SQL de MySQL)
```

`AxisSaga` carrega todo o runtime agnóstico de dialeto (engine, mediator, worker do resumer, janitor, o schema do store declarado uma única vez); depende de `AxisResult`, `AxisLogger`, `AxisMediator.Contracts`. Um adapter de storage só acrescenta o SQL específico do provedor: `AxisSaga.Postgres` depende de `Npgsql`, `AxisSaga.MySql` de `MySqlConnector`. O runtime da saga em si nunca publica eventos — um stage handler que precisa notificar o resto do sistema depende diretamente do `AxisBus` e publica na sua própria unit of work.

---

## Registrando

```csharp
using Axis;
using AxisSaga.Postgres;
using System.Reflection;

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
```

| Extensão | O que faz |
|---|---|
| `AddAxisSagaPostgres(settings)` | conecta o data source `Npgsql` e as store ports do Postgres, depois chama o `AddAxisSagaCore` compartilhado — registrando o runtime agnóstico de dialeto (`IAxisSagaMediator`, `IAxisSagaResumer`, `IAxisSagaJanitor`, o engine, o invoker de stage handler, o registry em memória de definições) e, quando `ResumerEnabled` está ligado, o worker embutido do resumer que migra o schema e faz polling — veja [Adapter Postgres](postgres-adapter.md) |
| `AddAxisSagaHandlers(assembly)` | scaneia o assembly por implementações de `IAxisSagaStageHandler<>` e registra cada uma como scoped |

`AxisSaga.MySql` expõe o espelho `AddAxisSagaMySql(settings)` — mesmo runtime, store ports de MySQL.

Qualquer que seja o adapter de storage escolhido, a chamada **sem serviceKey** recusa uma segunda — *um storage por processo por design* (todas as sagas de todos os BCs compartilham o mesmo schema `AXIS_SAGA`). Para rodar **vários** stores independentes por processo (um por subdomínio, cada um com seu banco), use o overload keyed `AddAxisSagaPostgres(serviceKey, settings)` / `AddAxisSagaMySql(serviceKey, settings)` — veja [Adapter Postgres · Keyed por subdomínio](postgres-adapter.md#keyed-por-subdomínio--vários-stores-num-processo).

---

## Definindo uma saga

```csharp
public record OrderPayload(AxisEntityId OrderId, decimal Amount, string CustomerEmail);

public static class OrderSagaDefinition
{
    public const string Name = "OrderSaga";

    public static void Configure(IAxisSagaConfigurator<OrderPayload> saga)
    {
        saga.AddStage("ReserveStock")
            .NextStageOnSuccess("ChargeCard")
            .RouteToOnError("CompensateOrder");

        saga.AddStage("ChargeCard")
            .FinishOnSuccess()
            .RouteToOnError("RefundStock", "CompensateOrder");

        saga.AddErrorStage("RefundStock")
            .NextStageOnSuccess("CompensateOrder");

        saga.AddErrorStage("CompensateOrder")
            .FinishOnSuccess();
    }
}
```

→ **[Configurator · `IAxisSagaConfigurator<TPayload>`](configuration.md)**

---

## Registrando a definição

A definição configurada precisa ser adicionada ao container para que o engine consiga resolvê-la — sem isso, `StartAsync` falha com `SAGA_DEFINITION_NOT_FOUND`. Registre a `AxisSagaDefinition` compilada produzida por `AxisSagaDefinitions.Define<TPayload>(name, configure)` como singleton:

```csharp
builder.Services.AddSingleton(
    AxisSagaDefinitions.Define<OrderPayload>(OrderSagaDefinition.Name, OrderSagaDefinition.Configure));
```

Adicione um `AddSingleton(...)` por saga. O registry em memória pega cada `AxisSagaDefinition` registrada no startup, e o initializer faz upsert de cada uma no catálogo.

---

## Escrevendo um stage handler

```csharp
public class ReserveStockHandler(IStockPort stock) : IAxisSagaStageHandler<OrderPayload>
{
    public string SagaName  => OrderSagaDefinition.Name;
    public string StageName => "ReserveStock";

    public Task<AxisResult<OrderPayload>> ExecuteAsync(OrderPayload payload)
        => stock.ReserveAsync(payload.OrderId, payload.Amount)
            .MapAsync(_ => payload);
}
```

Tanto `SagaName` quanto `StageName` precisam casar com a definição exatamente (case-sensitive). → **[Stage handlers](stage-handlers.md)**

---

## Iniciando a saga

```csharp
public class OrdersController(IAxisSagaMediator sagaMediator) : ControllerBase
{
    [HttpPost]
    public Task<AxisResult<string>> CreateAsync(CreateOrderRequest req)
        => sagaMediator.StartAsync(
            sagaId:   $"order-{Guid.CreateVersion7()}",
            sagaName: OrderSagaDefinition.Name,
            payload:  new OrderPayload(req.OrderId, req.Amount, req.CustomerEmail));
}
```

`StartAsync` insere a linha da instância, depois **dispara o engine em background** — o controller retorna na hora com o `sagaId`. O engine dirige a saga de `ReserveStock` até `Completed` (ou a cadeia de compensação) assincronamente.

→ **[Mediator · `IAxisSagaMediator`](mediator.md)**

---

## O que você ganha de graça

- Cada start / completion / failure de stage é logado em `axis_saga.saga_stage_logs`.
- Sucesso → avança para o próximo stage (ou finaliza). Erro → **roteia** para os stages de compensação configurados.
- Um worker de resumer embutido é hospedado automaticamente pelo adapter de storage (auto-registrado por `AddAxisSagaPostgres`/`AddAxisSagaMySql` enquanto `AxisSagaSettings.ResumerEnabled` for `true`) — você não escreve mais um `BackgroundService` na mão. Se o processo restartar (ou um run travar) no meio de uma saga, o **lease** de execução dela (`CLAIMED_UNTIL`) expira; o worker reivindica a instância travada e re-dispara o engine, que re-adquire o lease e assim respeita o cap global de concorrência.

→ **[Resumer · `IAxisSagaResumer`](resumer.md)**

---

## Veja também

- [Conceitos · stages e rotas](concepts.md) — as engrenagens
- [Configurator · `IAxisSagaConfigurator<TPayload>`](configuration.md) — o builder fluente
- [Stage handlers](stage-handlers.md) — `IAxisSagaStageHandler<TPayload>`
- [Mediator · `IAxisSagaMediator`](mediator.md) — start, read, resume
- [Resumer · `IAxisSagaResumer`](resumer.md) — recovery depois de restart
- [Adapter Postgres](postgres-adapter.md) — engine + storage + bootstrap
- [Schema do banco](database-schema.md) — tabelas `AXIS_SAGA`
- [Por que AxisSaga?](why-axissaga.md) — o argumento pela abstração
- [Referência da API](api-reference.md) — cada membro num só lugar

---

↩ [Voltar à documentação do AxisSaga](README.md)
