---
name: axis-validator
description: >
  Declarative input validation on Axis with `AxisValidatorBase<T>` (over FluentValidation), returning
  `AxisResult` instead of throwing. Use when writing or changing the validator of a Command/Query/integration
  message, or when understanding how validation runs in the mediator pipeline. This skill is a MAP: each row
  points to the canonical rule in `rules/` — open only the one the context asks for. It does NOT restate
  invariants nor carry code. It does NOT cover domain invariants (→ axis-domain-modeling), the return monad
  (→ axis-result), the ambient context/dispatch (→ axis-mediator) nor the use case the validator protects
  (→ axis-use-case-cqrs).
---

# AxisValidator — rule map (declarative input validation)

A **validator** guards the shape of an incoming message — presence, format, length, ranges, cross-field
consistency — and returns an `AxisResult` on the failure rail instead of throwing. `AxisValidatorBase<T>`
sits over FluentValidation and adds an Axis-flavoured helper vocabulary; the `ValidationBehavior` runs it
automatically in the mediator pipeline. The package is 2-application-flow; the localization packs
(`AxisValidator.Brazil`, `AxisValidator.Usa`) are optional add-ons.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/2-application-flow/axis-validator/`; open **only** the
rule the context requires.

## Rule map

### Start here — write a validator ⭐

| Context / what you were about to write | Rule |
|---|---|
| Create the validator class for a command/query/message | [validator-derive-base](../../rules/framework/2-application-flow/axis-validator/validator-derive-base.yaml) |
| About to hand-roll `RuleFor(...).Must(...).WithErrorCode(...)` — reach for the helper first | [validator-use-helpers-over-rulefor](../../rules/framework/2-application-flow/axis-validator/validator-use-helpers-over-rulefor.yaml) |
| Choosing what a failure carries — the **code** is the contract, not the message | [validator-error-code-is-contract](../../rules/framework/2-application-flow/axis-validator/validator-error-code-is-contract.yaml) |

### Presence & format helpers

| Context | Rule |
|---|---|
| Require a value (null / default / blank), optionally with dependent rules | [validator-not-null-or-empty](../../rules/framework/2-application-flow/axis-validator/validator-not-null-or-empty.yaml) |
| Require a string within a length limit (default 255) | [validator-required-with-max-length](../../rules/framework/2-application-flow/axis-validator/validator-required-with-max-length.yaml) |
| Require a URL-safe slug (`[a-zA-Z0-9-_]`, length) | [validator-required-slug](../../rules/framework/2-application-flow/axis-validator/validator-required-slug.yaml) |
| Require a valid email address | [validator-required-email](../../rules/framework/2-application-flow/axis-validator/validator-required-email.yaml) |
| Require an identifier that is a UUID **v7** | [validator-required-guid7](../../rules/framework/2-application-flow/axis-validator/validator-required-guid7.yaml) |
| Require a value a custom parse predicate accepts (the localization hook) | [validator-required-try-parse](../../rules/framework/2-application-flow/axis-validator/validator-required-try-parse.yaml) |

### Numbers & collections

| Context | Rule |
|---|---|
| Bound a nullable struct inclusively (null passes) | [validator-range](../../rules/framework/2-application-flow/axis-validator/validator-range.yaml) |
| Require a non-null, non-empty collection | [validator-required-collection](../../rules/framework/2-application-flow/axis-validator/validator-required-collection.yaml) |
| Cap a collection's size (null passes) | [validator-max-count](../../rules/framework/2-application-flow/axis-validator/validator-max-count.yaml) |

### Predicates & composition

| Context | Rule |
|---|---|
| Assert a custom predicate over a field or the whole instance | [validator-satisfies](../../rules/framework/2-application-flow/axis-validator/validator-satisfies.yaml) |
| Apply a predicate to every item of a collection | [validator-each-satisfies](../../rules/framework/2-application-flow/axis-validator/validator-each-satisfies.yaml) |
| Cross-field rule — both fields required, then a combined `AxisResult` check | [validator-dependent-rules](../../rules/framework/2-application-flow/axis-validator/validator-dependent-rules.yaml) |
| Compose a child validator for a nested object / each item | [validator-nested-validator](../../rules/framework/2-application-flow/axis-validator/validator-nested-validator.yaml) |

### Result, cancellation & the pipeline

| Context | Rule |
|---|---|
| What validation returns — `AxisResult`, never a thrown `ValidationException` | [validator-result-not-throw](../../rules/framework/2-application-flow/axis-validator/validator-result-not-throw.yaml) |
| Async validation and the ambient `CancellationToken` (never a parameter) | [validator-async-ambient-cancellation](../../rules/framework/2-application-flow/axis-validator/validator-async-ambient-cancellation.yaml) |
| How validation runs automatically before the handler (short-circuit) | [validator-behavior-short-circuit](../../rules/framework/2-application-flow/axis-validator/validator-behavior-short-circuit.yaml) |
| Wiring — `AddAxisValidator(assemblies)` | [validator-di-registration](../../rules/framework/2-application-flow/axis-validator/validator-di-registration.yaml) |

### Scope & localization

| Context | Rule |
|---|---|
| What belongs in a validator vs. the entity vs. the application layer | [validator-input-scope](../../rules/framework/2-application-flow/axis-validator/validator-input-scope.yaml) |
| Region-specific checks (CPF / BR cellphone, SSN / U.S. phone) on the railway | [validator-localization-packs](../../rules/framework/2-application-flow/axis-validator/validator-localization-packs.yaml) |

## See also

- `axis-use-case-cqrs` — the vertical slice the validator protects (Command/Query, Response, Handler, Validator).
- `axis-mediator` — the pipeline `ValidationBehavior` plugs into, and the ambient `CancellationToken` it flows.
- `axis-result` — the monad the adapter projects failures onto (`AxisError.ValidationRule(code)`).
- `axis-domain-modeling` — where domain invariants live (the entity, `AxisError.BusinessRule`), not the validator.
- `axis-rules` — how these rules are authored and maintained (the extraction method from code).
