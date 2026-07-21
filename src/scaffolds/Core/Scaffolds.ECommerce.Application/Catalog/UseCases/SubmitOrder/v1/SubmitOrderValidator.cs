using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.SubmitOrder;
using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;

namespace Scaffolds.ECommerce.Application.Catalog.UseCases.SubmitOrder.v1;

internal sealed class SubmitOrderValidator : AxisValidatorBase<SubmitOrderCommand>
{
    public SubmitOrderValidator()
    {
        Range<int>(x => x.Quantity, CatalogErrors.QuantityMustBePositive, min: 1);
        RequiredTryParse(x => x.CartId, CatalogErrors.CartIdInvalid, CartId.TryParse);
    }
}
