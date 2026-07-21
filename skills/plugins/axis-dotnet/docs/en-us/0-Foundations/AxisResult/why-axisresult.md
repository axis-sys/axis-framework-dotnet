# Why AxisResult? · comparison

> There are other Result libraries for C#. This page tells you why AxisResult is different — a direct comparison, no hand-waving.

---

## vs. FluentResults

FluentResults is popular but limited. It lacks monadic composition — you can't chain async operations without manual unwrapping. There's no `ValueTask` support, no tuple composition, no recovery patterns, and no typed error categories. It's a container, not a railway.

## vs. ErrorOr

ErrorOr offers basic chaining but misses the depth needed for production systems. No `ValueTask` variants, no `Zip` for combining independent operations, no `Recover`/`RecoverWhen` for conditional fallbacks, no `RequireNotFound` for idempotent creation patterns.

## vs. LanguageExt

LanguageExt is a 7.5MB functional programming framework. If you only need Result types, you're pulling in immutable collections, State monads, Reader monads, and an entirely non-idiomatic API. AxisResult gives you the composition power without the weight or the learning curve.

## vs. CSharpFunctionalExtensions

Solid library, but no `ValueTask` support, no `Zip`, no typed error categories, no recovery patterns, no parallel aggregation. It's good for basic Result patterns but falls short in complex domain scenarios.

## vs. Ardalis.Result

Designed for ASP.NET controllers, not for domain logic. Basic `Map`/`Bind` support, no composition depth, no async variants, no recovery. Great for HTTP response mapping, limited everywhere else.

---

## The comparison

| Feature | AxisResult | FluentResults | ErrorOr | LanguageExt | CSharpFunctExt |
|---------|:---:|:---:|:---:|:---:|:---:|
| Monadic composition (Then/Map) | **Yes** | Partial | Yes | Yes | Yes |
| Task + ValueTask async | **Yes** | No | No | No | No |
| Tuple composition (Zip) | **Up to 4** | No | No | Yes | No |
| Conditional recovery (Recover/RecoverWhen) | **Yes** | No | No | No | No |
| Typed error categories | **12 types** | No | Partial | No | No |
| Transient error detection | **Yes** | No | No | No | No |
| RequireNotFound pattern | **Yes** | No | No | No | No |
| LINQ query syntax | **Yes** | No | Partial | Yes | Yes |
| Parallel aggregation (Combine/All) | **Yes** | No | No | Yes | No |
| Error accumulation (OrElse) | **Yes** | Partial | No | No | No |
| Zero external dependencies | **Yes** | Partial | Partial | No | Yes |
| Lightweight (~240KB) | **Yes** | Yes | Yes | No (7.5MB) | Yes |
| CancellationToken-aware overloads | **Yes** | No | No | Partial | No |
| Parallel zip (independent ops) | **Yes** | No | No | Partial | No |

---

## See also

- [Railway-Oriented Programming](railway-oriented-programming.md) — the model these features implement
- [Getting started](getting-started.md) — install and start
- [API reference](api-reference.md) — the complete operator catalog

---

↩ [Back to AxisResult docs](README.md)
