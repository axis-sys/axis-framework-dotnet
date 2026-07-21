---
name: axis-result-httpresponse
description: >
  Render an `AxisResult` / `AxisResult<T>` to an ASP.NET Core `IActionResult` at the HTTP edge with the
  `AxisResult.HttpResponse` package (4-edge) — the `HttpContext.SendAsync` extensions, the `AxisErrorType`->HTTP
  status map, RFC 7807 `ProblemDetails` shaping, automatic `traceId` inclusion, and the success shapes (200/201/204).
  Use when writing a controller action that returns a result, when mapping an error category to a status, or
  when reusing the same rendering from middleware / an authorization filter outside MVC. This skill is a MAP:
  each row points to the canonical rule in `rules/` — open only the one the context asks for. It does NOT
  restate invariants nor carry code. It does NOT cover the result monad itself (-> axis-result), the mediator
  dispatch a controller calls (-> axis-mediator), nor controller conventions like routing/auth/OpenAPI
  (-> axis-webapi-controllers).
---

# AxisResult.HttpResponse — rule map (the HTTP edge)

`AxisResult.HttpResponse` is the **driving/edge** package that collapses a value-based `AxisResult` into an
ASP.NET Core response. Application code returns results everywhere; a single `HttpContext.SendAsync` call at the
controller turns the final result into a status (with or without a body) on success, or an RFC 7807
`ProblemDetails` on failure — with the status taken from the most severe `AxisErrorType`, internal errors
suppressed and the request `traceId` always attached automatically from `HttpContext.TraceIdentifier`.
The same failure rendering is reusable outside MVC
(middleware, authorization filters) through `AxisProblemDetailsBuilder`.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/4-edge/axis-result-httpresponse/`; open **only** the rule
the context requires.

## Rule map

### Start here — route by intent ⭐

| Context / what you were about to write | Rule |
|---|---|
| Return an `AxisResult` from a controller action (success status or value body, failure `ProblemDetails`) | [httpresponse-send-success-shapes](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-send-success-shapes.yaml) |
| Map an error category to an HTTP status by hand | [httpresponse-error-type-status-map](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-error-type-status-map.yaml) — don't; the package owns the map |
| Render an `AxisError` list from middleware / an `IAsyncAuthorizationFilter` (no `IActionResult`) | [httpresponse-problem-details-builder](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-problem-details-builder.yaml) |

### Render at the controller — `HttpContext.SendAsync`

| Context | Rule |
|---|---|
| Success shapes across both overloads, failure delegation, automatic `traceId` injection, the `statusCode` rail | [httpresponse-send-success-shapes](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-send-success-shapes.yaml) |
| Gotcha — `NoContent` (204) drops the value even on `AxisResult<T>`; any other status keeps the body | [httpresponse-no-content-drops-value](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-no-content-drops-value.yaml) |

### Failure → `ProblemDetails`

| Context | Rule |
|---|---|
| How a failure becomes a `ProblemDetails` (severity pick, internal suppression, `traceId`, `type` kebab, empty-errors fallback) and its reuse outside MVC | [httpresponse-problem-details-builder](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-problem-details-builder.yaml) |

### The `AxisErrorType` tables

| Context | Rule |
|---|---|
| Per-type HTTP status (400/401/403/404/409/422/429/500/503/504), Timeout≡GatewayTimeout→504, unmapped→500 | [httpresponse-error-type-status-map](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-error-type-status-map.yaml) |
| The severity ranking that picks the winner on a multi-error failure (InternalServerError highest) | [httpresponse-error-type-severity-ranking](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-error-type-severity-ranking.yaml) |
| The `ProblemDetails` `title` — standard reason phrase for the status, else `"Error"` | [httpresponse-problem-title-reason-phrase](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-problem-title-reason-phrase.yaml) |

### Configuration & wiring

| Context | Rule |
|---|---|
| The `type` base URI — startup-configured static, `https://axis.dev/problems/` default, ignore-blank, trailing-slash | [httpresponse-problem-type-base-uri-config](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-problem-type-base-uri-config.yaml) |
| `AddAxisResultHttpResponse` — applies the base URI from `AxisResult:Http:ProblemTypeBaseUri`, returns services unchanged | [httpresponse-registration-reads-config-key](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-registration-reads-config-key.yaml) |

## See also

- `axis-result` — the monad that flows through every handler, port and facade to reach this edge; `HttpContext.SendAsync` is its terminal step. `AxisError` and the 12 `AxisErrorType` categories come from here.
- `axis-webapi-controllers` — the controller that hosts the `HttpContext.SendAsync` call; routing, versioning, auth and OpenAPI are application conventions, not imposed by this package.
- `axis-mediator` — the dispatch whose `Task` a controller hands to `HttpContext.SendAsync`; the `traceId` is read from `HttpContext.TraceIdentifier` automatically, never passed by the caller.
- `axis-rules` — how these rules are authored and maintained (the extraction method from code and tests).
- `axis-dotnet-architect` — the hub; this is the driving-side edge adapter of the hexagon.
