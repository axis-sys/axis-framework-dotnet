using Scaffolds.ECommerce.Application.Catalog.Orders.Services.OrderPresenter;
using Scaffolds.ECommerce.Application.Catalog.Orders.Services.OrderPricing;
using Scaffolds.ECommerce.Contracts.Driven.Catalog.Orders;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.GetOrder;
using Scaffolds.ECommerce.Domain.Catalog.Orders;
using Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Orders.UseCases.GetOrder.v1;

internal sealed class GetOrderHandler(
    IAxisMediator mediator,
    IOrdersReaderPort ordersReader,
    IOrderPricingService pricing,
    IOrderPresenterService presenter
) : IAxisQueryHandler<GetOrderQuery, GetOrderResponse>
{
    public Task<AxisResult<GetOrderResponse>> HandleAsync(GetOrderQuery query)
    {
        var orderId = (OrderId)query.OrderId!;
        var callerId = mediator.AxisEntityId is { } caller ? (CustomerId?)caller.ToString() : null;

        return callerId.Rop()
            .Ensure(customerId => customerId.HasValue, SharedErrors.CustomerIdentityRequired)
            .ThenAsync(_ => ordersReader.GetByIdAsync(orderId))
            .EnsureAsync(order => order.CustomerId == callerId, OrderErrors.OrderNotFound)
            .MapAsync(order => Respond(order, pricing.Price(order)));
    }

    private GetOrderResponse Respond(IOrderEntityProperties order, AxisResult<decimal> total) => new()
    {
        OrderId = order.OrderId,
        ProductId = order.ProductId,
        Quantity = order.Quantity,
        Summary = presenter.Summary(order, total),
        Description = presenter.Describe(order, total),
    };
}
