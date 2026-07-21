using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.SubmitOrder;
using OrderId = Scaffolds.ECommerce.SharedKernel.ContextIds.OrderId;

namespace Scaffolds.ECommerce.Application.Catalog.UseCases.SubmitOrder.v1;

internal sealed class SubmitOrderHandler(
    IAxisMediator mediator,
    ICartItemsPort cartItems,
    IOrdersPort orders,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<SubmitOrderCommand, SubmitOrderResponse>
{
    #region scaffold:validate-order
    public Task<AxisResult<SubmitOrderResponse>> HandleAsync(SubmitOrderCommand command)
        => cartItems.GetByCartIdAsync(command.CartId)
            .MapAsync(IOrderEntityProperties (cartItem) => new OrderProperties(
                OrderId.New,
                CustomerId: mediator.AxisEntityId,
                cartItem.ProductId,
                command.Quantity,
                cartItem.CartId))
            .ThenAsync(orders.CreateAsync)
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .MapAsync(order => new SubmitOrderResponse { OrderId = order.OrderId, ProductId = order.ProductId, Quantity = order.Quantity});
    #endregion
}
