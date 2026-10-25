namespace Scaffolds.ECommerce.UnitTests;

internal sealed record FakeCartItem(
    CartId CartId,
    ProductId ProductId,
    int Quantity
) : ICartItemEntityProperties;
