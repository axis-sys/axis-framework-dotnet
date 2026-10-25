using Scaffolds.ECommerce.Domain.Catalog.Products;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;

public interface IProductsWritePort
{
    Task<AxisResult<IProductEntityProperties>> CreateAsync(IProductEntityProperties product);
    Task<AxisResult<IProductEntityProperties>> GetByIdForUpdateAsync(ProductId productId);
    Task<AxisResult> ReserveStockAsync(ProductId productId, int quantity);
}
