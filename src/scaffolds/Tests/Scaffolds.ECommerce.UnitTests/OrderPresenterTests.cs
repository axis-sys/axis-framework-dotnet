namespace Scaffolds.ECommerce.UnitTests;

public sealed class OrderPresenterTests
{
    [Fact]
    public void RendersSuccessWhenResultHasValue()
    {
        var orderId = Guid.NewGuid();
        AxisResult<SubmitOrderResponse> result = new SubmitOrderResponse { OrderId = orderId.ToString(), ProductId = Guid.NewGuid().ToString(), Quantity = 2 };

        Assert.Contains(orderId.ToString(), OrderPresenter.Describe(result));
        Assert.Contains(orderId.ToString(), OrderPresenter.Summary(result));
    }

    [Fact]
    public void RendersAllErrorCodesWhenResultIsFailure()
    {
        AxisResult<SubmitOrderResponse> result = new[]
        {
            AxisError.BusinessRule("INSUFFICIENT_STOCK"),
            AxisError.ValidationRule("QUANTITY_INVALID"),
        };

        Assert.Contains("INSUFFICIENT_STOCK", OrderPresenter.Describe(result));
        Assert.Contains("QUANTITY_INVALID", OrderPresenter.Describe(result));
        Assert.Contains("INSUFFICIENT_STOCK", OrderPresenter.Summary(result));
        Assert.Contains("QUANTITY_INVALID", OrderPresenter.Summary(result));
    }
}
