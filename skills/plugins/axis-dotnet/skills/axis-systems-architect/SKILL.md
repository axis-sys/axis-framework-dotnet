---
name: axis-systems-architect
description: >
  Plan BEFORE coding and decide WHERE elements live — the bounded-context boundary, the subdomain taxonomy
  (core / supporting / generic) and the channel between bounded contexts: facade (sync), `IAxisBus` (async)
  or a saga with compensation. Use when creating or altering a bounded context, drawing a BC boundary, or
  choosing how one BC talks to another before any code is written. This skill is a MAP: each row points to
  the canonical rule in `rules/` — open only the one the context asks for. It does NOT restate invariants nor
  carry code. It does NOT cover the code-level implementation of each piece (→ axis-dotnet-architect, the
  hub), the Facade/vertical slice detail (→ axis-use-case-cqrs), saga stage handlers (→ axis-saga) or bus
  handlers (→ axis-bus).
---

# AxisSystemsArchitect — rule map (plan first: where things live & how BCs talk)

This skill is the **planning face** of the Axis backend: before a single file is written, it fixes the
structural decisions that are expensive to reverse — the **bounded-context boundary**, the **subdomain
taxonomy** (core / supporting / generic, an input from product planning), the **project & folder topology**
(where an element lives), the **channel** a BC uses to reach another BC, and the **host / composition-root
model**. The code that materialises each decision lives one hop away in the implementation hub
(`axis-dotnet-architect`) and its sub-skills.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/conventions/architecture/`; open **only** the rule the context
requires.

## Rule map

### Topology & where things live

| Context / what you were about to decide | Rule |
|---|---|
| Deciding the project & folder layout — five sibling solution folders at the same level: `Core/` (`{App}.SharedKernel` + `{App}.Domain` + `{App}.Application`), `Contracts/` (`{App}.Contracts.Driving` + `{App}.Contracts.Driven`), `Adapters/` (`{App}.Adapters.Driving.Facade`, `{App}.Adapters.Driven.{Role}`), `Host/` (`{App}.Host`), `Tests/`; vertical-sliced BC → aggregate → feature inside each project; namespace = folder path, so a BC folder can later be promoted to its own microservice | [architecture-hexagonal-topology](../../rules/conventions/architecture/architecture-hexagonal-topology.yaml) |

### BC boundaries & cross-BC channels ⭐

| Context / what you were about to decide | Rule |
|---|---|
| Choosing **how** one BC talks to another — the decision table (facade `#sync` · bus `#async` · saga `#compensation`); never reach into another BC's ports or internals | [architecture-cross-bc-communication](../../rules/conventions/architecture/architecture-cross-bc-communication.yaml) |
| The **synchronous** channel — call the other BC's public `I{Entities}Facade` (one-to-one with its use cases; may wrap it in a local port to stay decoupled) | [architecture-facade-pattern](../../rules/conventions/architecture/architecture-facade-pattern.yaml) |
| The **asynchronous** channel — fire-and-topology bus events (sealed id-only records, own `Topic`, a consumer never fails the publisher) | [architecture-bus-events](../../rules/conventions/architecture/architecture-bus-events.yaml) |
| The **multi-BC process with compensation** channel — declare a saga (`Define<TPayload>`, framework-hosted resumer, 202 + polling GET) | [architecture-saga-definition](../../rules/conventions/architecture/architecture-saga-definition.yaml) |

### Multi-BC implementation sequencing & parallelization

| Context / what you were about to decide | Rule |
|---|---|
| Splitting implementation of a multi-BC plan across parallel agents/developers — build the consumption dependency graph (facade / saga stage / bus event) first; two BCs parallelize only with no edge between them; a dependent BC waits only on the specific consumed piece, not the whole producing BC | [process-multi-bc-implementation-parallelization](../../rules/conventions/process/process-multi-bc-implementation-parallelization.yaml) |

### Composition root / host model

| Context / what you were about to decide | Rule |
|---|---|
| Deciding the **host model** — one shared composition root wires the monolith (the only place that binds an `IAxis*` abstraction to a concrete adapter); a BC promoted to a microservice gets its own host / BFF, keeping its namespace | [architecture-composition-root](../../rules/conventions/architecture/architecture-composition-root.yaml) |

## See also

- `axis-dotnet-architect` — the code implementation **hub**; once the boundary and channel are fixed here,
  it routes to the sub-skill that writes each piece.
- `axis-use-case-cqrs` — owner of the **Facade** (the synchronous cross-BC doorway) and the vertical slice.
- `axis-bus` — the **asynchronous** channel in detail (`IAxisEvent` / `IAxisEventHandler`, out-of-band).
- `axis-saga` — the **saga with compensation** channel in detail (stage handlers, payload, resumer).
