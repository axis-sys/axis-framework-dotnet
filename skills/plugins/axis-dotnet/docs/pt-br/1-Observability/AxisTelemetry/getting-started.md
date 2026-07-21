# Primeiros passos · instalação e uso

> Instale a package, registre o adapter OpenTelemetry, plugue o behaviour no mediator e abra um span — em cinco minutos.

---

## Instalação

```
dotnet add package AxisTelemetry
```

A package depende diretamente apenas de `AxisLogger` (que traz `AxisResult` e `AxisMediator.Contracts` transitivamente). O adapter OpenTelemetry usa `System.Diagnostics.ActivitySource` e `System.Diagnostics.Metrics.Meter` da BCL.

---

## Registrando

```csharp
using Axis;

builder.Services
    .AddAxisMediator()
    .AddOpenTelemetryAxis();    // pluga OpenTelemetryAdapter como IAxisTelemetry + IAxisMetrics

// depois exponha o ActivitySource e o Meter do Axis no seu pipeline OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("Axis.AxisMediator").AddOtlpExporter())
    .WithMetrics(b => b.AddMeter ("Axis.AxisMediator").AddOtlpExporter());
```

`AddOpenTelemetryAxis()`:

- Registra `OpenTelemetryAdapter` como singleton.
- Amarra `IAxisTelemetry` e `IAxisMetrics` à mesma instância.

O adapter mantém um `ActivitySource` e um `Meter` processo-amplos, ambos com nome `"Axis.AxisMediator"`. Adicione ao seu pipeline OpenTelemetry (ou qualquer sink que consuma `Activity`/`Meter`) para que spans e métricas fluam para fora.

> **Vai exportar para Azure Monitor / Application Insights?** A package de pareamento `AxisTelemetry.AzureMonitor` substitui os dois registros acima por um único `AddAzureMonitorAxis(builder.Configuration)` — adapter, exporter e export de `ILogger` numa chamada só. Veja o [adapter Azure Monitor](azure-monitor.md).

---

## Abrindo um span

```csharp
public Task<AxisResult> CommitAsync()
{
    using var span = telemetry.StartSpan("db.postgres.commit", AxisSpanKind.Client);
    span.SetTag("db.system", "postgresql");

    try
    {
        await transaction.CommitAsync(ct);
        span.SetStatus(AxisSpanStatus.Ok);
        return AxisResult.Ok();
    }
    catch (Exception ex)
    {
        span.RecordException(ex);
        return AxisError.InternalServerError("POSTGRES_COMMIT_ERROR");
    }
}
```

`using var` garante que o span encerra quando o bloco sai.

---

## Auto-instrumentando o mediator

Registre `TelemetryBehavior` como `IAxisPipelineBehavior`:

```csharp
services.AddTransient(typeof(IAxisPipelineBehavior<>), typeof(TelemetryBehavior<>));
services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
```

Agora cada request do mediator ganha:

- um span chamado `AxisMediator.{RequestName}` com tags `TraceId`/`JourneyId`/`RequestType`/`AxisEntityId`/`RequestName`;
- um histogram `axis.handler.duration_ms`;
- um counter `axis.handler.invocations`;
- um counter `axis.handler.exceptions` (quando algo lança).

**Por que compensa:** o timing, tracing e counters cobrem cada handler no sistema — *uma* linha de registro substitui cada `using var span = …` por handler.

---

## Testes — desligue a telemetria

```csharp
services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
services.AddSingleton<IAxisMetrics>  (NullAxisTelemetry.Instance);
```

`NullAxisTelemetry` é no-op: cada `StartSpan` retorna `NullAxisSpan.Instance`, cada método de métricas é vazio. Barato, sem alocação, deixa testes unitários cegos para telemetria.

---

## Veja também

- [Os contratos](contracts.md) — `IAxisTelemetry` e `IAxisMetrics` em profundidade
- [Spans · `IAxisSpan`](spans.md) — tagear, gravar exceções, adicionar eventos
- [`TelemetryBehavior`](telemetry-behavior.md) — o que a auto-instrumentação grava de fato
- [Adapter OpenTelemetry](opentelemetry-adapter.md) — fiação de `ActivitySource` / `Meter`
- [Adapter Azure Monitor](azure-monitor.md) — o pareamento com Application Insights numa chamada só
- [Adapter null](null-adapter.md) — desligue em testes
- [Tag names](tag-names.md) — as constantes canônicas
- [Por que AxisTelemetry?](why-axistelemetry.md) — o argumento contra `ActivitySource` direto
- [Referência da API](api-reference.md) — cada membro num só lugar

---

↩ [Voltar à documentação do AxisTelemetry](README.md)
