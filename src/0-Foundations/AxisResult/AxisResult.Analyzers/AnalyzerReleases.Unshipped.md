; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AXIS0001 | Axis.Result | Warning | Bare .Value read on AxisResult of T; use Match or Deconstruct.
AXIS0002 | Axis.Result | Warning | Control flow branches on IsSuccess/IsFailure; compose with operators.
AXIS0003 | Axis.Result | Warning | try/catch on a method returning AxisResult; use AxisResult.Try/TryAsync.
AXIS0004 | Axis.Result | Warning | AxisResult.Try called without a typed errorHandler; the default leaks ex.Message as the code.
AXIS0005 | Axis.Result | Warning | AxisError code built from an exception message; use a stable UPPER_SNAKE_CASE constant.
AXIS0006 | Axis.Result | Warning | AxisResult deconstructed inside a method that returns AxisResult; deconstruct only at the terminal.
AXIS0007 | Axis.Result | Warning | Null-forgiving read of a deconstructed AxisResult value without proving success first.
