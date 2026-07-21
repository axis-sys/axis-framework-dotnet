using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.IntegrationTests;

internal sealed record FakeProduct(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;

internal static class TestData
{
    public static Sku NewSku() => (Sku)$"SKU-{Guid.NewGuid():N}";
}
