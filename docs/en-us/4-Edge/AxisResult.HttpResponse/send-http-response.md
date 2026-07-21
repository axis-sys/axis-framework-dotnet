# Convert · `HttpContext.SendAsync`

> The single entry point of the package: it awaits a `Task<AxisResult>` / `Task<AxisResult<T>>` and collapses the result into an ASP.NET Core `IActionResult` — success as a status (with or without a body), failure as RFC 7807 `ProblemDetails`.

```csharp
[HttpGet("{id:guid}")]
public Task<IActionResult> GetById(Guid id)
    => HttpContext.SendAsync(
        mediator.Cqrs.QueryAsync<GetPersonByIdQuery, PersonResponse>(new(id))); // 200 + body, or ProblemDetails
```

---

## When to use

At the **HTTP edge** — the controller action — when your application code already speaks `AxisResult`. It is the last step of the pipeline: everything above returns results, and this turns the final result into a response.

## When *not* to use

| You want to… | Use instead |
|---|---|
| collapse a result into a non-HTTP value (a DTO, a message, a CLI exit code) | [`Match`](../../0-Foundations/AxisResult/match.md) |
| build a fully custom `ProblemDetails` body (extra members, custom `type` URIs per endpoint) | `Match` + your own `Problem(...)` |
| keep mapping logic inside the domain/application layer | nothing here — this package is edge-only |

---

## Available overloads

Both overloads are extension members on `HttpContext` (static class `AxisHttpContextExtensions`): they take the result task and an optional `statusCode` (default `HttpStatusCode.OK`), `await` the task and render the result. The request `traceId` is injected automatically from `HttpContext.TraceIdentifier` — you never pass it. An `ArgumentNullException` is thrown if the context or the task is `null`.

| Overload | Signature |
|---|---|
| `Task<AxisResult>` → response | `Task<IActionResult> SendAsync(Task<AxisResult> resultTask, HttpStatusCode statusCode = OK)` |
| `Task<AxisResult<T>>` → response | `Task<IActionResult> SendAsync<TData>(Task<AxisResult<TData>> resultTask, HttpStatusCode statusCode = OK)` |

### What each case returns

| Result | `statusCode` | Returned `IActionResult` |
|---|---|---|
| `AxisResult` success | any | `StatusCodeResult(statusCode)` — no body |
| `AxisResult<T>` success | `NoContent` | `NoContentResult` — value dropped |
| `AxisResult<T>` success | any other | `ObjectResult(Value) { StatusCode }` |
| any failure | — | `ObjectResult(ProblemDetails) { StatusCode }` — status from the most severe error |

The `statusCode` is used **only** on the success rail; on failure the status is derived from the errors. See [Error → status mapping](error-status-mapping.md) for how that status and body are built.

---

## Real-world examples

### 1. Create — `201 Created` with body

```csharp
[HttpPost]
public Task<IActionResult> Create(CreatePersonCommand cmd)
    => HttpContext.SendAsync(
        mediator.Cqrs.ExecuteAsync<CreatePersonCommand, CreatePersonResponse>(cmd),
        HttpStatusCode.Created);
```

**Why it pays off:** the happy path is `201` with the created resource, and any validation/conflict/server error becomes the correct status and `ProblemDetails` — in the same line, with no branching.

### 2. Update — `204 No Content`

```csharp
[HttpPut("{id:guid}")]
public Task<IActionResult> Update(Guid id, UpdatePersonCommand cmd)
    => HttpContext.SendAsync(
        mediator.Cqrs.ExecuteAsync<UpdatePersonCommand, UpdatePersonResponse>(cmd with { PersonId = id }),
        HttpStatusCode.NoContent);
```

**Why it pays off:** even though the handler returns a value, `NoContent` drops it and answers `204` — you express the HTTP intent at the edge without changing the handler's contract.

### 3. Get — success `200`, missing `404`

```csharp
[HttpGet("{id:guid}")]
public Task<IActionResult> GetById(Guid id)
    => HttpContext.SendAsync(mediator.Cqrs.QueryAsync<GetPersonByIdQuery, PersonResponse>(new(id)));
```

If the handler returns `AxisError.NotFound("PERSON_NOT_FOUND")`, the response is `404` with:

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

**Why it pays off:** the `NotFound` *category* — decided in the domain — becomes a `404` at the edge, with a machine-readable `code` the client can branch on and a `traceId` that ties back to your logs.

### 4. Synchronous result

```csharp
[HttpDelete("{id:guid}")]
public Task<IActionResult> Delete(Guid id)
{
    AxisResult result = personService.Delete(id);
    return HttpContext.SendAsync(Task.FromResult(result));
}
```

**Why it pays off:** when there is no `Task` to await, `Task.FromResult` lifts the result onto the same rail — the wrapping is the only difference, the rendering is identical.

---

## See also

- [Error → status mapping](error-status-mapping.md) — the `AxisErrorType` → status table, severity selection and the `ProblemDetails` shape
- [Getting started](getting-started.md) — installation and the minimal lifecycle
- [API reference](api-reference.md) — the two overloads at a glance
- [`Match`](../../0-Foundations/AxisResult/match.md) — collapse a result into any non-HTTP value

---

↩ [Back to AxisResult.HttpResponse docs](README.md)
