using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class SubmitOrderHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task SubmitsAndPersistsTheOrderForTheProductCheckedOutIntoTheCartAsync()
    {
        var cartId = Guid.CreateVersion7().ToString();
        var productId = ProductId.New;
        CartItems.Setup(c => c.GetByCartIdAsync((CartId)cartId))
            .ReturnsAsync(AxisResult.Ok<ICartItemEntityProperties>(new FakeCartItem((CartId)cartId, productId, 2)));
        var facade = Build();

        var result = await facade.SubmitOrderAsync(new SubmitOrderCommand { Quantity = 2, CartId = cartId });

        var response = result.ShouldSucceed();
        Assert.Equal(productId.ToString(), response.ProductId);
        Orders.Verify(port => port.CreateAsync(It.IsAny<IOrderEntityProperties>()), Times.Once);
        UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task FailsFastOnTheFirstInvalidFieldAsync()
    {
        var facade = Build();

        var result = await facade.SubmitOrderAsync(new SubmitOrderCommand { Quantity = 0, CartId = "" });

        result.ShouldFailWithCode("QUANTITY_MUST_BE_POSITIVE");
        Orders.Verify(port => port.CreateAsync(It.IsAny<IOrderEntityProperties>()), Times.Never);
    }

    [Fact]
    public async Task FailsWhenTheCartWasNeverCheckedOutAsync()
    {
        var cartId = Guid.CreateVersion7().ToString();
        CartItems.Setup(c => c.GetByCartIdAsync((CartId)cartId))
            .ReturnsAsync(AxisError.NotFound("CART_ITEM_NOT_FOUND"));
        var facade = Build();

        var result = await facade.SubmitOrderAsync(new SubmitOrderCommand { Quantity = 1, CartId = cartId });

        result.ShouldFailWithCode("CART_ITEM_NOT_FOUND");
        Orders.Verify(port => port.CreateAsync(It.IsAny<IOrderEntityProperties>()), Times.Never);
    }
}
