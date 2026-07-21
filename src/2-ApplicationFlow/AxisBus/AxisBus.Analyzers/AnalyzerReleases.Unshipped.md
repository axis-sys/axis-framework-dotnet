; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AXIS0300 | Axis.Bus | Warning | IAxisBus.PublishAsync after the last SaveChangesAsync of the member; publish before the commit so the outbox drains the event.
