# AxisBus.Analyzers

Roslyn analyzers that enforce the **framework-tier** outbox rules of `AxisBus` at build time. They ship
**inside the `AxisBus` NuGet package** (`analyzers/dotnet/cs`), so any project that references `AxisBus`
gets them with no extra configuration.

## Diagnostics

| Id | Rule | Fires on |
|----|------|----------|
| `AXIS0300` | [`bus-outbox-enqueue-in-uow-transaction`](../../../../rules/framework/2-application-flow/axis-bus/bus-outbox-enqueue-in-uow-transaction.yaml) | an `IAxisBus.PublishAsync` call that comes **after** the last `SaveChangesAsync` of the enclosing member — the commit drains the outbox queue, so a publish after it strands the event outside the transaction |

Detection notes:

- The **publish** is matched semantically (`Axis.IAxisBus.PublishAsync`), so an unrelated `PublishAsync`
  on another type never fires.
- The **save** is matched by name (`SaveChangesAsync`, invocation or method group), because applications
  typically wrap `IAxisUnitOfWork` behind their own port and only the conventional name survives.
- Order is textual within the member, which mirrors execution order both in statement sequences and in
  fluent ROP chains (`...ThenAsync(unitOfWork.SaveChangesAsync)`).
- A member with **no** `SaveChangesAsync` is never flagged — the commit may legitimately live upstream
  (e.g. in a pipeline behavior). Cross-method ordering is out of scope for a syntax analyzer.

Category: `Axis.Bus`. Default severity: `warning`. Severity is tunable per repo via `.editorconfig`
(`dotnet_diagnostic.AXIS0300.severity = error` to make it a hard gate).
