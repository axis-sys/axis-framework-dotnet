using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.Contracts.Driven.Catalog;

public interface IProductsPort
{
    Task<AxisResult<IProductEntityProperties>> GetByIdAsync(ProductId productId);
    Task<AxisResult<IProductEntityProperties>> GetBySkuAsync(Sku sku);
    Task<AxisResult> ReserveStockAsync(ProductId productId, int quantity);
    Task<AxisResult<IProductEntityProperties>> CreateAsync(IProductEntityProperties properties);
}
