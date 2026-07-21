using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.CreateProduct;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.Application.Catalog.UseCases.CreateProduct.v1;

internal sealed class CreateProductHandler(
    IProductsPort products,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<CreateProductCommand, CreateProductResponse>
{
    #region scaffold:create-product
    // Guard ("must-not-exist") variant: read first, and RequireNotFound turns a hit into a conflict so
    // creation short-circuits — a duplicate SKU fails, it is not silently recovered. The idempotent dual
    // (create-first, recover the existing on conflict) is RegisterProductHandler / RecoverConflictAsync.
    public Task<AxisResult<CreateProductResponse>> HandleAsync(CreateProductCommand command)
        =>  products
            .GetBySkuAsync(command.Sku)
            .RequireNotFoundAsync(AxisError.Conflict(CatalogErrors.SkuAlreadyExists))
            .WithValueAsync<IProductEntityProperties>(new ProductProperties(ProductId.New, command.Sku, command.Name!, command.InitialStock))
            .ThenAsync(products.CreateAsync)
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .MapAsync(product => new CreateProductResponse { ProductId = product.ProductId });
    #endregion
}
