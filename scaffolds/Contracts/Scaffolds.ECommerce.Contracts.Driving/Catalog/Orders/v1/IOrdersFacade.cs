using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.GetOrder;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.SubmitOrder;

namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1;

public interface IOrdersFacade
{
    Task<AxisResult<SubmitOrderResponse>> SubmitOrderAsync(SubmitOrderCommand command);
    Task<AxisResult<GetOrderResponse>> GetOrderAsync(GetOrderQuery query);
}
