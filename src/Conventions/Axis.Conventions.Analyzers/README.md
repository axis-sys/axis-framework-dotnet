# Axis.Conventions.Analyzers

Opt-in Roslyn analyzers that enforce **Axis convention rules** (`rules/conventions/`) at build time.
Reference the package to apply the checks; each diagnostic links back to its canonical rule.

This package is **separate and opt-in** by design (ADR-0004): convention rules govern how you *build an
application* on Axis (hexagonal, CQRS, handler discipline), so they are never folded into a primitive's
package — a project that only wants a primitive must not inherit architecture rules it did not ask for.

## Rules

| ID | Category | Rule | Meaning |
|----|----------|------|---------|
| AXIS0600 | Axis.Architecture | architecture-handler-shape | A use-case handler must be `internal sealed`. |
| AXIS0601 | Axis.Architecture | architecture-validator-conventions | A validator (`: AxisValidatorBase<T>`) must be `internal sealed`. |
| AXIS0602 | Axis.Edge | edge-controller-shape | A controller (`: ControllerBase`) must be `sealed`. |
| AXIS0603 | Axis.Testing | testing-test-method-naming | A test method must be PascalCase with an `Async` suffix (Info). |
| AXIS0604 | Axis.Architecture | domain-properties-contract-pair | A domain properties record (`: I…Properties` in-assembly) must be `internal sealed`. |
| AXIS0605 | Axis.Edge | edge-route-injected-command-jsonignore | A route-injected command property (`command with { … }`) must be `[JsonIgnore]`. |
| AXIS0606 | Axis.Style | style-entity-record-parameter-per-line | An entity properties record (`: I…Properties`) with 2+ constructor parameters puts each on its own line. |

Detection is **semantic** (implemented interface / base type), except AXIS0603 which is syntactic
(the `[Fact]`/`[Theory]` attribute plus the method name) and AXIS0606 which is semantic on the implemented
interface plus syntactic on the parameter layout. Each analyzer is a no-op when the relevant Axis type is
absent from the compilation.

## Severity (`.editorconfig`)

`must` rules ship as `warning`, `should` as `info`. Raise per rule or per whole category:

```
[*.cs]
dotnet_diagnostic.AXIS0600.severity = error
# or an entire concern at once:
dotnet_analyzer_diagnostic.category-Axis.Architecture.severity = error
```

Opt out entirely with `<PackageReference Include="Axis.Conventions.Analyzers" ExcludeAssets="analyzers" />`.
