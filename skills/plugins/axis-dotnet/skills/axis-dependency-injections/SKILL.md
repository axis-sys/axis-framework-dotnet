---
name: axis-dependency-injections
description: >
  The AxisDependencyInjections package (0-foundations) — a source-less shim that transitively re-exports
  Microsoft's DI package under an Axis-owned name, in two variants: `AxisDependencyInjections.Abstractions`
  (the DI contracts — `IServiceCollection`, `ServiceLifetime`, the registration-extension helpers) and
  `AxisDependencyInjections` (the container implementation — `ServiceCollection`, `BuildServiceProvider`). Use when
  wiring a project's DI dependency: which variant to reference, and why referencing the Axis project is compile-time
  equivalent to referencing the Microsoft package directly. The package contains NO code, so there is nothing to call
  — only a reference decision. This skill is a MAP: each row points to the canonical rule under
  `rules/framework/0-foundations/axis-dependency-injections/`; open only the one needed. It does NOT
  cover a concrete `AddAxis*` registration (those live with each adapter — e.g. axis-validator, axis-storage), nor the
  mediator's DI wiring (→ axis-mediator).
---

# AxisDependencyInjections — rule map (the DI re-export shim)

`AxisDependencyInjections` is not a library you call: it is a pair of **source-less projects** that re-export
Microsoft's `Microsoft.Extensions.DependencyInjection[.Abstractions]` package under an Axis-owned name. Referencing
the Axis project brings the Microsoft DI surface (`IServiceCollection` and the `AddScoped`/`AddTransient` helpers)
into scope transitively. The only decision it governs is **which of the two variants a project references**.

This skill **does not restate** the invariants nor carry code — it **routes**. Each row points to the canonical
rule (in English) under `rules/framework/0-foundations/axis-dependency-injections/`; open **only** the rule the
context requires.

## Rule map

### What the package is ⭐

| Context / what you were about to assume | Rule |
|---|---|
| "Where are the AxisDependencyInjections types / helpers?" — there are none; it is a source-less transitive re-export of the Microsoft DI package | [di-reexport-shim](../../rules/framework/0-foundations/axis-dependency-injections/di-reexport-shim.yaml) |

### Which variant to reference

| Context | Rule |
|---|---|
| Choosing between `AxisDependencyInjections.Abstractions` (contracts, e.g. to declare `AddAxis*(this IServiceCollection)`) and `AxisDependencyInjections` (the container itself) | [di-abstractions-vs-implementation](../../rules/framework/0-foundations/axis-dependency-injections/di-abstractions-vs-implementation.yaml) |

## See also

- `axis-rules` — how these rules are authored/maintained (the extraction method from code).
- `axis-validator`, `axis-storage` — adapters that reference the Abstractions variant to expose their `AddAxis*`
  registration extension.
- `axis-mediator` — its own DI registration (`AddAxisMediator`) references the Microsoft DI packages directly rather
  than through this shim; the shim is optional and only partially adopted across the framework.
