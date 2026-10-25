using Scaffolds.ECommerce.Application.Catalog.CartItems.Aggregates;
using Scaffolds.ECommerce.Contracts.Driven.Catalog.CartItems;
using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Events.v1;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.CartItems.Events;

internal sealed class ProductCheckedOutEventHandler(
    ICartItemFactory cartItemFactory,
    IUnitOfWork unitOfWork,
    IAxisLogger<ProductCheckedOutEventHandler> logger
) : IAxisEventHandler<ProductCheckedOutEvent>
{
    public async Task<AxisResult> HandleAsync(ProductCheckedOutEvent @event)
    {
        var associated = await NewCartItemOf(@event)
            .ThenAsync(cartItemFactory.CreateAsync)
            .ThenAsync(_ => unitOfWork.SaveChangesAsync());

        logger.LogResult(ProductCheckedOutEvent.Topic, associated);
        return AxisResult.Ok();
    }

    private static AxisResult<ICartItemFactory.NewArgs> NewCartItemOf(ProductCheckedOutEvent @event)
        => CartId.TryParse(@event.CartId, out var cartId) && ProductId.TryParse(@event.ProductId, out var productId)
            ? new ICartItemFactory.NewArgs { CartId = cartId, ProductId = productId, Quantity = @event.Quantity }
            : CartItemErrors.ProductCheckedOutEventMalformed;
}
