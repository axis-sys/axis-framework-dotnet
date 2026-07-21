using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.Domain.Catalog.Products;

internal sealed record ProductProperties(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;
