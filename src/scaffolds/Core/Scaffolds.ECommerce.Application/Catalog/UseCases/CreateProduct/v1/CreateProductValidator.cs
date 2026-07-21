using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.CreateProduct;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.Application.Catalog.UseCases.CreateProduct.v1;

internal sealed class CreateProductValidator : AxisValidatorBase<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RequiredTryParse(x => x.Sku, CatalogErrors.SkuInvalid, Sku.TryParse);
        RequiredWithMaxLength(x => x.Name, CatalogErrors.NameRequired);
        Range<int>(x => x.InitialStock, CatalogErrors.InitialStockInvalid, min: 0);
    }
}
