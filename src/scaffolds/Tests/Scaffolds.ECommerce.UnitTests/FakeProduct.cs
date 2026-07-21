using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.UnitTests;

internal sealed record FakeProduct(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;
