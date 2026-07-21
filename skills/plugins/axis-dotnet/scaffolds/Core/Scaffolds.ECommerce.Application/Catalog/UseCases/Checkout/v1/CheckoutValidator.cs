using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Checkout;
using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.Application.Catalog.UseCases.Checkout.v1;

internal sealed class CheckoutValidator : AxisValidatorBase<CheckoutCommand>
{
    public CheckoutValidator()
    {
        RequiredTryParse(x => x.CartId, CatalogErrors.CartIdInvalid, CartId.TryParse);
        RequiredTryParse(x => x.ProductId, CatalogErrors.ProductIdInvalid, ProductId.TryParse);
        Range<int>(x => x.Quantity, CatalogErrors.QuantityMustBePositive, min: 1);
    }
}
