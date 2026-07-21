using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class CheckoutHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task ReservesStockAndReturnsTheAmbientCallerAsCustomerAsync()
    {
        var caller = AxisEntityId.New;
        Identity = caller;
        var product = new FakeProduct(ProductId.New, (Sku)"SKU-1", "Keyboard", Stock: 5);
        Products.Setup(p => p.GetByIdAsync(product.ProductId))
            .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(product));
        var facade = Build();

        var cartId = Guid.CreateVersion7().ToString();

        var result = await facade.CheckoutAsync(new CheckoutCommand { CartId = cartId, ProductId = product.ProductId, Quantity = 2 });

        var response = result.ShouldSucceed();
        Assert.Equal(caller, response.Customer);
        Assert.Equal(product.ProductId.ToString(), response.ProductId);
        Bus.Verify(bus => bus.PublishAsync(
            It.Is<ProductCheckedOutEvent>(e => e.CartId == cartId && e.ProductId == product.ProductId.ToString() && e.Quantity == 2),
            ProductCheckedOutEvent.Topic), Times.Once);
    }

    [Fact]
    public async Task FailsWithInsufficientStockWhenBelowRequestedAsync()
    {
        var productId = ProductId.New;
        Products.Setup(p => p.GetByIdAsync(productId))
            .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(new FakeProduct(productId, (Sku)"SKU-1", "Keyboard", Stock: 0)));
        var facade = Build();

        var result = await facade.CheckoutAsync(new CheckoutCommand { CartId = Guid.CreateVersion7().ToString(), ProductId = productId, Quantity = 1 });

        Assert.True(result.IsFailure);
        Assert.Equal("INSUFFICIENT_STOCK", result.Errors[0].Code);
    }
}
