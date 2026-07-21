# Registration & scanning

> Two extension calls handle everything: `AddAxisMediator()` wires the mediator and its accessors, `AddCqrsMediator(assembly)` scans the assembly for command / query / stream / event handlers and registers each one against its interface.

```csharp
builder.Services
    .AddAxisMediator()
    .AddCqrsMediator(Assembly.GetExecutingAssembly());
```

---

## When to use

`AddAxisMediator()` exactly once per app. `AddCqrsMediator(assembly)` once per assembly that contains handlers — most apps call it for the API assembly; some call it again for a class library that owns more handlers.

## When *not* to use

| You want to… | Use instead |
|---|---|
| register a single handler manually | `services.AddTransient<IAxisCommandHandler<TCommand>, MyHandler>();` (the scanner does this for you) |
| register validation / logging / telemetry behaviours | their own `Add*` extensions ([`AddAxisLogger`](../../1-Observability/AxisLogger/README.md), [`AddAxisValidator`](../AxisValidator/README.md), [`AddOpenTelemetryAxis`](../../1-Observability/AxisTelemetry/README.md)) |

---

## `AddAxisMediator()`

Reading `DependencyInjection.AddAxisMediator`:

| Type | Lifetime | Purpose |
|---|---|---|
| `IAxisMediatorHandler` → `AxisMediatorHandler` | scoped | the dispatcher (`ExecuteAsync`/`QueryAsync`/`StreamAsync`) |
| `IAxisMediator` → `AxisMediator` | scoped | the ambient context (constructor sets `accessor.AxisMediator = this`, `Dispose` clears it) |
| `IAxisMediatorAccessor` → `AxisMediatorAccessor` | singleton | holds the last-constructed `IAxisMediator` (for adapters outside the scope) |
| `IAxisMediatorContextAccessor` → `AxisMediatorContextAccessor` | singleton | `AsyncLocal` storage for `OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken` |

## `AddPerformanceBehavior()`

| Type | Lifetime | Purpose |
|---|---|---|
| `IAxisPipelineBehavior<,>` → `PerformanceBehavior<,>` | transient | the slow-request warning, see [Performance behaviour](performance-behavior.md) |

## `AddCqrsMediator(assembly)`

Reading `CQRS.DependencyInjection.AddCqrsMediator` directly — six `RegisterHandlers(...)` calls per assembly, one for each handler interface:

```csharp
RegisterHandlers(services, assembly, typeof(IAxisCommand<>));          // typed-command markers
RegisterHandlers(services, assembly, typeof(IAxisCommandHandler<>));    // void-command handlers
RegisterHandlers(services, assembly, typeof(IAxisCommandHandler<,>));   // typed-command handlers
RegisterHandlers(services, assembly, typeof(IAxisQueryHandler<,>));     // query handlers
RegisterHandlers(services, assembly, typeof(IAxisStreamQueryHandler<,>)); // stream-query handlers
RegisterHandlers(services, assembly, typeof(IAxisEventHandler<>));      // event handlers (for AxisBus)
```

`RegisterHandlers`:

1. Picks every non-abstract, non-generic class in the assembly.
2. Keeps those that implement at least one generic interface matching the handler shape.
3. For each matching interface variant, calls `services.AddTransient(interface, implementation)`.

| Behaviour | Detail |
|---|---|
| Lifetime | **transient** — one handler instance per dispatch |
| Multiple handlers per request type | only the last registered wins for commands and queries (single-handler shapes); events accept many |
| Internal types | included (the scanner does not filter by `IsPublic`) |
| Generic handlers | not included (`IsGenericType: false`) |

---

## Real-world examples

### 1. App with handlers split into two assemblies

```csharp
builder.Services
    .AddAxisMediator()
    .AddCqrsMediator(typeof(CreateOrderHandler).Assembly)       // OrderModule
    .AddCqrsMediator(typeof(CreatePersonHandler).Assembly);     // PersonModule
```

**Why it pays off:** each module owns its handlers, and the composition root opts each one in. New module? One more line, no rewrite.

### 2. Adding a handler manually

```csharp
services.AddTransient<IAxisCommandHandler<RecalculateInvoicesCommand>, RecalculateInvoicesHandler>();
```

If the handler is generic, or you want a different lifetime, register it yourself. The scanner skips generic types so you can manage them by hand.

### 3. Replacing a handler in tests

```csharp
services.RemoveAll(typeof(IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>));
services.AddTransient<IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>, FakeCreateOrderHandler>();
```

**Why it pays off:** the rest of the pipeline is intact (logging, validation, telemetry). Only the handler is swapped — the test uses every behaviour the production path uses.

---

## See also

- [Getting started](getting-started.md) — the minimal setup
- [Dispatching · `IAxisMediatorHandler`](dispatching.md) — what the dispatcher does at runtime
- [Pipeline behaviours](pipeline-behaviors.md) — the open-generic extension point
- [`AxisLogger`](../../1-Observability/AxisLogger/README.md) · [`AxisValidator`](../AxisValidator/README.md) · [`AxisTelemetry`](../../1-Observability/AxisTelemetry/README.md) — the in-box behaviours' own `Add*` extensions

---

↩ [Back to AxisMediator docs](README.md)
