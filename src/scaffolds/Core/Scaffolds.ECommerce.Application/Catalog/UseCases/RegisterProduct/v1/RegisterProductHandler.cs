using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.RegisterProduct;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.Application.Catalog.UseCases.RegisterProduct.v1;

internal sealed class RegisterProductHandler(
    IProductsPort products, 
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<RegisterProductCommand, RegisterProductResponse>
{
    #region scaffold:register-product
    public Task<AxisResult<RegisterProductResponse>> HandleAsync(RegisterProductCommand command)
        =>  products
            .CreateAsync(new ProductProperties(ProductId.New, command.Sku, command.Name!, command.InitialStock))
            .RecoverConflictAsync(() => products.GetBySkuAsync(command.Sku))
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .MapAsync(product => new RegisterProductResponse { ProductId = product.ProductId });
    #endregion
}
