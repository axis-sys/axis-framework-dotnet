# Getting started · installation and usage

> Install the packages, register the mediator, scan handlers, write a command, dispatch it — five-minute path from zero to a typed pipeline.

---

## Installation

```
dotnet add package AxisMediator              # the mediator core
dotnet add package AxisMediator.Contracts    # marker interfaces (transitively brought in by the core)
```

`AxisMediator` depends on `AxisResult` (return types) and `AxisLogger` (the dispatcher logs every handled request).

---

## Registering

```csharp
using AxisMediator;
using System.Reflection;

builder.Services
    .AddAxisMediator()                                          // mediator + accessors + dispatcher
    .AddCqrsMediator(Assembly.GetExecutingAssembly());           // scan handlers
```

`AddAxisMediator()` registers:

- `IAxisMediatorHandler` → `AxisMediatorHandler` (scoped)
- `IAxisMediator` → `AxisMediator` (scoped)
- `IAxisMediatorAccessor` → `AxisMediatorAccessor` (singleton)
- `IAxisMediatorContextAccessor` → `AxisMediatorContextAccessor` (singleton, `AsyncLocal`-backed)

`AddCqrsMediator(assembly)` scans the assembly for every implementation of `IAxisCommand<>`, `IAxisCommandHandler<>`, `IAxisCommandHandler<,>`, `IAxisQueryHandler<,>`, `IAxisStreamQueryHandler<,>`, `IAxisEventHandler<>` and registers them as transient against their interface.

---

## Writing a command + handler

```csharp
using Axis;
using AxisMediator.Contracts.CQRS.Commands;

public record CreateOrderCommand(AxisEntityId CustomerId, AxisEntityId ProductId, int Quantity)
    : IAxisCommand<CreateOrderResponse>;

public record CreateOrderResponse(AxisEntityId OrderId) : IAxisCommandResponse;

public class CreateOrderHandler(IOrderFactory factory, IUnitOfWork uow)
    : IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
        => factory.CreateAsync(cmd)
            .ThenAsync(order => uow.SaveChangesAsync().Map(_ => order))
            .MapAsync(order => new CreateOrderResponse(order.OrderId));
}
```

The scanner finds the handler automatically.

A handler reads the authenticated caller from the ambient `IAxisMediator` and guards it before touching a port — the token is never a parameter, and the writable context accessor is never injected:

<!-- scaffold:checkout -->

```csharp
public Task<AxisResult<CheckoutResponse>> HandleAsync(CheckoutCommand command)
{
    return products
        .GetByIdAsync(command.ProductId)
        .EnsureAsync(product => product.Stock >= command.Quantity, AxisError.BusinessRule(CatalogErrors.InsufficientStock))
        .ThenAsync(product => products.ReserveStockAsync(product.ProductId, command.Quantity))
        // Publish before SaveChangesAsync: the atomic outbox drains the enqueued event in the very same
        // transaction (architecture-events-published-in-unit-of-work). The cart consumer picks this up
        // out of the band and associates the reserved product with the cart (architecture-bus-events).
        .ThenAsync(product => bus.PublishAsync(
                new ProductCheckedOutEvent(
                    command.CartId,
                    product.ProductId.ToString(),
                    command.Quantity
                ), ProductCheckedOutEvent.Topic))
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .MapAsync(product => new CheckoutResponse
        {
            Customer = mediator.AxisEntityId!.Value,
            ProductId = product.ProductId,
        });
}
```

<!-- /scaffold -->

---

## Dispatching

Dispatch is issued from a Facade — never from inside a handler (a handler that reaches for `mediator.Cqrs` is flagged by AXIS0401):

<!-- scaffold:catalog-facade -->

```csharp
public Task<AxisResult<GetProductResponse>> GetProductAsync(GetProductQuery query)
    => mediator.Cqrs.QueryAsync<GetProductQuery, GetProductResponse>(query);

public Task<AxisResult<CheckoutResponse>> CheckoutAsync(CheckoutCommand command)
    => mediator.Cqrs.ExecuteAsync<CheckoutCommand, CheckoutResponse>(command);

public Task<AxisResult<CreateProductResponse>> CreateProductAsync(CreateProductCommand command)
    => mediator.Cqrs.ExecuteAsync<CreateProductCommand, CreateProductResponse>(command);

public Task<AxisResult<RegisterProductResponse>> RegisterProductAsync(RegisterProductCommand command)
    => mediator.Cqrs.ExecuteAsync<RegisterProductCommand, RegisterProductResponse>(command);

public Task<AxisResult<SubmitOrderResponse>> SubmitOrderAsync(SubmitOrderCommand command)
    => mediator.Cqrs.ExecuteAsync<SubmitOrderCommand, SubmitOrderResponse>(command);
```

<!-- /scaffold -->

`IAxisMediator.Cqrs` exposes the dispatcher. The four methods (`ExecuteAsync` × 2, `QueryAsync`, `StreamAsync`) build the pipeline and call the handler.

---

## Adding a pipeline behaviour

```csharp
builder.Services.AddAxisLogger();                             // gives you IAxisLogger<T>
builder.Services.AddLoggingBehavior();                        // logs Handling X automatically
builder.Services.AddAxisValidator(Assembly.GetExecutingAssembly());  // FluentValidation behaviour
builder.Services.AddOpenTelemetryAxis();                      // gives you IAxisTelemetry / IAxisMetrics
builder.Services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
builder.Services.AddPerformanceBehavior();                    // the in-box slow-request warning
```

A behaviour implements one of the two `IAxisPipelineBehavior` overloads, shares state through `AxisPipelineContext` under a key constant, and calls `next` exactly once:

<!-- scaffold:pipeline-behavior -->

```csharp
public Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
{
    context.Set(PipelineKeys.Actor, mediator.AxisEntityId);
    return next();
}
```

<!-- /scaffold -->

**Why it pays off:** the handler is unchanged. Logging, validation, telemetry and a slow-request warning all wrap the pipeline transparently; the handler reads as if none of that existed.

---

## See also

- [CQRS · commands, queries, streams, events](cqrs.md) — model the request
- [The mediator and the accessors](mediator-and-accessors.md) — the ambient context
- [Dispatching · `IAxisMediatorHandler`](dispatching.md) — the four methods
- [Pipeline behaviours](pipeline-behaviors.md) — write cross-cutting code
- [Pipeline context](pipeline-context.md) — share state between behaviours
- [Registration & scanning](registration.md) — what each `Add*` does
- [Why AxisMediator?](why-axismediator.md) — the case against MediatR
- [API reference](api-reference.md) — every member in one place

---

↩ [Back to AxisMediator docs](README.md)
