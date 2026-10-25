using System.Globalization;
using Scaffolds.ECommerce.Domain.Catalog.Orders;

namespace Scaffolds.ECommerce.Application.Catalog.Orders.Services.OrderPresenter;

internal sealed class OrderPresenterService : IOrderPresenterService
{
    public string Describe(IOrderEntityProperties order, AxisResult<decimal> total)
        => total.Match(
            onSuccess: amount => string.Create(CultureInfo.InvariantCulture, $"Order {order.OrderId}: {order.Quantity} x product {order.ProductId}, total {amount:0.00}"),
            onFailure: _ => $"Order {order.OrderId}: {order.Quantity} x product {order.ProductId}, total unavailable ({total.JoinErrorCodes()})");

    public string Summary(IOrderEntityProperties order, AxisResult<decimal> total)
    {
        var (isSuccess, amount, _) = total;
        return isSuccess
            ? string.Create(CultureInfo.InvariantCulture, $"Order {order.OrderId} totals {amount:0.00}")
            : $"Order {order.OrderId} has no total: {total.JoinErrorCodes()}";
    }
}
