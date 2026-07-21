; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AXIS0400 | Axis.Mediator | Warning | Handler declares a CancellationToken parameter; the token is ambient on IAxisMediator.
AXIS0401 | Axis.Mediator | Warning | CQRS dispatch (mediator.Cqrs) issued from inside a handler; dispatch belongs in the Facade.
AXIS0402 | Axis.Mediator | Warning | Handler injects IAxisMediatorContextAccessor; read the ambient context through IAxisMediator.
AXIS0403 | Axis.Mediator | Info | String-literal key on AxisPipelineContext; use an AxisPipelineContextKeys constant.
