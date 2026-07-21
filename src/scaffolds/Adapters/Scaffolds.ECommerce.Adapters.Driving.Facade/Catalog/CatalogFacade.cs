using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1._Shared;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Checkout;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.CreateProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.GetProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.RegisterProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.SubmitOrder;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade.Catalog;

internal sealed class CatalogFacade(IAxisMediator mediator) : ICatalogFacade
{
    #region scaffold:catalog-facade
    public Task<AxisResult<GetProductResponse>> GetProductAsync(GetProductQuery query)
        => mediator.Cqrs.QueryAsync<GetProductQuery, GetProductResponse>(query);

    public Task<AxisResult<CheckoutResponse>> CheckoutAsync(CheckoutCommand command)
        => mediator.Cqrs.ExecuteAsync<CheckoutCommand, CheckoutResponse>(command);

    public Task<AxisResult<CreateProductResponse>> CreateProductAsync(CreateProductCommand command)
        => mediator.Cqrs.ExecuteAsync<CreateProductCommand, CreateProductResponse>(command);

    public Task<AxisResult<RegisterProductResponse>> RegisterProductAsync(RegisterProductCommand command)
        => mediator.Cqrs.ExecuteAsync<RegisterProductCommand, RegisterProductResponse>(command);

    public Task<AxisResult<SubmitOrderResponse>> SubmitOrderAsync(SubmitOrderCommand command)
        => mediator.Cqrs.ExecuteAsync<SubmitOrderCommand, SubmitOrderResponse>(command);
    #endregion
}
