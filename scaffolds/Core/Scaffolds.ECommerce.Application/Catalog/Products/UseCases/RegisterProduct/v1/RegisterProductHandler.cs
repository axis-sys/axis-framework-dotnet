using Scaffolds.ECommerce.Application.Catalog.Products.Aggregates;
using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.RegisterProduct;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Products.UseCases.RegisterProduct.v1;

internal sealed class RegisterProductHandler(
    IProductsReaderPort productsReader,
    IProductFactory productFactory,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<RegisterProductCommand, RegisterProductResponse>
{
    public Task<AxisResult<RegisterProductResponse>> HandleAsync(RegisterProductCommand command)
    {
        var sku = (Sku)command.Sku!;

        return productFactory
            .CreateAsync(new IProductFactory.NewArgs
            {
                Sku = sku,
                Name = command.Name!,
                InitialStock = command.InitialStock!.Value,
            })
            .RecoverConflictAsync(() => productsReader.GetBySkuAsync(sku))
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .MapAsync(product => new RegisterProductResponse { ProductId = product.ProductId });
    }
}
