# Primeiros passos · instalação e uso

> Instale a package, retorne `AxisResult` dos seus handlers e renderize com uma única chamada no controller.

## Instalação

```
dotnet add package AxisResult.HttpResponse
```

A package traz o [`AxisResult`](../../0-Foundations/AxisResult/README.md) transitivamente e referencia o framework compartilhado do ASP.NET Core (`Microsoft.AspNetCore.App`), então adicione-a a um **projeto web** (`Microsoft.NET.Sdk.Web` ou um projeto que já tenha como alvo o ASP.NET Core). Tudo vive no namespace `Axis` — o mesmo namespace raiz do `AxisResult` — então nenhum `using` extra é necessário além do que você já tem.

## Renderizando um `AxisResult` (sem valor)

Para operações que não produzem valor (deletar, verificar, marcar como lido), o trilho de sucesso retorna um status code sem corpo. A conversão é sempre `HttpContext.SendAsync`, que recebe uma `Task` — quando o resultado é síncrono, embrulhe com `Task.FromResult`:

```csharp
[HttpDelete("{id:guid}")]
public Task<IActionResult> Delete(Guid id)
{
    AxisResult result = personService.Delete(id);
    return HttpContext.SendAsync(Task.FromResult(result));   // 200 no sucesso
}
```

O `traceId` da requisição é injetado automaticamente de `HttpContext.TraceIdentifier` — você nunca o passa; ele já chega ao `ProblemDetails` de toda falha.

## Renderizando um `AxisResult<T>` (com valor)

Quando a operação carrega um valor, o sucesso vira um `ObjectResult` com esse valor serializado no corpo:

```csharp
[HttpGet("{id:guid}")]
public Task<IActionResult> GetById(Guid id)
{
    AxisResult<PersonResponse> result = personService.GetById(id);
    return HttpContext.SendAsync(Task.FromResult(result));   // 200 + corpo
}
```

## Escolhendo o status de sucesso

O segundo argumento define o status usado **no sucesso**. Use `Created` para inserções, `Accepted` para trabalho enfileirado e `NoContent` para descartar o corpo por completo:

```csharp
return HttpContext.SendAsync(Task.FromResult(result), HttpStatusCode.Created);   // 201 + corpo
return HttpContext.SendAsync(Task.FromResult(result), HttpStatusCode.NoContent); // 204, sem corpo
```

> `NoContent` é tratado de forma especial: mesmo num `AxisResult<T>`, o valor é descartado e um `NoContentResult` é retornado.

## Encadeando a partir de um handler (a forma real)

Na prática o handler já retorna um `Task<AxisResult<T>>` e você o passa direto, sem `Task.FromResult`:

```csharp
[HttpPost]
public Task<IActionResult> Create(CreatePersonCommand cmd)
    => HttpContext.SendAsync(
        mediator.Cqrs.ExecuteAsync<CreatePersonCommand, CreatePersonResponse>(cmd),
        HttpStatusCode.Created);
```

**Por que compensa:** uma expressão, sem cerimônia de `await`, sem `try/catch` e sem o ramo `if (result.IsFailure)` — o sucesso vira `201` e toda categoria de falha vira o status correto com um corpo RFC 7807, automaticamente.

## Veja também

- [Converter · `HttpContext.SendAsync`](send-http-response.md) — cada sobrecarga e seu comportamento
- [Mapeamento erro → status](error-status-mapping.md) — qual erro vira qual status, e a forma do `ProblemDetails`
- [Por que AxisResult.HttpResponse?](why-axisresult-httpresponse.md) — o que isto substitui

---

↩ [Voltar à documentação do AxisResult.HttpResponse](README.md)
