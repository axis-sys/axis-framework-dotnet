# Adapter null · `NullAxisTelemetry`

> Uma implementação no-op de `IAxisTelemetry` + `IAxisMetrics`, mais um `NullAxisSpan` no-op. Todo método é vazio, todo span é a mesma instância compartilhada. Use para desligar telemetria sem mudar call sites.

```csharp
services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
services.AddSingleton<IAxisMetrics>  (NullAxisTelemetry.Instance);
```

---

## Quando usar

- **Testes unitários** que não devem depender ou assert contra o pipeline de telemetria.
- **Ferramentas CLI pontuais** onde abrir um span e métrica por chamada é overhead puro.
- **Uma fase de migração** onde você quer a fiação no lugar mas a telemetria desligada.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| enviar para produção | o [adapter OpenTelemetry](opentelemetry-adapter.md) |
| capturar *alguns* sinais (ex.: só métricas) | um adapter custom que delega ao real seletivamente |

---

## O que ele faz

Lendo `NullAxisTelemetry` direto:

| Método | Comportamento |
|---|---|
| `StartSpan(name, kind)` | retorna `NullAxisSpan.Instance` |
| `CurrentTraceId` | `null` |
| `CurrentSpanId` | `null` |
| `RecordHistogram(name, value, tags)` | vazio |
| `IncrementCounter(name, delta, tags)` | vazio |

`NullAxisSpan`:

| Método | Comportamento |
|---|---|
| `TraceId` | `string.Empty` |
| `SpanId` | `string.Empty` |
| `SetTag(...)` | retorna `this` |
| `SetStatus(...)` | retorna `this` |
| `RecordException(...)` | retorna `this` |
| `AddEvent(...)` | retorna `this` |
| `Dispose()` | vazio |

Ambos expõem um campo `Instance` static; nada aloca no caminho quente.

---

## Exemplo real — um grafo DI limpo de teste unitário

```csharp
public class FakeServiceFixture
{
    public IServiceProvider Build()
    {
        var services = new ServiceCollection();

        services
            .AddAxisMediator()
            .AddAxisLogger();

        services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
        services.AddSingleton<IAxisMetrics>  (NullAxisTelemetry.Instance);

        // … o resto da fiação de teste

        return services.BuildServiceProvider();
    }
}
```

**Por que compensa:** o handler em teste ainda chama `telemetry.StartSpan(...)` e `metrics.IncrementCounter(...)`. O adapter null engole tudo; asserções ficam focadas no comportamento da aplicação, não na fiação de telemetria.

---

## Veja também

- [Os contratos](contracts.md) — o que o adapter implementa
- [Adapter OpenTelemetry](opentelemetry-adapter.md) — o equivalente de produção
- [Spans · `IAxisSpan`](spans.md) — `NullAxisSpan` casa a interface

---

↩ [Voltar à documentação do AxisTelemetry](README.md)
