using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Domain.Catalog.Products;

internal sealed record ProductProperties(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;
