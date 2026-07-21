using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.Domain.Catalog.Products;

public interface IProductEntityProperties
{
    ProductId ProductId { get; }
    Sku Sku { get; }
    string Name { get; }
    int Stock { get; }
}
