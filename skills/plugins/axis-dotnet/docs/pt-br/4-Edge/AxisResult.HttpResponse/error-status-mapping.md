# Mapeamento erro → status · `AxisErrorType`

> Uma falha nunca precisa ser traduzida à mão. Cada `AxisErrorType` tem um status code HTTP canônico; a package escolhe o status do erro **mais grave** e renderiza a falha inteira como `ProblemDetails` RFC 7807.

```csharp
AxisResult<PersonResponse> result = AxisError.NotFound("PERSON_NOT_FOUND");
return HttpContext.SendAsync(Task.FromResult(result));   // → 404 + ProblemDetails
```

---

## O mapeamento

| `AxisErrorType` | Status HTTP |
|---|:--:|
| `ValidationRule` | 400 Bad Request |
| `Unauthorized` | 401 Unauthorized |
| `Forbidden` | 403 Forbidden |
| `NotFound` | 404 Not Found |
| `Conflict` | 409 Conflict |
| `BusinessRule` | 422 Unprocessable Entity |
| `TooManyRequests` | 429 Too Many Requests |
| `InternalServerError` | 500 Internal Server Error |
| `Mapping` | 500 Internal Server Error |
| `ServiceUnavailable` | 503 Service Unavailable |
| `Timeout` | 504 Gateway Timeout |
| `GatewayTimeout` | 504 Gateway Timeout |

Qualquer tipo não mapeado recai em `500`. A tabela espelha as 12 categorias de `AxisErrorType` descritas em [AxisResult · Erros e tipos](../../0-Foundations/AxisResult/errors-and-types.md).

---

## O mais grave vence (falhas com múltiplos erros)

Uma falha pode carregar **muitos** erros. O status code é retirado do erro com a maior **severidade**, não do primeiro da lista:

| `AxisErrorType` | Severidade |
|---|:--:|
| `InternalServerError` | 100 |
| `Mapping` | 95 |
| `ServiceUnavailable` | 90 |
| `GatewayTimeout` | 85 |
| `Timeout` | 80 |
| `Unauthorized` | 70 |
| `Forbidden` | 65 |
| `Conflict` | 60 |
| `BusinessRule` | 55 |
| `NotFound` | 50 |
| `TooManyRequests` | 45 |
| `ValidationRule` | 40 |

```csharp
AxisResult result = AxisResult.Combine(
    AxisError.ValidationRule("NAME_REQUIRED"),    // severidade 40
    AxisError.InternalServerError("DB_TIMEOUT")); // severidade 100  ← vence

return HttpContext.SendAsync(Task.FromResult(result));   // → 500, não 400
```

**Por que compensa:** uma requisição que esbarrou numa regra de validação e numa falha de servidor é reportada como falha de servidor — o cliente faz retry ou escala corretamente em vez de "consertar" uma entrada que nunca foi o problema real.

---

## A forma do `ProblemDetails` (RFC 7807)

Toda falha é renderizada como um `ProblemDetails` padrão, mais duas `Extensions`: o `traceId` da requisição e a lista dos erros **visíveis**.

```json
{
  "type": "https://axis.dev/problems/validation-rule",
  "title": "Bad Request",
  "status": 400,
  "detail": "2 error(s) returned. 0 internal error(s) suppressed.",
  "traceId": "0HMVABC123",
  "errors": [
    { "code": "NAME_REQUIRED",  "type": "ValidationRule" },
    { "code": "EMAIL_INVALID",  "type": "ValidationRule" }
  ]
}
```

- **`type`** — `https://axis.dev/problems/<kebab-case>` por padrão, onde o slug é o `AxisErrorType` mais grave (`ValidationRule` → `validation-rule`). A URI base é configurável na inicialização — veja [`AxisProblemDetailsConfiguration`](api-reference.md#configurando-a-uri-base-do-type).
- **`title`** — a reason phrase padrão do status (`"Bad Request"`, `"Not Found"`, …).
- **`status`** — o status code escolhido, também definido no `ObjectResult`.
- **`detail`** — um resumo de contagem: quantos erros são expostos e quantos internos foram suprimidos.
- **`traceId`** / **`errors`** — carregados em `ProblemDetails.Extensions`, então serializam como membros de topo ao lado dos campos da RFC.

---

## Erros internos são suprimidos

Entradas `InternalServerError` nunca aparecem no array `errors`. Elas são **contadas** em `detail`, mas seu `code` é removido, de modo que detalhes de implementação (um driver falhando, o nome de uma dependência) nunca chegam ao cliente:

```json
{
  "type": "https://axis.dev/problems/internal-server-error",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "0 error(s) returned. 1 internal error(s) suppressed.",
  "traceId": "0HMVABC123",
  "errors": []
}
```

**Por que compensa:** o cliente fica sabendo *que* o servidor falhou (e recebe um `traceId` para reportar), mas nunca *o quê* falhou — nenhum vocabulário interno vaza pela borda.

## O `traceId` é automático

`traceId` não é um parâmetro nem um extra opcional: a package o injeta automaticamente de `HttpContext.TraceIdentifier`, e ele sempre acompanha a resposta, ligando o relato de um cliente direto aos seus logs.

## Fallback defensivo

Se um resultado de falha de alguma forma chegar à borda **sem** erros na lista, a package responde `500` com `detail = "Failure without errors."` em vez de lançar exceção — uma guarda extra que jamais deveria disparar pela API pública.

---

## Veja também

- [Converter · `HttpContext.SendAsync`](send-http-response.md) — a extensão que produz essas respostas
- [AxisResult · Erros e tipos](../../0-Foundations/AxisResult/errors-and-types.md) — de onde vêm o `AxisError` e as 12 categorias
- [Referência da API](api-reference.md) — as duas sobrecargas num piscar de olhos

---

↩ [Voltar à documentação do AxisResult.HttpResponse](README.md)
