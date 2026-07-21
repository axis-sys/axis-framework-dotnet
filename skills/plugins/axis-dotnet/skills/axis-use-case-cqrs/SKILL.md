---
name: axis-use-case-cqrs
description: >
  Create or change a CQRS use case on Axis — the vertical slice Command/Query → Response → Handler →
  Validator, and the **Facade that exposes it** (the canonical home of the Facade). Use when adding a
  business operation to a bounded context (insert, update, list, get): glob an adjacent use case of the
  same kind and mirror it. This skill is a MAP: each row points to the canonical rule in `rules/` — open
  only the one the context asks for. It does NOT restate invariants nor carry code. It does NOT cover the
  return monad (→ axis-result), the dispatch/ambient context (→ axis-mediator), input validation in
  detail (→ axis-validator), nor the factory/aggregate the handler drives (→ axis-domain-modeling).
---

# AxisUseCaseCqrs — rule map (the CQRS vertical slice + the Facade)

A **use case** is one business operation shaped as a vertical slice: a `Command`/`Query` and its
`Response` (contracts), a `Handler` (behavior), an optional `Validator`, and the public **Facade** that
is the only doorway into the slice. The contracts sit in the driving-contracts project; the handler and
validator sit in the application project; the Facade interface lives with the contracts and its
implementation is a thin internal mediator adapter. This is the point where Product intent meets code.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/conventions/`; open **only** the rule the context requires.

## Rule map

### Start here — the vertical slice ⭐

| Context / what you were about to write | Rule |
|---|---|
| Lay out the application slice — `{BC}/{Aggregate}/UseCases/{UseCase}/v1/`, handler (+ validator) only, siblings for factories/services | [architecture-vertical-slice-layout](../../rules/conventions/architecture/architecture-vertical-slice-layout.yaml) |
| Write the handler — internal sealed `{UseCase}Handler`, primary-ctor port injection, one railway chain to the response, no `CancellationToken` | [architecture-handler-shape](../../rules/conventions/architecture/architecture-handler-shape.yaml) |
| Don't re-authorize in the handler — the `[Authorize]` edge already gated identity/access; read the ambient identity for business use, not to re-check it (authentication/token handlers and genuine domain permission gates are the exception, applied with context) | [architecture-handler-no-authorization](../../rules/conventions/architecture/architecture-handler-no-authorization.yaml) |
| Commit the write — one `SaveChangesAsync` at the tail of the command handler's chain (never in queries/factories) | [persistence-unit-of-work](../../rules/conventions/persistence/persistence-unit-of-work.yaml) |

### The Facade (canonical home)

| Context | Rule |
|---|---|
| Expose the slice — the public `I{Entities}Facade` at `{BC}/{Aggregate}/v1/` in the driving contracts; the `internal sealed` impl lives in the dedicated `{App}.Adapters.Driving.Facade` project (never in the Application), one method per use case | [architecture-facade-pattern](../../rules/conventions/architecture/architecture-facade-pattern.yaml) |
| Who enters through it — controllers depend on the facade, never on the mediator, ports or services | [edge-controller-facade-injection](../../rules/conventions/edge/edge-controller-facade-injection.yaml) |

### Contracts — where they live & how they are shaped

| Context | Rule |
|---|---|
| Which contracts, and where — Command/Query/Response/sub-DTOs/facade live in the `{App}.Contracts.Driving` project (a `Contracts/` solution folder, **sibling** of `Core/` — never nested inside it); driven ports live in the parallel `{App}.Contracts.Driven` | [architecture-contracts-location](../../rules/conventions/architecture/architecture-contracts-location.yaml) |
| Folder & namespace — `{BC}/{Aggregate}/v1/{UseCase}/`, namespace mirrors the path, `v2` as a sibling tree | [architecture-contracts-versioned-layout](../../rules/conventions/architecture/architecture-contracts-versioned-layout.yaml) |
| DTO shape — primitive-typed, nullable inits on input, required inits on output, no domain types | [architecture-contracts-primitive-dtos](../../rules/conventions/architecture/architecture-contracts-primitive-dtos.yaml) |

## See also

- `axis-result` — the monad the handler returns (`Then`/`Map`/`Ensure`); the railway the whole slice rides.
- `axis-mediator` — the CQRS dispatch the Facade calls (`mediator.Cqrs`) and the ambient `CancellationToken`.
- `axis-validator` — the `{UseCase}Validator` that guards the input before the handler runs.
- `axis-domain-modeling` — the factory/aggregate/ports the handler drives inside the railway chain.
- `axis-webapi-controllers` — exposing the Facade over HTTP (controller + E2E gate).
- `axis-dotnet-architect` — the plugin hub that routes here and owns the cross-cutting inviolable rules.
