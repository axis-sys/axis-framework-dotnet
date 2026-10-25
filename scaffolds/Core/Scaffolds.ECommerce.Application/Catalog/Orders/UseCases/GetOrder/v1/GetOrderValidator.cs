using Scaffolds.ECommerce.Contracts.Driven.Catalog.Orders;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.GetOrder;
using Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Orders.UseCases.GetOrder.v1;

internal sealed class GetOrderValidator : AxisValidatorBase<GetOrderQuery>
{
    public GetOrderValidator()
    {
        RequiredTryParse(x => x.OrderId, OrderErrors.OrderIdInvalid, OrderId.TryParse);
    }
}
