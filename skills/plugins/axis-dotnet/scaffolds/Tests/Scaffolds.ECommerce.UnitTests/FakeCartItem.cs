using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.UnitTests;

internal sealed record FakeCartItem(
    CartId CartId,
    ProductId ProductId,
    int Quantity
) : ICartItemEntityProperties;
