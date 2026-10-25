namespace Scaffolds.ECommerce.UnitTests;

internal sealed record FakeProduct(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;
