using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.SubmitOrder;
using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Orders.UseCases.SubmitOrder.v1;

internal sealed class SubmitOrderValidator : AxisValidatorBase<SubmitOrderCommand>
{
    public SubmitOrderValidator()
    {
        RequiredRange(x => x.Quantity, SharedErrors.QuantityMustBePositive, min: 1);
        RequiredTryParse(x => x.CartId, SharedErrors.CartIdInvalid, CartId.TryParse);
    }
}
