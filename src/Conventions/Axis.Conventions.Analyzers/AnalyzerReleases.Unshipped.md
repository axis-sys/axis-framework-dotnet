; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AXIS0600 | Axis.Architecture | Warning | Use-case handler must be internal sealed (architecture-handler-shape).
AXIS0601 | Axis.Architecture | Warning | Validator must be internal sealed (architecture-validator-conventions).
AXIS0602 | Axis.Edge | Warning | Controller must be sealed (edge-controller-shape).
AXIS0603 | Axis.Testing | Info | Test method must be PascalCase with an Async suffix (testing-test-method-naming).
AXIS0604 | Axis.Architecture | Warning | Domain properties record must be internal sealed (domain-properties-contract-pair).
AXIS0605 | Axis.Edge | Warning | Route-injected command property must be [JsonIgnore] (edge-route-injected-command-jsonignore).
AXIS0606 | Axis.Style | Warning | Entity properties record with 2+ constructor parameters must put each on its own line (style-entity-record-parameter-per-line).
AXIS0607 | Axis.Architecture | Warning | At most one public DependencyInjection class per assembly; per-feature decomposition stays internal (architecture-di-registration).
