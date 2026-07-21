using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.SubmitOrder;

namespace Scaffolds.ECommerce.Application.Samples;

public static class OrderPresenter
{
    #region scaffold:describe-order
    public static string Describe(AxisResult<SubmitOrderResponse> result)
        => result.Match(
            onSuccess: order => $"Order {order.OrderId} placed: {order.Quantity} x product {order.ProductId}",
            onFailure: _ => $"Order failed: {result.JoinErrorCodes()}");
    #endregion

    #region scaffold:order-summary
    public static string Summary(AxisResult<SubmitOrderResponse> result)
    {
        var (isSuccess, order, _) = result;
        return isSuccess ? $"Order {order!.OrderId} confirmed" : $"Rejected: {result.JoinErrorCodes()}";
    }
    #endregion
}
