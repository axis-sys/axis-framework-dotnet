using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.GetOrder;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.SubmitOrder;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade.Catalog.Orders;

internal sealed class OrdersFacade(IAxisMediator mediator) : IOrdersFacade
{
    public Task<AxisResult<SubmitOrderResponse>> SubmitOrderAsync(SubmitOrderCommand command)
        => mediator.Cqrs.ExecuteAsync<SubmitOrderCommand, SubmitOrderResponse>(command);

    public Task<AxisResult<GetOrderResponse>> GetOrderAsync(GetOrderQuery query)
        => mediator.Cqrs.QueryAsync<GetOrderQuery, GetOrderResponse>(query);
}
