# AxisTelemetry — Documentação

> 🌐 [English (README principal)](../../../en-us/1-Observability/AxisTelemetry/README.md)

**Tracing e métricas para o pipeline do Axis** — `IAxisTelemetry` para spans, `IAxisMetrics` para counters e histograms, um `OpenTelemetryAdapter` sobre `System.Diagnostics.ActivitySource` + `Meter`, um `NullAxisTelemetry` para quando você quer telemetria desligada, e um `TelemetryBehavior` que embrulha cada request do mediator com um span `AxisMediator.{RequestName}` mais métricas de duração / invocações / exceções.

```csharp
using var span = telemetry.StartSpan("db.postgres.commit", AxisSpanKind.Client);
span.SetTag("db.system", "postgresql");
try
{
    await transaction.CommitAsync();
    span.SetStatus(AxisSpanStatus.Ok);
}
catch (Exception ex)
{
    span.RecordException(ex);
    throw;
}
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### As duas interfaces em 60 segundos

```csharp
public interface IAxisTelemetry
{
    IAxisSpan StartSpan(string operationName, AxisSpanKind kind = AxisSpanKind.Internal);
    string? CurrentTraceId { get; }
    string? CurrentSpanId { get; }
}

public interface IAxisMetrics
{
    void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags);
    void IncrementCounter(string name, long delta = 1, params KeyValuePair<string, object?>[] tags);
}
```

Tracing de um lado, métricas do outro. O `OpenTelemetryAdapter` embarcado implementa os dois. → **[Os contratos](contracts.md)**

### `IAxisSpan` — o span com que você de fato trabalha

```csharp
public interface IAxisSpan : IDisposable
{
    string TraceId { get; }
    string SpanId { get; }
    IAxisSpan SetTag(string key, object? value);
    IAxisSpan SetStatus(AxisSpanStatus status, string? description = null);
    IAxisSpan RecordException(Exception exception);
    IAxisSpan AddEvent(string name, params KeyValuePair<string, object?>[] attributes);
}
```

Fluente — todo mutador retorna `this`. `Dispose()` encerra o span. → **[Spans · `IAxisSpan`](spans.md)**

### `TelemetryBehavior` — auto-instrumentação do mediator

`IAxisPipelineBehavior` opt-in que abre um span `AxisMediator.{RequestName}` em torno de cada request, tag-eia com `TraceId`/`JourneyId`/`RequestType`/`AxisEntityId`/`RequestName`, cronometra, grava:

- `axis.handler.duration_ms` (histogram)
- `axis.handler.invocations` (counter)
- `axis.handler.exceptions` (counter, em exceções não tratadas)

→ **[`TelemetryBehavior` — instrumentação automática](telemetry-behavior.md)**

### Adapters

| Adapter | Use quando |
|---|---|
| **`OpenTelemetryAdapter`** | produção — emite para `ActivitySource` + `Meter`; pareie com o exporter OpenTelemetry de sua escolha |
| **`AxisTelemetry.AzureMonitor`** (pacote separado) | produção no Azure — o adapter acima já pareado com o distro oficial do Azure Monitor / Application Insights, com controles de custo (sampling, filtro de logs) e fallback no-op quando não há connection string |
| **`NullAxisTelemetry`** | testes, ferramentas single-process — toda chamada é no-op, todo span é `NullAxisSpan.Instance` |

→ **[Adapter OpenTelemetry](opentelemetry-adapter.md)** · **[Adapter Azure Monitor](azure-monitor.md)** · **[Adapter null](null-adapter.md)**

### Instalação

```
dotnet add package AxisTelemetry
```

`AxisTelemetry` depende diretamente apenas de `AxisLogger` (que traz `AxisResult` e `AxisMediator.Contracts` transitivamente). O adapter OpenTelemetry usa `System.Diagnostics.ActivitySource` e `System.Diagnostics.Metrics.Meter` da BCL — sem packages NuGet extras.

Exportando para Azure Monitor / Application Insights? Instale o pacote de pareamento no lugar:

```
dotnet add package AxisTelemetry.AzureMonitor
```

→ **[Adapter Azure Monitor](azure-monitor.md)**

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Contratos · `IAxisTelemetry` / `IAxisMetrics`** | as duas portas | [contracts.md](contracts.md) |
| **Spans · `IAxisSpan`** ⭐ | abrir, tagear, gravar exceções, dispor | [spans.md](spans.md) |
| **`TelemetryBehavior`** | auto-traçar cada request do mediator | [telemetry-behavior.md](telemetry-behavior.md) |
| **Adapter OpenTelemetry** | tracing + métricas de produção | [opentelemetry-adapter.md](opentelemetry-adapter.md) |
| **Adapter Azure Monitor** | exportar para o Application Insights, controlar a conta | [azure-monitor.md](azure-monitor.md) |
| **Adapter null** | desligar telemetria em testes | [null-adapter.md](null-adapter.md) |
| **Tag names** | as constantes canônicas | [tag-names.md](tag-names.md) |
| **Por quê?** | o argumento pela abstração | [why-axistelemetry.md](why-axistelemetry.md) |
| **Referência** | cada membro num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [Os contratos](contracts.md) · [Por que AxisTelemetry?](why-axistelemetry.md)

**Fundamentos:** [Spans · `IAxisSpan`](spans.md) · [`TelemetryBehavior`](telemetry-behavior.md) · [Adapter OpenTelemetry](opentelemetry-adapter.md) · [Adapter Azure Monitor](azure-monitor.md)

**Referência e extras:** [Adapter null](null-adapter.md) · [Tag names](tag-names.md) · [Referência da API](api-reference.md)

---

## Princípios de design

1. **Duas portas estreitas, um adapter.** `IAxisTelemetry` para traces, `IAxisMetrics` para métricas; o adapter OpenTelemetry implementa ambas com um único tipo.
2. **Código de aplicação vendor-neutral.** `ActivitySource` e `Meter` vivem na BCL; a aplicação só vê `IAxisSpan`.
3. **Instrumentação no nível do pipeline.** Registre `TelemetryBehavior` e cada command/query é cronometrado, tagged e traced — sem boilerplate por handler.
4. **Null é um adapter first-class.** `NullAxisTelemetry.Instance` deixa testes e ferramentas pontuais pularem o custo sem mudar o código chamador.
5. **Nomes de tag são constantes.** `TelemetryTagNames.RequestName` (e cia) evitam typos e tornam queries no sink previsíveis.

---

## Licença

Apache 2.0
