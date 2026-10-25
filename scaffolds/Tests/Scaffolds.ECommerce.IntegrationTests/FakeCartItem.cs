namespace Scaffolds.ECommerce.IntegrationTests;

internal sealed record FakeCartItem(
    CartId CartId,
    ProductId ProductId,
    int Quantity
) : ICartItemEntityProperties;
