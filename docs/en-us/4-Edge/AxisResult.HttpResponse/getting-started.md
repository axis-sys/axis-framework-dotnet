# Getting started · installation and usage

> Install the package, return `AxisResult` from your handlers, and render it with a single `HttpContext.SendAsync` call at the controller.

## Installation

```
dotnet add package AxisResult.HttpResponse
```

The package brings in [`AxisResult`](../../0-Foundations/AxisResult/README.md) transitively and references the ASP.NET Core shared framework (`Microsoft.AspNetCore.App`), so add it to a **web project** (`Microsoft.NET.Sdk.Web` or a project that already targets ASP.NET Core). Everything lives in the `Axis` namespace — the same root namespace as `AxisResult` — so no extra `using` is needed beyond the one you already have.

## Rendering an `AxisResult` (no value)

For operations that produce no value (delete, verify, mark-as-read), the success rail returns a bodyless status code. `SendAsync` takes the pending result and attaches the request `traceId` from `HttpContext.TraceIdentifier` automatically — you never pass it:

```csharp
[HttpDelete("{id:guid}")]
public Task<IActionResult> Delete(Guid id)
    => HttpContext.SendAsync(personService.DeleteAsync(id));   // 200 on success
```

> Already holding a synchronous `AxisResult`? Lift it with `HttpContext.SendAsync(Task.FromResult(result))`.

## Rendering an `AxisResult<T>` (with value)

When the operation carries a value (a `Task<AxisResult<PersonResponse>>` here), success becomes an `ObjectResult` with that value serialized in the body:

```csharp
[HttpGet("{id:guid}")]
public Task<IActionResult> GetById(Guid id)
    => HttpContext.SendAsync(personService.GetByIdAsync(id));   // 200 + body
```

## Choosing the success status

The second argument sets the status used **on success**. Use `Created` for inserts, `Accepted` for queued work, and `NoContent` to discard the body entirely:

```csharp
return HttpContext.SendAsync(personService.CreateAsync(cmd), HttpStatusCode.Created);   // 201 + body
return HttpContext.SendAsync(personService.UpdateAsync(cmd), HttpStatusCode.NoContent); // 204, no body
```

> `NoContent` is special-cased: even on an `AxisResult<T>`, the value is dropped and a `NoContentResult` is returned.

## Chaining from a handler (the real shape)

In practice you hand `SendAsync` a handler call that already returns a `Task<AxisResult<T>>` and render it inline:

```csharp
[HttpPost]
public Task<IActionResult> Create(CreatePersonCommand cmd)
    => HttpContext.SendAsync(
        mediator.Cqrs.ExecuteAsync<CreatePersonCommand, CreatePersonResponse>(cmd),
        HttpStatusCode.Created);
```

**Why it pays off:** one expression, no `await` ceremony, no `try/catch`, and no `if (result.IsFailure)` branch — success becomes `201` and every failure category becomes the right status with an RFC 7807 body, automatically.

## See also

- [Convert · `HttpContext.SendAsync`](send-http-response.md) — both overloads and their behaviour
- [Error → status mapping](error-status-mapping.md) — which error becomes which status, and the `ProblemDetails` shape
- [Why AxisResult.HttpResponse?](why-axisresult-httpresponse.md) — what this replaces

---

↩ [Back to AxisResult.HttpResponse docs](README.md)
