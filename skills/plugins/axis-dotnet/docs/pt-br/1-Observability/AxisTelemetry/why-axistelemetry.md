# Por que AxisTelemetry? · comparação

> Há outras maneiras de instrumentar código .NET. Esta página diz por que o AxisTelemetry é diferente — uma comparação direta, sem mão na cintura.

---

## vs. `ActivitySource` / `Meter` direto

`System.Diagnostics.ActivitySource` e `System.Diagnostics.Metrics.Meter` são os substratos. AxisTelemetry os usa. Chamá-los direto de handlers tem três arestas:

1. **Boilerplate.** `var activity = source.StartActivity(...)` → `try/finally` → tag → set status → `Dispose()` — em cada site.
2. **Sem status ciente de `Result`.** Traduzir um `AxisResult` em `ActivityStatusCode` é problema seu em cada call site.
3. **Integração com mediator é problema seu.** Sem tags `TraceId`/`JourneyId` automáticas, sem timing por handler, sem counters de exceção.

**AxisTelemetry** move tudo isso para `TelemetryBehavior` mais o `IAxisSpan` fluente. Código de produção fica focado na operação de negócio.

## vs. `OpenTelemetry.Trace.Tracer` (o tipo do SDK)

O `Tracer` do SDK OpenTelemetry tem forma similar, mas acoplado fortemente à package do SDK. AxisTelemetry fica nas types da BCL para que um sink diferente (App Insights via `ApplicationInsights.WorkerService`, OTLP cru, SDK do Datadog) consiga escutar sem refiar o código da aplicação.

## vs. um `IInstrumentation` caseiro

DIY. Mesma forma, mas você redescobre: a interface de `Span`, o adapter `Null`, o `Pipeline behaviour`, as constantes de tag name. `AxisTelemetry` poupa o custo — e é projetado junto com `AxisMediator`, `AxisLogger`, `AxisRepository` para que a auto-instrumentação acenda nas packages.

---

## A comparação

| Característica | AxisTelemetry | `ActivitySource` direto | SDK OpenTelemetry direto | `IInstrumentation` caseiro |
|---|:--:|:--:|:--:|:--:|
| `IAxisSpan` fluente | **Sim** | Não (`Activity.SetTag` manual) | Não (`TelemetrySpan` manual) | Talvez |
| `IAxisMetrics` para counters / histograms | **Sim** | Não (`Meter`/`Counter<T>` cru) | Sim | Talvez |
| Pipeline behaviour automático (`TelemetryBehavior`) | **Sim** | Não | Não | Talvez |
| `NullAxisTelemetry` para testes | **Sim** | n/a | n/a | Talvez |
| Constantes de tag name | **Sim** | Não | Não | Talvez |
| Só primitivas BCL (sem NuGet além da BCL) | **Sim** | Sim | Não | Sim |
| Status ciente de `AxisResult` | **Sim** | Não | Não | Talvez |

---

## Veja também

- [Os contratos](contracts.md) — a superfície
- [`TelemetryBehavior`](telemetry-behavior.md) — o operador que justifica a abstração
- [Adapter OpenTelemetry](opentelemetry-adapter.md) — a implementação na caixa

---

↩ [Voltar à documentação do AxisTelemetry](README.md)
