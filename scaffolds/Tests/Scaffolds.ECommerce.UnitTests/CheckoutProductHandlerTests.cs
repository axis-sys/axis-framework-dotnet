using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class CheckoutProductHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task ReservesStockAndReturnsTheAmbientCallerWhenTheStockSufficesAsync()
    {
        var caller = AxisEntityId.New;
        Identity = caller;
        var product = new FakeProduct(ProductId.New, (Sku)"SKU-1", "Keyboard", Stock: 5);
        ProductsWrite.Setup(p => p.GetByIdForUpdateAsync(product.ProductId))
            .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(product));
        var facade = Build<IProductsFacade>();
        string cartId = CartId.New;

        var result = await facade.CheckoutProductAsync(new CheckoutProductCommand { CartId = cartId, ProductId = product.ProductId, Quantity = 2 });

        var response = result.ShouldSucceed();
        Assert.Equal(caller.ToString(), response.CustomerId);
        Assert.Equal(product.ProductId.ToString(), response.ProductId);
        Bus.Verify(bus => bus.PublishAsync(
            It.Is<ProductCheckedOutEvent>(e => e.CartId == cartId && e.ProductId == product.ProductId.ToString() && e.Quantity == 2),
            ProductCheckedOutEvent.Topic), Times.Once);
    }

    [Fact]
    public async Task RenewsTheTransactionBeforeTheLockingReadWhenCheckingOutAsync()
    {
        List<string> calls = [];
        UnitOfWork.Setup(u => u.RenewAsync())
            .Callback(() => calls.Add(nameof(IUnitOfWork.RenewAsync)))
            .ReturnsAsync(AxisResult.Ok());
        ProductsWrite.Setup(p => p.GetByIdForUpdateAsync(It.IsAny<ProductId>()))
            .Callback(() => calls.Add(nameof(IProductsWritePort.GetByIdForUpdateAsync)))
            .ReturnsAsync((ProductId id) => AxisResult.Ok<IProductEntityProperties>(new FakeProduct(id, (Sku)"SKU-1", "Keyboard", Stock: 5)));
        ProductsWrite.Setup(p => p.ReserveStockAsync(It.IsAny<ProductId>(), It.IsAny<int>()))
            .Callback(() => calls.Add(nameof(IProductsWritePort.ReserveStockAsync)))
            .ReturnsAsync(AxisResult.Ok());
        var facade = Build<IProductsFacade>();

        var result = await facade.CheckoutProductAsync(new CheckoutProductCommand { CartId = CartId.New, ProductId = ProductId.New, Quantity = 1 });

        result.ShouldSucceed();
        string[] expectedOrder = [nameof(IUnitOfWork.RenewAsync), nameof(IProductsWritePort.GetByIdForUpdateAsync), nameof(IProductsWritePort.ReserveStockAsync)];
        Assert.Equal(expectedOrder, calls);
        ProductsReader.Verify(p => p.GetByIdAsync(It.IsAny<ProductId>()), Times.Never);
    }

    [Fact]
    public async Task FailsWithInsufficientStockWhenBelowRequestedAsync()
    {
        var productId = ProductId.New;
        ProductsWrite.Setup(p => p.GetByIdForUpdateAsync(productId))
            .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(new FakeProduct(productId, (Sku)"SKU-1", "Keyboard", Stock: 0)));
        var facade = Build<IProductsFacade>();

        var result = await facade.CheckoutProductAsync(new CheckoutProductCommand { CartId = CartId.New, ProductId = productId, Quantity = 1 });

        result.ShouldFailWithCode(ProductErrors.InsufficientStock.Code);
        ProductsWrite.Verify(p => p.ReserveStockAsync(It.IsAny<ProductId>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task FailsUnauthorizedWithoutReservingStockWhenTheCallerIsAnonymousAsync()
    {
        Identity = null;
        var facade = Build<IProductsFacade>();

        var result = await facade.CheckoutProductAsync(new CheckoutProductCommand { CartId = CartId.New, ProductId = ProductId.New, Quantity = 1 });

        result.ShouldFailWithCode(SharedErrors.CustomerIdentityRequired.Code);
        ProductsWrite.Verify(p => p.ReserveStockAsync(It.IsAny<ProductId>(), It.IsAny<int>()), Times.Never);
    }
}
