# Reuso fora do MVC · `AxisProblemDetailsBuilder`

> A mesma renderização de `ProblemDetails` baseada em severidade que o `HttpContext.SendAsync` usa no controller, exposta como um tipo independente — para middleware e authorization filters que rodam fora do pipeline `IActionResult`.

```csharp
// Num middleware — sem ActionContext, então escreva a resposta você mesmo
await context.WriteProblemDetailsAsync(AxisError.Forbidden("TENANT_NOT_ALLOWED"));

// Num IAsyncAuthorizationFilter — curto-circuite o MVC atribuindo um resultado
context.Result = context.HttpContext.ToProblemDetailsResult(errors);
```

---

## Quando usar

Fora do pipeline de actions do MVC — um middleware, um `IAsyncAuthorizationFilter`, ou qualquer outro adapter que já tenha um `AxisResult`/lista de `AxisError` em mãos de forma síncrona e precise da mesma regra de status/`ProblemDetails` que os controllers recebem via [`HttpContext.SendAsync`](send-http-response.md), sem depender de `IActionResult`.

## Quando *não* usar

| Você quer… | Use em vez disso |
|---|---|
| renderizar dentro de uma action de controller | [`HttpContext.SendAsync`](send-http-response.md) — mesma regra, retorna `IActionResult` |
| customizar o corpo do `ProblemDetails` por endpoint | `Match` + seu próprio `ProblemDetails` |
| um handler genérico para *exceções não tratadas* | isto só renderiza um `AxisResult`/`AxisError` que você já tem em mãos de forma síncrona — veja [Por que AxisResult.HttpResponse?](why-axisresult-httpresponse.md) |

---

## Os três membros, separados pelo que fazem

O builder **constrói**; as duas extensões **consomem**. Cada um é nomeado pelo seu efeito, então nenhum membro surpreende no call-site.

| Membro | Assinatura | Efeito |
|---|---|---|
| `AxisProblemDetailsBuilder.Build` | `(int StatusCode, ProblemDetails Details) Build(IReadOnlyList<AxisError> errors, string traceId)` | **Computa.** Puro — sem `HttpContext`, sem I/O. A renderização de falha do `HttpContext.SendAsync` delega para ele. |
| `HttpContext.WriteProblemDetailsAsync` | `Task WriteProblemDetailsAsync(this HttpContext context, IReadOnlyList<AxisError> errors)` | **Escreve.** Define `Response.StatusCode` e serializa o corpo. A resposta foi enviada — não escreva de novo. |
| `HttpContext.ToProblemDetailsResult` | `ObjectResult ToProblemDetailsResult(this HttpContext context, IReadOnlyList<AxisError> errors)` | **Converte.** Retorna um `ObjectResult`; deixa a resposta intocada para o MVC enviar. |

Cada extensão tem uma sobrecarga de um único `AxisError` que delega para a de lista. Ambas derivam status e corpo do `Build`, então nenhuma consegue divergir do que um controller retorna. O `traceId` vem de `HttpContext.TraceIdentifier` automaticamente.

> **Por que não `BuildAsync`?** Porque `Build` e `BuildAsync` se leriam como um par sync/async da mesma operação — e não são. `Build` te entrega um valor; o caminho de escrita faz I/O e não devolve nada. Nomear cada membro pelo seu efeito mantém o sufixo `Builder` honesto.

---

## Exemplo real — um middleware de header de tenant

```csharp
internal sealed class CurrentTenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenantAccessor accessor)
    {
        if (!context.Request.Headers.TryGetValue("X-Tenant", out var tenant))
        {
            await context.WriteProblemDetailsAsync(AxisError.ValidationRule("TENANT_HEADER_MISSING"));
            return;
        }

        await accessor.Set(tenant.ToString())
            .Match(onSuccess: () => next(context),
                   onFailure: context.WriteProblemDetailsAsync);   // method group — sem lambda
    }
}
```

**Por que compensa:** o middleware nunca reimplementa "qual erro vence" ou "como suprimir um erro interno" — ele reusa exatamente a mesma regra de todo controller da API, então um novo call-site não consegue mais divergir dela silenciosamente. Como a extensão pende de `HttpContext`, o `onFailure` a recebe como method group. O builder foi extraído como um tipo público exatamente por isso: uma aplicação de referência anterior tinha dois consumidores middleware/filter reimplementando essa renderização manualmente fora do pipeline do MVC, um deles com um bug de severidade latente causado pela própria duplicação.

## Exemplo real — um authorization filter

```csharp
internal sealed class PermissionAuthorizationFilter(IAuthorizationFacade facade) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var grants = await facade.GetTenantGrantsAsync(query);

        context.Result = grants.Match(
            onSuccess: response => IsGranted(response) ? null : context.HttpContext.ToProblemDetailsResult(AxisError.Forbidden("PERMISSION_DENIED")),
            onFailure: context.HttpContext.ToProblemDetailsResult);
    }
}
```

**Por que compensa:** um filter **não** deve escrever a resposta — ele curto-circuita o MVC atribuindo `context.Result`. O `ToProblemDetailsResult` retorna o `ObjectResult` e deixa a resposta em paz, que é exatamente o contrato de que o filter precisa.

## Por que uma tupla, não um `IActionResult`

`(int, ProblemDetails)` é dado puro — não pressupõe um `ActionContext` do MVC. Um controller envolve isso num `ObjectResult` (é exatamente o que `HttpContext.SendAsync` faz internamente); um middleware escreve as duas partes direto em `HttpContext.Response`. Qualquer um dos dois chamadores recebe os mesmos valores, sem um tipo específico de adapter no meio.

---

## Veja também

- [Converter · `HttpContext.SendAsync`](send-http-response.md) — o wrapper voltado a controller sobre este mesmo builder
- [Mapeamento erro → status](error-status-mapping.md) — a tabela de mapeamento, severidade e a forma do `ProblemDetails` que este builder implementa
- [Referência da API](api-reference.md) — a superfície pública completa num piscar de olhos

---

↩ [Voltar à documentação do AxisResult.HttpResponse](README.md)
