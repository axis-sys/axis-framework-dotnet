# Getting started · installation and usage

> Install the package, register it in DI, log your first structured entry — and optionally turn on the request-logging behaviour with one extra line.

---

## Installation

```
dotnet add package AxisLogger
```

`AxisLogger` is tiny — it depends directly on `AxisMediator.Contracts`, with `AxisResult` (used by `LogResult`) coming transitively through it. Sinks plug in through `Microsoft.Extensions.Logging` as usual (Serilog, OpenTelemetry, console, file).

---

## Registering

```csharp
using Axis;

builder.Services
    .AddAxisMediator()       // provides IAxisMediator (TraceId/OriginId/JourneyId)
    .AddAxisLogger();        // wires IAxisLogger<T> as scoped over the calling assembly
```

> **Heads up:** `AxisLogger<T>`'s constructor also requires a `TimeProvider`, and neither `AddAxisMediator()` nor `AddAxisLogger()` registers one. Resolving `IAxisLogger<T>` with only the registration above throws a DI resolution error. Get one by calling `AddLoggingBehavior()` (see [Turning on automatic request logging](#turning-on-automatic-request-logging) below — it registers `TimeProvider.System`), or register it yourself with `builder.Services.AddSingleton(TimeProvider.System);`.

Both extensions live as **C# 12 extensions** on `IServiceCollection`:

```csharp
extension(IServiceCollection services)
{
    public IServiceCollection AddAxisLogger() { … }
    public IServiceCollection AddLoggingBehavior() { … }
}
```

> `AddAxisLogger()` calls `services.AddLogging()` internally, so the underlying `ILogger<T>` pipeline is ready even if you forgot to register it.

---

## Logging structured entries

```csharp
public class CreatePersonHandler(IAxisLogger<CreatePersonHandler> logger, ...)
{
    public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
    {
        logger.LogInformation("Creating person",
            ("Document", cmd.Document),
            ("Tenant",   cmd.Tenant));

        // … pipeline
    }
}
```

Every entry is automatically enriched with `UtcTime`, `OriginId`, `TraceId` and `JourneyId` resolved from the ambient `IAxisMediator`. Your sink sees a single structured object per line — never a templated string with values inlined.

---

## Logging an `AxisResult` outcome

```csharp
return factory.CreateAsync(cmd)
    .ThenAsync(person => uow.SaveChangesAsync())
    .TapAsync(r => logger.LogResult("CreatePerson", r))
    .MapAsync(_ => new CreatePersonResponse { … });
```

**Why it pays off:** `LogResult` picks `Information` on success and `Error` on failure, attaches `Tag`, `RequestName` and (on failure) the full `AxisErrorList` as a structured property. One method, one decision-free log call.

---

## Turning on automatic request logging

```csharp
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddLoggingBehavior();   // adds IAxisPipelineBehavior<TRequest> / <TRequest, TResponse>
```

Now every mediator request logs `Handling {RequestName}` with structured properties at the top of every handler — no per-handler boilerplate.

---

## See also

- [The `IAxisLogger<T>` contract](iaxislogger.md) — every overload
- [`LogResult` — structured outcomes](log-result.md) — the railway companion
- [`LoggingBehavior` — automatic request logging](logging-behavior.md) — opt-in mediator pipeline
- [Categories and structured properties](categories.md) — what `T` does
- [Why AxisLogger?](why-axislogger.md) — the case against `ILogger<T>` directly
- [API reference](api-reference.md) — every member in one place

---

↩ [Back to AxisLogger docs](README.md)
