using Scaffolds.ECommerce.Contracts.Driven.Catalog.CartItems;
using Scaffolds.ECommerce.Contracts.Driven.Catalog.Orders;
using Scaffolds.ECommerce.Domain.Catalog.Orders;
using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Orders.Aggregates;

internal interface IOrderFactory
{
    sealed record NewArgs
    {
        public required CustomerId CustomerId { get; init; }
        public required CartId CartId { get; init; }
        public required int Quantity { get; init; }
    }

    Task<AxisResult<IOrderEntityProperties>> CreateAsync(NewArgs args);
}

internal sealed class OrderFactory(
    ICartItemsReaderPort cartItems,
    IOrdersWritePort orders
) : IOrderFactory
{
    public Task<AxisResult<IOrderEntityProperties>> CreateAsync(IOrderFactory.NewArgs args)
        => cartItems.GetByCartIdAsync(args.CartId)
            .MapAsync(IOrderEntityProperties (cartItem) => new OrderProperties(
                OrderId.New,
                args.CustomerId,
                cartItem.ProductId,
                args.Quantity,
                cartItem.CartId))
            .ThenAsync(orders.CreateAsync);
}
