# Error → status mapping · `AxisErrorType`

> A failure never has to be translated by hand. Each `AxisErrorType` has one canonical HTTP status code; the package picks the status of the **most severe** error and renders the whole failure as RFC 7807 `ProblemDetails`.

```csharp
AxisResult<PersonResponse> result = AxisError.NotFound("PERSON_NOT_FOUND");
return HttpContext.SendAsync(Task.FromResult(result));   // → 404 + ProblemDetails
```

---

## The mapping

| `AxisErrorType` | HTTP status |
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

Any unmapped type falls back to `500`. The table mirrors the 12 `AxisErrorType` categories described in [AxisResult · Errors and types](../../0-Foundations/AxisResult/errors-and-types.md).

---

## Most-severe wins (multi-error failures)

A failure can carry **many** errors. The status code is taken from the error with the highest **severity**, not the first one in the list:

| `AxisErrorType` | Severity |
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
    AxisError.ValidationRule("NAME_REQUIRED"),    // severity 40
    AxisError.InternalServerError("DB_TIMEOUT")); // severity 100  ← wins

return HttpContext.SendAsync(Task.FromResult(result));   // → 500, not 400
```

**Why it pays off:** a request that tripped both a validation rule and a server fault is reported as a server fault — the client retries or escalates correctly instead of "fixing" input that was never the real problem.

---

## The `ProblemDetails` shape (RFC 7807)

Every failure renders as a standard `ProblemDetails`, plus two `Extensions`: the request `traceId` and a list of the **visible** errors.

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

- **`type`** — `https://axis.dev/problems/<kebab-case>` by default, where the slug is the most-severe `AxisErrorType` (`ValidationRule` → `validation-rule`). The base URI is configurable at startup — see [`AxisProblemDetailsConfiguration`](api-reference.md#configuring-the-type-base-uri).
- **`title`** — the standard reason phrase for the status (`"Bad Request"`, `"Not Found"`, …).
- **`status`** — the chosen status code, also set on the `ObjectResult`.
- **`detail`** — a count summary: how many errors are surfaced and how many internal ones were suppressed.
- **`traceId`** / **`errors`** — carried in `ProblemDetails.Extensions`, so they serialize as top-level members alongside the RFC fields.

---

## Internal errors are suppressed

`InternalServerError` entries never appear in the `errors` array. They are **counted** in `detail` but their `code` is stripped, so implementation details (a failing driver, a dependency name) never reach the client:

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

**Why it pays off:** the client learns *that* the server failed (and gets a `traceId` to report), but never *what* failed — no internal vocabulary leaks across the edge.

## The `traceId` is mandatory

`traceId` is not an optional extra — and it is not something you pass. `SendAsync` reads it from `HttpContext.TraceIdentifier` automatically, and it always rides along in the response, tying a client's report straight back to your logs.

## Defensive fallback

If a failed result somehow reaches the edge with **no** errors in its list, the package answers `500` with `detail = "Failure without errors."` rather than throwing — a belt-and-suspenders guard that should never trigger through the public API.

---

## See also

- [Convert · `HttpContext.SendAsync`](send-http-response.md) — the extension that produces these responses
- [AxisResult · Errors and types](../../0-Foundations/AxisResult/errors-and-types.md) — where `AxisError` and the 12 categories come from
- [API reference](api-reference.md) — the two overloads at a glance

---

↩ [Back to AxisResult.HttpResponse docs](README.md)
