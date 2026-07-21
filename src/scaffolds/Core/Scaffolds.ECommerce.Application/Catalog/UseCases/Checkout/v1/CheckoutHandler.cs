using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Checkout;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Events;

namespace Scaffolds.ECommerce.Application.Catalog.UseCases.Checkout.v1;

internal sealed class CheckoutHandler(
    IAxisMediator mediator,
    IProductsPort products,
    IAxisBus bus,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<CheckoutCommand, CheckoutResponse>
{
    #region scaffold:checkout
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
    #endregion
}
