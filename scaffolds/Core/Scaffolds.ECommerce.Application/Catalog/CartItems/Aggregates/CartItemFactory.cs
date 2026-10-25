using Scaffolds.ECommerce.Contracts.Driven.Catalog.CartItems;
using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Domain.Catalog.CartItems;
using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.CartItems.Aggregates;

internal interface ICartItemFactory
{
    sealed record NewArgs
    {
        public required CartId CartId { get; init; }
        public required ProductId ProductId { get; init; }
        public required int Quantity { get; init; }
    }

    Task<AxisResult<ICartItemEntityProperties>> CreateAsync(NewArgs args);
}

internal sealed class CartItemFactory(
    IProductsReaderPort products,
    ICartItemsWritePort cartItems
) : ICartItemFactory
{
    public Task<AxisResult<ICartItemEntityProperties>> CreateAsync(ICartItemFactory.NewArgs args)
        => products.GetByIdAsync(args.ProductId)
            .MapAsync(ICartItemEntityProperties (product) => new CartItemProperties(args.CartId, product.ProductId, args.Quantity))
            .ThenAsync(cartItem => cartItems.CreateAsync(cartItem).RecoverConflictAsync(() => cartItems.UpdateAsync(cartItem)));
}
