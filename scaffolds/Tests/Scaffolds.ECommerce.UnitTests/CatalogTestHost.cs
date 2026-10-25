using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Adapters.Driving.Facade;
using Scaffolds.ECommerce.Application;
using Scaffolds.ECommerce.Application.Catalog;
using Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;

namespace Scaffolds.ECommerce.UnitTests;

public abstract class CatalogTestHost
{
    protected Mock<IProductsReaderPort> ProductsReader { get; } = new(MockBehavior.Loose);
    protected Mock<IProductsWritePort> ProductsWrite { get; } = new(MockBehavior.Loose);
    protected Mock<IOrdersReaderPort> OrdersReader { get; } = new(MockBehavior.Loose);
    protected Mock<IOrdersWritePort> OrdersWrite { get; } = new(MockBehavior.Loose);
    protected Mock<ICartItemsReaderPort> CartItemsReader { get; } = new(MockBehavior.Loose);
    protected Mock<ICartItemsWritePort> CartItemsWrite { get; } = new(MockBehavior.Loose);
    protected Mock<IAxisBus> Bus { get; } = new(MockBehavior.Loose);
    protected Mock<IUnitOfWork> UnitOfWork { get; } = new(MockBehavior.Loose);
    protected AxisEntityId? Identity { get; set; } = AxisEntityId.New;
    protected CatalogOptions Catalog { get; set; } = new() { UnitPrice = 10m, DiscountRate = 0.1m };

    protected CatalogTestHost()
    {
        ProductsReader.Setup(p => p.GetByIdAsync(It.IsAny<ProductId>()))
            .ReturnsAsync((ProductId id) => AxisResult.Ok<IProductEntityProperties>(new FakeProduct(id, (Sku)"SKU-DEFAULT", "Default", 100)));
        ProductsReader.Setup(p => p.GetBySkuAsync(It.IsAny<Sku>()))
            .ReturnsAsync(ProductErrors.ProductNotFound);
        ProductsWrite.Setup(p => p.GetByIdForUpdateAsync(It.IsAny<ProductId>()))
            .ReturnsAsync((ProductId id) => AxisResult.Ok<IProductEntityProperties>(new FakeProduct(id, (Sku)"SKU-DEFAULT", "Default", 100)));
        ProductsWrite.Setup(p => p.ReserveStockAsync(It.IsAny<ProductId>(), It.IsAny<int>()))
            .ReturnsAsync(AxisResult.Ok());
        ProductsWrite.Setup(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()))
            .ReturnsAsync((IProductEntityProperties product) => AxisResult.Ok(product));
        OrdersReader.Setup(o => o.GetByIdAsync(It.IsAny<OrderId>()))
            .ReturnsAsync(OrderErrors.OrderNotFound);
        OrdersWrite.Setup(o => o.CreateAsync(It.IsAny<IOrderEntityProperties>())).ReturnsAsync(AxisResult.Ok());
        CartItemsReader.Setup(c => c.GetByCartIdAsync(It.IsAny<CartId>()))
            .ReturnsAsync((CartId cartId) => AxisResult.Ok<ICartItemEntityProperties>(new FakeCartItem(cartId, ProductId.New, 1)));
        CartItemsWrite.Setup(c => c.CreateAsync(It.IsAny<ICartItemEntityProperties>()))
            .ReturnsAsync((ICartItemEntityProperties cartItem) => AxisResult.Ok(cartItem));
        CartItemsWrite.Setup(c => c.UpdateAsync(It.IsAny<ICartItemEntityProperties>()))
            .ReturnsAsync((ICartItemEntityProperties cartItem) => AxisResult.Ok(cartItem));
        Bus.Setup(b => b.PublishAsync(It.IsAny<ProductCheckedOutEvent>(), It.IsAny<string[]>()))
            .ReturnsAsync(AxisResult.Ok());
        UnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(AxisResult.Ok());
        UnitOfWork.Setup(u => u.RenewAsync()).ReturnsAsync(AxisResult.Ok());
    }

    protected TFacade Build<TFacade>(Action<IServiceCollection>? configure = null) where TFacade : notnull
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddECommerceApplication()
            .AddECommerceFacade()
            .AddSingleton(Options.Create(Catalog))
            .AddSingleton(ProductsReader.Object)
            .AddSingleton(ProductsWrite.Object)
            .AddSingleton(OrdersReader.Object)
            .AddSingleton(OrdersWrite.Object)
            .AddSingleton(CartItemsReader.Object)
            .AddSingleton(CartItemsWrite.Object)
            .AddSingleton(Bus.Object)
            .AddSingleton(UnitOfWork.Object);
        configure?.Invoke(services);
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = Identity;
        return provider.CreateScope().ServiceProvider.GetRequiredService<TFacade>();
    }
}
