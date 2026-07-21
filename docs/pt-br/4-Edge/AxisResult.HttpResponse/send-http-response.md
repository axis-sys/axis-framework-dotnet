# Converter · `HttpContext.SendAsync`

> O único ponto de entrada da package: uma extensão sobre `HttpContext` que colapsa um `AxisResult` / `AxisResult<T>` em um `IActionResult` do ASP.NET Core — sucesso como status (com ou sem corpo), falha como `ProblemDetails` RFC 7807.

```csharp
[HttpGet("{id:guid}")]
public Task<IActionResult> GetById(Guid id)
    => HttpContext.SendAsync(                          // 200 + corpo, ou ProblemDetails
        mediator.Cqrs.QueryAsync<GetPersonByIdQuery, PersonResponse>(new(id)));
```

---

## Quando usar

Na **borda HTTP** — a action do controller — quando o seu código de aplicação já fala `AxisResult`. É o último passo do pipeline: tudo acima retorna resultados, e isto transforma o resultado final numa resposta.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| colapsar um resultado num valor não-HTTP (um DTO, uma mensagem, um exit code de CLI) | [`Match`](../../0-Foundations/AxisResult/match.md) |
| construir um corpo `ProblemDetails` totalmente customizado (membros extras, URIs de `type` por endpoint) | `Match` + seu próprio `Problem(...)` |
| manter a lógica de mapeamento dentro da camada de domínio/aplicação | nada aqui — esta package é só de borda |

---

## Sobrecargas disponíveis

As duas sobrecargas são extension members de `HttpContext`, declaradas em `AxisHttpContextExtensions`: cada uma recebe a `Task` do resultado e um `statusCode` opcional (padrão `HttpStatusCode.OK`), aguarda a task e renderiza. O `traceId` da requisição é injetado automaticamente de `HttpContext.TraceIdentifier` — o chamador nunca o passa. Um `HttpContext` ou uma task nulos lançam `ArgumentNullException`.

| Sobrecarga | Assinatura |
|---|---|
| `Task<AxisResult>` → resultado | `Task<IActionResult> SendAsync(Task<AxisResult> resultTask, HttpStatusCode statusCode = OK)` |
| `Task<AxisResult<T>>` → resultado | `Task<IActionResult> SendAsync<TData>(Task<AxisResult<TData>> resultTask, HttpStatusCode statusCode = OK)` |

### O que cada caso retorna

| Resultado | `statusCode` | `IActionResult` retornado |
|---|---|---|
| sucesso `AxisResult` | qualquer | `StatusCodeResult(statusCode)` — sem corpo |
| sucesso `AxisResult<T>` | `NoContent` | `NoContentResult` — valor descartado |
| sucesso `AxisResult<T>` | qualquer outro | `ObjectResult(Value) { StatusCode }` |
| qualquer falha | — | `ObjectResult(ProblemDetails) { StatusCode }` — status vindo do erro mais grave |

O `statusCode` é usado **apenas** no trilho de sucesso; na falha o status deriva dos erros. Veja [Mapeamento erro → status](error-status-mapping.md) para como esse status e esse corpo são construídos.

---

## Exemplos reais

### 1. Create — `201 Created` com corpo

```csharp
[HttpPost]
public Task<IActionResult> Create(CreatePersonCommand cmd)
    => HttpContext.SendAsync(
        mediator.Cqrs.ExecuteAsync<CreatePersonCommand, CreatePersonResponse>(cmd),
        HttpStatusCode.Created);
```

**Por que compensa:** o caminho feliz é `201` com o recurso criado, e qualquer erro de validação/conflito/servidor vira o status correto e o `ProblemDetails` — na mesma linha, sem ramificação.

### 2. Update — `204 No Content`

```csharp
[HttpPut("{id:guid}")]
public Task<IActionResult> Update(Guid id, UpdatePersonCommand cmd)
    => HttpContext.SendAsync(
        mediator.Cqrs.ExecuteAsync<UpdatePersonCommand, UpdatePersonResponse>(cmd with { PersonId = id }),
        HttpStatusCode.NoContent);
```

**Por que compensa:** mesmo que o handler retorne um valor, `NoContent` o descarta e responde `204` — você expressa a intenção HTTP na borda sem mudar o contrato do handler.

### 3. Get — sucesso `200`, ausente `404`

```csharp
[HttpGet("{id:guid}")]
public Task<IActionResult> GetById(Guid id)
    => HttpContext.SendAsync(mediator.Cqrs.QueryAsync<GetPersonByIdQuery, PersonResponse>(new(id)));
```

Se o handler retornar `AxisError.NotFound("PERSON_NOT_FOUND")`, a resposta é `404` com:

```json
{
  "type": "https://axis.dev/problems/not-found",
  "title": "Not Found",
  "status": 404,
  "detail": "1 error(s) returned. 0 internal error(s) suppressed.",
  "traceId": "0HMV…",
  "errors": [{ "code": "PERSON_NOT_FOUND", "type": "NotFound" }]
}
```

**Por que compensa:** a *categoria* `NotFound` — decidida no domínio — vira um `404` na borda, com um `code` legível por máquina no qual o cliente pode ramificar e um `traceId` que liga de volta aos seus logs.

### 4. Resultado síncrono

```csharp
[HttpDelete("{id:guid}")]
public Task<IActionResult> Delete(Guid id)
{
    AxisResult result = personService.Delete(id);
    return HttpContext.SendAsync(Task.FromResult(result));
}
```

**Por que compensa:** quando não há `Task` para aguardar, `Task.FromResult` embrulha o resultado sem custo real e a mesma sobrecarga renderiza do mesmo jeito — o estilo de chamada é a única diferença.

---

## Veja também

- [Mapeamento erro → status](error-status-mapping.md) — a tabela `AxisErrorType` → status, a seleção por severidade e a forma do `ProblemDetails`
- [Primeiros passos](getting-started.md) — instalação e o ciclo de vida mínimo
- [Referência da API](api-reference.md) — as duas sobrecargas num piscar de olhos
- [`Match`](../../0-Foundations/AxisResult/match.md) — colapsar um resultado em qualquer valor não-HTTP

---

↩ [Voltar à documentação do AxisResult.HttpResponse](README.md)
