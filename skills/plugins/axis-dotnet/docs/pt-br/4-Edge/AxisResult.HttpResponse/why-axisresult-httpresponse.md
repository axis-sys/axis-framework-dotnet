# Por que AxisResult.HttpResponse? · comparação

> Uma package de borda minúscula que se paga já na segunda vez que você escreve um controller. Aqui está o que ela substitui e por que um adaptador dedicado vence as alternativas.

## vs. mapear à mão com `Match`

Você *pode* traduzir cada resultado no controller com `Match`:

```csharp
return result.Match(
    onSuccess: value  => Ok(value),
    onFailure: errors => errors[0].Type switch
    {
        AxisErrorType.NotFound       => NotFound(),
        AxisErrorType.ValidationRule => BadRequest(),
        AxisErrorType.Conflict       => Conflict(),
        // … mais onze, repetidos em todo controller, fáceis de errar
        _ => StatusCode(500)
    });
```

Três problemas: o `switch` é **duplicado** em todo endpoint, ele inspeciona `errors[0]` — então um `ValidationRule` encobrindo um `InternalServerError` retorna `400` em vez de `500` — e nada força o `ProblemDetails`, o `traceId` ou a supressão de erros internos a serem consistentes. `HttpContext.SendAsync` faz tudo isso de uma vez — inclusive capturar o `traceId` automaticamente de `HttpContext.TraceIdentifier`.

## vs. `ProblemDetails` / exception filters do ASP.NET Core

O `ProblemDetails` do framework e o middleware baseado em exceções assumem que **exceções** são o seu canal de falha. O ponto central do AxisResult é que erros são **valores** — eles nunca lançam, então não há nada para um exception filter capturar. Esta package faz a ponte entre falhas baseadas em valor e o corpo RFC 7807 sem ressuscitar o fluxo-de-controle-por-exceção.

## vs. uma library genérica `Result` → `IActionResult` (ex.: `Ardalis.Result.AspNetCore`)

Adaptadores genéricos mapeiam um conjunto pequeno e fixo de status e não sabem nada sobre a sua taxonomia de erros. Esta package é construída **para** `AxisError`: todas as 12 categorias de `AxisErrorType`, seleção por severidade em falhas com múltiplos erros, supressão de erros internos e um payload `code`/`type` estável — sem código de cola para manter sincronizado.

---

## A comparação

| Recurso | **AxisResult.HttpResponse** | `Match` à mão | Filter `ProblemDetails` do ASP.NET | Adaptador `Result` genérico |
|---|:--:|:--:|:--:|:--:|
| Uma chamada por endpoint | **Sim** | Não (switch por endpoint) | Parcial | Sim |
| Todas as 12 `AxisErrorType` mapeadas | **Sim** | Manual | Não | Parcial |
| Status por severidade em múltiplos erros | **Sim** | Não | Não | Não |
| Corpo `ProblemDetails` RFC 7807 | **Sim** | Manual | Sim | Parcial |
| Erros internos suprimidos | **Sim** | Manual | Não | Não |
| `traceId` sempre incluído | **Sim** | Manual | Parcial | Não |
| Funciona com erros baseados em valor (sem lançar) | **Sim** | Sim | Não | Sim |

---

## Veja também

- [Converter · `HttpContext.SendAsync`](send-http-response.md) — a única chamada de que tudo isto trata
- [Mapeamento erro → status](error-status-mapping.md) — a tabela e as regras de severidade que o tornam consistente
- [Primeiros passos](getting-started.md) — instale e use num controller

---

↩ [Voltar à documentação do AxisResult.HttpResponse](README.md)
