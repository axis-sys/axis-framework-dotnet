using Scaffolds.ECommerce.Adapters.Driving.Facade;
using Scaffolds.ECommerce.Application;
using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.UnitTests;
public abstract class CatalogTestHost
{
    protected Mock<IProductsPort> Products { get; } = new(MockBehavior.Loose);
    protected Mock<IOrdersPort> Orders { get; } = new(MockBehavior.Loose);
    protected Mock<ICartItemsPort> CartItems { get; } = new(MockBehavior.Loose);
    protected Mock<IAxisBus> Bus { get; } = new(MockBehavior.Loose);
    protected Mock<IUnitOfWork> UnitOfWork { get; } = new(MockBehavior.Loose);
    protected AxisEntityId? Identity { get; set; } = AxisEntityId.New;

    protected CatalogTestHost()
    {
        Products.Setup(p => p.GetByIdAsync(It.IsAny<ProductId>()))
            .ReturnsAsync((ProductId id) => AxisResult.Ok<IProductEntityProperties>(new FakeProduct(id, (Sku)"SKU-DEFAULT", "Default", 100)));
        Products.Setup(p => p.GetBySkuAsync(It.IsAny<Sku>()))
            .ReturnsAsync(AxisError.NotFound("PRODUCT_NOT_FOUND"));
        Products.Setup(p => p.ReserveStockAsync(It.IsAny<ProductId>(), It.IsAny<int>()))
            .ReturnsAsync(AxisResult.Ok());
        Products.Setup(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()))
            .ReturnsAsync((IProductEntityProperties properties) => AxisResult.Ok(properties));
        Orders.Setup(o => o.CreateAsync(It.IsAny<IOrderEntityProperties>())).ReturnsAsync(AxisResult.Ok());
        CartItems.Setup(c => c.GetByCartIdAsync(It.IsAny<CartId>()))
            .ReturnsAsync((CartId cartId) => AxisResult.Ok<ICartItemEntityProperties>(new FakeCartItem(cartId, ProductId.New, 1)));
        CartItems.Setup(c => c.CreateAsync(It.IsAny<ICartItemEntityProperties>()))
            .ReturnsAsync((ICartItemEntityProperties cartItem) => AxisResult.Ok(cartItem));
        CartItems.Setup(c => c.UpdateAsync(It.IsAny<ICartItemEntityProperties>()))
            .ReturnsAsync((ICartItemEntityProperties cartItem) => AxisResult.Ok(cartItem));
        Bus.Setup(b => b.PublishAsync(It.IsAny<ProductCheckedOutEvent>(), It.IsAny<string[]>()))
            .ReturnsAsync(AxisResult.Ok());
        UnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(AxisResult.Ok());
    }

    protected ICatalogFacade Build()
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddECommerceApplication()
            .AddECommerceFacade()
            .AddSingleton(Products.Object)
            .AddSingleton(Orders.Object)
            .AddSingleton(CartItems.Object)
            .AddSingleton(Bus.Object)
            .AddSingleton(UnitOfWork.Object)
            .BuildServiceProvider();

        provider.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = Identity;
        return provider.CreateScope().ServiceProvider.GetRequiredService<ICatalogFacade>();
    }
}
