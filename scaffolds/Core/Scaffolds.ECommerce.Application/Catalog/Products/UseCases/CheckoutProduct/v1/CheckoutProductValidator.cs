using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;
using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Products.UseCases.CheckoutProduct.v1;

internal sealed class CheckoutProductValidator : AxisValidatorBase<CheckoutProductCommand>
{
    public CheckoutProductValidator()
    {
        RequiredTryParse(x => x.CartId, SharedErrors.CartIdInvalid, CartId.TryParse);
        RequiredTryParse(x => x.ProductId, ProductErrors.ProductIdInvalid, ProductId.TryParse);
        RequiredRange(x => x.Quantity, SharedErrors.QuantityMustBePositive, min: 1);
    }
}
