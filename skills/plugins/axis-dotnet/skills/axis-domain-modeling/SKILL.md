---
name: axis-domain-modeling
description: >
  Tactical DDD modeling on Axis — the core of an aggregate in C#: domain entities (Properties + Rules),
  AggregateFactory/AggregateApplication, Reader/Writer ports + IUnitOfWork, and value objects built with the
  `[ValueObject]` generator. Use when creating or changing an aggregate's domain or its ports, and when
  classifying it as N0 lookup, N1 CRUD or N2 behavior-rich (ADR-0010). This skill is a MAP: each row points to
  the canonical rule in `rules/` — open only the one the context asks for. It does NOT restate the invariants
  nor carry code. It does NOT cover persistence of the ports (→ axis-repository-postgres), the use case that
  drives the factory (→ axis-use-case-cqrs), the `[ValueObject]` generator internals (→ axis-types), input
  validation (→ axis-validator) nor the return monad (→ axis-result).
---

# AxisDomainModeling — rule map (tactical DDD: the aggregate core)

An **aggregate** is the unit of tactical modeling in Axis. Its core is a small set of parts that always fit
together: the **state contract** (a public `Properties` interface plus an internal sealed record), the
**construction seam** (an application `{Aggregate}Factory` at N1, or a domain `AggregateFactory` +
`AggregateApplication` once a real rule appears at N2), the **persistence ports** (`Reader`/`Writer` returning
`AxisResult`, committed through the single `IUnitOfWork`), and the **value objects** that type its ids and
scalars via the `[ValueObject]` source generator. Every aggregate rests at one of three levels — **N0** lookup,
**N1** CRUD, **N2** behavior-rich (ADR-0010) — and upgrades to N2 the moment its first domain rule appears.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/conventions/`; open **only** the rule the context requires.

## Rule map

### Start here — the aggregate levels ⭐

| Context / what you were about to do | Rule |
|---|---|
| Classify an aggregate as N0 lookup / N1 CRUD / N2 behavior-rich, and know when the first domain rule forces the N2 upgrade in the same change | [domain-aggregate-levels](../../rules/conventions/domain/domain-aggregate-levels.yaml) |

### The entity core — Properties contract pair

| Context | Rule |
|---|---|
| Model a persisted entity's state — a public `I{Entity}EntityProperties` interface plus an internal sealed positional record in the same folder | [domain-properties-contract-pair](../../rules/conventions/domain/domain-properties-contract-pair.yaml) |
| Name the interface by level — `I{X}EntityProperties` for a mutable entity (backed by a WritePort) vs plain `I{X}Properties` for a reader-only seeded lookup | [style-entity-suffix-naming](../../rules/conventions/style/style-entity-suffix-naming.yaml) |

### Value objects

| Context | Rule |
|---|---|
| Build a value object — `[ValueObject]` on a `readonly partial record struct` whose private constructor is the only throw site; UUID v7 for ids | [domain-value-object-generator](../../rules/conventions/domain/domain-value-object-generator.yaml) |

### Factories & ports (Reader/Writer + IUnitOfWork)

| Context | Rule |
|---|---|
| Construct an N1 aggregate through an internal `{Aggregate}Factory` with a nested `NewArgs` record — checks preconditions via reader ports, stages via the writer, never commits | [domain-aggregate-factories](../../rules/conventions/domain/domain-aggregate-factories.yaml) |
| Split persistence into separate `I{Entities}ReaderPort` and `I{Entities}WritePort`, every method returning `AxisResult` over the property interfaces | [persistence-reader-write-ports](../../rules/conventions/persistence/persistence-reader-write-ports.yaml) |
| Commit through the single `IUnitOfWork` contract — `SaveChangesAsync` once per use case, `RenewAsync` only for lock-refresh reads | [persistence-unit-of-work-contract](../../rules/conventions/persistence/persistence-unit-of-work-contract.yaml) |
| Decide who calls `SaveChangesAsync` and when — the owning handler at the tail of the chain; factories, ports and query handlers never commit | [persistence-unit-of-work](../../rules/conventions/persistence/persistence-unit-of-work.yaml) |

### Feature layout

| Context | Rule |
|---|---|
| Place the aggregate's files — one `{BC}/{Feature}` subfolder per layer, mirrored by namespace, child entities nested under the parent | [architecture-one-folder-per-feature](../../rules/conventions/architecture/architecture-one-folder-per-feature.yaml) |

## See also

- [`axis-types`](../axis-types/SKILL.md) — the `[ValueObject]` source generator and the kernel id primitives (`AxisEntityId`, UUID v7) that the value objects and ids in this skill build on.
- `axis-use-case-cqrs` — the command handler / use case that drives the `{Aggregate}Factory` and owns the single `SaveChangesAsync` commit.
- `axis-repository-postgres` — the Postgres adapter that implements the Reader/Writer ports and the `IUnitOfWork` this skill only declares.
- `axis-dotnet-architect` — the hub carrying the cross-cutting unbreakable rules (everything returns `AxisResult`, no `try/catch`, swappable `IAxis*` ports).
