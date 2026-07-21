---
name: axis-webapi-controllers
description: >
  Create or change an HTTP endpoint at the Axis edge — a sealed `ControllerBase` controller in the
  host that renders `AxisResult` to `IActionResult`, plus the class attribute stack, URL
  versioning, auth/tenancy and the saga 202+polling pattern. Use when adding or altering a route,
  standing up a controller for a new aggregate, versioning an endpoint, or exposing a use case/saga at
  the edge. This skill is a MAP: each row points to the canonical rule in `rules/` — open only the one
  the context asks for. It does NOT restate the shape nor carry code. GATE: a new or changed controller
  REQUIRES E2E coverage (→ testing-e2e-controller-coverage-gate). It does NOT cover the framework render
  package (→ axis-result-httpresponse), the use case exposed (→ axis-use-case-cqrs), the railway that
  reaches the edge (→ axis-result), nor the saga orchestration (→ axis-saga).
---

# AxisWebApi Controllers — rule map (the HTTP edge)

A **controller** is the driving edge of a bounded context: a sealed `ControllerBase` class living in the
single shared host (one controller folder per bounded context — a separate BFF appears only when a BC is
promoted to a microservice) that injects
a driving facade, hands each action's `AxisResult` to the framework render extension, and carries the
canonical class attribute stack — API-controller marker, bounded-context tag, authorization, optional
tenant marker, API version, versioned route. Authentication, URL versioning and OpenAPI are conventions
the edge **converges on**, not behaviours imposed by any `Axis*` package.

This skill **does not restate** the shape nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/conventions/`; open **only** the rule the context requires.

> **GATE — E2E is mandatory.** A new or changed controller is not done without its end-to-end journey:
> the happy path plus an anonymous 401, and a 403 (with the privileged bypass) wherever a permission
> applies. See
> [testing-e2e-controller-coverage-gate](../../rules/conventions/testing/testing-e2e-controller-coverage-gate.yaml).

## Rule map

### Start here — the controller shape ⭐

| Context / what you were about to write | Rule |
|---|---|
| Create the controller — sealed `ControllerBase` (never the view-aware `Controller`), primary constructor, the fixed class attribute stack, one expression-bodied `{Verb}[{Noun}]Async` action per use case, one folder per BC | [edge-controller-shape](../../rules/conventions/edge/edge-controller-shape.yaml) |
| What a controller may inject — a driving `I{Entities}Facade`, never the mediator, a port, a repository or an application service | [edge-controller-facade-injection](../../rules/conventions/edge/edge-controller-facade-injection.yaml) |
| The facade the controller depends on — the one public doorway into the BC's use cases (interface at `{BC}/{Aggregate}/v1/` in `{App}.Contracts.Driving`; `internal sealed` impl in the dedicated `{App}.Adapters.Driving.Facade` project, one dispatch expression per method) | [architecture-facade-pattern](../../rules/conventions/architecture/architecture-facade-pattern.yaml) |
| Where the controller is hosted — controllers live in the host project, one folder per BC; one shared composition root wires the monolith, and a BFF only appears when a BC is promoted to a microservice | [architecture-composition-root](../../rules/conventions/architecture/architecture-composition-root.yaml) |

### Rendering AxisResult at the edge

| Context | Rule |
|---|---|
| Terminate every action by handing its result `Task` to `HttpContext.SendAsync` (trace id injected automatically; second arg for non-200 success); never inspect `IsSuccess`/`Match` or hand-build `Ok`/`Problem` in a controller | [edge-axisresult-render](../../rules/conventions/edge/edge-axisresult-render.yaml) |

### Versioning & OpenAPI

| Context | Rule |
|---|---|
| Version every business route in a URL path segment (`api/v{version}/{resource}`); suffix the controller class with the version it serves so a later version coexists file-by-file | [edge-url-versioning](../../rules/conventions/edge/edge-url-versioning.yaml) |
| Make the rendered docs UI the project's documentation hub — info / security / per-operation transformers over the OpenAPI document, exposed only in development | [edge-openapi-doc-hub](../../rules/conventions/edge/edge-openapi-doc-hub.yaml) |
| Carry full `///` XML docs on controllers and actions (`<summary>`, `<param>`, `<response code>`) so they feed the generated OpenAPI document | [edge-xml-docs-feed-openapi](../../rules/conventions/edge/edge-xml-docs-feed-openapi.yaml) |
| Where XML docs are allowed at all under the minimal-comment policy — only on the driving DTOs (commands, queries, responses) that render into OpenAPI | [process-xml-doc-policy](../../rules/conventions/process/process-xml-doc-policy.yaml) |

