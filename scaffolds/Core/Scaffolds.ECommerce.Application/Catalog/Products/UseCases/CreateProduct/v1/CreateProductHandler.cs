using Scaffolds.ECommerce.Application.Catalog.Products.Aggregates;
using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CreateProduct;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Products.UseCases.CreateProduct.v1;

internal sealed class CreateProductHandler(
    IProductsReaderPort productsReader,
    IProductFactory productFactory,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<CreateProductCommand, CreateProductResponse>
{
    public Task<AxisResult<CreateProductResponse>> HandleAsync(CreateProductCommand command)
    {
        var sku = (Sku)command.Sku!;

        return productsReader
            .GetBySkuAsync(sku)
            .RequireNotFoundAsync(ProductErrors.SkuAlreadyExists)
            .ThenAsync(() => productFactory.CreateAsync(new IProductFactory.NewArgs
            {
                Sku = sku,
                Name = command.Name!,
                InitialStock = command.InitialStock!.Value,
            }))
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .MapAsync(product => new CreateProductResponse { ProductId = product.ProductId });
    }
}
