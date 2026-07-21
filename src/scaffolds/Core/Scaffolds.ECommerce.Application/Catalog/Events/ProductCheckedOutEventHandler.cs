using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Events;
using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.Application.Catalog.Events;

#region scaffold:product-checked-out-consumer
internal sealed class ProductCheckedOutEventHandler(
    ICartItemsPort cartItems,
    IUnitOfWork unitOfWork,
    IAxisLogger<ProductCheckedOutEventHandler> logger
) : IAxisEventHandler<ProductCheckedOutEvent>
{
    public Task<AxisResult> HandleAsync(ProductCheckedOutEvent @event)
    {
        if (!CartId.TryParse(@event.CartId, out var cartId) || !ProductId.TryParse(@event.ProductId, out var productId))
        {
            // A malformed bus payload is a poison message, not a transient failure: log it and return Ok so
            // the dispatcher never retries it (architecture-bus-events — a consumer's failure never blocks
            // the publisher, but a permanent failure must not be retried forever either).
            logger.LogWarning("Discarding ProductCheckedOutEvent with a malformed id", ("cartId", @event.CartId), ("productId", @event.ProductId));
            return AxisResult.Ok().AsTaskAsync();
        }

        var cartItem = new CartItemProperties(cartId, productId, @event.Quantity);
        return cartItems.CreateAsync(cartItem)
            .RecoverConflictAsync(() => cartItems.UpdateAsync(cartItem))
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .ToAxisResultAsync();
    }
}
#endregion
