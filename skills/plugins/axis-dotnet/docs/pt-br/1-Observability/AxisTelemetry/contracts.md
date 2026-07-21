# Os contratos · `IAxisTelemetry`, `IAxisMetrics`

> Duas portas estreitas — `IAxisTelemetry` para spans, `IAxisMetrics` para counters e histograms. O `OpenTelemetryAdapter` embarcado implementa ambas; o `NullAxisTelemetry` embarcado implementa ambas como no-ops.

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

---

## Quando usar

`IAxisTelemetry` sempre que você quer um **span** em torno de uma unidade de trabalho (`StartSpan` retorna um `IAxisSpan` que você tag-eia e dispõe). `IAxisMetrics` sempre que você quer um **counter** ou um **histogram** — invocações por handler, latência, taxa de hit do cache.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| logar uma mensagem | [`AxisLogger`](../AxisLogger/README.md) |
| publicar um evento de domínio | [`AxisBus`](../../2-ApplicationFlow/AxisBus/README.md) |
| auto-instrumentar cada request do mediator | [`TelemetryBehavior`](telemetry-behavior.md) — chama estes contratos por baixo |

---

## `AxisSpanKind` — os cinco kinds

| Valor | Quando | Equivalente OpenTelemetry |
|---|---|---|
| `Internal` (padrão) | trabalho in-process (handler, validator) | `ActivityKind.Internal` |
| `Server` | request HTTP / gRPC entrante | `ActivityKind.Server` |
| `Client` | chamada HTTP / DB / externa saindo | `ActivityKind.Client` |
| `Producer` | publicando mensagem num bus | `ActivityKind.Producer` |
| `Consumer` | consumindo mensagem de um bus | `ActivityKind.Consumer` |

O kind dirige ferramentas downstream (waterfalls, service graphs). Pegue o que case com a operação.

---

## O que `IAxisTelemetry.CurrentTraceId` / `CurrentSpanId` retornam

| Adapter | `CurrentTraceId` | `CurrentSpanId` |
|---|---|---|
| `OpenTelemetryAdapter` | `Activity.Current?.TraceId.ToString()` | `Activity.Current?.SpanId.ToString()` |
| `NullAxisTelemetry` | `null` | `null` |

Úteis para enrichment de log em seus próprios adapters, ou para gravar um span id num header de response.

---

## Exemplos reais

### 1. Traçar uma chamada HTTP downstream

```csharp
public async Task<AxisResult<Person>> GetUpstreamAsync(AxisEntityId id)
{
    using var span = telemetry.StartSpan("http.lookup.person", AxisSpanKind.Client);
    span.SetTag("http.url", $"https://upstream/person/{id}");

    var response = await httpClient.GetAsync($"/person/{id}");
    span.SetTag("http.status_code", (int)response.StatusCode);

    if (!response.IsSuccessStatusCode)
    {
        span.SetStatus(AxisSpanStatus.Error, $"HTTP {(int)response.StatusCode}");
        return AxisError.ServiceUnavailable("PERSON_LOOKUP_UNAVAILABLE");
    }

    span.SetStatus(AxisSpanStatus.Ok);
    return await response.Content.ReadFromJsonAsync<Person>();
}
```

**Por que compensa:** o span e suas tags viajam como um objeto estruturado; a chamada aparece na sua UI de trace como um span `Client` com método HTTP, URL e status code, prontos para consultar.

### 2. Histogram de lookups de cache

```csharp
public async Task<AxisResult<T?>> GetAsync<T>(string key)
{
    var sw = Stopwatch.StartNew();
    var result = await innerCache.GetAsync<T>(key);
    sw.Stop();

    metrics.RecordHistogram("cache.lookup_ms", sw.Elapsed.TotalMilliseconds,
        new("cache.key_prefix", key.Split(':')[0]),
        new("cache.hit", result.IsSuccess && result.Value is not null));

    return result;
}
```

**Por que compensa:** o histogram é particionado por prefixo e hit/miss; Prometheus / Grafana podem graficar p99 de lookup por prefixo, hit rate por prefixo, e alertar quando um prefixo específico degrada.

### 3. Counter para um evento que importa

```csharp
public async Task<AxisResult> HandleAsync(WebhookReceivedEvent @event)
{
    metrics.IncrementCounter("webhook.received",
        new("webhook.provider", @event.Provider),
        new("webhook.type",     @event.Type));

    return await processor.HandleAsync(@event);
}
```

**Por que compensa:** o counter é multi-dimensional desde o dia um — fatiar por provider ou type é uma query no sink, não mudança de código.

---

## Veja também

- [Spans · `IAxisSpan`](spans.md) — o objeto que `StartSpan` retorna
- [`TelemetryBehavior`](telemetry-behavior.md) — usa os dois contratos automaticamente
- [Adapter OpenTelemetry](opentelemetry-adapter.md) — a implementação na caixa
- [Tag names](tag-names.md) — as constantes canônicas

---

↩ [Voltar à documentação do AxisTelemetry](README.md)
