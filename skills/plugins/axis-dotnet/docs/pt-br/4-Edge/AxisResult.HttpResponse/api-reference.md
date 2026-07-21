# Referência da API

> A superfície pública completa, agrupada por responsabilidade. Use para consulta; para a justificativa, siga os links de seta para as páginas de detalhe.

O grosso da API pública é a classe estática `AxisHttpContextExtensions` (namespace `Axis`), mais `AxisProblemDetailsBuilder` (a mesma renderização, desacoplada de `IActionResult`), um pequeno registro de DI e uma classe de configuração de startup cobertos abaixo. O mapeamento erro→status (`AxisErrorTypeHttpMapping`) é `internal` e é documentado, não exposto.

## Extensões de resposta HTTP

Extension members de `HttpContext` declarados em `AxisHttpContextExtensions` — no controller, a chamada é `HttpContext.SendAsync(...)`. Um `HttpContext` ou uma `resultTask` nulos lançam `ArgumentNullException`.

| Método | Assinatura | Descrição |
|---|---|---|
| `SendAsync` | `Task<IActionResult> SendAsync(Task<AxisResult> resultTask, HttpStatusCode statusCode = OK)` | Aguarda a task e renderiza um resultado sem valor: `StatusCodeResult(statusCode)` no sucesso, `ProblemDetails` na falha. |
| `SendAsync<TData>` | `Task<IActionResult> SendAsync<TData>(Task<AxisResult<TData>> resultTask, HttpStatusCode statusCode = OK)` | Aguarda a task e renderiza um resultado com valor: `ObjectResult(Value)` (ou `NoContentResult` quando `statusCode` é `NoContent`) no sucesso, `ProblemDetails` na falha. |

**Parâmetros**

- `resultTask` — a `Task` do resultado a renderizar. Para um resultado síncrono, use `HttpContext.SendAsync(Task.FromResult(result))`.
- `statusCode` — o status usado **apenas** no trilho de sucesso (padrão `HttpStatusCode.OK`); `HttpStatusCode.NoContent` produz um `204` sem corpo. Na falha o status deriva dos erros.

O `traceId` não é um parâmetro: ele é injetado automaticamente de `HttpContext.TraceIdentifier` e exposto em `ProblemDetails.Extensions["traceId"]`.

→ [Converter · `HttpContext.SendAsync`](send-http-response.md)

## Reuso fora do MVC · `AxisProblemDetailsBuilder`

Para middleware e `IAsyncAuthorizationFilter` — código que roda antes ou à margem do MVC e não tem um `ActionContext` para devolver um `IActionResult`. O builder computa; as extensões sobre `HttpContext` consomem, e cada uma é nomeada pelo seu efeito.

| Membro | Assinatura | Descrição |
|---|---|---|
| `AxisProblemDetailsBuilder.Build` | `(int StatusCode, ProblemDetails Details) Build(IReadOnlyList<AxisError> errors, string traceId)` | Computação pura do par status/`ProblemDetails`; sem dependência de `HttpContext`. A renderização de falha do `HttpContext.SendAsync` delega para isto. |
| `WriteProblemDetailsAsync` | `Task WriteProblemDetailsAsync(this HttpContext context, AxisError error)` | Sobrecarga de conveniência para um único erro, sobre a de baixo. |
| `WriteProblemDetailsAsync` | `Task WriteProblemDetailsAsync(this HttpContext context, IReadOnlyList<AxisError> errors)` | Chama `Build` e então **escreve** o `StatusCode` e o corpo JSON direto em `context.Response`. Para middleware. |
| `ToProblemDetailsResult` | `ObjectResult ToProblemDetailsResult(this HttpContext context, AxisError error)` | Sobrecarga de conveniência para um único erro, sobre a de baixo. |
| `ToProblemDetailsResult` | `ObjectResult ToProblemDetailsResult(this HttpContext context, IReadOnlyList<AxisError> errors)` | Chama `Build` e envolve num `ObjectResult` **sem tocar na resposta**. Para um `IAsyncAuthorizationFilter`, que atribui `context.Result`. |

As extensões tiram o `traceId` de `HttpContext.TraceIdentifier`. Não existe `BuildAsync` de propósito: `Build` e `BuildAsync` se leriam como um par sync/async da mesma operação, mas o caminho de escrita faz I/O e não devolve nada.

→ [Reuso fora do MVC · `AxisProblemDetailsBuilder`](problem-details-builder.md)

## Renderização de falha (comportamento interno)

Não faz parte da API pública, mas é útil conhecer — estas regras são aplicadas quando um resultado é uma falha:

| Aspecto | Comportamento |
|---|---|
| Status code | do `AxisErrorType` **mais grave** da lista de erros |
| `type` | `{ProblemTypeBaseUri}<kebab-case-do-tipo>` — `https://axis.dev/problems/` a menos que sobrescrito, veja abaixo |
| `title` | reason phrase padrão do status code |
| `detail` | `"{visíveis} error(s) returned. {internos} internal error(s) suppressed."` |
| extensão `errors` | apenas erros visíveis, cada um `{ code, type }`; `InternalServerError` removido |
| extensão `traceId` | sempre o `HttpContext.TraceIdentifier` da requisição |
| fallback sem erros | `500` com `detail = "Failure without errors."` |

→ [Mapeamento erro → status](error-status-mapping.md)

## Configurando a URI base do `type`

| Membro | Assinatura | Descrição |
|---|---|---|
| `AxisProblemDetailsConfiguration.ProblemTypeBaseUri` | `static string ProblemTypeBaseUri { get; }` | URI base atual, padrão `AxisProblemDetailsConfiguration.DefaultProblemTypeBaseUri` (`"https://axis.dev/problems/"`) |
| `AxisProblemDetailsConfiguration.ConfigureProblemTypeBaseUri` | `static void ConfigureProblemTypeBaseUri(string? baseUri)` | sobrescreve a URI base uma única vez na inicialização; um valor nulo/vazio é ignorado, uma barra final é adicionada quando ausente |
| `AddAxisResultHttpResponse` | `IServiceCollection AddAxisResultHttpResponse(this IServiceCollection services, IConfiguration configuration)` | lê a chave de configuração `AxisResult:Http:ProblemTypeBaseUri` e chama `ConfigureProblemTypeBaseUri` com ela; não faz nada quando a chave está ausente ou vazia |

```csharp
// appsettings.json: { "AxisResult": { "Http": { "ProblemTypeBaseUri": "https://problems.example.test/" } } }
builder.Services.AddAxisResultHttpResponse(builder.Configuration);
```

## Veja também

- [Converter · `HttpContext.SendAsync`](send-http-response.md) — sobrecargas e exemplos
- [Reuso fora do MVC · `AxisProblemDetailsBuilder`](problem-details-builder.md) — a mesma renderização para middleware/filters
- [Mapeamento erro → status](error-status-mapping.md) — a tabela de mapeamento, severidade e a forma do `ProblemDetails`
- [Primeiros passos](getting-started.md) — instalação e uso mínimo

---

↩ [Voltar à documentação do AxisResult.HttpResponse](README.md)
