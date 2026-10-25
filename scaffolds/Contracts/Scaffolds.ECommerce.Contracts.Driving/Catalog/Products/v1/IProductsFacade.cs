using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CreateProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.RegisterProduct;

namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;

public interface IProductsFacade
{
    Task<AxisResult<GetProductResponse>> GetProductAsync(GetProductQuery query);
    Task<AxisResult<CreateProductResponse>> CreateProductAsync(CreateProductCommand command);
    Task<AxisResult<RegisterProductResponse>> RegisterProductAsync(RegisterProductCommand command);
    Task<AxisResult<CheckoutProductResponse>> CheckoutProductAsync(CheckoutProductCommand command);
}
