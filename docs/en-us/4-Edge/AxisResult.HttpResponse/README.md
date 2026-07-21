# AxisResult.HttpResponse — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/4-Edge/AxisResult.HttpResponse/README.md)

**The HTTP edge for [AxisResult](../../0-Foundations/AxisResult/README.md)** — one extension member on `HttpContext` turns an `AxisResult` / `AxisResult<T>` into an ASP.NET Core `IActionResult`, mapping every `AxisErrorType` to the right status code and rendering failures as RFC 7807 `ProblemDetails` (with the request `traceId` and internal errors suppressed).

```csharp
// In a controller — one line maps success and every failure category
[HttpPost]
public Task<IActionResult> Create(CreatePersonCommand cmd)
    => HttpContext.SendAsync(
        mediator.Cqrs.ExecuteAsync<CreatePersonCommand, CreatePersonResponse>(cmd),
        HttpStatusCode.Created);
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail you need.

---

## The trunk (read first)

### From `AxisResult` to HTTP in 60 seconds

Your handlers return an `AxisResult` / `AxisResult<T>`. The controller's only job is to render it. This package is that renderer:

```
AxisResult ──success──▶ StatusCodeResult (200, or your status)
                                         │
AxisResult<T> ─success─▶ ObjectResult { Value, status }  (204 → NoContentResult)
                                         │
either ───────failure──▶ ObjectResult { ProblemDetails }  (status from the error)
```

A single call, `HttpContext.SendAsync`, awaits your handler's task and collapses both rails into the correct HTTP response. → **[Convert · `HttpContext.SendAsync`](send-http-response.md)**

### Success vs. failure

- **Success, no value** (`AxisResult`) → `StatusCodeResult` with `statusCode` (default `200 OK`).
- **Success, with value** (`AxisResult<T>`) → `ObjectResult` carrying `Value`; `HttpStatusCode.NoContent` yields a bodyless `204`.
- **Failure** → an `ObjectResult` wrapping a `ProblemDetails`, whose status is chosen from the **most severe** error in the list.

### Error → status, the short version

Each `AxisErrorType` maps to a status code; on a multi-error failure the highest-severity error wins, `InternalServerError` is never leaked into the payload, and the `traceId` always travels in `extensions`. → **[Error → status mapping](error-status-mapping.md)**

### Installation

```
dotnet add package AxisResult.HttpResponse
```

Depends on [`AxisResult`](../../0-Foundations/AxisResult/README.md) and targets ASP.NET Core (`FrameworkReference Microsoft.AspNetCore.App`) — add it to a web project. → Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Convert · `HttpContext.SendAsync`** ⭐ | turn an `AxisResult` into an `IActionResult` | [send-http-response.md](send-http-response.md) |
| **Reuse outside MVC · `AxisProblemDetailsBuilder`** | render the same `ProblemDetails` rule from middleware/filters, with no `IActionResult` | [problem-details-builder.md](problem-details-builder.md) |
| **Mapping · `AxisErrorType` → status** | know which error becomes which status code | [error-status-mapping.md](error-status-mapping.md) |
| **Why?** | the case for a dedicated edge package | [why-axisresult-httpresponse.md](why-axisresult-httpresponse.md) |
| **Reference** | every extension at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [Convert · `HttpContext.SendAsync`](send-http-response.md) · [Why AxisResult.HttpResponse?](why-axisresult-httpresponse.md)

**Fundamentals:** [Error → status mapping](error-status-mapping.md) · [`ProblemDetails` shape](error-status-mapping.md#the-problemdetails-shape-rfc-7807)

**Reference & extras:** [API reference](api-reference.md)

---

## Design principles

1. **One line at the controller.** Success and every failure category collapse into a single `HttpContext.SendAsync` call — no `try/catch`, no branch you can forget.
2. **The error type chooses the status.** Status codes are derived from `AxisErrorType`, not hand-written per endpoint, so the mapping is consistent across the whole API.
3. **Most-severe wins.** A failure with many errors returns the status of the gravest one — a `ValidationRule` next to an `InternalServerError` is a `500`, not a `400`.
4. **Never leak internals.** `InternalServerError` entries are counted but stripped from the payload; clients see *that* something failed, not *what*.
5. **Always traceable.** The request `traceId` is taken from `HttpContext.TraceIdentifier` automatically — never passed by hand — and always rides along in `ProblemDetails.Extensions`, so a client report ties straight back to your logs.

---

## License

Apache 2.0
