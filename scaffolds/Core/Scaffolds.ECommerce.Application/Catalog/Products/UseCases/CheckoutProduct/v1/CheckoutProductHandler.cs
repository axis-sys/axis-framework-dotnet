using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;
using Scaffolds.ECommerce.SharedKernel.Catalog.Events.v1;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Products.UseCases.CheckoutProduct.v1;

internal sealed class CheckoutProductHandler(
    IAxisMediator mediator,
    IProductsWritePort productsWrite,
    IAxisBus bus,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<CheckoutProductCommand, CheckoutProductResponse>
{
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
}
