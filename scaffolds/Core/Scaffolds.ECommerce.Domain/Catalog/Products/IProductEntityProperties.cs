using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Domain.Catalog.Products;

public interface IProductEntityProperties
{
    ProductId ProductId { get; }
    Sku Sku { get; }
    string Name { get; }
    int Stock { get; }
}
