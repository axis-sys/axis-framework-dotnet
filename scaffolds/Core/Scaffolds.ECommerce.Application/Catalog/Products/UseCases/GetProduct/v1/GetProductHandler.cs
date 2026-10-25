using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Products.UseCases.GetProduct.v1;

internal sealed class GetProductHandler(IProductsReaderPort productsReader) : IAxisQueryHandler<GetProductQuery, GetProductResponse>
{
    public Task<AxisResult<GetProductResponse>> HandleAsync(GetProductQuery query)
    {
        var productId = (ProductId)query.ProductId!;

        return productsReader.GetByIdAsync(productId)
            .MapAsync(product => new GetProductResponse
            {
                ProductId = product.ProductId,
                Name = product.Name,
                Stock = product.Stock,
            });
    }
}
