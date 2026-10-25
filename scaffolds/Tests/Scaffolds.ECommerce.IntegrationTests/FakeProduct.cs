namespace Scaffolds.ECommerce.IntegrationTests;

internal sealed record FakeProduct(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;
