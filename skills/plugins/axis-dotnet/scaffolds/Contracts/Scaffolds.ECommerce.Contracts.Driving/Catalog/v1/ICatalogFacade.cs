using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Checkout;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.CreateProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.GetProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.RegisterProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.SubmitOrder;

namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1;

public interface ICatalogFacade
{
    Task<AxisResult<GetProductResponse>> GetProductAsync(GetProductQuery query);
    Task<AxisResult<CheckoutResponse>> CheckoutAsync(CheckoutCommand command);
    Task<AxisResult<CreateProductResponse>> CreateProductAsync(CreateProductCommand command);
    Task<AxisResult<RegisterProductResponse>> RegisterProductAsync(RegisterProductCommand command);
    Task<AxisResult<SubmitOrderResponse>> SubmitOrderAsync(SubmitOrderCommand command);
}
