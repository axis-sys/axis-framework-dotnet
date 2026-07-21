using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.GetProduct;

namespace Scaffolds.ECommerce.Application.Catalog.UseCases.GetProduct.v1;

internal sealed class GetProductHandler(IProductsPort products) : IAxisQueryHandler<GetProductQuery, GetProductResponse>
{
    #region scaffold:get-product

    public Task<AxisResult<GetProductResponse>> HandleAsync(GetProductQuery query)
        => products.GetByIdAsync(query.ProductId)
            .MapAsync(product => new GetProductResponse
            {
                ProductId = product.ProductId,
                Name = product.Name,
                Stock = product.Stock,
            });
    #endregion
}
    