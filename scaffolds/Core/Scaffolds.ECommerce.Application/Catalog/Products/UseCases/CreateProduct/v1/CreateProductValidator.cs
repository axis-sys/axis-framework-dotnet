using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CreateProduct;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Products.UseCases.CreateProduct.v1;

internal sealed class CreateProductValidator : AxisValidatorBase<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RequiredTryParse(x => x.Sku, ProductErrors.SkuInvalid, Sku.TryParse);
        RequiredWithMaxLength(x => x.Name, SharedErrors.NameRequired);
        RequiredRange(x => x.InitialStock, ProductErrors.InitialStockInvalid, min: 0);
    }
}
