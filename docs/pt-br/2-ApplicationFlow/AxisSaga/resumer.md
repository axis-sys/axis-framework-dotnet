# Resumer · `IAxisSagaResumer`

> Um worker de recovery embutido, por polling. Periodicamente scaneia o banco por instâncias **travadas** (sagas não-terminais cujo **lease** de execução expirou) e re-dispara o engine para que o processo possa pegar onde o anterior parou. O framework hospeda esse worker por você — você não escreve um.

```csharp
public interface IAxisSagaResumer
{
    Task<int> RunOnceAsync(CancellationToken cancellationToken);
}
```

---

## Quando usar

Num deploy de produção você não hospeda o resumer — `AddAxisSagaPostgres` / `AddAxisSagaMySql` registram um worker hospedado embutido (`AxisSagaResumerWorker`) que chama `RunOnceAsync` a cada `ResumerPollInterval` enquanto `AxisSagaSettings.ResumerEnabled` for `true` (o padrão). O `SagaResumer` agnóstico de dialeto (registrado sob essa interface no core da saga) faz o trabalho; o worker só dirige o loop dele. Você interage com `IAxisSagaResumer` diretamente só para os casos de nicho abaixo (um health probe, um dreno único).

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| retomar imediatamente depois de uma correção | [`IAxisSagaMediator.ResumeAsync(sagaId)`](mediator.md) |
| rodar só num node específico | gate o hosted service por um check de leader-election (o resumer em si é seguro de rodar em todos os nodes, mas talvez você não queira a competição) |
| drenar *todas* as sagas no shutdown | um handler de graceful-shutdown que chama `RunOnceAsync` uma vez e espera |

---

## O que "travada" significa

Uma saga está **travada** quando é não-terminal **e seu lease de execução expirou** — `CLAIMED_UNTIL IS NULL OR CLAIMED_UNTIL < NOW()`. Cada run do engine segura um lease (carimbado em `CLAIMED_BY` / `CLAIMED_UNTIL`) e um heartbeat o renova a cada `ResumeAfter / 4` enquanto os stages executam; um dono que crashou ou travou para de renovar, o lease caduca, e o próximo passo do resumer o reivindica:

| Status | Lease | Diagnóstico |
|---|---|---|
| `Pending` | ausente ou expirado | iniciada mas nunca reivindicada por um run (ou o reivindicante morreu antes de carimbar um lease vivo) |
| `Running` | expirado | o engine estava dirigindo, mas parou (crash de processo, OOM kill) |
| `Compensating` | expirado | igual, no meio da compensação |

O resumer reivindica linhas `Pending` / `Running` / `Compensating` cujo lease caducou; ele **não** toca em `Completed` / `Compensated` / `Failed` (estes são terminais), nem em qualquer saga cujo lease ainda esteja vivo (um dono está dirigindo ativamente).

> `ResumeAfter` (padrão 60 segundos) é a duração do lease. Ajuste para ser maior que a duração do pior caso do stage mais longo, mais uma margem — caso contrário, um stage legitimamente em execução pode ter seu lease caducado e ser reivindicado no meio do caminho.

---

## O que ele faz

Para cada saga travada que o resumer acha:

1. Ele chama o engine (o mesmo caminho de código que `IAxisSagaMediator.ResumeAsync(sagaId)`), que primeiro **re-adquire o lease** via `AcquireLeaseAsync` — um claim atômico que também impõe o **teto global de concorrência** (veja abaixo). Se o lease já está com um run vivo ou o teto está cheio, o re-disparo é pulado.
2. O engine lê o estado atual, localiza o `CurrentStage`, resolve o handler que casa, e roda.
3. Se o stage já estava semi-executado (uma linha de log `Started` mas sem `Completed` / `Failed`), o handler precisa ser **idempotente**: re-rodar com o mesmo payload deve produzir o mesmo resultado ou ser no-op.

> Idempotência é responsabilidade do handler. Um padrão típico é checar o estado upstream primeiro (`stock.GetReservationAsync` antes do `ReserveAsync`), ou usar chaves de idempotência derivadas de `(SagaId, StageName)`.

`RunOnceAsync` retorna o **número de sagas que ele disparou**.

---

## O loop é hospedado por você

Você **não** escreve um `BackgroundService`. O adapter de storage registra um `AxisSagaResumerWorker` embutido (um `BackgroundService` interno) no momento em que você chama `AddAxisSagaPostgres` / `AddAxisSagaMySql` com `ResumerEnabled = true`. No startup ele migra o schema da saga e faz upsert das definições em processo, e então faz polling de `RunOnceAsync` a cada `ResumerPollInterval` — aproximadamente equivalente a:

```csharp
// Dentro do framework — você não escreve isto.
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await storageInitializer.InitializeAsync();          // migração idempotente do schema
    while (!stoppingToken.IsCancellationRequested)
    {
        using var scope = scopeFactory.CreateScope();    // um scope novo por passo
        await scope.ServiceProvider.GetRequiredService<IAxisSagaResumer>().RunOnceAsync(stoppingToken);
        await Task.Delay(settings.ResumerPollInterval, stoppingToken);
    }
}
```

