# Primeiros passos · instalação e uso

> Instale as packages, registre o mediator, scaneie handlers, escreva um command, despache — caminho de cinco minutos de zero a um pipeline tipado.

---

## Instalação

```
dotnet add package AxisMediator              # o core do mediator
dotnet add package AxisMediator.Contracts    # marker interfaces (trazida transitivamente pelo core)
```

`AxisMediator` depende de `AxisResult` (tipos de retorno) e `AxisLogger` (o dispatcher loga cada request tratado).

---

## Registrando

```csharp
using AxisMediator;
using System.Reflection;

builder.Services
    .AddAxisMediator()                                          // mediator + accessors + dispatcher
    .AddCqrsMediator(Assembly.GetExecutingAssembly());           // scaneia handlers
```

`AddAxisMediator()` registra:

- `IAxisMediatorHandler` → `AxisMediatorHandler` (scoped)
- `IAxisMediator` → `AxisMediator` (scoped)
- `IAxisMediatorAccessor` → `AxisMediatorAccessor` (singleton)
- `IAxisMediatorContextAccessor` → `AxisMediatorContextAccessor` (singleton, backed por `AsyncLocal`)

`AddCqrsMediator(assembly)` scaneia o assembly por cada implementação de `IAxisCommand<>`, `IAxisCommandHandler<>`, `IAxisCommandHandler<,>`, `IAxisQueryHandler<,>`, `IAxisStreamQueryHandler<,>`, `IAxisEventHandler<>` e os registra como transient contra suas interfaces.

---

## Escrevendo um command + handler

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

O scanner acha o handler automaticamente.

Um handler lê o chamador autenticado a partir do `IAxisMediator` ambiente e o protege antes de tocar em qualquer port — o token nunca é parâmetro, e o context accessor gravável nunca é injetado:

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

## Despachando

O dispatch é emitido a partir de um Facade — nunca de dentro de um handler (um handler que recorre a `mediator.Cqrs` é sinalizado pelo AXIS0401):

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

`IAxisMediator.Cqrs` expõe o dispatcher. Os quatro métodos (`ExecuteAsync` × 2, `QueryAsync`, `StreamAsync`) constroem o pipeline e chamam o handler.

---

## Adicionando um pipeline behaviour

```csharp
builder.Services.AddAxisLogger();                             // te dá IAxisLogger<T>
builder.Services.AddLoggingBehavior();                        // loga Handling X automaticamente
builder.Services.AddAxisValidator(Assembly.GetExecutingAssembly());  // behaviour FluentValidation
builder.Services.AddOpenTelemetryAxis();                      // te dá IAxisTelemetry / IAxisMetrics
builder.Services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
builder.Services.AddPerformanceBehavior();                    // o alerta de slow-request da caixa
```

Um behaviour implementa uma das duas sobrecargas de `IAxisPipelineBehavior`, compartilha estado através do `AxisPipelineContext` sob uma constante de chave, e chama `next` exatamente uma vez:

<!-- scaffold:pipeline-behavior -->

```csharp
public Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
{
    context.Set(PipelineKeys.Actor, mediator.AxisEntityId);
    return next();
}
```

<!-- /scaffold -->

**Por que compensa:** o handler fica inalterado. Logging, validação, telemetria e o alerta de slow-request embrulham o pipeline transparentemente; o handler lê como se nada disso existisse.

---

## Veja também

- [CQRS · commands, queries, streams, eventos](cqrs.md) — modele a request
- [O mediator e os accessors](mediator-and-accessors.md) — o contexto ambiente
- [Despachando · `IAxisMediatorHandler`](dispatching.md) — os quatro métodos
- [Pipeline behaviours](pipeline-behaviors.md) — escreva código transversal
- [Pipeline context](pipeline-context.md) — compartilhe state entre behaviours
- [Registro e scanning](registration.md) — o que cada `Add*` faz
- [Por que AxisMediator?](why-axismediator.md) — o argumento contra MediatR
- [Referência da API](api-reference.md) — cada membro num só lugar

---

↩ [Voltar à documentação do AxisMediator](README.md)
