# Axis Framework

**An opinionated .NET framework for hexagonal, DDD and CQRS applications**, built on Railway-Oriented Programming.
Expected failures travel as `AxisResult` values instead of exceptions, every piece of infrastructure is a swappable
port, and every use case is a vertical slice — command or query, handler, validator — exposed by a facade.

```csharp
public Task<AxisResult<CheckoutProductResponse>> HandleAsync(CheckoutProductCommand command)
{
    var productId = (ProductId)command.ProductId!;

    return mediator.AxisEntityId.Rop()
        .Ensure(caller => caller.HasValue, SharedErrors.CustomerIdentityRequired)
        .ThenAsync(_ => unitOfWork.RenewAsync())
        .ThenAsync(_ => productsWrite.GetByIdForUpdateAsync(productId))
        .EnsureAsync(product => product.Stock >= command.Quantity, ProductErrors.InsufficientStock)
        .ThenAsync(product => productsWrite.ReserveStockAsync(product.ProductId, command.Quantity!.Value))
        .ThenAsync(product => bus.PublishAsync(
                new ProductCheckedOutEvent(
                    command.CartId!,
                    product.ProductId,
                    command.Quantity!.Value
                ), ProductCheckedOutEvent.Topic))
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .MapAsync(product => new CheckoutProductResponse
        {
            CustomerId = mediator.AxisEntityId!.Value,
            ProductId = product.ProductId,
        });
}
```

The checkout use case of the reference application
([`CheckoutProductHandler.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductHandler.cs)):
one railway from the caller's identity to the response, with no `try/catch`, no `if` and one commit at the end.

## Where to start

| You want to… | Go to |
|---|---|
| learn how an Axis application is laid out and written | the [application guide](guide/index.md) |
| use one package: its concepts, operators and pitfalls | [Packages](articles/index.md) |
| look up the contract of a type or member | the [API reference](api/index.md) |
| see all of it in working code | the [reference application](https://github.com/axis-sys/axis-framework-dotnet/tree/main/scaffolds) |

## How it is distributed

Each package lives in its own repository under [axis-sys](https://github.com/axis-sys), with its code, tests,
articles and a usage agent, and ships to NuGet with the same id (`AxisResult`, `AxisMediator`, `AxisSaga`...). The
[hub](https://github.com/axis-sys/axis-framework-dotnet) holds the reference application, which consumes every
package from NuGet exactly as your application would, this site, and the
[conventions analyzers](guide/analyzers.md) (`Axis.Conventions.Analyzers`), which it releases and runs on the
reference application.

## License

Apache-2.0.
