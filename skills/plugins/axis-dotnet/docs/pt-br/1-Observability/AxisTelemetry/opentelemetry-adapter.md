# Adapter OpenTelemetry

> A implementação embarcada de `IAxisTelemetry` + `IAxisMetrics` sobre `System.Diagnostics.ActivitySource` e `System.Diagnostics.Metrics.Meter`. Ambas são primitivas da BCL — o SDK do OpenTelemetry consome elas, mas outros ouvintes também (Application Insights, Sentry, seu coletor próprio).

```csharp
services.AddOpenTelemetryAxis();
```

---

## Quando usar

Produção. Em qualquer lugar onde você queira spans e métricas fluindo para um sink. Pareie com `OpenTelemetry.Extensions.Hosting` e os exporters de sua escolha para enviar os dados. Para Azure Monitor / Application Insights, o [`AxisTelemetry.AzureMonitor`](azure-monitor.md) entrega esse pareamento pronto.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| rodar testes sem custo de instrumentação | [`NullAxisTelemetry`](null-adapter.md) |
| escrever um sink diferente (SDK do Datadog, HTTP cru) | um adapter custom implementando ambos os contratos |

---

## O que `AddOpenTelemetryAxis()` registra

Lendo `DependencyInjection.AddOpenTelemetryAxis`:

```csharp
services.AddSingleton<OpenTelemetryAdapter>();
services.AddSingleton<IAxisTelemetry>(sp => sp.GetRequiredService<OpenTelemetryAdapter>());
services.AddSingleton<IAxisMetrics>  (sp => sp.GetRequiredService<OpenTelemetryAdapter>());
```

Um singleton serve as duas portas. O adapter mantém um `ActivitySource` e `Meter` processo-amplos, ambos chamados `"Axis.AxisMediator"`. Adicione-os ao seu pipeline OpenTelemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("Axis.AxisMediator").AddOtlpExporter())
    .WithMetrics(b => b.AddMeter ("Axis.AxisMediator").AddOtlpExporter());
```

> A constante `SourceName` é `OpenTelemetryAdapter.SourceName = "Axis.AxisMediator"` — use a constante se quiser garantir que fica sincronizada.

---

## Como cada método do contrato mapeia para primitivas BCL

| Contrato | Chamada BCL | Notas |
|---|---|---|
| `StartSpan(name, kind)` | `ActivitySource.StartActivity(name, MapKind(kind))` | embrulhado em `ActivityAxisSpan(activity)` |
| `CurrentTraceId` | `Activity.Current?.TraceId.ToString()` | o trace id W3C |
| `CurrentSpanId` | `Activity.Current?.SpanId.ToString()` | o span id W3C |
| `RecordHistogram(name, value, tags)` | `_histograms.GetOrAdd(name, Meter.CreateHistogram<double>(n)).Record(value, tags)` | o histogram é criado lazy e cacheado |
| `IncrementCounter(name, delta, tags)` | `_counters.GetOrAdd(name, Meter.CreateCounter<long>(n)).Add(delta, tags)` | o counter é criado lazy e cacheado |

O caching lazy significa que você pode chamar `RecordHistogram("my.metric", ...)` de qualquer caminho — o `Histogram<double>` é criado na primeira chamada e reusado depois, atomicamente (concurrent-dictionary).

---

## `AxisSpanKind` → `ActivityKind`

| `AxisSpanKind` | `ActivityKind` |
|---|---|
| `Internal` | `Internal` |
| `Server` | `Server` |
| `Client` | `Client` |
| `Producer` | `Producer` |
| `Consumer` | `Consumer` |

---

## Exemplo real — fiação de produção

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()
    .AddOpenTelemetryAxis();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("orders-api"))
    .WithTracing(b => b
        .AddSource(OpenTelemetryAdapter.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(b => b
        .AddMeter(OpenTelemetryAdapter.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

**Por que compensa:** o SDK do OpenTelemetry exporta seus spans e métricas ao lado da instrumentação do ASP.NET Core e do HTTP-client — um pipeline OTLP, um trace por request, com os spans `AxisMediator.{RequestName}` aninhados onde devem.

---

## Veja também

- [Os contratos](contracts.md) — `IAxisTelemetry` e `IAxisMetrics`
- [Spans · `IAxisSpan`](spans.md) — `ActivityAxisSpan` é o que `StartSpan` retorna
- [Adapter null](null-adapter.md) — vire para no-ops em testes
- [`TelemetryBehavior`](telemetry-behavior.md) — o behaviour que alimenta este adapter

---

↩ [Voltar à documentação do AxisTelemetry](README.md)
