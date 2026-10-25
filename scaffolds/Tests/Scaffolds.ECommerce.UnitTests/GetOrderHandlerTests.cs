using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.GetOrder;
using Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class GetOrderHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task PricesAndDescribesTheOrderWhenItBelongsToTheCallerAsync()
    {
        var order = OrderOf(CallerId(), quantity: 3);
        OrdersReader.Setup(port => port.GetByIdAsync(order.OrderId)).ReturnsAsync(AxisResult.Ok<IOrderEntityProperties>(order));
        var facade = Build<IOrdersFacade>();

        var result = await facade.GetOrderAsync(new GetOrderQuery { OrderId = order.OrderId });

        var response = result.ShouldSucceed();
        Assert.Equal(order.ProductId.ToString(), response.ProductId);
        Assert.Equal(3, response.Quantity);
        Assert.Equal($"Order {order.OrderId} totals 27.00", response.Summary);
        Assert.Equal($"Order {order.OrderId}: 3 x product {order.ProductId}, total 27.00", response.Description);
    }

    [Fact]
    public async Task DescribesTheOrderWithoutATotalWhenTheUnitPriceIsNotPositiveAsync()
    {
        Catalog = new() { UnitPrice = 0m, DiscountRate = 0.1m };
        var order = OrderOf(CallerId(), quantity: 2);
        OrdersReader.Setup(port => port.GetByIdAsync(order.OrderId)).ReturnsAsync(AxisResult.Ok<IOrderEntityProperties>(order));
        var facade = Build<IOrdersFacade>();

        var result = await facade.GetOrderAsync(new GetOrderQuery { OrderId = order.OrderId });

        var response = result.ShouldSucceed();
        Assert.Equal($"Order {order.OrderId} has no total: {OrderErrors.PriceMustBePositive.Code}", response.Summary);
        Assert.Equal($"Order {order.OrderId}: 2 x product {order.ProductId}, total unavailable ({OrderErrors.PriceMustBePositive.Code})", response.Description);
    }

    [Fact]
    public async Task FailsWithNotFoundWhenTheOrderBelongsToAnotherCustomerAsync()
    {
        var order = OrderOf(CustomerId.New, quantity: 1);
        OrdersReader.Setup(port => port.GetByIdAsync(order.OrderId)).ReturnsAsync(AxisResult.Ok<IOrderEntityProperties>(order));
        var facade = Build<IOrdersFacade>();

        var result = await facade.GetOrderAsync(new GetOrderQuery { OrderId = order.OrderId });

        result.ShouldFailWithCode(OrderErrors.OrderNotFound.Code);
        Assert.Equal(AxisErrorType.NotFound, Assert.Single(result.Errors).Type);
    }

    [Fact]
    public async Task FailsWithNotFoundWhenTheOrderIsUnknownAsync()
    {
        var facade = Build<IOrdersFacade>();

        var result = await facade.GetOrderAsync(new GetOrderQuery { OrderId = OrderId.New });

        result.ShouldFailWithCode(OrderErrors.OrderNotFound.Code);
    }

    [Fact]
    public async Task FailsUnauthorizedWithoutReadingTheOrderWhenTheCallerIsAnonymousAsync()
    {
        Identity = null;
        var facade = Build<IOrdersFacade>();

        var result = await facade.GetOrderAsync(new GetOrderQuery { OrderId = OrderId.New });

        result.ShouldFailWithCode(SharedErrors.CustomerIdentityRequired.Code);
        OrdersReader.Verify(port => port.GetByIdAsync(It.IsAny<OrderId>()), Times.Never);
    }

    [Fact]
    public async Task FailsValidationWithoutReadingTheOrderWhenTheIdIsNotAVersion7GuidAsync()
    {
        var facade = Build<IOrdersFacade>();

        var result = await facade.GetOrderAsync(new GetOrderQuery { OrderId = Guid.NewGuid().ToString() });

        result.ShouldFailWithCode(OrderErrors.OrderIdInvalid);
        OrdersReader.Verify(port => port.GetByIdAsync(It.IsAny<OrderId>()), Times.Never);
    }

    private CustomerId CallerId() => (CustomerId)Identity!.Value.ToString();

    private static FakeOrder OrderOf(CustomerId customerId, int quantity)
        => new(OrderId.New, customerId, ProductId.New, quantity, CartId.New);
}