### Auth & tenancy

| Context | Rule |
|---|---|
| Named authentication schemes — one validates the external IdP's tokens (token-exchange endpoint only), the default validates the API's own thin session token; ordinary protected endpoints use the default | [edge-auth-schemes](../../rules/conventions/edge/edge-auth-schemes.yaml) |
| Granular permissions — a `module.action` permission attribute (constants, no literals) plus one global filter that resolves grants through the authorization facade (401/403 as ProblemDetails) | [edge-permission-authorization](../../rules/conventions/edge/edge-permission-authorization.yaml) |
| The gate lives here, not in handlers — the edge enforces identity/permissions before the mediator, so a use-case handler never re-authorizes; the exception is a handler that IS the authentication mechanism (token/refresh) or a genuine domain permission decision the edge cannot make | [architecture-handler-no-authorization](../../rules/conventions/architecture/architecture-handler-no-authorization.yaml) |
| Multi-tenant scoping — a tenant marker attribute plus middleware that reads the header, validates membership and publishes the tenant through the ambient accessor (single-tenant apps skip this) | [edge-tenant-scoping-middleware](../../rules/conventions/edge/edge-tenant-scoping-middleware.yaml) |

### Saga endpoints (202 + polling)

| Context | Rule |
|---|---|
| Expose a cross-BC use case as a saga — start POST answers 202 Accepted with the saga id, a GET on the run resource polls status until terminal, cancel is a POST on the same run resource; single-BC endpoints stay synchronous 200/201 | [edge-saga-endpoints](../../rules/conventions/edge/edge-saga-endpoints.yaml) |

### The E2E gate (mandatory)

| Context | Rule |
|---|---|
| **The gate** — every new/changed controller needs an E2E journey through the full production pipeline (a real database container where the app has one; the in-memory driven adapter otherwise): happy path + anonymous 401, and 403 (plus the admin bypass) where permissions apply; a reflective test proves every controller activates from the production container | [testing-e2e-controller-coverage-gate](../../rules/conventions/testing/testing-e2e-controller-coverage-gate.yaml) |
| Testing a saga-backed endpoint from the outside — assert 202 + pending body, poll via a bounded helper to a terminal status, deserialize into the production driving-contract records | [testing-e2e-saga-edge-202-polling](../../rules/conventions/testing/testing-e2e-saga-edge-202-polling.yaml) |

## See also

- [`axis-result-httpresponse`](../axis-result-httpresponse/SKILL.md) — the framework package that maps `AxisErrorType` to an HTTP status and renders `AxisResult` → `IActionResult` (`HttpContext.SendAsync`, `AxisProblemDetailsBuilder`). The edge owns zero mapping code.
- `axis-use-case-cqrs` — the vertical slice a controller exposes, and the home of the `I{Entities}Facade` the controller injects.
- `axis-result` — the railway that flows from handler to the HTTP edge; the controller is where it terminates.
- `axis-saga` — the orchestration behind a 202 + polling endpoint (the edge governs only its HTTP surface).
- `axis-e2e-tests` — how to write the E2E journey the gate above requires.
- `axis-dotnet-architect` — the plugin hub; cross-cutting inbreakable rules and where the edge fits the hexagon.
