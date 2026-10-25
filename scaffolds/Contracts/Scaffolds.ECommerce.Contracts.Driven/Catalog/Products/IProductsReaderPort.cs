using Scaffolds.ECommerce.Domain.Catalog.Products;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;

public interface IProductsReaderPort
{
    Task<AxisResult<IProductEntityProperties>> GetByIdAsync(ProductId productId);
    Task<AxisResult<IProductEntityProperties>> GetBySkuAsync(Sku sku);
}
