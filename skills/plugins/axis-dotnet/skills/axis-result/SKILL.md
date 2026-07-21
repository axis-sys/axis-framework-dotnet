---
name: axis-result
description: >
  Railway-Oriented Programming in C# with AxisResult — the Axis Result monad: errors as values, monadic
  composition, async/ValueTask. Use when writing, reviewing or refactoring code that returns `AxisResult` /
  `AxisResult<T>`: creation, chaining (Then/Map/Ensure), recovery (Recover/OrElse), combination
  (Zip/Combine/All), typed errors (`AxisError` + 12 categories), exception boundary (Try/TryBind), terminal
  Match, transient failure (IsTransientFailure), logging (LogIfFailure/LogIfSuccess) and Task vs ValueTask.
  It ALSO covers the HTTP edge: `HttpContext.SendAsync` (AxisResult → IActionResult) and `AxisProblemDetailsBuilder`
  (RFC 7807 ProblemDetails outside MVC, in middleware and auth filters). It is the TRACK through almost the
  whole Axis backend. This skill is a MAP: each item points to the canonical rule in rules/ — open only the
  one the context asks for. It does NOT cover the mediator (→ axis-mediator), validation (→ axis-validator)
  nor controller conventions (→ axis-webapi-controllers).
---

# AxisResult — rule map (ROP + HTTP edge)

