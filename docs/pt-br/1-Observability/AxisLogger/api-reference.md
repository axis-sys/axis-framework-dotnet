# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## O contrato — `IAxisLogger<T>`

| Método | Assinatura | Descrição |
|---|---|---|
| `LogDebug` | `void LogDebug(string, params (string Key, object? Value)[])` | entrada debug estruturada |
| `LogInformation` | `void LogInformation(string, params (string Key, object? Value)[])` | entrada information estruturada |
| `LogWarning` | `void LogWarning(string, params (string Key, object? Value)[])` | entrada warning estruturada |
| `LogError` (sem exceção) | `void LogError(string, params (string Key, object? Value)[])` | entrada error estruturada, sem stack trace |
| `LogError` (com exceção) | `void LogError(Exception, string, params (string Key, object? Value)[])` | entrada error estruturada com stack trace |
| `LogCritical` | `void LogCritical(string, params (string Key, object? Value)[])` | entrada critical estruturada |
| `LogResult` | `void LogResult(string tag, AxisResult result)` | logue um desfecho de `AxisResult`; `Information` no sucesso, `Error` na falha |

→ [O contrato `IAxisLogger<T>`](iaxislogger.md) · [`LogResult`](log-result.md)

---

## Enriquecimento sempre-ligado

O scope de cada entrada carrega:

| Chave | Fonte |
|---|---|
| `UtcTime` | `TimeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss.fff zzz")` |
| `OriginId` | `IAxisMediator.OriginId` |
| `TraceId` | `IAxisMediator.TraceId` |
| `JourneyId` | `IAxisMediator.JourneyId` |

Mais suas propriedades `(Key, Value)` (que sobrescrevem os padrões se compartilham uma chave).

→ [Categorias e propriedades estruturadas](categories.md)

---

## Pipeline behaviour — `LoggingBehavior`

| Tipo | Onde se senta | Método |
|---|---|---|
| `LoggingBehavior<TRequest>` | requests sem response | `HandleAsync(TRequest, AxisPipelineContext, Func<Task<AxisResult>>)` |
| `LoggingBehavior<TRequest, TResponse>` | requests com response tipada | `HandleAsync(TRequest, AxisPipelineContext, Func<Task<AxisResult<TResponse>>>)` |

Ambos logam `"Handling {RequestName}"` em `Information` antes de chamar `next()`.

→ [`LoggingBehavior` — request logging automático](logging-behavior.md)

---

## Extensões de DI (extensions de C# 12 em `IServiceCollection`)

| Extensão | Efeito |
|---|---|
| `AddAxisLogger()` | `services.AddLogging()` + `services.AddScoped(typeof(IAxisLogger<>), typeof(AxisLogger<>))` |
| `AddLoggingBehavior()` | `services.AddSingleton(TimeProvider.System)` + registra `LoggingBehavior<>` e `LoggingBehavior<,>` como transient `IAxisPipelineBehavior` |

---

## Tabela de comportamento do `LogResult`

| `result.IsSuccess` | `LogLevel` | Propriedades adicionadas |
|---|---|---|
| `true` | `Information` | `Tag`, `RequestName` |
| `false` | `Error` | `Tag`, `RequestName`, `AxisErrorList` |

Mais o enriquecimento sempre-ligado.

→ [`LogResult` — desfechos estruturados](log-result.md)

---

## Extensões de tap sobre o desfecho — `AxisResultLoggingExtensions`

Logging de efeito colateral, família Tap, para `AxisResult`: loga o desfecho e retorna o MESMO result inalterado, então a chamada encaixa numa cadeia `Then`/`Match` em vez de um `if (result.IsFailure) logger.LogWarning(...)` manual em cada call site.

| Método | Assinatura | Descrição |
|---|---|---|
| `LogIfFailure` | `AxisResult LogIfFailure<T>(this AxisResult, IAxisLogger<T>, AxisFailureLogSeverity, string, params (string Key, object? Value)[])` | loga só na falha (`Warning` ou `Error`, conforme a severidade); anexa os erros como `AxisErrorList`; retorna o mesmo result inalterado |
| `LogIfFailure` (tipado) | `AxisResult<TValue> LogIfFailure<T, TValue>(this AxisResult<TValue>, IAxisLogger<T>, AxisFailureLogSeverity, string, params (string Key, object? Value)[])` | igual ao anterior, preservando o `.Value` na cadeia |
| `LogIfSuccess` | `AxisResult LogIfSuccess<T>(this AxisResult, IAxisLogger<T>, AxisSuccessLogSeverity, string, params (string Key, object? Value)[])` | loga só no sucesso (`Information` ou `Warning`, conforme a severidade); retorna o mesmo result inalterado |
| `LogIfSuccess` (tipado) | `AxisResult<TValue> LogIfSuccess<T, TValue>(this AxisResult<TValue>, IAxisLogger<T>, AxisSuccessLogSeverity, string, params (string Key, object? Value)[])` | igual ao anterior, preservando o `.Value` na cadeia |

| Enum | Valores | Usado por |
|---|---|---|
| `AxisFailureLogSeverity` | `Warning`, `Error` | `LogIfFailure` |
| `AxisSuccessLogSeverity` | `Information`, `Warning` | `LogIfSuccess` |

---

## Veja também

- [Primeiros passos](getting-started.md) — instale, registre, logue
- [Por que AxisLogger?](why-axislogger.md) — o argumento pela abstração
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisLogger](README.md)
