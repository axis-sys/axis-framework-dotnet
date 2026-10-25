namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class OrdersRepositoryDualDbTests(DualDbFixture fixture)
{
    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReadsBackEveryColumnWhenAnOrderIsCreatedAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var order = new FakeOrder(OrderId.New, CustomerId.New, ProductId.New, Quantity: 3, CartId.New);

        using (var scope = services.CreateScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<IOrdersWritePort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await orders.CreateAsync(order)).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        using (var scope = services.CreateScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<IOrdersReaderPort>();
            var got = (await orders.GetByIdAsync(order.OrderId)).ShouldSucceed();
            Assert.Equal(order.CustomerId, got.CustomerId);
            Assert.Equal(order.ProductId, got.ProductId);
            Assert.Equal(3, got.Quantity);
            Assert.Equal(order.CartId, got.CartId);
        }
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithConflictWhenTheOrderIdAlreadyExistsAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var order = new FakeOrder(OrderId.New, CustomerId.New, ProductId.New, Quantity: 1, CartId.New);

        using (var scope = services.CreateScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<IOrdersWritePort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await orders.CreateAsync(order)).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        using (var scope = services.CreateScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<IOrdersWritePort>();
            var duplicate = await orders.CreateAsync(order with { CartId = CartId.New });
            duplicate.ShouldFailWithCode(OrderErrors.OrderAlreadyExists.Code);
        }
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithNotFoundWhenTheOrderDoesNotExistAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        using var scope = services.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrdersReaderPort>();

        var result = await orders.GetByIdAsync(OrderId.New);

        result.ShouldFailWithCode(OrderErrors.OrderNotFound.Code);
    }
}
