using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Products.UseCases.GetProduct.v1;

internal sealed class GetProductValidator : AxisValidatorBase<GetProductQuery>
{
    public GetProductValidator()
    {
        RequiredTryParse(x => x.ProductId, ProductErrors.ProductIdInvalid, ProductId.TryParse);
    }
}
