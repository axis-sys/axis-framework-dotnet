using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.SubmitOrder;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class SubmitOrderHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task SubmitsAndPersistsTheOrderWhenTheCartWasCheckedOutAsync()
    {
        var cartId = CartId.New;
        var productId = ProductId.New;
        CartItemsReader.Setup(c => c.GetByCartIdAsync(cartId))
            .ReturnsAsync(AxisResult.Ok<ICartItemEntityProperties>(new FakeCartItem(cartId, productId, 2)));
        var facade = Build<IOrdersFacade>();

        var result = await facade.SubmitOrderAsync(new SubmitOrderCommand { Quantity = 2, CartId = cartId });

        var response = result.ShouldSucceed();
        Assert.Equal(productId.ToString(), response.ProductId);
        OrdersWrite.Verify(port => port.CreateAsync(It.Is<IOrderEntityProperties>(order => order.CustomerId == (CustomerId)Identity!.Value.ToString())), Times.Once);
        UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ReportsEveryViolatedRuleWithoutSubmittingWhenTheQuantityAndTheCartAreInvalidAsync()
    {
        var facade = Build<IOrdersFacade>();

        var result = await facade.SubmitOrderAsync(new SubmitOrderCommand { Quantity = 0, CartId = "" });

        result.ShouldFailWithCode(SharedErrors.QuantityMustBePositive);
        result.ShouldFailWithCode(SharedErrors.CartIdInvalid);
        OrdersWrite.Verify(port => port.CreateAsync(It.IsAny<IOrderEntityProperties>()), Times.Never);
    }

    [Fact]
    public async Task FailsWhenTheCartWasNeverCheckedOutAsync()
    {
        var cartId = CartId.New;
        CartItemsReader.Setup(c => c.GetByCartIdAsync(cartId))
            .ReturnsAsync(CartItemErrors.CartItemNotFound);
        var facade = Build<IOrdersFacade>();

        var result = await facade.SubmitOrderAsync(new SubmitOrderCommand { Quantity = 1, CartId = cartId });

        result.ShouldFailWithCode(CartItemErrors.CartItemNotFound.Code);
        OrdersWrite.Verify(port => port.CreateAsync(It.IsAny<IOrderEntityProperties>()), Times.Never);
    }

    [Fact]
    public async Task FailsUnauthorizedWithoutCreatingTheOrderWhenTheCallerIsAnonymousAsync()
    {
        Identity = null;
        var facade = Build<IOrdersFacade>();

        var result = await facade.SubmitOrderAsync(new SubmitOrderCommand { Quantity = 1, CartId = CartId.New });

        result.ShouldFailWithCode(SharedErrors.CustomerIdentityRequired.Code);
        OrdersWrite.Verify(port => port.CreateAsync(It.IsAny<IOrderEntityProperties>()), Times.Never);
    }
}
