# Tag names

> Duas classes estáticas — `TelemetryTagNames` e `AuthTelemetryTagNames` — que mantêm cada constante que o framework usa quando tag-eia spans e métricas. Use no lugar de strings inline para manter queries no sink previsíveis e seguras a refactor.

```csharp
span.SetTag(TelemetryTagNames.RequestName, "CreatePersonCommand");
```

---

## Quando usar

Sempre, quando você está tag-eando spans ou métricas com os sinais **definidos pelo framework** (request name, trace id, journey id, identidade, auth result etc.). Para tags específicas da aplicação (ex.: `"order.id"`), use suas próprias constantes ou strings inline.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| inventar uma nova tag de escopo do framework | estenda `TelemetryTagNames` e reuse; não espalhe strings novas |
| nomear uma tag pontual da aplicação (`"warehouse"`, `"step"`) | uma `const string` local ou string inline |

---

## `TelemetryTagNames`

| Constante | Valor | Significado |
|---|---|---|
| `AxisEntityId` | `"axis.axis_entity_id"` | o `AxisEntityId` do usuário autenticado |
| `TraceId` | `"axis.trace_id"` | o `TraceId` do mediator |
| `JourneyId` | `"axis.journey_id"` | o `JourneyId` do mediator (saga / longo) |
| `RequestType` | `"axis.request_type"` | `"command"` ou `"query"` |
| `RequestName` | `"axis.request_name"` | o `typeof(TRequest).Name` |
| `ResultSuccess` | `"axis.result_success"` | `true`/`false` de `AxisResult.IsSuccess` |
| `ErrorCodes` | `"axis.error_codes"` | `result.Errors[*].Code` separados por vírgula |
| `ExceptionType` | `"axis.exception_type"` | `ex.GetType().Name` |

## `AuthTelemetryTagNames`

| Constante | Valor | Significado |
|---|---|---|
| `Scheme` | `"auth.scheme"` | o scheme de auth que tratou o request (ex.: `"jwt-bearer"`, `"oauth2"`) |
| `Result` | `"auth.result"` | o resultado da tentativa de auth (`"success"`, `"failure"` etc.) |
| `FailureReason` | `"auth.failure_reason"` | um código curto de motivo quando a tentativa falhou |
| `ApiId` | `"auth.api_id"` | a id da API externa envolvida (quando aplicável) |
| `BruteForceSuspected` | `"auth.brute_force_suspected"` | `true` quando a falha faz parte de um padrão suspeito de brute-force |

---

## Por que constantes e não strings

| Sem constantes | Com constantes |
|---|---|
| `"axis.request_name"` digitado à mão em cada site → risco de typo | `TelemetryTagNames.RequestName` → compilador pega typos |
| Renomear exige grep + edit manual | renomeie num lugar; o compilador propaga |
| Queries no sink referenciam strings mágicas | queries no sink referenciam uma lista documentada e estável |

---

## Exemplo real — tag-eando a partir de um adapter custom

```csharp
public class CustomAuthHandler
{
    public async Task<AxisResult> HandleAsync(AuthRequest request)
    {
        using var span = telemetry.StartSpan("auth.handle", AxisSpanKind.Server)
            .SetTag(AuthTelemetryTagNames.Scheme, "jwt-bearer")
            .SetTag(AuthTelemetryTagNames.ApiId, request.ApiId);

        var result = await authenticator.AuthenticateAsync(request);

        span.SetTag(AuthTelemetryTagNames.Result, result.IsSuccess ? "success" : "failure");

        if (result.IsFailure)
            span.SetTag(AuthTelemetryTagNames.FailureReason, result.Errors[0].Code);

        return result;
    }
}
```

**Por que compensa:** todo dashboard que já filtra por `auth.scheme = "jwt-bearer"` continua funcionando. Tags novas só precisam ser adicionadas uma vez ao arquivo de constantes — cada site que usa pega a mudança em compile time.

---

## Veja também

- [`TelemetryBehavior`](telemetry-behavior.md) — usa cada `TelemetryTagNames.*` automaticamente
- [Spans · `IAxisSpan`](spans.md) — `SetTag` é o método que essas constantes alimentam
- [Os contratos](contracts.md) — o que carrega a tag

---

↩ [Voltar à documentação do AxisTelemetry](README.md)