Para optar por sair — um processo que inicia/aguarda sagas mas não deve rodar o loop, ou um teste sem banco vivo — defina `ResumerEnabled = false`:

```csharp
builder.Services.AddAxisSagaPostgres(new AxisSagaSettings
{
    ConnectionString = "…",
    ResumerEnabled   = false,
});
```

**Por que compensa:** recovery de crash vem ligado por padrão, sem boilerplate por aplicação para esquecer. O claim é seguro de rodar em todo node — o store *reivindica* as instâncias travadas com um único `SELECT … FOR UPDATE SKIP LOCKED` chaveado no lease expirado (`CLAIMED_UNTIL IS NULL OR CLAIMED_UNTIL < NOW()`), então um resumer concorrente em outro node pula as linhas já travadas. O re-disparo então re-adquire o lease atomicamente via `AcquireLeaseAsync`, de modo que múltiplos resumers não disparam de novo a mesma saga.

---

## Ajustando `AxisSagaSettings`

```csharp
public class AxisSagaSettings
{
    public required string  ConnectionString    { get; init; }
    public TimeSpan         ResumerPollInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan         ResumeAfter         { get; init; } = TimeSpan.FromSeconds(60);
    public int              ResumeBatchSize     { get; init; } = 100;
    public bool             ResumerEnabled      { get; init; } = true;
}
```

| Setting | Significado | Padrão |
|---|---|---|
| `ResumerPollInterval` | com que frequência o worker chama `RunOnceAsync` | 30s |
| `ResumeAfter` | a duração do **lease** de execução; também por quanto tempo um lease precisa estar expirado antes do resumer reivindicar a saga | 60s |
| `ResumeBatchSize` | número máximo de sagas stale reivindicadas por poll (o `LIMIT` da query de claim) | 100 |
| `ResumerEnabled` | se o adapter de storage hospeda o worker de resumer embutido | `true` |

> Mire `ResumeAfter >= 2 × ResumerPollInterval` — assim o worker nunca reivindica uma saga cujo lease o engine acabou de renovar.

### Teto global de concorrência

`RunOnceAsync` também é **ciente do teto** (cap-aware). Um único limite por processo mora no banco, não nos settings: a linha singleton `AXIS_SAGA.SAGA_SETTINGS` guarda `MAX_CONCURRENT_SAGAS` (semeado em `20`, `NULL` = ilimitado), ajustável em runtime com um `UPDATE` simples, sem redeploy. Antes de reivindicar, o resumer lê o teto e a contagem de **leases vivos** e busca no máximo o número de slots livres, então ele nunca dispara um batch que bateria imediatamente no gate. O teto é, no fim, imposto atomicamente dentro do `AcquireLeaseAsync` (um claim só vinga enquanto menos que `MAX_CONCURRENT_SAGAS` sagas seguram um lease vivo), então o pré-corte do resumer é só uma otimização — o gate atômico do engine continua a autoridade.

---

## Exemplos reais

### 1. O worker embutido (padrão)

Só registre um adapter de storage — o loop do resumer vem junto. Nada mais a escrever:

```csharp
builder.Services.AddAxisSagaPostgres(new AxisSagaSettings { ConnectionString = "…" });
// ResumerEnabled é true por padrão → AxisSagaResumerWorker é hospedado automaticamente.
```

### 2. Retomar sob demanda de um command

```csharp
public class RetrySagaHandler(IAxisSagaMediator sagas) : IAxisCommandHandler<RetrySagaCommand>
{
    public Task<AxisResult> HandleAsync(RetrySagaCommand cmd)
        => sagas.ResumeAsync(cmd.SagaId);
}
```

**Por que compensa:** ops pode pedir ao sistema para retentar uma saga manualmente via um admin command, sem restartar o processo.

### 3. Um health probe via `RunOnceAsync`

```csharp
public class SagaResumerHealthProbe(IAxisSagaResumer resumer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext _, CancellationToken ct)
    {
        var resumed = await resumer.RunOnceAsync(ct);
        return HealthCheckResult.Healthy($"resumed {resumed} sagas");
    }
}
```

**Por que compensa:** um único liveness probe ao mesmo tempo **prova** que o banco está alcançável e **dirige** recovery. O número de sagas retomadas é uma métrica útil.

---

## Veja também

- [Mediator · `IAxisSagaMediator`](mediator.md) — `ResumeAsync` manual
- [Adapter Postgres](postgres-adapter.md) — o que um adapter de storage embutido (Postgres, MySQL, entre outros) faz por baixo
- [Schema do banco](database-schema.md) — quais colunas o resumer consulta (as colunas de lease `CLAIMED_BY` / `CLAIMED_UNTIL`)
- [Conceitos · stages e rotas](concepts.md) — a máquina de estados que o engine dirige

---

↩ [Voltar à documentação do AxisSaga](README.md)