`AxisResult` is a *Result monad* for C# (Railway-Oriented Programming): errors are **values**, composition
over ceremony. It is the **success/failure track** that runs through almost the whole Axis backend — from
the repository to the HTTP response.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/`; the AI opens **only** the rule the context requires.
Each rule brings its invariants and points to the operator doc (`derives_to.docs`), where the runnable code
example lives. Start from the discovery rule ⭐ whenever you are about to write imperative control
(`if/else`, `try/catch`, `.Value`, null-check) over a result.

> **Scope: the whole track, a single map.** This skill covers the three packages the track runs through:
> `AxisResult` (0-foundations, the monad), `AxisLogger` (1-observability, only what combines a result with
> logging) and `AxisResult.HttpResponse` (4-edge, where the track becomes an HTTP response). The **controller
> conventions** — attributes, URL versioning, OpenAPI/Scalar, the E2E gate — are not part of this track and
> live in `axis-webapi-controllers`.

> **Decoupled from the mediator.** `AxisResult` does **not** depend on `IAxisMediator` — the lib works on its
> own in any project. The mediator is the Axis pattern's **recommendation** (when it fits), not a requirement
> of the lib. See `axis-mediator`.

## Rule map

### Start here — discovery ⭐

| Context / what you were about to write | Rule |
|---|---|
| About to use `if/else`, `try/catch`, a raw `.Value` or a null-check on a result | [result-operator-selection](../../rules/framework/0-foundations/axis-result/result-operator-selection.yaml) — routes the smell to the canonical operator |
| Branch flow on `IsSuccess`/`IsFailure` | [result-no-if-else-flow](../../rules/framework/0-foundations/axis-result/result-no-if-else-flow.yaml) — compose with operators, not with a branch |
| Throw an exception to signal an expected failure | [result-no-throw](../../rules/framework/0-foundations/axis-result/result-no-throw.yaml) — failure is a value, return `AxisResult` |
| Build `ProblemDetails`/HTTP status by hand from errors | [httpresponse-problem-details-builder](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-problem-details-builder.yaml) — the edge already has the rule; don't reimplement |

### Fundamentals

| Context | Rule |
|---|---|
| Model an error (Code + Type, no Message) | [result-errors-as-values](../../rules/framework/0-foundations/axis-result/result-errors-as-values.yaml) |
| 12 typed categories, HTTP map, `IsTransient` | [result-error-typing](../../rules/framework/0-foundations/axis-result/result-error-typing.yaml) |
| Access the value safely (`.Value` throws) | [result-value-access-safety](../../rules/framework/0-foundations/axis-result/result-value-access-safety.yaml) |
| Deconstruct a result (`var (ok, value, _) = …`) mid-pipeline | [result-deconstruct-terminal-only](../../rules/framework/0-foundations/axis-result/result-deconstruct-terminal-only.yaml) — deconstruction is terminal-only; on the rail, chain with `Then` |
| Enter the track (`.Rop()`, implicit conversions, `AsTaskAsync`) | [result-pipeline-entry](../../rules/framework/0-foundations/axis-result/result-pipeline-entry.yaml) |

### Compose

| Context | Rule |
|---|---|
| Transform a value that **cannot** fail | [result-map-cannot-fail](../../rules/framework/0-foundations/axis-result/result-map-cannot-fail.yaml) |
| Chain a fallible step (valueless Then **preserves** the value) | [result-then-value-preservation](../../rules/framework/0-foundations/axis-result/result-then-value-preservation.yaml) |
| Inline business invariant over the value | [result-ensure-invariants](../../rules/framework/0-foundations/axis-result/result-ensure-invariants.yaml) |
| Skip a fallible step when the desired state already holds (success guard) | [result-thenunless-success-guard](../../rules/framework/0-foundations/axis-result/result-thenunless-success-guard.yaml) |
| Run a same-type transforming step only when a condition on the value holds | [result-thenwhen-conditional-step](../../rules/framework/0-foundations/axis-result/result-thenwhen-conditional-step.yaml) |
| Side effect on success/failure, without changing the track | [result-tap-side-effects](../../rules/framework/0-foundations/axis-result/result-tap-side-effects.yaml) |
| Collapse the pipeline into a final value (terminal) | [result-match-terminal](../../rules/framework/0-foundations/axis-result/result-match-terminal.yaml) |

### Combine

| Context | Rule |
|---|---|
| 2–4 **different** values into a tuple | [result-zip-heterogeneous](../../rules/framework/0-foundations/axis-result/result-zip-heterogeneous.yaml) |
| On a tuple track: about to write `.Value1`/`.Value2`, or a side effect mid-tuple | [result-zip-heterogeneous](../../rules/framework/0-foundations/axis-result/result-zip-heterogeneous.yaml) — spread overloads (`ThenAsync`/`ZipAsync`/`MapAsync` with one parameter per element); a 1-param lambda **nests** the tuple |
| Independent side concurrently | [result-zip-parallel](../../rules/framework/0-foundations/axis-result/result-zip-parallel.yaml) |
| N results, collecting **all** errors | [result-combine-all](../../rules/framework/0-foundations/axis-result/result-combine-all.yaml) |

### Recover / errors

| Context | Rule |
|---|---|
| Recover on purpose (ALL vs ANY nuance on NotFound) | [result-recover-discipline](../../rules/framework/0-foundations/axis-result/result-recover-discipline.yaml) |
| Rewrite errors when crossing a boundary | [result-maperror](../../rules/framework/0-foundations/axis-result/result-maperror.yaml) |
| Existence: `RequireNotFound` / `OrElse`-on-NotFound | [result-notfound-idempotency](../../rules/framework/0-foundations/axis-result/result-notfound-idempotency.yaml) |
| Converge *found* + *NotFound* into a **new type** (not Recover) | [result-else-notfound](../../rules/framework/0-foundations/axis-result/result-else-notfound.yaml) |
| Decide whether the failure deserves a retry (poll loop, worker) | [result-transient-failure-classification](../../rules/framework/0-foundations/axis-result/result-transient-failure-classification.yaml) — `IsTransientFailure`; sanctioned exception to no-if-else |

### Observe (AxisLogger)

| Context | Rule |
|---|---|
| Log a result's outcome without `if (result.IsFailure)` | [logger-result-log-if](../../rules/framework/1-observability/axis-logger/logger-result-log-if.yaml) — `LogIfFailure`/`LogIfSuccess` compose in the chain |

### Exception boundary

| Context | Rule |
|---|---|
| `try/catch` only in infra, with a typed handler | [result-try-boundary](../../rules/framework/0-foundations/axis-result/result-try-boundary.yaml) |
| Edge operation that already returns `AxisResult` (flatten) | [result-trybind-flatten](../../rules/framework/0-foundations/axis-result/result-trybind-flatten.yaml) |

### Async

| Context | Rule |
|---|---|
| `Task` by default; `ValueTask` only on a measured hot path | [result-task-by-default](../../rules/framework/0-foundations/axis-result/result-task-by-default.yaml) |
| `CancellationToken` on the CT-aware overloads | [result-cancellation](../../rules/framework/0-foundations/axis-result/result-cancellation.yaml) |
| LINQ query syntax (optional) | [result-linq-query-syntax](../../rules/framework/0-foundations/axis-result/result-linq-query-syntax.yaml) |

## HTTP edge — `AxisResult.HttpResponse` (4-edge)

Where the track ends: the final `AxisResult` becomes status + body. The rule is **the same** for every
call-site — controller, middleware or authorization filter: the status of the **most severe error**,
`InternalServerError` counted but never exposed, `traceId` always present. What changes is only **who
consumes it**, and each member is named by its effect:

| You are in… | Call | Effect |
|---|---|---|
| controller (action) | `HttpContext.SendAsync(resultTask, status)` | returns `IActionResult`; `traceId` injected automatically |
| middleware | `context.WriteProblemDetailsAsync(errors)` | **writes** the response |
| `IAsyncAuthorizationFilter` | `context.ToProblemDetailsResult(errors)` | **converts** into `ObjectResult`, without touching the response |
| anywhere, I just want the status+body pair | `AxisProblemDetailsBuilder.Build(errors, traceId)` | **computes**, pure |

### Rules

| Context | Rule |
|---|---|
| Render `ProblemDetails` outside MVC (middleware, `IAsyncAuthorizationFilter`) — or understand where `HttpContext.SendAsync` gets its status/body from | [httpresponse-problem-details-builder](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-problem-details-builder.yaml) |
| The `HttpContext.SendAsync` overloads — success shapes and delegation to the builder on failure | [httpresponse-send-success-shapes](../../rules/framework/4-edge/axis-result-httpresponse/httpresponse-send-success-shapes.yaml) |

### Documentation

| You want… | Doc |
|---|---|
| the package map (the 5-minute trunk) | [README](../../docs/en-us/4-Edge/AxisResult.HttpResponse/README.md) |
| to install and use the minimum | [getting-started](../../docs/en-us/4-Edge/AxisResult.HttpResponse/getting-started.md) |
| to convert `AxisResult` → `IActionResult` in a controller ⭐ | [send-http-response](../../docs/en-us/4-Edge/AxisResult.HttpResponse/send-http-response.md) |
| the same rendering **outside** MVC (`AxisProblemDetailsBuilder`) | [problem-details-builder](../../docs/en-us/4-Edge/AxisResult.HttpResponse/problem-details-builder.md) |
| to know which `AxisErrorType` maps to which status, severity and `ProblemDetails` shape | [error-status-mapping](../../docs/en-us/4-Edge/AxisResult.HttpResponse/error-status-mapping.md) |
| the full public surface | [api-reference](../../docs/en-us/4-Edge/AxisResult.HttpResponse/api-reference.md) |
| the rationale for a dedicated edge package | [why-axisresult-httpresponse](../../docs/en-us/4-Edge/AxisResult.HttpResponse/why-axisresult-httpresponse.md) |

> PT-BR version of each page in `docs/pt-br/4-Edge/AxisResult.HttpResponse/`.

## See also

`AxisResult` is the track; these neighbors produce, consume or flank it — none re-teaches the monad:

- `axis-use-case-cqrs` — the handler/Facade whose return **is** `AxisResult` and whose pipeline uses the
  `Then`/`Map`/`Ensure` from here (home of the command-pipeline scaffold).
- `axis-mediator` — CQRS dispatch and ambient context; **owner** of the "CancellationToken is ambient, never
  a parameter" rule.
- `axis-validator` — declarative input validation of the `Command`/`Query` that **returns** `AxisResult`
  (short-circuits before the handler).
- `axis-webapi-controllers` — the **controller conventions** (attributes, versioning, OpenAPI/Scalar, E2E
  gate). The rendering itself — `HttpContext.SendAsync` and `AxisProblemDetailsBuilder` — belongs to this skill,
  section *HTTP edge*.
- `axis-rules` — the method for authoring the missing rule of the `HttpContext.SendAsync` overloads.
- `axis-dotnet-architect` — hub: the backend's unbreakable rules and the lexicon that anchors the `Code`s.
