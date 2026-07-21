using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.GetProduct;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.Application.Catalog.UseCases.GetProduct.v1;

internal sealed class GetProductValidator : AxisValidatorBase<GetProductQuery>
{
    public GetProductValidator()
    {
        RequiredTryParse(x => x.ProductId, CatalogErrors.ProductIdInvalid, ProductId.TryParse);
    }
}
