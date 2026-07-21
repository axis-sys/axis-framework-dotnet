# Contrato · `IAxisLogger<T>`

> Seis níveis de log familiares mais um `LogResult` ciente da ferrovia. Todo overload aceita `params (string Key, object? Value)[] properties` para enriquecimento estruturado. Cada entrada é embrulhada em um `ILogger.BeginScope(...)` carregando `UtcTime`, `OriginId`, `TraceId`, `JourneyId` e suas propriedades.

```csharp
public interface IAxisLogger<in T>
{
    void LogDebug(string message, params (string Key, object? Value)[] properties);
    void LogInformation(string message, params (string Key, object? Value)[] properties);
    void LogWarning(string message, params (string Key, object? Value)[] properties);
    void LogError(string message, params (string Key, object? Value)[] properties);
    void LogError(Exception exception, string message, params (string Key, object? Value)[] properties);
    void LogCritical(string message, params (string Key, object? Value)[] properties);
    void LogResult(string tag, AxisResult result);
}
```

---

## Quando usar

Injete `IAxisLogger<MyClass>` no lugar de `ILogger<MyClass>` em qualquer código que rode dentro do pipeline do Axis — handlers, behaviours, adapters, services. O `T` é a categoria; as props estruturadas são como você descreve *o que* aconteceu.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| logar de código de infra que roda **fora** de um escopo `IAxisMediator` (workers de background sem mediator) | `ILogger<T>` direto |
| construir mensagens interpoladas só para humanos | nada — mas passe valores como propriedades estruturadas, não como `$"…{x}…"` |
| emitir métricas ou traces (não logs) | [`AxisTelemetry`](../AxisTelemetry/README.md) |

---

## O que cada overload faz

Lendo `AxisLogger<T>` direto:

| Método | `LogLevel` | Exceção | Notas |
|---|---|---|---|
| `LogDebug` | `Debug` | — | suprimido a menos que `Debug` esteja habilitado no sink |
| `LogInformation` | `Information` | — | o padrão para "isto aconteceu" |
| `LogWarning` | `Warning` | — | algo fora do esperado, recuperável |
| `LogError` (overload string) | `Error` | — | algo falhou mas não lançou |
| `LogError` (overload com exceção) | `Error` | a exceção | para catches na borda |
| `LogCritical` | `Critical` | — | algo fundamental quebrou |
| `LogResult` | `Information` ou `Error` | — | nível escolhido por `result.IsSuccess`; veja [`LogResult`](log-result.md) |

Todo método passa por `Write(level, exception?, message, properties)`, que:

1. Pula a chamada completamente se `logger.IsEnabled(level)` for `false`.
2. Chama `logger.BeginScope(BuildScope(properties))`.
3. Chama `logger.Log(level, [exception], message)`.

---

## O que o `BuildScope` coloca na entrada

Sempre presentes:

| Chave | Fonte |
|---|---|
| `UtcTime` | `TimeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss.fff zzz")` |
| `OriginId` | `mediator.OriginId` — o sistema upstream que iniciou a jornada |
| `TraceId` | `mediator.TraceId` — a correlação de trace por requisição |
| `JourneyId` | `mediator.JourneyId` — o id da saga/jornada longa (se houver) |

Mais cada par `(Key, Value)` que você passou. Suas propriedades **sobrescrevem** os padrões se compartilham uma chave.

---

## Exemplos reais

### 1. Adicionando contexto a uma única entrada

```csharp
logger.LogInformation("Order created",
    ("OrderId",    order.OrderId),
    ("CustomerId", order.CustomerId),
    ("Total",      order.Total));
```

**Por que compensa:** o sink vê três campos estruturados lado a lado com `TraceId`/`OriginId`/`JourneyId`. Filtrar "todos os pedidos do cliente X com total > 1000" é uma query, não uma regex.

### 2. Logando uma exceção capturada na borda

```csharp
try
{
    return await httpClient.GetFromJsonAsync<Person>(url);
}
catch (HttpRequestException ex)
{
    logger.LogError(ex, "Upstream lookup failed", ("Url", url));
    return AxisError.ServiceUnavailable("PERSON_LOOKUP_UNAVAILABLE");
}
```

**Por que compensa:** o stack trace vive na entrada, a URL é uma propriedade estruturada (não enfiada na mensagem), e o `TraceId` carrega dali pelo resto da requisição.

### 3. Tag-eando uma entrada estilo métrica

```csharp
logger.LogInformation("Webhook received",
    ("Provider",  "stripe"),
    ("EventType", payload.Type),
    ("Tenant",    tenant));
```

**Por que compensa:** o sink (Serilog/OpenTelemetry/Datadog) pode agrupar contagens de evento por `Provider`/`EventType`/`Tenant` sem parsear strings.

---

## Veja também

- [`LogResult` — desfechos estruturados](log-result.md) — o companheiro da ferrovia
- [`LoggingBehavior` — request logging automático](logging-behavior.md) — pipeline behaviour opt-in
- [Categorias e propriedades estruturadas](categories.md) — o que o `T` faz de fato
- [Referência da API](api-reference.md) — cada membro, num só lugar

---

↩ [Voltar à documentação do AxisLogger](README.md)
