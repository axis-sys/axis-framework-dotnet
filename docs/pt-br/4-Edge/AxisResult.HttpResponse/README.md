# AxisResult.HttpResponse — Documentação

> 🌐 [English (README principal)](../../../en-us/4-Edge/AxisResult.HttpResponse/README.md)

**A borda HTTP do [AxisResult](../../0-Foundations/AxisResult/README.md)** — uma extensão sobre `HttpContext` transforma um `AxisResult` / `AxisResult<T>` em um `IActionResult` do ASP.NET Core, mapeando cada `AxisErrorType` para o status code correto e renderizando falhas como `ProblemDetails` (RFC 7807, com o `traceId` da requisição e os erros internos suprimidos).

```csharp
// Em um controller — uma linha mapeia sucesso e toda categoria de falha
[HttpPost]
public Task<IActionResult> Create(CreatePersonCommand cmd)
    => HttpContext.SendAsync(
        mediator.Cqrs.ExecuteAsync<CreatePersonCommand, CreatePersonResponse>(cmd),
        HttpStatusCode.Created);
```

Use esta página como um **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe que você precisa.

---

## O tronco (leia primeiro)

### Do `AxisResult` ao HTTP em 60 segundos

Seus handlers retornam um `AxisResult` / `AxisResult<T>`. A única função do controller é renderizá-lo. Esta package é esse renderizador:

```
AxisResult ──sucesso──▶ StatusCodeResult (200, ou o seu status)
                                        │
AxisResult<T> ─sucesso─▶ ObjectResult { Value, status }  (204 → NoContentResult)
                                        │
qualquer ─────falha────▶ ObjectResult { ProblemDetails }  (status vindo do erro)
```

Uma única chamada, `HttpContext.SendAsync`, colapsa os dois trilhos na resposta HTTP correta. → **[Converter · `HttpContext.SendAsync`](send-http-response.md)**

### Sucesso vs. falha

- **Sucesso, sem valor** (`AxisResult`) → `StatusCodeResult` com `statusCode` (padrão `200 OK`).
- **Sucesso, com valor** (`AxisResult<T>`) → `ObjectResult` carregando `Value`; `HttpStatusCode.NoContent` resulta em um `204` sem corpo.
- **Falha** → um `ObjectResult` envolvendo um `ProblemDetails`, cujo status é escolhido a partir do erro **mais grave** da lista.

### Erro → status, a versão curta

Cada `AxisErrorType` mapeia para um status code; numa falha com múltiplos erros vence o de maior severidade, `InternalServerError` nunca vaza no payload, e o `traceId` sempre viaja em `extensions`. → **[Mapeamento erro → status](error-status-mapping.md)**

### Instalação

```
dotnet add package AxisResult.HttpResponse
```

Depende do [`AxisResult`](../../0-Foundations/AxisResult/README.md) e tem como alvo o ASP.NET Core (`FrameworkReference Microsoft.AspNetCore.App`) — adicione-a a um projeto web. → Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Converter · `HttpContext.SendAsync`** ⭐ | transformar um `AxisResult` em `IActionResult` | [send-http-response.md](send-http-response.md) |
| **Reuso fora do MVC · `AxisProblemDetailsBuilder`** | renderizar a mesma regra de `ProblemDetails` a partir de middleware/filters, sem `IActionResult` | [problem-details-builder.md](problem-details-builder.md) |
| **Mapeamento · `AxisErrorType` → status** | saber qual erro vira qual status code | [error-status-mapping.md](error-status-mapping.md) |
| **Por quê?** | a justificativa de uma package de borda dedicada | [why-axisresult-httpresponse.md](why-axisresult-httpresponse.md) |
| **Referência** | cada extensão num piscar de olhos | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [Converter · `HttpContext.SendAsync`](send-http-response.md) · [Por que AxisResult.HttpResponse?](why-axisresult-httpresponse.md)

**Fundamentos:** [Mapeamento erro → status](error-status-mapping.md) · [A forma do `ProblemDetails`](error-status-mapping.md#a-forma-do-problemdetails-rfc-7807)

**Referência e extras:** [Referência da API](api-reference.md)

---

## Princípios de design

1. **Uma linha no controller.** Sucesso e toda categoria de falha colapsam numa única chamada `HttpContext.SendAsync` — sem `try/catch`, sem ramo que você possa esquecer.
2. **O tipo do erro escolhe o status.** Os status codes derivam do `AxisErrorType`, não são escritos à mão por endpoint, então o mapeamento é consistente em toda a API.
3. **O mais grave vence.** Uma falha com muitos erros retorna o status do mais grave — um `ValidationRule` ao lado de um `InternalServerError` é um `500`, não um `400`.
4. **Nunca vaze detalhes internos.** Entradas `InternalServerError` são contadas mas removidas do payload; o cliente vê *que* algo falhou, não *o quê*.
5. **Sempre rastreável.** O `traceId` da requisição é capturado automaticamente de `HttpContext.TraceIdentifier` e sempre acompanha o `ProblemDetails.Extensions`, então o relato de um cliente liga direto aos seus logs — sem que o controller precise tocá-lo.

---

## Licença

Apache 2.0
