# API reference

> The complete public surface, grouped by responsibility. Use it for lookup; for the rationale, follow the arrow links into the detail pages.

The bulk of the public API is the static class `AxisHttpContextExtensions` (namespace `Axis`), plus `AxisProblemDetailsBuilder` (the same rendering, decoupled from `IActionResult`), a small DI registration and a startup-configuration class covered below. The error→status mapping (`AxisErrorTypeHttpMapping`) is `internal` and is documented, not exposed.

## HTTP response extensions

Both methods are extension members on `HttpContext` (declared in `AxisHttpContextExtensions`), called as `HttpContext.SendAsync(...)`. Each throws `ArgumentNullException` when the context or the task is `null`.

| Method | Signature | Description |
|---|---|---|
| `SendAsync` | `Task<IActionResult> SendAsync(Task<AxisResult> resultTask, HttpStatusCode statusCode = OK)` | Awaits the task, then renders the valueless result: `StatusCodeResult(statusCode)` on success, `ProblemDetails` on failure. |
| `SendAsync<TData>` | `Task<IActionResult> SendAsync<TData>(Task<AxisResult<TData>> resultTask, HttpStatusCode statusCode = OK)` | Awaits the task, then renders the result with a value: `ObjectResult(Value)` (or `NoContentResult` when `statusCode` is `NoContent`) on success, `ProblemDetails` on failure. |

**Parameters**

- `resultTask` — the pending result to render. For a result you already hold synchronously, pass `Task.FromResult(result)`.
- `statusCode` — the status used **only** on the success rail (default `HttpStatusCode.OK`); `HttpStatusCode.NoContent` produces a bodyless `204`. On failure the status is derived from the errors.

The correlation id is not a parameter: it is taken from `HttpContext.TraceIdentifier` automatically and surfaced in `ProblemDetails.Extensions["traceId"]`.

→ [Convert · `HttpContext.SendAsync`](send-http-response.md)

## Reuse outside MVC · `AxisProblemDetailsBuilder`

For middleware and `IAsyncAuthorizationFilter` — code that runs before or beside MVC and has no `ActionContext` to return an `IActionResult` through. The builder computes; the extensions on `HttpContext` consume, and each is named for its effect.

| Member | Signature | Description |
|---|---|---|
| `AxisProblemDetailsBuilder.Build` | `(int StatusCode, ProblemDetails Details) Build(IReadOnlyList<AxisError> errors, string traceId)` | Pure computation of the status/`ProblemDetails` pair; no `HttpContext` dependency. `SendAsync`'s failure rendering delegates to this. |
| `WriteProblemDetailsAsync` | `Task WriteProblemDetailsAsync(this HttpContext context, AxisError error)` | Single-error convenience overload of the one below. |
| `WriteProblemDetailsAsync` | `Task WriteProblemDetailsAsync(this HttpContext context, IReadOnlyList<AxisError> errors)` | Calls `Build`, then **writes** `StatusCode` and the JSON body straight to `context.Response`. For middleware. |
| `ToProblemDetailsResult` | `ObjectResult ToProblemDetailsResult(this HttpContext context, AxisError error)` | Single-error convenience overload of the one below. |
| `ToProblemDetailsResult` | `ObjectResult ToProblemDetailsResult(this HttpContext context, IReadOnlyList<AxisError> errors)` | Calls `Build`, then wraps it in an `ObjectResult` **without touching the response**. For an `IAsyncAuthorizationFilter`, which assigns `context.Result`. |

The extensions take the `traceId` from `HttpContext.TraceIdentifier`. There is deliberately no `BuildAsync`: `Build` and `BuildAsync` would read as a sync/async pair of the same operation, but the write path performs I/O and returns nothing.

→ [Reuse outside MVC · `AxisProblemDetailsBuilder`](problem-details-builder.md)

## Failure rendering (internal behaviour)

Not part of the public API, but useful to know — these rules are applied when a result is a failure:

| Aspect | Behaviour |
|---|---|
| Status code | from the **most severe** `AxisErrorType` in the error list |
| `type` | `{ProblemTypeBaseUri}<kebab-case-of-type>` — `https://axis.dev/problems/` unless overridden, see below |
| `title` | standard reason phrase for the status code |
| `detail` | `"{visible} error(s) returned. {internal} internal error(s) suppressed."` |
| `errors` extension | visible errors only, each `{ code, type }`; `InternalServerError` stripped |
| `traceId` extension | always the request `traceId` (`HttpContext.TraceIdentifier`) |
| no-error fallback | `500` with `detail = "Failure without errors."` |

→ [Error → status mapping](error-status-mapping.md)

## Configuring the `type` base URI

| Member | Signature | Description |
|---|---|---|
| `AxisProblemDetailsConfiguration.ProblemTypeBaseUri` | `static string ProblemTypeBaseUri { get; }` | current base URI, defaults to `AxisProblemDetailsConfiguration.DefaultProblemTypeBaseUri` (`"https://axis.dev/problems/"`) |
| `AxisProblemDetailsConfiguration.ConfigureProblemTypeBaseUri` | `static void ConfigureProblemTypeBaseUri(string? baseUri)` | overrides the base URI once at startup; a null/blank value is ignored, a trailing slash is added when missing |
| `AddAxisResultHttpResponse` | `IServiceCollection AddAxisResultHttpResponse(this IServiceCollection services, IConfiguration configuration)` | reads the `AxisResult:Http:ProblemTypeBaseUri` config key and calls `ConfigureProblemTypeBaseUri` with it; a no-op when the key is absent or blank |

```csharp
// appsettings.json: { "AxisResult": { "Http": { "ProblemTypeBaseUri": "https://problems.example.test/" } } }
builder.Services.AddAxisResultHttpResponse(builder.Configuration);
```

## See also

- [Convert · `HttpContext.SendAsync`](send-http-response.md) — overloads and examples
- [Reuse outside MVC · `AxisProblemDetailsBuilder`](problem-details-builder.md) — the same rendering for middleware/filters
- [Error → status mapping](error-status-mapping.md) — the mapping table, severity and `ProblemDetails` shape
- [Getting started](getting-started.md) — installation and minimal usage

---

↩ [Back to AxisResult.HttpResponse docs](README.md)
