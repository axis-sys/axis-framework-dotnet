using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.RegisterProduct;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Products.UseCases.RegisterProduct.v1;

internal sealed class RegisterProductValidator : AxisValidatorBase<RegisterProductCommand>
{
    public RegisterProductValidator()
    {
        RequiredTryParse(x => x.Sku, ProductErrors.SkuInvalid, Sku.TryParse);
        RequiredWithMaxLength(x => x.Name, SharedErrors.NameRequired);
        RequiredRange(x => x.InitialStock, ProductErrors.InitialStockInvalid, min: 0);
    }
}
