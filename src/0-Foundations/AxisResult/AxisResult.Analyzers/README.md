# AxisResult.Analyzers

Roslyn analyzers that enforce the **framework-tier** railway-oriented-programming rules of `AxisResult`
at build time. They ship **inside the `AxisResult` NuGet package** (`analyzers/dotnet/cs`), so any project
that references `AxisResult` gets them with no extra configuration — a deterministic, build-time
enforcement for the rules that are syntactic enough for an analyzer, as opposed to the context-dependent
conventions reviewed by hand (see `rules/README.md`).

## Diagnostics

| Id | Rule | Fires on |
|----|------|----------|
| `AXIS0001` | [`result-value-access-safety`](../../../../rules/framework/0-foundations/axis-result/result-value-access-safety.yaml) | a bare `.Value` read on `AxisResult<T>` — leave the rail with `Match` or the positional `Deconstruct` |
| `AXIS0002` | [`result-no-if-else-flow`](../../../../rules/framework/0-foundations/axis-result/result-no-if-else-flow.yaml) | branching on `IsSuccess`/`IsFailure` (`if`/ternary/loop) — compose with `Then`/`Map`/`Ensure` or `Match` |
| `AXIS0003` | [`result-no-throw`](../../../../rules/framework/0-foundations/axis-result/result-no-throw.yaml) | `try/catch` inside a method that returns `AxisResult` — wrap the boundary with `Try`/`TryAsync` |
| `AXIS0004` | [`result-try-boundary`](../../../../rules/framework/0-foundations/axis-result/result-try-boundary.yaml) | `Try`/`TryAsync`/`TryBind` without a typed `errorHandler` — the default leaks `ex.Message` as the code |
| `AXIS0005` | [`result-error-typing`](../../../../rules/framework/0-foundations/axis-result/result-error-typing.yaml) | an `AxisError` code built from an exception message — codes are stable UPPER_SNAKE_CASE constants |
| `AXIS0006` | [`result-deconstruct-terminal-only`](../../../../rules/framework/0-foundations/axis-result/result-deconstruct-terminal-only.yaml) | deconstructing an `AxisResult` (tuple, positional pattern or `switch`) **inside the rail** — a method that itself returns `AxisResult`; the message suggests the operator that fits the observed shape |
| `AXIS0007` | [`result-value-access-safety`](../../../../rules/framework/0-foundations/axis-result/result-value-access-safety.yaml) | `!` (null-forgiving) on a deconstructed value without a prior read of the success flag — `.Value` in disguise |

`AXIS0002` covers the three spellings of the outcome branch: the direct member access, the flag
laundered through a `bool` local, and the property pattern (`r is { IsSuccess: true }`). The
**positional** pattern at the terminal stays sanctioned — it is the exit `AXIS0006` protects.

Detection is **semantic** (via the symbol/semantic model), so `Nullable<T>.Value`, `KeyValuePair.Value`,
`try/finally` without a catch, and `try/catch` in non-`AxisResult` methods are never flagged. Each analyzer
is a **no-op** in a compilation that does not reference `AxisResult`, and **auto-exempts** the `AxisResult`
library itself (its internals legitimately use `.Value`/`IsSuccess`/`throw`).

Category: `Axis.Result`. Default severity: `warning` (visible in the IDE and in `dotnet build`, non-blocking).

## Configuring severity (`.editorconfig`)

Put an `.editorconfig` at the consumer's repo root. Elevate to `error` to make the build (and therefore CI
and the `pre-push` hook) **fail** on a violation — this is how the diagnostics become a hard gate:

```ini
# Turn the whole AxisResult ROP family into build-breaking errors:
[*.cs]
dotnet_analyzer_diagnostic.category-Axis.Result.severity = error

# ...or tune a single rule:
dotnet_diagnostic.AXIS0001.severity = error      # bare .Value  -> error
dotnet_diagnostic.AXIS0002.severity = warning    # branching    -> warning
dotnet_diagnostic.AXIS0003.severity = suggestion # try/catch    -> IDE suggestion only

# Relax the rule in a specific area (e.g. legacy/interop code):
[legacy/**.cs]
dotnet_diagnostic.AXIS0001.severity = none
```

## Opting out entirely

A project that wants `AxisResult` but not its analyzers can exclude them at the reference:

```xml
<PackageReference Include="AxisResult" Version="..." ExcludeAssets="analyzers" />
```

## Scope

This package enforces only the **AxisResult** contract (framework-tier). Architectural conventions
(Hexagonal, CQRS, handler discipline, folder structure) are **convention-tier** and will ship in a
separate, opt-in `Axis.Conventions.Analyzers` package — never folded into a primitive's package, so a
project that only wants the monad never receives architecture rules it did not ask for.
