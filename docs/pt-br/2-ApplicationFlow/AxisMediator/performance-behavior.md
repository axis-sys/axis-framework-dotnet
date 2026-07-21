# Performance behaviour · `PerformanceBehavior`

> Um `IAxisPipelineBehavior<TRequest, TResponse>` opt-in que cronometra o pipeline interno com um `Stopwatch` e emite um `IAxisLogger<TRequest>.LogWarning` quando excede **500 ms**.

```csharp
services.AddPerformanceBehavior();
```

---

## Quando usar

Sempre, salvo política de latência mais elaborada. O behaviour é barato (um `Stopwatch` por request) e te dá um warning estruturado por handler lento — fácil de filtrar, fácil de alertar.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| mudar o threshold | escreva seu próprio behaviour (veja abaixo) |
| cronometrar **commands sem response** também | escreva o seu — o embarcado só registra para `<TRequest, TResponse>` |
| gravar histograms / counters | `TelemetryBehavior` do [`AxisTelemetry`](../../1-Observability/AxisTelemetry/telemetry-behavior.md) — já grava `axis.handler.duration_ms` |

---

## O que ele faz

Lendo `PerformanceBehavior` direto:

```csharp
internal class PerformanceBehavior<TRequest, TResponse>(IAxisLogger<TRequest> logger)
    : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<AxisResult<TResponse>> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
            logger.LogWarning($"Slow request: {typeof(TRequest).Name} took {sw.ElapsedMilliseconds}ms");

        return response;
    }
}
```

- O threshold é **500 ms** (um `const int`).
- O warning é estruturado via `IAxisLogger<TRequest>` — a entrada inclui `TraceId`/`OriginId`/`JourneyId` auto-enriquecidos.
- Só o overload `<TRequest, TResponse>` existe — o sabor void-command **não** é cronometrado por este behaviour.

---

## Registro

```csharp
builder.Services
    .AddAxisLogger()           // IAxisLogger<T>
    .AddPerformanceBehavior(); // PerformanceBehavior<,> registrado como transient IAxisPipelineBehavior<,>
```

`AddPerformanceBehavior` registra o open generic `PerformanceBehavior<,>` contra `IAxisPipelineBehavior<,>` — então todo command-com-response e query pega de graça.

---

## Exemplo real — uma versão com threshold custom

Se 500 ms é sensível demais (ou frouxo demais) para seu domínio, escreva o seu:

```csharp
public class StrictPerformanceBehavior<TRequest, TResponse>(IAxisLogger<TRequest> logger)
    : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    private const int ThresholdMs = 200;

    public async Task<AxisResult<TResponse>> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > ThresholdMs)
            logger.LogWarning("Slow request",
                ("RequestName", typeof(TRequest).Name),
                ("DurationMs", sw.ElapsedMilliseconds));

        return response;
    }
}

services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(StrictPerformanceBehavior<,>));
```

**Por que compensa:** copie o padrão, ajuste seu threshold, nomeie seus campos como seu sink espera. O behaviour é pequeno o bastante para você ser dono.

---

## Veja também

- [Pipeline behaviours](pipeline-behaviors.md) — o padrão geral
- [`TelemetryBehavior`](../../1-Observability/AxisTelemetry/telemetry-behavior.md) — grava `axis.handler.duration_ms` para histograms
- [`AxisLogger`](../../1-Observability/AxisLogger/README.md) — o logger estruturado que o warning usa

---

↩ [Voltar à documentação do AxisMediator](README.md)
