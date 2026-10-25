using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Domain.Catalog.CartItems;

internal sealed record CartItemProperties(
    CartId CartId,
    ProductId ProductId,
    int Quantity
) : ICartItemEntityProperties;
