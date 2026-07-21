using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.RegisterProduct;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.Application.Catalog.UseCases.RegisterProduct.v1;

internal sealed class RegisterProductValidator : AxisValidatorBase<RegisterProductCommand>
{
    public RegisterProductValidator()
    {
        RequiredTryParse(x => x.Sku, CatalogErrors.SkuInvalid, Sku.TryParse);
        RequiredWithMaxLength(x => x.Name, CatalogErrors.NameRequired);
        Range<int>(x => x.InitialStock, CatalogErrors.InitialStockInvalid, min: 0);
    }
}
