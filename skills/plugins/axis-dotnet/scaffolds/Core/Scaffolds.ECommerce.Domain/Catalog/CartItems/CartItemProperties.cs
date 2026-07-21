using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.Domain.Catalog.CartItems;

internal sealed record CartItemProperties(
    CartId CartId,
    ProductId ProductId,
    int Quantity
) : ICartItemEntityProperties;
