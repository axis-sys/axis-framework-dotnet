using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CreateProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.RegisterProduct;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade.Catalog.Products;

internal sealed class ProductsFacade(IAxisMediator mediator) : IProductsFacade
{
    public Task<AxisResult<GetProductResponse>> GetProductAsync(GetProductQuery query)
        => mediator.Cqrs.QueryAsync<GetProductQuery, GetProductResponse>(query);

    public Task<AxisResult<CreateProductResponse>> CreateProductAsync(CreateProductCommand command)
        => mediator.Cqrs.ExecuteAsync<CreateProductCommand, CreateProductResponse>(command);

    public Task<AxisResult<RegisterProductResponse>> RegisterProductAsync(RegisterProductCommand command)
        => mediator.Cqrs.ExecuteAsync<RegisterProductCommand, RegisterProductResponse>(command);

    public Task<AxisResult<CheckoutProductResponse>> CheckoutProductAsync(CheckoutProductCommand command)
        => mediator.Cqrs.ExecuteAsync<CheckoutProductCommand, CheckoutProductResponse>(command);
}
