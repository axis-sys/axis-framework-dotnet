# `TelemetryBehavior` — instrumentação automática

> Um `IAxisPipelineBehavior` opt-in que embrulha cada request do mediator com um span `AxisMediator.{RequestName}` e grava métricas de duração / invocações / exceções. Um registro, tudo traçado.

```csharp
services.AddTransient(typeof(IAxisPipelineBehavior<>), typeof(TelemetryBehavior<>));
services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
```

---

## Quando usar

Sempre — a menos que tenha razão pra não. O behaviour é barato (um span aberto/fechado por request, três escritas de métrica), e te dá a visão por handler que você teria que ligar à mão em cada um.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| pular telemetria completamente | plugue `NullAxisTelemetry` no lugar, e pule este behaviour |
| gravar métricas **de negócio** (pedidos / minuto, receita / dia) | um behaviour custom ou chame `IAxisMetrics` do handler |
| cronometrar só um subconjunto de requests | um behaviour custom com predicate |

---

## O que o behaviour grava

Lendo `TelemetryBehavior<TRequest>` e `<TRequest, TResponse>` direto:

### O span

| Aspecto | Valor |
|---|---|
| Nome | `$"AxisMediator.{typeof(TRequest).Name}"` |
| Kind | (padrão) `Internal` |
| `TelemetryTagNames.TraceId` | `mediator.TraceId` |
| `TelemetryTagNames.JourneyId` | `mediator.JourneyId` |
| `TelemetryTagNames.RequestType` | `"command"` (overload não-genérico) ou `request is IAxisQuery ? "query" : "command"` (overload tipado) |
| `TelemetryTagNames.AxisEntityId` | `mediator.AxisEntityId` |
| `TelemetryTagNames.RequestName` | `typeof(TRequest).Name` |
| `TelemetryTagNames.ResultSuccess` | `result.IsSuccess` |
| `TelemetryTagNames.ErrorCodes` | `result.Errors[*].Code` separados por vírgula (em falha) |
| Status | `AxisSpanStatus.Ok` (sucesso) / `AxisSpanStatus.Error` (falha) |
| Exceção | `span.RecordException(ex)` (em exceção não tratada) |

O span também é escrito no `context` do pipeline via `AxisPipelineContextKeys.Span`, para que behaviours a jusante consigam ler.

### As métricas

| Métrica | Tipo | Tags |
|---|---|---|
| `axis.handler.duration_ms` | histogram (`double`) | `RequestName`, `ResultSuccess` |
| `axis.handler.invocations` | counter (`long`, delta 1) | `RequestName`, `ResultSuccess` |
| `axis.handler.exceptions` | counter (`long`, delta 1) | `RequestName`, `ExceptionType` |

O counter de exceção só incrementa quando o handler **lança** (não quando retorna `Error`). Retornar um `AxisResult` falho só bate o `axis.handler.invocations` com `ResultSuccess = false`.

---

## Exemplo real — o que aparece no sink

Para um `CreateOrderCommand` bem-sucedido levando 42 ms:

```
span AxisMediator.CreateOrderCommand
    axis.trace_id      = …
    axis.journey_id    = …
    axis.request_type  = command
    axis.axis_entity_id = 1|01927a8b-…
    axis.request_name  = CreateOrderCommand
    axis.result_success = true
    status = Ok
metric axis.handler.duration_ms  = 42 (RequestName=CreateOrderCommand, ResultSuccess=true)
metric axis.handler.invocations  +1   (RequestName=CreateOrderCommand, ResultSuccess=true)
```

Para um run falho (erro de validação, sem exceção):

```
span AxisMediator.CreateOrderCommand
    … mesmas tags …
    axis.result_success = false
    axis.error_codes    = "PERSON_EMAIL_INVALID"
    status = Error
metric axis.handler.duration_ms  = 5  (RequestName=CreateOrderCommand, ResultSuccess=false)
metric axis.handler.invocations  +1   (RequestName=CreateOrderCommand, ResultSuccess=false)
```

Para uma exceção (sem `axis.error_codes` porque o result nunca foi retornado):

```
span AxisMediator.CreateOrderCommand
    … mesmas tags …
    event "exception" { type, message, stacktrace }
    status = Error
metric axis.handler.invocations  +1   (RequestName=CreateOrderCommand, ResultSuccess=false)
metric axis.handler.exceptions   +1   (RequestName=CreateOrderCommand, ExceptionType=DbUpdateException)
```

**Por que compensa:** dashboards conseguem mostrar p99 de latência por comando, taxa de falha por comando, e contagem de exceções por tipo — sem código de instrumentação por handler.

---

## Veja também

- [Os contratos](contracts.md) — as duas portas que o behaviour usa
- [Spans · `IAxisSpan`](spans.md) — o objeto que o behaviour abre
- [Tag names](tag-names.md) — as constantes tag-eadas em cada span
- [Adapter OpenTelemetry](opentelemetry-adapter.md) — o que recebe os spans/métricas

---

↩ [Voltar à documentação do AxisTelemetry](README.md)
