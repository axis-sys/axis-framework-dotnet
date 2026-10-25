using Scaffolds.ECommerce.Application.Catalog.Orders.Aggregates;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.SubmitOrder;
using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Orders.UseCases.SubmitOrder.v1;

internal sealed class SubmitOrderHandler(
    IAxisMediator mediator,
    IOrderFactory orderFactory,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<SubmitOrderCommand, SubmitOrderResponse>
{
    public Task<AxisResult<SubmitOrderResponse>> HandleAsync(SubmitOrderCommand command)
    {
        var cartId = (CartId)command.CartId!;
        var callerId = mediator.AxisEntityId is { } caller ? (CustomerId?)caller.ToString() : null;

        return callerId.Rop()
            .Ensure(customerId => customerId.HasValue, SharedErrors.CustomerIdentityRequired)
            .ThenAsync(customerId => orderFactory.CreateAsync(new IOrderFactory.NewArgs
            {
                CustomerId = customerId!.Value,
                CartId = cartId,
                Quantity = command.Quantity!.Value,
            }))
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .MapAsync(order => new SubmitOrderResponse { OrderId = order.OrderId, ProductId = order.ProductId, Quantity = order.Quantity });
    }
}
