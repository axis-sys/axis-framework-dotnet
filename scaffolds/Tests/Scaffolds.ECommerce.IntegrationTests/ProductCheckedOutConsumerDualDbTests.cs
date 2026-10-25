using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;
using Scaffolds.ECommerce.SharedKernel.Catalog.Events.v1;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class ProductCheckedOutConsumerDualDbTests(DualDbFixture fixture)
{
    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task UpdatesTheCartItemWhenTheSameCartIsCheckedOutTwiceAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var first = new FakeProduct(ProductId.New, TestData.NewSku(), "First", Stock: 5);
        var second = new FakeProduct(ProductId.New, TestData.NewSku(), "Second", Stock: 5);
        await SeedAsync(services, first, second);
        services.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = AxisEntityId.New;
        var cartId = CartId.New;

        await CheckoutAsync(services, cartId, first.ProductId, quantity: 1);
        var firstDelivered = await OutboxDispatch.DrainCleanlyAsync(services);
        await CheckoutAsync(services, cartId, second.ProductId, quantity: 3);
        var secondDelivered = await OutboxDispatch.DrainCleanlyAsync(services);

        Assert.True(firstDelivered);
        Assert.True(secondDelivered);
        var cartItem = (await ReadCartItemAsync(services, cartId)).ShouldSucceed();
        Assert.Equal(second.ProductId, cartItem.ProductId);
        Assert.Equal(3, cartItem.Quantity);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task DeliversTheEventWithoutAssociatingACartWhenItsIdsAreMalformedAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        await PublishWithAStateChangeAsync(services, new ProductCheckedOutEvent("not-a-cart-id", "not-a-product-id", Quantity: 1));

        var delivered = await OutboxDispatch.DrainCleanlyAsync(services);

        Assert.True(delivered);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task DeliversTheEventWithoutAssociatingACartWhenTheProductIsUnknownAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var cartId = CartId.New;
        await PublishWithAStateChangeAsync(services, new ProductCheckedOutEvent(cartId, ProductId.New, Quantity: 1));

        var delivered = await OutboxDispatch.DrainCleanlyAsync(services);

        Assert.True(delivered);
        (await ReadCartItemAsync(services, cartId)).ShouldFailWithCode(CartItemErrors.CartItemNotFound.Code);
    }

    private static async Task SeedAsync(ServiceProvider services, params FakeProduct[] seeded)
    {
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        foreach (var product in seeded)
            (await products.CreateAsync(product)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task PublishWithAStateChangeAsync(ServiceProvider services, ProductCheckedOutEvent @event)
    {
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsWritePort>();
        var bus = scope.ServiceProvider.GetRequiredService<IAxisBus>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await products.CreateAsync(new FakeProduct(ProductId.New, TestData.NewSku(), "Published with", Stock: 1))).ShouldSucceed();
        (await bus.PublishAsync(@event, ProductCheckedOutEvent.Topic)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task CheckoutAsync(ServiceProvider services, CartId cartId, ProductId productId, int quantity)
    {
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsFacade>();

        (await products.CheckoutProductAsync(new CheckoutProductCommand { CartId = cartId, ProductId = productId, Quantity = quantity })).ShouldSucceed();
    }

    private static async Task<AxisResult<ICartItemEntityProperties>> ReadCartItemAsync(ServiceProvider services, CartId cartId)
    {
        using var scope = services.CreateScope();
        var cartItems = scope.ServiceProvider.GetRequiredService<ICartItemsReaderPort>();

        return await cartItems.GetByCartIdAsync(cartId);
    }
}
